Shader "Custom/CrucibleBurnZone"
{
    Properties
    {
        [HDR] _CoreColor ("Core Ember Color", Color) = (2.4, 1.35, 0.28, 1.0)
        [HDR] _EmberColor ("Ember Crack Color", Color) = (1.9, 0.48, 0.06, 1.0)
        _CharColor ("Charred Crust Color", Color) = (0.10, 0.04, 0.03, 0.92)
        _CrackScale ("Crack Scale", Range(1.0, 16.0)) = 5.5
        _CrackSharpness ("Crack Sharpness", Range(1.0, 8.0)) = 3.2
        _CoreHeat ("Core Heat", Range(0.0, 2.0)) = 0.85
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 3.2
        _EdgeNoiseScale ("Edge Noise Scale", Range(1.0, 12.0)) = 3.8
        _EdgeNoiseAmp ("Edge Noise Amplitude", Range(0.0, 0.45)) = 0.22
        _EdgeSoftness ("Edge Softness", Range(0.05, 0.6)) = 0.24
        _PhaseFade ("Lifecycle Fade", Range(0.0, 1.0)) = 1.0
        [Toggle] _IsFireballCore ("Fireball Core Mode", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+8"
            "IgnoreProjector" = "True"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "CrucibleBurnZone"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EmberColor;
                half4 _CharColor;
                half _CrackScale;
                half _CrackSharpness;
                half _CoreHeat;
                half _PulseSpeed;
                half _EdgeNoiseScale;
                half _EdgeNoiseAmp;
                half _EdgeSoftness;
                half _PhaseFade;
                half _IsFireballCore;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float RidgeNoise(float2 p)
            {
                float n = ValueNoise2D(p);
                return 1.0 - abs(n * 2.0 - 1.0);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = worldPos;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float t = _Time.y;
                float fade = saturate((float)_PhaseFade);

                if (_IsFireballCore > 0.5h)
                {
                    float2 sphereUV = input.positionOS.xy * 2.4 + input.positionOS.yz * 1.7;
                    float c1 = RidgeNoise(sphereUV * (float)_CrackScale + float2(t * 0.75, -t * 0.55));
                    float c2 = RidgeNoise(sphereUV * ((float)_CrackScale * 1.6) - float2(t * 0.6, t * 0.4) + float2(11.3, 27.9));
                    float magma = pow(saturate(c1 * 0.55 + c2 * 0.45), max(0.5, (float)_CrackSharpness));

                    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float ndotv = saturate(abs(dot(normalize(input.normalWS), viewDirWS)));
                    float fresnel = pow(1.0 - ndotv, 2.0);

                    half3 coreRgb = lerp(_EmberColor.rgb, _CoreColor.rgb, (half)saturate(magma + fresnel * 0.5));
                    coreRgb = lerp(_CharColor.rgb, coreRgb, (half)saturate(0.45 + magma * 0.55 + fresnel * 0.4));
                    return half4(coreRgb * (half)fade, 1.0h);
                }

                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);

                float edgeNoise = ValueNoise2D(centered * (float)_EdgeNoiseScale + float2(5.2, 9.1)) * 0.65
                                + ValueNoise2D(centered * ((float)_EdgeNoiseScale * 2.3) - float2(3.7, 1.9)) * 0.35;
                float distortedR = radius + (edgeNoise - 0.5) * (float)_EdgeNoiseAmp;
                float discMask = 1.0 - smoothstep(saturate(1.0 - (float)_EdgeSoftness), 1.0, distortedR);

                float crackA = RidgeNoise(centered * (float)_CrackScale + float2(t * 0.12, -t * 0.08));
                float crackB = RidgeNoise(centered * ((float)_CrackScale * 1.85) + float2(-t * 0.16, t * 0.11) + float2(17.3, 31.7));
                float crackField = pow(saturate(crackA * 0.6 + crackB * 0.4), max(0.5, (float)_CrackSharpness));

                float coreHeat = pow(saturate(1.0 - distortedR), 2.1) * (float)_CoreHeat;
                float pulse = 0.85 + 0.15 * sin(t * (float)_PulseSpeed + edgeNoise * 6.2831);
                float emberMask = saturate(crackField * 1.25 + coreHeat) * pulse;

                float heatFade = pow(fade, 1.4);
                half3 rgb = lerp(_CharColor.rgb, _EmberColor.rgb, (half)saturate(emberMask * heatFade));
                rgb += _CoreColor.rgb * (half)(saturate(coreHeat * crackField + coreHeat * 0.6) * heatFade);

                half alpha = (half)saturate(discMask * (float)_CharColor.a * fade);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
