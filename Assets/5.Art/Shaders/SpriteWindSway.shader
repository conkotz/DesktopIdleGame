Shader "Custom/SpriteWindSway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _WindStrength ("Wind Strength (radians)", Range(0, 0.2)) = 0.1
        _WindSpeed ("Wind Speed (cycles/sec)", Range(0, 4)) = 1.2
        _BaseAnchorHeight ("Base Anchor Height", Range(0, 1)) = 0.2
        _PhaseOffset ("Phase Offset", Float) = 0
        _WindVariation ("Wind Variation", Range(0, 2)) = 0.4
        _SpriteBottomY ("Sprite Bottom Y", Float) = -2.75
        _SpriteHeight ("Sprite Height", Float) = 5.5
        _SpriteCenterX ("Sprite Center X", Float) = 0
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
            float _WindStrength;
            float _WindSpeed;
            float _BaseAnchorHeight;
            float _PhaseOffset;
            float _WindVariation;
            float _SpriteBottomY;
            float _SpriteHeight;
            float _SpriteCenterX;
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

            v2f vert(appdata_t input)
            {
                v2f output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float spriteHeight = max(_SpriteHeight, 0.001);
                float anchorY = _SpriteBottomY + spriteHeight * _BaseAnchorHeight;
                float topY = _SpriteBottomY + spriteHeight;
                float swayMask = saturate((input.vertex.y - anchorY) / max(topY - anchorY, 0.001));
                float phase = _PhaseOffset;
                float variation = _WindVariation;

                // Slow envelopes vary swing strength and time-between-swings over time.
                float varScale = saturate(variation / 0.75);
                float env = _Time.y * (0.25 + variation * 0.12) + phase * 0.2;
                float strengthMod = 0.78 + 0.22 * sin(env) + 0.1 * sin(env * 2.35 + variation * 1.7);
                float paceMod = 0.84 + 0.16 * sin(env * 0.58 + variation * 1.25) + 0.08 * sin(env * 1.47 + phase);
                strengthMod = clamp(strengthMod, 0.58, 1.12);
                paceMod = clamp(paceMod, 0.68, 1.22);
                strengthMod = lerp(1.0, strengthMod, varScale);
                paceMod = lerp(1.0, paceMod, varScale);

                float time = _Time.y * _WindSpeed * paceMod;
                float swayWave =
                    sin(time + phase) * 0.52 +
                    sin(time * 1.71 + phase * 1.31 + variation * 4.2) * 0.28 +
                    sin(time * 0.53 + phase * 2.07 + variation * 2.8) * 0.20;
                float angle = swayWave * _WindStrength * strengthMod * swayMask;

                float2 pivot = float2(_SpriteCenterX, anchorY);
                float2 offset = input.vertex.xy - pivot;
                float cosA = cos(angle);
                float sinA = sin(angle);
                input.vertex.x = cosA * offset.x - sinA * offset.y + pivot.x;
                input.vertex.y = sinA * offset.x + cosA * offset.y + pivot.y;

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
                fixed4 color = tex2D(_MainTex, input.texcoord) * input.color;

                #if ETC1_EXTERNAL_ALPHA
                fixed4 alpha = tex2D(_AlphaTex, input.texcoord);
                color.a = lerp(color.a, alpha.r, _EnableExternalAlpha);
                #endif

                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
