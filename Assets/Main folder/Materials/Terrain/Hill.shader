Shader "Custom/Hill"
{
    Properties
    {
        [Header(Wyzynna Trawa (Highland Grass))]
        _HighlandGrassDark("Ciemniejsza trawa (Dark Grass)", Color) = (0.24, 0.38, 0.16, 1)
        _HighlandGrassLight("Jasna trawa wyzynna (Light Grass)", Color) = (0.42, 0.58, 0.22, 1)
        _GrassScale("Skala trawy (Grass Scale)", Float) = 1.1
        _GrassSmoothness("Matowosc trawy (Grass Smoothness)", Range(0.0, 1.0)) = 0.05

        [Header(Odsłoniecia Skalne na Szczycie (Rock Outcrops))]
        _RockColorDark("Ciemna skala wapienna (Dark Rock)", Color) = (0.38, 0.38, 0.36, 1)
        _RockColorLight("Jasna skala (Light Rock)", Color) = (0.60, 0.60, 0.56, 1)
        _RockOutcropScale("Skala wychodni skalnych (Rock Scale)", Float) = 1.8
        _RockCoverage("Ilosc skal na szczycie (Rock Coverage)", Range(0.0, 1.0)) = 0.38
        _RockSmoothness("Gladkosc skaly (Rock Smoothness)", Range(0.0, 1.0)) = 0.18

        [Header(Klify na Sciankach Bocznych (Cliff Strata))]
        _CliffStrataScale("Skala pasow klifu (Cliff Scale)", Float) = 4.5
        _BumpStrength("Sila wypuklosci (Bump Strength)", Range(0.0, 5.0)) = 2.0
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
                float4 _HighlandGrassDark;
                float4 _HighlandGrassLight;
                float _GrassScale;
                float _GrassSmoothness;

                float4 _RockColorDark;
                float4 _RockColorLight;
                float _RockOutcropScale;
                float _RockCoverage;
                float _RockSmoothness;

                float _CliffStrataScale;
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

            float EvaluateHeight(float3 posWS, float topMask)
            {
                float2 wpos = posWS.xz;
                float grassFbm = fbm(wpos * _GrassScale, 2);
                float rockNoise = fbm(wpos * _RockOutcropScale, 3);
                float rockMask = smoothstep(1.0 - _RockCoverage - 0.1, 1.0 - _RockCoverage + 0.1, rockNoise);

                float topHeight = lerp(0.15 * grassFbm, 0.35 + 0.15 * rockNoise, rockMask);
                float cliffHeight = 0.25 * gradientNoise(float2(posWS.y * _CliffStrataScale, (wpos.x + wpos.y) * 0.5));

                return lerp(cliffHeight, topHeight, topMask);
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

                // --- 1. Góra Heksa: Trawa Wyżynna i Odsłonięcia Skalne ---
                float grassFbm = fbm(wpos * _GrassScale, 3);
                float3 grassCol = lerp(_HighlandGrassDark.rgb, _HighlandGrassLight.rgb, grassFbm);

                float rockNoise = fbm(wpos * _RockOutcropScale, 3);
                float rockMask = smoothstep(1.0 - _RockCoverage - 0.1, 1.0 - _RockCoverage + 0.1, rockNoise);
                float3 rockCol = lerp(_RockColorDark.rgb, _RockColorLight.rgb, rockNoise);

                float3 topAlbedo = lerp(grassCol, rockCol, rockMask);
                float topSmoothness = lerp(_GrassSmoothness, _RockSmoothness, rockMask);

                // --- 2. Boki Heksa: Skalisty Klif z Uwarstwieniem ---
                float strata = gradientNoise(float2(input.positionWS.y * _CliffStrataScale, (wpos.x + wpos.y) * 0.6));
                float3 cliffCol = lerp(_RockColorDark.rgb * 0.9, _RockColorLight.rgb * 0.85, strata);

                // --- 3. Kompozycja Albedo i Smoothness ---
                float3 finalAlbedo = lerp(cliffCol, topAlbedo, topMask);
                float finalSmoothness = lerp(_RockSmoothness, topSmoothness, topMask);

                // --- 4. Proceduralny Relief 3D ---
                float delta = 0.04;
                float hL = EvaluateHeight(input.positionWS - float3(delta, 0.0, 0.0), topMask);
                float hR = EvaluateHeight(input.positionWS + float3(delta, 0.0, 0.0), topMask);
                float hD = EvaluateHeight(input.positionWS - float3(0.0, 0.0, delta), topMask);
                float hU = EvaluateHeight(input.positionWS + float3(0.0, 0.0, delta), topMask);

                float3 bumpOffset = float3((hL - hR) * _BumpStrength, 0.0, (hD - hU) * _BumpStrength);
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
