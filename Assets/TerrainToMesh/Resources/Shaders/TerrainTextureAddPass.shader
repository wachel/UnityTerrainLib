﻿Shader "Mobile/TerrainTextureAddPass" {
	Properties {
		//[HideInInspector] _Control ("Control (RGBA)", 2D) = "black" {}
		//[HideInInspector] _Splat3 ("Layer 3 (A)", 2D) = "white" {}
		//[HideInInspector] _Splat2 ("Layer 2 (B)", 2D) = "white" {}
		//[HideInInspector] _Splat1 ("Layer 1 (G)", 2D) = "white" {}
		//[HideInInspector] _Splat0 ("Layer 0 (R)", 2D) = "white" {}
		//[HideInInspector] _Normal3 ("Normal 3 (A)", 2D) = "bump" {}
		//[HideInInspector] _Normal2 ("Normal 2 (B)", 2D) = "bump" {}
		//[HideInInspector] _Normal1 ("Normal 1 (G)", 2D) = "bump" {}
		//[HideInInspector] _Normal0 ("Normal 0 (R)", 2D) = "bump" {}
	}

	CGINCLUDE
		#pragma surface surf Lambert decal:add vertex:SplatmapVert finalcolor:SplatmapFinalColor finalprepass:SplatmapFinalPrepass finalgbuffer:SplatmapFinalGBuffer
		#pragma multi_compile_fog
		#define TERRAIN_SPLAT_ADDPASS
		#include "TerrainSplatmapCommon.cginc"

		void surf(Input IN, inout SurfaceOutput o)
		{
			o.Albedo = fixed3(0,0,0);
			o.Alpha = 0;
			o.Normal = fixed3(0, 0, 0);
		}
	ENDCG

	Category {
		Tags {
			"Queue" = "Geometry+2"
			"IgnoreProjector"="True"
			"RenderType" = "Opaque"
		}
		SubShader { // for sm3.0+ targets
			Cull Front	//防止画出来
			ZTest Less	//防止画出来
			CGPROGRAM
				#pragma target 3.0
				#pragma multi_compile __ _TERRAIN_NORMAL_MAP
			ENDCG
		}
		SubShader { // for sm2.0 targets
			Cull Front	//防止画出来
			ZTest Less	//防止画出来
			CGPROGRAM
			ENDCG
		}
	}

	Fallback off
}
