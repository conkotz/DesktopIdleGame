Shader "Custom/SpriteWaterSurface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _RippleSpeed ("Ripple Speed", Range(0, 3)) = 0.9
        _RippleStrength ("Ripple Strength", Range(0, 0.025)) = 0.015
        _RippleScale ("Ripple Scale", Range(1, 30)) = 10
        _ShimmerStrength ("Shimmer Strength", Range(0, 0.2)) = 0.14
        _PhaseOffset ("Phase Offset", Float) = 0
        _WaterRectMin ("Water Rect Min (UV)", Vector) = (0.34, 0.36, 0, 0)
        _WaterRectMax ("Water Rect Max (UV)", Vector) = (0.66, 0.51, 0, 0)
        _WaterEdgeSoftness ("Water Edge Softness", Range(0.001, 0.15)) = 0.025
        _ColorMaskStrength ("Color Mask Strength", Range(0, 2)) = 1
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _RendererColor;
            float _RippleSpeed;
            float _RippleStrength;
            float _RippleScale;
            float _ShimmerStrength;
            float _PhaseOffset;
            float4 _WaterRectMin;
            float4 _WaterRectMax;
            float _WaterEdgeSoftness;
            float _ColorMaskStrength;
            float _EnableExternalAlpha;

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float4 _MainTex_ST;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float GetRectMask(float2 uv)
            {
                float2 rectMin = _WaterRectMin.xy;
                float2 rectMax = _WaterRectMax.xy;
                float edge = max(_WaterEdgeSoftness, 0.001);
                float2 lower = smoothstep(rectMin, rectMin + edge, uv);
                float2 upper = smoothstep(rectMax, rectMax - edge, uv);
                return lower.x * lower.y * upper.x * upper.y;
            }

            float GetColorMask(fixed4 sampleColor)
            {
                float teal = saturate((sampleColor.b - sampleColor.r) * 3.5 + 0.05);
                float green = saturate((sampleColor.g - sampleColor.r) * 2.0 + 0.05);
                float colorMask = teal * green;
                return lerp(1.0, colorMask, saturate(_ColorMaskStrength));
            }

            v2f vert(appdata_t input)
            {
                v2f output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #ifdef PIXELSNAP_ON
                input.vertex = UnityPixelSnap(input.vertex);
                #endif

                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color * _RendererColor;

                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                fixed4 baseColor = tex2D(_MainTex, uv) * input.color;
                float waterMask = GetRectMask(uv) * GetColorMask(baseColor);
                waterMask *= waterMask;

                float waveTime = _Time.y * _RippleSpeed + _PhaseOffset;
                float2 ripple = float2(
                    sin((uv.x + uv.y) * _RippleScale + waveTime),
                    cos((uv.x - uv.y) * _RippleScale * 1.17 - waveTime * 1.08)
                );
                float2 sampleUv = uv + ripple * _RippleStrength * waterMask;

                fixed4 color = tex2D(_MainTex, sampleUv) * input.color;

                #if ETC1_EXTERNAL_ALPHA
                fixed4 alpha = tex2D(_AlphaTex, sampleUv);
                color.a = lerp(color.a, alpha.r, _EnableExternalAlpha);
                #endif

                float shimmer = sin(waveTime * 2.2 + uv.x * 24.0 + uv.y * 17.0) * 0.5 + 0.5;
                color.rgb = lerp(color.rgb, color.rgb * (1.0 + _ShimmerStrength * shimmer), waterMask);

                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
