Shader "Custom/SpriteWindSway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _WindStrength ("Sway Amount (world units)", Range(0, 0.5)) = 0.15
        _WindSpeed ("Wind Speed (cycles/sec)", Range(0, 2)) = 0.32
        _BaseAnchorHeight ("Base Anchor Height", Range(0, 1)) = 0.15
        _PhaseOffset ("Phase Offset", Float) = 0
        _SwayWave ("Sway Wave", Float) = 0
        _SpriteBottomY ("Sprite Bottom Y", Float) = -2.75
        _SpriteHeight ("Sprite Height", Float) = 5.5
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
            float _SwayWave;
            float _SpriteBottomY;
            float _SpriteHeight;
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

                float wave = _SwayWave;
                if (abs(wave) < 0.0001)
                    wave = sin(_Time.y * _WindSpeed + _PhaseOffset);

                input.vertex.x += wave * _WindStrength * swayMask;

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
