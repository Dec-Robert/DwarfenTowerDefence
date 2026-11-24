Shader "Custom/SoulMonolithBeam"
{
    Properties
    {
        [HDR] _LowChargeColor ("Low Charge Color", Color) = (0.22, 0.92, 1.0, 1.0)
        [HDR] _MidChargeColor ("Mid Charge Color", Color) = (0.42, 0.55, 1.45, 1.0)
        [HDR] _HighChargeColor ("High Charge Color", Color) = (1.45, 0.32, 1.85, 1.0)
        [HDR] _CoreColor ("Core Color", Color) = (1.35, 1.55, 1.8, 1.0)
        _Charge01 ("Charge (0..1)", Range(0.0, 1.0)) = 0.0
        _ScrollSpeed ("Energy Scroll Speed", Range(0.5, 15.0)) = 4.5
        _PulseSpeed ("Pulse Speed", Range(1.0, 20.0)) = 7.5
        _EdgeSoftness ("Edge Softness", Range(0.05, 0.6)) = 0.28
        _Intensity ("Intensity", Range(0.2, 4.0)) = 1.65
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+12"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "SoulMonolithBeam"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LowChargeColor;
                half4 _MidChargeColor;
                half4 _HighChargeColor;
                half4 _CoreColor;
                half _Charge01;
                half _ScrollSpeed;
                half _PulseSpeed;
                half _EdgeSoftness;
                half _Intensity;
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

                float charge = saturate((float)_Charge01);
                float edgeDist = abs(input.uv.y - 0.5) * 2.0;

                float outerMask = 1.0 - smoothstep(saturate(1.0 - (float)_EdgeSoftness), 1.0, edgeDist);
                float coreMask = pow(saturate(1.0 - edgeDist), 2.6);

                float speed = (float)_ScrollSpeed + charge * 5.0;
                float flow = 0.72 + 0.28 * sin(input.uv.x * 18.0 - _Time.y * speed);
                float wisp = 0.80 + 0.20 * cos(input.uv.x * 9.5 + edgeDist * 4.2 - _Time.y * (speed * 1.4));
                float pulse = 0.90 + 0.10 * sin(_Time.y * ((float)_PulseSpeed + charge * 6.0));

                float endFade = smoothstep(0.0, 0.04, input.uv.x) * smoothstep(1.0, 0.94, input.uv.x);

                half3 beamHue = charge < 0.375
                    ? lerp(_LowChargeColor.rgb, _MidChargeColor.rgb, (half)(charge / 0.375))
                    : lerp(_MidChargeColor.rgb, _HighChargeColor.rgb, (half)((charge - 0.375) / 0.625));

                half3 rgb = (beamHue * (half)(flow * wisp) + _CoreColor.rgb * (half)coreMask)
                          * input.color.rgb * _Intensity * (half)(pulse * (1.0 + charge * 0.6));
                half alpha = (half)saturate(outerMask * endFade * (float)input.color.a * (0.68 + 0.32 * charge));

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
