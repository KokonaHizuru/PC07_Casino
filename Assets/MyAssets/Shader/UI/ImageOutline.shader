Shader "Aqua/UI/Image Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width (Texture Pixels)", Range(0,32)) = 2
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "ImageOutline"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 mask : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _TextureSampleAdd;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                float2 pixelSize = output.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            // テクスチャの外側は透明として扱い、Repeat設定による回り込みを防ぐ。
            half SampleAlpha(float2 uv)
            {
                half inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                return saturate((tex2D(_MainTex, uv) + _TextureSampleAdd).a) * inside;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd);
                source.rgb *= input.color.rgb;
                source.a = saturate(source.a);
                half expandedAlpha = source.a;
                // 複数の半径で調べ、細い画像でも太い線の内側に隙間を作らない。
                [loop]
                for (int radius = 1; radius <= 32; radius++)
                {
                    if ((float)(radius - 1) >= _OutlineWidth) break;
                    float2 offset = abs(_MainTex_TexelSize.xy) * min((float)radius, _OutlineWidth);
                    float2 diagonal = offset * 0.70710678;
                    expandedAlpha = max(expandedAlpha, SampleAlpha(input.texcoord + float2(offset.x, 0)));
                    expandedAlpha = max(expandedAlpha, SampleAlpha(input.texcoord - float2(offset.x, 0)));
                    expandedAlpha = max(expandedAlpha, SampleAlpha(input.texcoord + float2(0, offset.y)));
                    expandedAlpha = max(expandedAlpha, SampleAlpha(input.texcoord - float2(0, offset.y)));
                    expandedAlpha = max(expandedAlpha, SampleAlpha(input.texcoord + diagonal));
                    expandedAlpha = max(expandedAlpha, SampleAlpha(input.texcoord - diagonal));
                    expandedAlpha = max(expandedAlpha, SampleAlpha(input.texcoord + float2(diagonal.x, -diagonal.y)));
                    expandedAlpha = max(expandedAlpha, SampleAlpha(input.texcoord + float2(-diagonal.x, diagonal.y)));
                }
                half outlineAlpha = expandedAlpha * _OutlineColor.a * (1 - source.a);
                fixed4 result = fixed4(source.rgb * source.a + _OutlineColor.rgb * outlineAlpha,
                    source.a + outlineAlpha);
                half visibility = input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                visibility *= mask.x * mask.y;
                #endif
                result *= visibility;
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
