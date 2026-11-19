Shader "Custom/ProjectileTrail"
{
    Properties
    {
        [HDR] _Color ("Kolor bazowy - Base Color", Color) = (0.86, 0.84, 0.78, 1.0)
        _Intensity ("Intensywnosc (czytelność w nocy) - Intensity", Range(0.2, 3.0)) = 1.15
        _FadePower ("Krzywa zaniku ogona - Tail Fade Power", Range(0.5, 4.0)) = 1.35
        _EdgeSoftness ("Miekkosc krawedzi - Edge Softness", Range(0.01, 0.5)) = 0.16
        _CoreBoost ("Podbicie rdzenia - Core Boost", Range(0.0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "ProjectileTrail"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _FadePower;
                half _EdgeSoftness;
                half _CoreBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Antyaliasingowana krawedz poprzeczna wstegi pod kamere TD
                float edgeDist = abs(input.uv.y - 0.5) * 2.0;
                float aa = max(fwidth(edgeDist), 0.002);
                float edgeStart = saturate(1.0 - (float)_EdgeSoftness);
                float edgeMask = 1.0 - smoothstep(edgeStart - aa, 1.0 + aa * 0.5, edgeDist);

                // Zanik wzdłuż ogona sterowany gradientem TrailRenderera (t=0 głowa -> t=1 ogon)
                float tailFade = pow(saturate((float)input.color.a), (float)_FadePower);

                // Subtelne rozjasnienie osi srodkowej wstegi
                float core = saturate(1.0 - edgeDist * edgeDist) * (float)_CoreBoost * tailFade;
                half3 rgb = (input.color.rgb * _Color.rgb * _Intensity) + (half3)core;
                half alpha = (half)saturate(tailFade * edgeMask * (float)_Color.a);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
