using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TerrainTextureIndexUpdater : MonoBehaviour
{
    Terrain terrain;
    TerrainData terrainData;

    //[HideInInspector]
    public Material bakeMat;
    //[HideInInspector]
    public Material renderMat;
    //[HideInInspector]
    public RenderTexture controlTexture;
    //[HideInInspector]
    public Texture textureArray;
    public Texture normalTextureArray;

    [Range(0,5)]
    public float heightScale = 2;

    RenderTexture rt0;
    RenderTexture rt1;

    private void OnEnable() {
        terrain = GetComponent<Terrain>();
        terrainData = terrain.terrainData;
        bakeMat = new Material(Shader.Find("Hidden/BakeTextureIndex"));
        rt0 = new RenderTexture(terrainData.alphamapWidth, terrainData.alphamapHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); 
        rt1 = new RenderTexture(terrainData.alphamapWidth, terrainData.alphamapHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

        rt0.filterMode = FilterMode.Bilinear;
        rt1.filterMode = FilterMode.Bilinear;

        if (renderMat == null) {
            renderMat = new Material(Shader.Find("Mobile/TerrainTextureIndex"));
        } else {
            renderMat.shader = Shader.Find("Mobile/TerrainTextureIndex");
        }
        terrain.materialType = Terrain.MaterialType.Custom;
        terrain.materialTemplate = renderMat;

        controlTexture = BakeControlTexture();
        textureArray = CreateTextureArray();
        normalTextureArray = CreateNormalTextureArray();
        renderMat.SetTexture("_TexArray", textureArray);
        renderMat.SetTexture("_NormalArray", normalTextureArray);
        renderMat.SetFloat("_TexArrayNum", terrainData.splatPrototypes.Length);
        UpdateMaterial();
    }

    private void OnDisable() {
        terrain.materialType = Terrain.MaterialType.BuiltInStandard;
    }

    void Start () {
		
	}

    void UpdateMaterial() {
        renderMat.SetTexture("_IndexControl", BakeControlTexture());
        renderMat.SetVectorArray("_TerrainScaleOffset", GetScaleOffsets(terrain.terrainData));
        renderMat.SetFloat("_HeightScale", heightScale);
        renderMat.SetTexture("_Normal", terrain.terrainData.splatPrototypes[0].normalMap);
        //Shader.SetGlobalVectorArray("_TerrainScaleOffset", GetScaleOffsets(terrain.terrainData));
    }
	
	void Update () {
        if (!Application.isPlaying) {
            UpdateMaterial();
        }
    }

    RenderTexture BakeControlTexture() {
        rt0.DiscardContents();
        rt1.DiscardContents();
        RenderTexture.active = rt1;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = rt0;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        for (int i = 0; i < terrainData.alphamapTextures.Length; i++) {
            RenderTexture.active = i % 2 == 0 ? rt1 : rt0;//pingpang
            bakeMat.SetTexture("_LastResult", i % 2 == 0 ? rt0 : rt1);
            bakeMat.SetTexture("_SplatAlpha", terrainData.alphamapTextures[i]);
            bakeMat.SetFloat("_StartLayerIndex", i * 4);
            Graphics.Blit(Texture2D.blackTexture, bakeMat);
        }
        GL.End();
        RenderTexture result = RenderTexture.active;
        RenderTexture.active = null;
        return result;
    }

    Texture CreateTextureArray() {
        Texture firstTexture = terrainData.splatPrototypes[0].texture;
        RenderTexture rt = new RenderTexture(firstTexture.width, firstTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.useMipMap = true;
        rt.filterMode = FilterMode.Bilinear;
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        rt.volumeDepth = terrainData.splatPrototypes.Length;
        rt.autoGenerateMips = true;

        Material mat = new Material(Shader.Find("Hidden/BlitCopy"));
        for (int i = 0; i < terrainData.splatPrototypes.Length; i++) {
            Graphics.SetRenderTarget(rt, 0, CubemapFace.Unknown, i);
            Graphics.Blit(terrainData.splatPrototypes[i].texture, mat);
        }
        return rt;
    }

    Texture2D CreateTexture(Color color) {
        Texture2D tex = new Texture2D(1, 1);
        tex.name = "ColorTexture";
        tex.SetPixel(0, 0, color);
        return tex;
    }

    Texture CreateNormalTextureArray() {
        Texture firstTexture = terrainData.splatPrototypes[0].normalMap;
        RenderTexture rt = new RenderTexture(firstTexture.width, firstTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.useMipMap = true;
        rt.filterMode = FilterMode.Bilinear;
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        rt.volumeDepth = terrainData.splatPrototypes.Length;
        rt.autoGenerateMips = true;
        Texture2D defaultNormal = CreateTexture(new Color(0.5f, 0.5f, 1));

        Material mat = new Material(Shader.Find("Hidden/BlitCopy"));
        for (int i = 0; i < terrainData.splatPrototypes.Length; i++) {
            Graphics.SetRenderTarget(rt, 0, CubemapFace.Unknown, i);
            if (terrainData.splatPrototypes[i].normalMap) {
                Graphics.Blit(terrainData.splatPrototypes[i].normalMap, mat);
            } else {
                Graphics.Blit(defaultNormal, mat);
            }
        }
        return rt;
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
}
