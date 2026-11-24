Shader "Custom/BombardMuzzleVFX"
{
    Properties
    {
        [HDR] _Color ("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR] _CoreColor ("Core Color", Color) = (1.0, 0.95, 0.8, 1.0)
        _Intensity ("Intensity", Range(0.1, 6.0)) = 1.0
        _EdgeSoftness ("Edge Softness", Range(0.05, 0.95)) = 0.5
        _FalloffPower ("Falloff Power", Range(0.3, 4.0)) = 1.25
        _CoreBoost ("Core Boost", Range(0.0, 4.0)) = 0.25
        _CorePower ("Core Power", Range(0.5, 6.0)) = 2.2
        _LobeCount ("Organic Lobe Count", Range(1.0, 8.0)) = 4.0
        _LobeAmplitude ("Organic Lobe Amplitude", Range(0.0, 0.35)) = 0.12
        _TurbulenceSpeed ("Turbulence Speed", Range(0.0, 20.0)) = 2.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+15"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "BombardMuzzleVFX"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _Intensity;
                half _EdgeSoftness;
                half _FalloffPower;
                half _CoreBoost;
                half _CorePower;
                half _LobeCount;
                half _LobeAmplitude;
                half _TurbulenceSpeed;
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

                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float angle = atan2(centered.y, centered.x);

                float lobeFreq = max(1.0, (float)_LobeCount);
                float anim = _Time.y * (float)_TurbulenceSpeed;
                float wobble = sin(angle * lobeFreq + anim) * (float)_LobeAmplitude
                             + cos(angle * (lobeFreq * 2.0 - 1.0) - anim * 1.3) * ((float)_LobeAmplitude * 0.45);

                float distortedRadius = radius + wobble * smoothstep(0.05, 0.55, radius);
                float edgeStart = saturate(1.0 - (float)_EdgeSoftness);
                float mask = 1.0 - smoothstep(edgeStart, 1.0, distortedRadius);
                mask = pow(saturate(mask), max(0.01, (float)_FalloffPower));

                float coreFactor = pow(saturate(1.0 - radius), max(0.1, (float)_CorePower)) * (float)_CoreBoost;
                half3 rgb = (input.color.rgb * _Color.rgb * _Intensity) + (_CoreColor.rgb * (half)coreFactor * input.color.a);
                half alpha = (half)saturate(mask * (float)input.color.a * (float)_Color.a);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
