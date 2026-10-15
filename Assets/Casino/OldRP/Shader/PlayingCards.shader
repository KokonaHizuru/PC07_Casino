Shader "Custom/PlayingCards"
{
	Properties
	{
		_MaskColor("Color Mask", Color) = (1,0,0,1)
		_OverlayTex("Albedo (RGB)", 2D) = "white" {}
		_MaskTex("Mask (RGB)", 2D) = "white" {}
		_MetallicGlossMap("Metallic", 2D) = "white" {}
		_BumpMap("Normal Map", 2D) = "bump" {}

		[Toggle] _Invert_Mask("Invert Mask", Float) = 0
	}
	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM

		#pragma shader_feature _INVERT_MASK_ON
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0

		#include "UnityCG.cginc"

		sampler2D _OverlayTex;
		sampler2D _MaskTex;
		sampler2D _MetallicGlossMap;
		sampler2D _BumpMap;

		struct Input {
			float2 uv2_OverlayTex : TEXCOORD0;
			float2 uv2_MaskTex : TEXCOORD0;
			float2 uv_MetallicGlossMap;
			float2 uv_BumpMap;
		};


		fixed4 _MaskColor;

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D(_OverlayTex, IN.uv2_OverlayTex.xy);
			fixed4 mask = tex2D(_MaskTex, IN.uv2_MaskTex.xy);
			fixed4 m = tex2D(_MetallicGlossMap, IN.uv_MetallicGlossMap);

			#ifdef _INVERT_MASK_ON
				c = lerp(c, _MaskColor, pow(mask.r, 2));
			#else
				c = lerp(_MaskColor,c, pow(mask.r, 2));
			#endif

			o.Albedo = c;
			o.Metallic = m.rgb;
			o.Smoothness = m.a;
			o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
		}

		ENDCG

	}
		FallBack "Standard"
}


