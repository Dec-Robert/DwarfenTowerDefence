Shader "Custom/Sinkhole"
{
    Properties
    {
        [Header(Ciemny Mul i Bloto (Mud and Silt))]
        _MudColorDark("Ciemny mul (Dark Mud)", Color) = (0.12, 0.10, 0.08, 1)
        _MudColorLight("Wilgotna ziemia (Damp Earth)", Color) = (0.24, 0.20, 0.16, 1)
        _MudScale("Skala mulu (Mud Scale)", Float) = 1.3
        _MudSmoothness("Wilgotnosc mulu (Mud Smoothness)", Range(0.0, 1.0)) = 0.40

        [Header(Stojaca Woda i Kaludze (Water Puddles))]
        _WaterColor("Ciemna metna woda (Water Color)", Color) = (0.08, 0.12, 0.12, 1)
        _WaterLevel("Ilosc wody / kaluz (Water Level)", Range(0.0, 0.8)) = 0.34
        _WaterSmoothness("Lustro wody (Water Smoothness)", Range(0.5, 1.0)) = 0.92

        [Header(Spekana Skala i Relief (Cracked Rock and Bump))]
        _RockColor("Ciemna skala (Dark Rock)", Color) = (0.17, 0.16, 0.16, 1)
        _BumpStrength("Sila wypuklosci (Bump Strength)", Range(0.0, 5.0)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MudColorDark;
                float4 _MudColorLight;
                float _MudScale;
                float _MudSmoothness;

                float4 _WaterColor;
                float _WaterLevel;
                float _WaterSmoothness;

                float4 _RockColor;
                float _BumpStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float  fogFactor    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            float gradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float2 g00 = hash2(i + float2(0.0, 0.0)) * 2.0 - 1.0;
                float2 g10 = hash2(i + float2(1.0, 0.0)) * 2.0 - 1.0;
                float2 g01 = hash2(i + float2(0.0, 1.0)) * 2.0 - 1.0;
                float2 g11 = hash2(i + float2(1.0, 1.0)) * 2.0 - 1.0;

                float n00 = dot(g00, f - float2(0.0, 0.0));
                float n10 = dot(g10, f - float2(1.0, 0.0));
                float n01 = dot(g01, f - float2(0.0, 1.0));
                float n11 = dot(g11, f - float2(1.0, 1.0));

                return 0.5 + 0.5 * lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
            }

            float fbm(float2 p, int octaves)
            {
                float val = 0.0;
                float amp = 0.5;
                float freq = 1.0;
                for (int i = 0; i < octaves; i++)
                {
                    val += amp * gradientNoise(p * freq);
                    freq *= 2.0;
                    amp *= 0.5;
                }
                return val;
            }

            float EvaluateRawHeight(float2 wpos)
            {
                return fbm(wpos * _MudScale, 3);
            }

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 wpos = input.positionWS.xz;
                float topMask = smoothstep(0.2, 0.6, input.normalWS.y);

                // --- 1. Wysokość i Maska Kałuż Wody ---
                float rawHeight = EvaluateRawHeight(wpos);
                float waterMask = smoothstep(_WaterLevel + 0.04, _WaterLevel - 0.04, rawHeight) * topMask;

                // --- 2. Kolory Błota i Spękanej Skały ---
                float mudNoise = fbm(wpos * (_MudScale * 1.8), 2);
                float3 mudCol = lerp(_MudColorDark.rgb, _MudColorLight.rgb, mudNoise);

                float rockFactor = smoothstep(0.65, 0.85, rawHeight);
                float3 groundCol = lerp(mudCol, _RockColor.rgb, rockFactor);

                // Na ściankach bocznych ciemny podziemny przekrój
                groundCol *= lerp(0.7, 1.0, topMask);

                // --- 3. Kompozycja Albedo i Smoothness ---
                float3 finalAlbedo = lerp(groundCol, _WaterColor.rgb, waterMask);
                float finalSmoothness = lerp(_MudSmoothness, _WaterSmoothness, waterMask);

                // --- 4. Relief 3D (Woda jest idealnie gładka, woda tłumi bump) ---
                float delta = 0.04;
                float hL = EvaluateRawHeight(wpos - float2(delta, 0.0));
                float hR = EvaluateRawHeight(wpos + float2(delta, 0.0));
                float hD = EvaluateRawHeight(wpos - float2(0.0, delta));
                float hU = EvaluateRawHeight(wpos + float2(0.0, delta));

                float3 bumpOffset = float3((hL - hR) * _BumpStrength, 0.0, (hD - hU) * _BumpStrength);
                // Woda wypłaszcza wektor normalny do idealnej tafli
                bumpOffset *= (1.0 - waterMask) * topMask;
                float3 normalWS = normalize(input.normalWS + bumpOffset);

                // --- 5. URP PBR Lighting ---
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo;
                surfaceData.metallic = 0.0;
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.smoothness = finalSmoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
                finalColor.rgb = MixFog(finalColor.rgb, inputData.fogCoord);
                return finalColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INPUT_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
