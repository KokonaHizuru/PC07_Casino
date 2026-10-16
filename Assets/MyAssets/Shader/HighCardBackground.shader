Shader "Casino/Camera Gradient Background"
{
    Properties
    {
        _BottomColor ("Bottom Color", Color) = (0.025, 0.002, 0.005, 1)
        _TopColor ("Top Color", Color) = (0.28, 0.012, 0.018, 1)
        _GradientPower ("Gradient Curve", Range(0.2, 4)) = 1
        _LightColor ("Top Light Color", Color) = (0.8, 0.38, 0.12, 1)
        _LightStrength ("Top Light Strength", Range(0, 2)) = 0.18
        _LightSpread ("Top Light Spread", Range(0.01, 1)) = 0.45
        _LightSoftness ("Top Light Falloff", Range(0.2, 4)) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            half4 _BottomColor, _TopColor, _LightColor;
            float _GradientPower, _LightStrength, _LightSpread, _LightSoftness;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 screenPosition : TEXCOORD0;
            };

            // カメラの向きに関係なく、画面の上下を基準としたグラデーションにする。
            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(vertex);
                output.screenPosition = ComputeScreenPos(output.position);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = saturate(input.screenPosition.y / input.screenPosition.w);
                half3 background = lerp(_BottomColor.rgb, _TopColor.rgb, pow(height, max(0.01, _GradientPower)));
                float glow = smoothstep(1.0 - max(0.01, _LightSpread), 1.0, height);
                glow = pow(glow, max(0.01, _LightSoftness)) * max(0.0, _LightStrength);
                return half4(background + _LightColor.rgb * glow, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
