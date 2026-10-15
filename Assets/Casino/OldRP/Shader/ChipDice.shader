Shader "Custom/ChipDice"
{
	Properties
	{
		_MaskColor("Color Mask", Color) = (1,0,0,1)
		_MaskColor2("Color Mask2", Color) = (0,1,0,1)
		_MaskTex("Mask (RGB)", 2D) = "white" {}
		_MetallicGlossMap("Metallic", 2D) = "white" {}
		_BumpMap("Normal Map", 2D) = "bump" {}
		_DetailNormalMap("Detail Normal", 2D) = "bump" {}

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

		sampler2D _MaskTex;
		sampler2D _MetallicGlossMap;
		sampler2D _BumpMap;
		sampler2D _DetailNormalMap;

		struct Input {
			float2 uv_MaskTex;
			float2 uv_MetallicGlossMap;
			float2 uv_BumpMap;
			float2 uv_DetailNormalMap;
		};


		fixed4 _MaskColor;
		fixed4 _MaskColor2;

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = _MaskColor2;
			fixed4 mask = tex2D(_MaskTex, IN.uv_MaskTex);
			fixed4 m = tex2D(_MetallicGlossMap, IN.uv_MetallicGlossMap);

			#ifdef _INVERT_MASK_ON
				c = lerp(c, _MaskColor, pow(mask.r, 2));
			#else
				c = lerp(_MaskColor,c, pow(mask.r, 2));
			#endif

			o.Albedo = c;
			o.Metallic = m.rgb;
			o.Smoothness = m.a;

			float3 normal1 = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
			float3 normal2 = UnpackNormal(tex2D(_DetailNormalMap, IN.uv_DetailNormalMap));

			o.Normal = normalize(half3(normal1.xy + normal2.xy, normal1.z * normal2.z));
		}

		ENDCG

	}
	FallBack "Standard"
}