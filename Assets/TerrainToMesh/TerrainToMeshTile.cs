using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TerrainConverter
{
    public class TerrainToMeshTile : MonoBehaviour
    {
        [Serializable]
        public class LodMeshInfo
        {
            public int[] layerIndices;
            public List<TerrainToMeshTool.VecInt3> intVertices;
            public List<TerrainToMeshTool.VecNormal> vecNormals;
            public List<int[]> indices;
            public int GetMemorySize() {
                int size = 0;
                size += layerIndices.Length * 4;
                size += intVertices == null ? 0 : intVertices.Count * Marshal.SizeOf(typeof(TerrainToMeshTool.VecInt3));
                size += vecNormals == null ? 0 : vecNormals.Count * Marshal.SizeOf(typeof(TerrainToMeshTool.VecNormal));
                for (int i = 0; i < indices.Count; i++) {
                    size += indices[i].Length * 4;
                }
                return size;
            }
        }
        public LodMeshInfo[] lodMeshInfos = new LodMeshInfo[0];

        public Material matBase;
        public Texture2D splatController;
        public List<Renderer> renderers = new List<Renderer>();
        public List<Material> matAdd { get; set; }
        public List<Material> matFirst { get; set; }

        public TerrainData terrainData { get; set; }
        public int lodLevel { get; set; }
        public int newLodLevel { get; set; }
        public Node[] roots { get; set; }//整个场景的根节点
        public Node[] trees { get; set;}//自己Tile区域的根节点
        public LayerType layerType { get; set; }
        public float[,] heights { get; set; }
        public float lodPower { get; set; }
        public Material terrainMaterial { get; set; }
        public Texture bakedControlTexture { get; set; }
        public Material terrainTextureIndexMaterial { get; set; }
        public Texture2DArray texArray { get; set; }

        public void UpdateLightmap(int lightmapIndex, Vector4 lightmapScaleOffset)
        {
            foreach (var r in renderers)
            {
                r.lightmapIndex = lightmapIndex;
                r.lightmapScaleOffset = lightmapScaleOffset;
            }
        }

        public void DynamicUpdateChildren()
        {
            lodLevel = newLodLevel;
            while(transform.childCount > 0) {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }

            renderers = CreateRenderersForOneLodLevel(lodLevel);
        }

        //创建支持submesh的Renderer，并设置材质数组
        MeshRenderer CreateMeshRenderer_SubMesh(Mesh mesh, bool[] layerValid, int[] layerIndices,int lod)
        {
            if (mesh)
            {
                GameObject obj = new GameObject("layer_lod_" + lod);
                MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                MeshFilter filter = obj.AddComponent<MeshFilter>();
                obj.transform.SetParent(transform, false);
                filter.sharedMesh = mesh;
                Material[] sharedMaterials = new Material[mesh.subMeshCount];
                int subMeshIndex = 0;
                for (int i = 0; i < layerIndices.Length; i++) {
                    if (layerValid[layerIndices[i]]) {
                        sharedMaterials[subMeshIndex] = (i == 0) ? matFirst[layerIndices[i]] : matAdd[layerIndices[i]];
                        subMeshIndex++;
                    }
                }
                renderer.sharedMaterials = sharedMaterials;
                return renderer;
            }
            return null; 
        }

        //创建一个单一的MeshRenderer
        MeshRenderer CreateMeshRenderer_SingleMesh(Mesh mesh,int lod)
        {
            if (mesh) {
                GameObject obj = new GameObject("layer_lod_" + lod);
                MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                MeshFilter filter = obj.AddComponent<MeshFilter>();
                obj.transform.SetParent(transform, false);
                filter.sharedMesh = mesh;
                Material sharedMaterial = new Material(terrainMaterial);
                sharedMaterial.SetTexture("_Control", bakedControlTexture);
                sharedMaterial.EnableKeyword("_MESH_TERRAIN_MIX");
                int[] maxIndices = trees[lod].GetMaxNValieNumIndices(4);
                for (int i = 0; i < 4; i++) {
                    sharedMaterial.SetTexture("_Splat" + i, terrainData.splatPrototypes[maxIndices[i]].texture);
                    sharedMaterial.SetTexture("_Normal" + i, terrainData.splatPrototypes[maxIndices[i]].normalMap);

                    Vector2 tileSize = terrainData.splatPrototypes[0].tileSize;
                    sharedMaterial.SetTextureScale("_Splat" + i, new Vector2(terrainData.size.x / tileSize.x, terrainData.size.z / tileSize.y));
                    sharedMaterial.SetTextureOffset("_Splat" + i, terrainData.splatPrototypes[0].tileOffset);
                }
                renderer.material = sharedMaterial;
                return renderer;
            }
            return null;
        }

        //创建一个单一的MeshRenderer,用Texture Array渲染
        MeshRenderer CreateMeshRenderer_SingleMesh_TextureIndex(Mesh mesh, int lod) {
            if (mesh) {
                GameObject obj = new GameObject("layer_lod_" + lod);
                MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                MeshFilter filter = obj.AddComponent<MeshFilter>();
                obj.transform.SetParent(transform, false);
                filter.sharedMesh = mesh;
                renderer.material = terrainTextureIndexMaterial;
                return renderer;
            }
            return null;
        }

        //创建一个MeshRenderer,用于多个Renderer类型中的一个layer
        MeshRenderer CreateMeshRenderer_OneOfMultiMesh(Mesh mesh, int layer, bool firstLayer, int lod) {
            if (mesh) {
                GameObject obj = new GameObject("layer_lod_" + lod + "_layer" + layer);
                MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                MeshFilter filter = obj.AddComponent<MeshFilter>();
                obj.transform.SetParent(transform, false);
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = firstLayer ? matFirst[layer] : matAdd[layer];
                return renderer;
            }
            return null;
        }

        public void CollectMeshInfo(int maxLodLevel) {
            lodMeshInfos = new LodMeshInfo[maxLodLevel + 1];
            for (int lod = 0; lod <= maxLodLevel; lod++) {
                lodMeshInfos[lod] = new LodMeshInfo();
                if (layerType == LayerType.SingleMesh_Max4Layer) {
                    lodMeshInfos[lod].layerIndices = new int[1];
                    lodMeshInfos[lod].layerIndices[0] = 0;
                } else {
                    lodMeshInfos[lod].layerIndices = new int[trees[0].validNums.Length];
                    int maxIndex = trees[0].GetMaxValidNumIndex();
                    for (int i = 0; i < lodMeshInfos[lod].layerIndices.Length; i++) {
                        lodMeshInfos[lod].layerIndices[i == maxIndex ? 0 : (i < maxIndex ? i + 1 : i)] = i;
                    }
                }

                lodMeshInfos[lod].intVertices = TerrainToMeshTool.GetIntVertices(trees[lod]);
                lodMeshInfos[lod].vecNormals = TerrainToMeshTool.GetNormals(lodMeshInfos[lod].intVertices, terrainData);
                lodMeshInfos[lod].indices = TerrainToMeshTool.GetIndices(trees[lod], lodMeshInfos[lod].layerIndices);
            }
        }

        //创建某个级别的LOD的网格
        public List<Renderer> CreateRenderersForOneLodLevel(int lod) {
            List<Renderer> lodRenderers = new List<Renderer>(10);
            if (lod >= 0 && lod < lodMeshInfos.Length) {
                lodRenderers.Clear();
                if (lod < 2) {
                    bool[] layerValid = new bool[lodMeshInfos[lod].layerIndices.Length];

                    if (layerType == LayerType.SubMesh) {
                        for (int i = 0; i < lodMeshInfos[lod].layerIndices.Length; i++) {
                            if (lodMeshInfos[lod].indices[lodMeshInfos[lod].layerIndices[i]].Length > 0) {
                                layerValid[i] = true;
                            }
                        }
                        Mesh mesh = TerrainToMeshTool.CreateMesh(terrainData, heights, lodMeshInfos[lod].intVertices, lodMeshInfos[lod].vecNormals, lodMeshInfos[lod].indices, -1);
                        MeshRenderer renderer = CreateMeshRenderer_SubMesh(mesh, layerValid, lodMeshInfos[lod].layerIndices, lod);
                        if (renderer) {
                            lodRenderers.Add(renderer);
                        }
                    }
                    else if(layerType == LayerType.SingleMesh_Max4Layer) {
                        Mesh mesh = TerrainToMeshTool.CreateMesh(terrainData, heights, lodMeshInfos[lod].intVertices, lodMeshInfos[lod].vecNormals, lodMeshInfos[lod].indices, 0);
                        MeshRenderer renderer = CreateMeshRenderer_SingleMesh(mesh, lod);
                        if (renderer) {
                            lodRenderers.Add(renderer);
                        }
                    }
                    else if(layerType == LayerType.SingleMesh_LayerIndexTexture) {
                        Mesh mesh = TerrainToMeshTool.CreateMesh(terrainData, heights, lodMeshInfos[lod].intVertices, lodMeshInfos[lod].vecNormals, lodMeshInfos[lod].indices, 0);
                        MeshRenderer renderer = CreateMeshRenderer_SingleMesh_TextureIndex(mesh, lod);
                        if (renderer) {
                            lodRenderers.Add(renderer);
                        }
                    }
                    else {
                        for (int i = 0; i < lodMeshInfos[lod].layerIndices.Length; i++) {
                            Mesh mesh = TerrainToMeshTool.CreateMesh(terrainData, heights, lodMeshInfos[lod].intVertices, lodMeshInfos[lod].vecNormals, lodMeshInfos[lod].indices, i);
                            MeshRenderer renderer = CreateMeshRenderer_OneOfMultiMesh(mesh, lodMeshInfos[lod].layerIndices[i], i == 0, lod);
                            if (renderer) {
                                lodRenderers.Add(renderer);
                            }
                        }
                    }
                }
                else {
                    List<TerrainToMeshTool.VecInt3> intVertices = TerrainToMeshTool.GetIntVertices(trees[lod]);
                    List<TerrainToMeshTool.VecNormal> vecNormals = TerrainToMeshTool.GetNormals(intVertices, terrainData);
                    List<int[]> indices = TerrainToMeshTool.GetIndices(trees[lod], new int[] { 0 });

                    Mesh mesh = TerrainToMeshTool.CreateMesh(terrainData, heights, intVertices, vecNormals, indices, 0);

                    GameObject obj = new GameObject("base_lod_" + lod);
                    MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                    MeshFilter filter = obj.AddComponent<MeshFilter>();
                    renderer.sharedMaterial = matBase;
                    filter.sharedMesh = mesh;
                    obj.transform.SetParent(transform, false);
                    lodRenderers.Add(renderer);
                }
            }
            return lodRenderers;
        }

        public void ClearCollectedInfo() {
            lodMeshInfos = new LodMeshInfo[0];
        }

        //call from editor
        public void CreateStaticChildren()
        {
            while (transform.childCount > 0) {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }

            LOD[] lods = new LOD[lodMeshInfos.Length];
            for (int lod = 0; lod < lodMeshInfos.Length; lod++)
            {
                List<Renderer> lodRenderers = CreateRenderersForOneLodLevel(lod);
                renderers.AddRange(lodRenderers);
                lods[lod].renderers = lodRenderers.ToArray();
                float f = (lod + 1.0f) / (lodMeshInfos.Length + 0.1f);
                lods[lod].screenRelativeTransitionHeight = Mathf.Pow(1-f, lodPower);
            }

            LODGroup lodg = this.gameObject.AddComponent<LODGroup>();
            lodg.SetLODs(lods);
        }
    }
}