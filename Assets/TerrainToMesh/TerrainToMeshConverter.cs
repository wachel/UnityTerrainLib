using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;



namespace TerrainConverter
{
    [System.Serializable]
    public class AlphaLayer
    {
        public short[] validNums;
    }


    [System.Serializable]
    public class LodNodeTree
    {
        public byte[] tree;
        public AlphaLayer[] alphaLayers;//在每个层上有效像素数量[layer]
    }

    [System.Serializable]
    public class LayerProperty
    {
        public bool simpleLayer;
    }

    public enum LayerType
    {
        MultiMesh,      //不同的层用不同的MeshRenderer
        SubMesh,        //不同的层用相同的顶点buffer
        SingleMesh_Max4Layer,     //只有面积最多的四个层
        SingleMesh_LayerIndexTexture,
    }

    [ExecuteInEditMode]
    public class TerrainToMeshConverter : MonoBehaviour
    {
        public Terrain terrain;
        public int gridSize = 64;
        public float minError = 0.1f;
        public float maxError = 20.0f;
        public float alphaMapThreshold = 0.02f;
        public int maxLodLevel = 3;
        public float lodPower = 1.0f;
        public bool staticLodMesh = false;
        public LayerType layerType = LayerType.SingleMesh_Max4Layer;

        public Texture2D bakedControlTexture;

        public Shader shaderFirst;
        public Shader shaderAdd;
        public Shader shaderBase;
        public Shader shaderTextureIndex;

        public LodNodeTree[] trees;
        public TerrainToMeshTile[] tiles;
        public LayerProperty[] layerProperties;
        public Node[] roots { get; set; }

        Transform rootTransfrom;

        [SerializeField]
        public Material terrainTextureIndexMaterial = null;

        public void OnEnable()
        {
            if (!staticLodMesh) {
                ClearChildren();
                LoadNodes();
                CollectInfos();
            } else {
                if (tiles != null) {
                    foreach (TerrainToMeshTile tile in tiles) {
                        tile.UpdateLightmap(terrain.lightmapIndex, terrain.lightmapScaleOffset);
                    }
                }
            }
        }

        public void LoadNodes()
        {
            roots = new Node[maxLodLevel + 1];
            for (int i = 0; i <= maxLodLevel; i++) {
                Node node = new Node(0, 0, terrain.terrainData.heightmapWidth - 1);
                node.CreateChildFromBytes(trees[i].tree);
                node.SetAlphaBytes(trees[i].alphaLayers);
                roots[i] = node;
            }

        }

        public void ClearChildren()
        {
            var children = new List<GameObject>();
            foreach (Transform child in GetRootTransform()) children.Add(child.gameObject);
            children.ForEach(child => DestroyImmediate(child));
        }


        Transform GetRootTransform() {
            if (rootTransfrom) {
                return rootTransfrom;
            }
            rootTransfrom = transform.Find("TerrainMesh");
            if (rootTransfrom == null) {
                rootTransfrom = new GameObject("TerrainMesh").transform;
                rootTransfrom.SetParent(transform);
            }
            return rootTransfrom;
        }

        Vector4[] GetScaleOffsets(TerrainData terrainData) {
            Vector4[] scaleOffsets = new Vector4[terrainData.splatPrototypes.Length];
            for (int i = 0; i < terrainData.splatPrototypes.Length; i++) {
                Vector2 tileSize = terrainData.splatPrototypes[i].tileSize;
                Vector2 scale = new Vector2(terrainData.size.x / tileSize.x, terrainData.size.z / tileSize.y);
                Vector2 offset = terrainData.splatPrototypes[i].tileOffset;
                scaleOffsets[i] = new Vector4(scale.x, scale.y, offset.x, offset.y);
            }
            return scaleOffsets;
        }

        public void CollectInfos() 
        {
            TerrainData terrainData = terrain.terrainData;
            Texture2D baseTexture = TerrainToMeshTool.BakeBaseTexture(terrain.terrainData);
            List<Material> matAdd = new List<Material>();
            List<Material> matFirst = new List<Material>();
            Material matBase = new Material(shaderBase);
            matBase.SetTexture("_MainTex", baseTexture);
            float[,] heights = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight);
            Texture2DArray texArray = null;
            Material terrainMaterial = new Material(Shader.Find("Nature/Terrain/Diffuse"));

            if (layerType == LayerType.SingleMesh_LayerIndexTexture) {
                bakedControlTexture = TerrainToMeshTool.BakeTextureIndex(terrain.terrainData);
                {
                    Texture firstTexture = terrainData.splatPrototypes[0].texture;
                    texArray = new Texture2DArray(firstTexture.width, firstTexture.height, terrainData.splatPrototypes.Length, TextureFormat.ARGB32,true, true);
                    for (int i = 0; i < terrainData.splatPrototypes.Length; i++) {
                        texArray.SetPixels32(terrainData.splatPrototypes[i].texture.GetPixels32(), i);
                    }
                    texArray.Apply();
                }
                if (terrainTextureIndexMaterial == null) {
                    terrainTextureIndexMaterial = new Material(Shader.Find("Mobile/TerrainTextureIndex"));
                } else {
                    terrainTextureIndexMaterial.shader = Shader.Find("Mobile/TerrainTextureIndex");
                }
                terrainTextureIndexMaterial.SetTexture("_IndexControl", bakedControlTexture);
                terrainTextureIndexMaterial.SetTexture("_TexArray", texArray);
                terrainTextureIndexMaterial.SetFloat("_TexArrayNum", terrainData.splatPrototypes.Length);

                terrainTextureIndexMaterial.SetVectorArray("_ScaleOffset", GetScaleOffsets(terrainData));

            } else if(layerType == LayerType.SingleMesh_Max4Layer) {
                bakedControlTexture = TerrainToMeshTool.BakeControlTexture(terrain.terrainData, roots[0], gridSize, 4);
            } else {
                for (int l = 0; l < terrainData.alphamapLayers; l++) {
                    LayerProperty lp = l < layerProperties.Length ? layerProperties[l] : null;
                    matAdd.Add(TerrainToMeshTool.GetMaterial(terrain, l, shaderAdd, lp));
                    matFirst.Add(TerrainToMeshTool.GetMaterial(terrain, l, shaderFirst, lp));
                }
            }

            int w = terrainData.heightmapWidth - 1;
            int gridNumX = w / gridSize;
            tiles = new TerrainToMeshTile[gridNumX * gridNumX];

            for (int x = 0; x < gridNumX; x++) {
                for (int y = 0; y < gridNumX; y++) {
                    GameObject objGrid = new GameObject("mesh_" + x + "_" + y);
                    objGrid.transform.SetParent(GetRootTransform(), false);
                    TerrainToMeshTile tile = objGrid.AddComponent<TerrainToMeshTile>();
                    tiles[y * gridNumX + x] = tile;
                    tile.matBase = matBase;
                    tile.matAdd = matAdd;
                    tile.matFirst = matFirst;
                    tile.lodLevel = -1;
                    tile.terrainData = terrainData;
                    tile.heights = heights;
                    tile.roots = roots;
                    tile.trees = new Node[roots.Length];
                    tile.layerType = layerType;
                    tile.lodPower = lodPower;
                    tile.terrainMaterial = terrainMaterial;
                    tile.bakedControlTexture = bakedControlTexture;
                    tile.terrainTextureIndexMaterial = terrainTextureIndexMaterial;
                    tile.texArray = texArray;
                    for (int lod = 0; lod < roots.Length; lod++) {
                        tile.trees[lod] = roots[lod].FindSizeNode(x * gridSize, y * gridSize, gridSize);
                        TerrainToMeshTool.SetNodeSkirts(tile.trees[lod], tile.trees[lod]);
                    }
                }
            }

            for (int x = 0; x < gridNumX; x++) {
                for (int y = 0; y < gridNumX; y++) {
                    tiles[y * gridNumX + x].CollectMeshInfo(maxLodLevel);
                }
            }
        }

        public void ClearCollectedInfo() {
            for (int i = 0; i < tiles.Length; i++) {
                tiles[i].ClearCollectedInfo();
            }
        }

        public void CreateStaticMeshes() {
            for (int i = 0; i < tiles.Length; i++) {
                tiles[i].CreateStaticChildren();
                tiles[i].UpdateLightmap(terrain.lightmapIndex, terrain.lightmapScaleOffset);
            }
        }

        public int GetMemorySize() {
            int totalSize = 0;
            for (int i = 0; i < tiles.Length; i++) {
                for(int l = 0; l < tiles[i].lodMeshInfos.Length; l++) {
                    totalSize += tiles[i].lodMeshInfos[l].GetMemorySize();
                }
            }
            return totalSize;
        }

        Vector2 GetCameraXZPosition() {
            Vector2 camera = new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z);
#if QINSHI
            if (CameraManager.Instance && CameraManager.Instance.controller.GetFollowTargetObject()) {
                Vector3 targetPos = CameraManager.Instance.controller.GetFollowTargetObject().transform.position;
                camera = new Vector2(targetPos.x, targetPos.z);
            }
#endif
            return camera;
        }

        public void Update()
        {
            if (!staticLodMesh) {
                TerrainData terrainData = terrain.terrainData;
                int w = terrainData.heightmapWidth - 1;
                int gridNumX = w / gridSize;
                Vector2 camera = GetCameraXZPosition();
                float viewDistance = Camera.main.farClipPlane;
                for (int x = 0; x < gridNumX; x++) {
                    for (int y = 0; y < gridNumX; y++) {
                        Vector2 center = new Vector2(transform.position.x, transform.position.z) + new Vector2(y * gridSize, x * gridSize) + new Vector2(gridSize, gridSize) * 0.5f;
                        float t = Mathf.Clamp01(((center - camera).magnitude - gridSize / 2) / viewDistance);
                        tiles[y * gridNumX + x].newLodLevel = Mathf.Min((int)(maxLodLevel * Mathf.Pow(t,lodPower)), maxLodLevel);
                    }
                }
                for (int x = 0; x < gridNumX; x++) {
                    for (int y = 0; y < gridNumX; y++) {
                        Vector2 center = new Vector2(transform.position.x, transform.position.z) + new Vector2(y * gridSize, x * gridSize) + new Vector2(gridSize, gridSize) * 0.5f;
                        float t = 1 - Mathf.Clamp01((center - camera).magnitude / viewDistance);
                        tiles[y * gridNumX + x].newLodLevel = Mathf.Min((int)(maxLodLevel * (1 - t * t)), maxLodLevel);
                        if (tiles[y * gridNumX + x].lodLevel != tiles[y * gridNumX + x].newLodLevel) {
                            tiles[y * gridNumX + x].DynamicUpdateChildren();
                            tiles[y * gridNumX + x].UpdateLightmap(terrain.lightmapIndex, terrain.lightmapScaleOffset);
                        }
                    }
                }
            }

#if UNITY_EDITOR
            if (terrainTextureIndexMaterial != null) {
                terrainTextureIndexMaterial.SetVectorArray("_ScaleOffset", GetScaleOffsets(terrain.terrainData));
            }
#endif
        }
    }

}
