
Shader "Mobile/TerrainTextureIndex" {
	Properties {
		_TexArray("Texture Array", 2DArray) = "" {}
		_IndexControl("Control Texture", 2D) = "black" {}
		_HeightScale("HeightScale",float) = 2
	}
	SubShader {
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+1"}
		LOD 150
 
		CGPROGRAM
		#pragma surface surf Lambert noforwardadd
 
		sampler2D _IndexControl;

		struct Input {
			float2 uv_IndexControl;
		};

		UNITY_DECLARE_TEX2DARRAY(_TexArray);
		float4 _TerrainScaleOffset[32];
		float4 _IndexControl_TexelSize;
		float _HeightScale;
 
		void surf(Input IN, inout SurfaceOutput o) {
			fixed4 alpha = tex2D(_IndexControl, IN.uv_IndexControl);
			float2 pointUV = (floor(IN.uv_IndexControl * _IndexControl_TexelSize.zw) + 0.5) * _IndexControl_TexelSize.xy;
			fixed4 index = tex2Dlod(_IndexControl, float4(pointUV, 0, 0));
		
			float index0 = index.r * 255;
			float2 uv0 = (IN.uv_IndexControl + _TerrainScaleOffset[index0].zw) * _TerrainScaleOffset[index0].xy;
			fixed4 col0 = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(uv0, index0));
		
			float index1 = index.g * 255; 
			float2 uv1 = (IN.uv_IndexControl + _TerrainScaleOffset[index1].zw) * _TerrainScaleOffset[index1].xy;
			fixed4 col1 = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(uv1, index1));
		
			float height = (1 - abs(alpha.w * 2 - 1)) * _HeightScale;
			fixed4 col = lerp(col0, col1, clamp(alpha.w + (-col0.a + col1.a) * height, 0, 1));
		
			o.Albedo = col.rgb;  
			o.Alpha = 1; 
		}

		ENDCG
	}
 
	Dependency "AddPassShader" = "Mobile/TerrainTextureAddPass"
	Fallback "Mobile/VertexLit"
}


