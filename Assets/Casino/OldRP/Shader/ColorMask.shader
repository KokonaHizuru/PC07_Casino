Shader "Custom/ColorMaskRGB"
{
	Properties
	{
		_RedMaskColor("Color Mask Red", Color) = (1,0,0,1)
		_GreenMaskColor("Color Mask Green", Color) = (0,1,0,1)
		_BlueMaskColor("Color Mask Blue", Color) = (0,0,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_MaskTex("Mask (RGB)", 2D) = "white" {}
		_MetallicGlossMap("Metallic", 2D) = "white" {}
		_BumpMap("Normal Map", 2D) = "bump" {}
		_DetailNormalMap("Detail Normal", 2D) = "bump" {}
	}
	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0

		sampler2D _MainTex;
		sampler2D _MaskTex;
		sampler2D _MetallicGlossMap;
		sampler2D _BumpMap;
		sampler2D _DetailNormalMap;

		struct Input {
			float2 uv_MainTex;
			float2 uv_MaskTex;
			float2 uv_MetallicGlossMap;
			float2 uv_BumpMap;
			float2 uv_DetailNormalMap;
		};


		fixed4 _RedMaskColor;
		fixed4 _GreenMaskColor;
		fixed4 _BlueMaskColor;

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
			fixed4 mask = tex2D(_MaskTex, IN.uv_MainTex);
			fixed4 m = tex2D(_MetallicGlossMap, IN.uv_MetallicGlossMap);

			c = lerp(c, _RedMaskColor, pow(mask.r, 2));
			c = lerp(c, _GreenMaskColor, pow(mask.g, 2));
			c = lerp(c, _BlueMaskColor, pow(mask.b, 2));
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