Shader "Custom/TmpMist"
{
    Properties
    {
        [Header(Mist Appearance)]
        _BaseColor          ("Mist Color", Color) = (0.55, 0.45, 0.75, 0.5)
        _EmissionColor      ("Emission Color", Color) = (0.35, 0.2, 0.55, 1.0)
        _EmissionStrength   ("Emission Strength", Range(0.0, 3.0)) = 0.4
        _MistAlpha          ("Mist Alpha", Range(0.0, 1.0)) = 0.55
        _GroundMistFade     ("Ground Mist Fade", Range(0.0, 1.0)) = 1.0

        [Header(Noise and Animation)]
        _NoiseScale         ("Noise Scale", Range(0.1, 10.0)) = 1.2
        _NoiseSpeed         ("Noise Speed", Range(0.0, 5.0)) = 0.5
        _NoiseStrength      ("Noise Strength", Range(0.0, 1.0)) = 0.35

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 0
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

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float  _EmissionStrength;
                float  _MistAlpha;
                float  _GroundMistFade;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _NoiseStrength;
                float  _Cull;
                float  _SrcBlend;
                float  _DstBlend;
                float  _ZWrite;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float t = _Time.y * _NoiseSpeed;
                float2 uv1 = input.positionWS.xz * _NoiseScale + float2(t * 0.15, t * 0.10);
                float2 uv2 = input.positionWS.xz * (_NoiseScale * 1.7) - float2(t * 0.10, t * 0.18);
                float n = lerp(ValueNoise(uv1), ValueNoise(uv2), 0.5);

                half alpha = _MistAlpha * _BaseColor.a * _GroundMistFade;
                alpha *= lerp(1.0 - _NoiseStrength, 1.0 + _NoiseStrength * 0.5, n);
                alpha = saturate(alpha);

                half3 col = _BaseColor.rgb + (_EmissionColor.rgb * _EmissionStrength * (0.8 + 0.4 * n));

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
