Shader "Custom/Mountain"
{
    Properties
    {
        [Header(Skala Granitowa (Granite Rock))]
        _RockColorDark("Ciemny grafit/lupek (Dark Rock)", Color) = (0.18, 0.19, 0.21, 1)
        _RockColorLight("Jasny granit (Light Rock)", Color) = (0.35, 0.36, 0.38, 1)
        _RockScale("Skala faktury skaly (Rock Scale)", Float) = 1.4
        _RockSmoothness("Gladkosc skaly (Rock Smoothness)", Range(0.0, 1.0)) = 0.12

        [Header(Zyly Rudy Mineralnej (Ore Veins))]
        _OreColor("Kolor rudy (Ore Color)", Color) = (0.84, 0.50, 0.22, 1)
        _OreScale("Skala zyl rudy (Ore Scale)", Float) = 2.8
        _OreThreshold("Ilosc rudy (Ore Threshold)", Range(0.5, 0.95)) = 0.74
        _OreMetallic("Metalicznosc rudy (Ore Metallic)", Range(0.0, 1.0)) = 0.85
        _OreSmoothness("Polysk rudy (Ore Smoothness)", Range(0.0, 1.0)) = 0.60

        [Header(Rzezba 3D (Depth and Bump))]
        _BumpStrength("Sila zrzeszenia skaly (Bump Strength)", Range(0.0, 5.0)) = 2.4
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
                float4 _RockColorDark;
                float4 _RockColorLight;
                float _RockScale;
                float _RockSmoothness;

                float4 _OreColor;
                float _OreScale;
                float _OreThreshold;
                float _OreMetallic;
                float _OreSmoothness;

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

            float voronoiCracks(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float minDist = 10.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(float(x), float(y));
                        float2 h = hash2(i + neighbor);
                        float2 pointInCell = neighbor + 0.1 + 0.8 * h;
                        float d = length(pointInCell - f);
                        minDist = min(minDist, d);
                    }
                }
                return minDist;
            }

            float EvaluateHeight(float2 wpos, float topMask)
            {
                float rockFbm = fbm(wpos * _RockScale, 3);
                float cracks = voronoiCracks(wpos * (_RockScale * 1.5));
                float rockHeight = 0.25 * rockFbm + 0.15 * cracks;

                // Żyły rudy lekko wgłębione w szczeliny skalne
                float veinNoise = fbm(wpos * _OreScale + rockFbm * 1.5, 3);
                float oreMask = smoothstep(_OreThreshold, _OreThreshold + 0.08, veinNoise) * topMask;

                return rockHeight - 0.06 * oreMask;
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

                // --- 1. Faktura Skały Granitowej ---
                float rockFbm = fbm(wpos * _RockScale, 4);
                float cracks = voronoiCracks(wpos * (_RockScale * 1.5));
                float rockTone = saturate(rockFbm * 0.7 + cracks * 0.4);
                float3 rockColor = lerp(_RockColorDark.rgb, _RockColorLight.rgb, rockTone);

                // Na ściankach bocznych dodajemy poziome uwarstwienie
                float strata = gradientNoise(float2(input.positionWS.y * 6.0, wpos.x * 0.5));
                rockColor *= lerp(0.85 + 0.25 * strata, 1.0, topMask);

                // --- 2. Żyły Rudy Mineralnej ---
                float veinNoise = fbm(wpos * _OreScale + rockFbm * 1.5, 3);
                float oreMask = smoothstep(_OreThreshold, _OreThreshold + 0.08, veinNoise) * topMask;
                float oreGlint = gradientNoise(wpos * (_OreScale * 3.0));
                float3 oreCol = _OreColor.rgb * (0.8 + 0.4 * oreGlint);

                // --- 3. Kompozycja Materiału ---
                float3 finalAlbedo = lerp(rockColor, oreCol, oreMask);
                float finalMetallic = lerp(0.0, _OreMetallic, oreMask);
                float finalSmoothness = lerp(_RockSmoothness, _OreSmoothness, oreMask);

                // --- 4. Proceduralny Relief 3D ---
                float delta = 0.04;
                float hL = EvaluateHeight(wpos - float2(delta, 0.0), topMask);
                float hR = EvaluateHeight(wpos + float2(delta, 0.0), topMask);
                float hD = EvaluateHeight(wpos - float2(0.0, delta), topMask);
                float hU = EvaluateHeight(wpos + float2(0.0, delta), topMask);

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
                surfaceData.metallic = finalMetallic;
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
