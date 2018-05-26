Shader "Hidden/BakeTextureIndex"
{
	Properties
	{
		_LastResult("LastResult", 2D) = "black" {}			//上次计算的结果，rg保存最大的index，ba保存最大的值
		_SplatAlpha("Splat Alpha",2D) = "black" {}			//rgba传入四个通道
		_StartLayerIndex("LayerIndex", float) = 0			//从哪层开始，即上面的r对应第几层
	}
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		//得到base texture
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _LastResult;
			sampler2D _SplatAlpha;
			float4 _SplatAlpha_ST;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _SplatAlpha);
				return o;
			}

			float4 UpdateMax(float4 last, float value, float index) {
				if (value > last.z && value > last.w) {
					last.yw = last.xz;
					last.x = index;
					last.z = value;
				}
				else if(value > last.w){
					last.y = index;
					last.w = value;
				}
				return last;
			}
			
			float _StartLayerIndex;
			fixed4 frag (v2f i) : SV_Target
			{
				float4 last = tex2D(_LastResult, i.uv);
				float4 alpha = tex2D(_SplatAlpha, i.uv);

				last = UpdateMax(last, alpha.x, (_StartLayerIndex + 0) / 255);
				last = UpdateMax(last, alpha.y, (_StartLayerIndex + 1) / 255);
				last = UpdateMax(last, alpha.z, (_StartLayerIndex + 2) / 255);
				last = UpdateMax(last, alpha.w, (_StartLayerIndex + 3) / 255);
				return last;

			}
			ENDCG
		}

	}
}
