Shader "Custom/FertileSoil"
{
    Properties
    {
        [Header(Gleba (Soil))]
        _SoilColorDark("Ciemna gleba (Dark Soil)", Color) = (0.10, 0.06, 0.03, 1)
        _SoilColorLight("Jasna gleba (Light Soil)", Color) = (0.24, 0.15, 0.08, 1)
        _SoilScale("Skala faktury gleby (Soil Scale)", Float) = 1.2
        _SoilSmoothness("Wilgotnosc gleby (Soil Smoothness)", Range(0.0, 1.0)) = 0.42

        [Header(Bujna Trawa (Lush Grass))]
        _GrassColorDeep("Gleboka zielen (Deep Grass)", Color) = (0.16, 0.35, 0.08, 1)
        _GrassColorLush("Soczysta zielen (Lush Grass)", Color) = (0.36, 0.72, 0.15, 1)
        _GrassScale("Skala kep trawy (Grass Scale)", Float) = 0.65
        _GrassCoverage("Pokrycie trawa (Grass Coverage)", Range(0.0, 1.0)) = 0.68
        _GrassSmoothness("Matowosc trawy (Grass Smoothness)", Range(0.0, 1.0)) = 0.06

        [Header(Kwiaty (Flowers))]
        _FlowerColor1("Kolor kwiatow 1 - Zloty (Flower 1)", Color) = (0.98, 0.82, 0.18, 1)
        _FlowerColor2("Kolor kwiatow 2 - Jasny (Flower 2)", Color) = (0.96, 0.96, 0.90, 1)
        _FlowerColor3("Kolor kwiatow 3 - Fiolet (Flower 3)", Color) = (0.76, 0.32, 0.82, 1)
        _FlowerScale("Zageszczenie obszarow (Flower Scale)", Float) = 2.4
        _FlowerDensity("Prawdopodobienstwo (Density)", Range(0.0, 1.0)) = 0.65
        _FlowerSize("Wielkosc kepki kwiatow (Size)", Range(0.1, 1.5)) = 0.55

        [Header(Wypuklosci 3D (Depth and Bump))]
        _BumpStrength("Sila wypuklosci (Bump Strength)", Range(0.0, 5.0)) = 1.8
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

            // Universal Pipeline keywords
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
                float4 _SoilColorDark;
                float4 _SoilColorLight;
                float _SoilScale;
                float _SoilSmoothness;

                float4 _GrassColorDeep;
                float4 _GrassColorLush;
                float _GrassScale;
                float _GrassCoverage;
                float _GrassSmoothness;

                float4 _FlowerColor1;
                float4 _FlowerColor2;
                float4 _FlowerColor3;
                float _FlowerScale;
                float _FlowerDensity;
                float _FlowerSize;

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

            // --- Procedural Math & Noise Functions ---

            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            float hash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
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

            float2 voronoi(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float minDist = 10.0;
                float cellId = 0.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(float(x), float(y));
                        float2 cellCoord = i + neighbor;
                        float2 h = hash2(cellCoord);
                        float2 pointInCell = neighbor + 0.2 + 0.6 * h;
                        float d = length(pointInCell - f);
                        if (d < minDist)
                        {
                            minDist = d;
                            cellId = hash1(cellCoord);
                        }
                    }
                }
                return float2(minDist, cellId);
            }

            // Evaluates procedural height for relief / normal calculation
            float EvaluateHeight(float2 wpos, float topMask)
            {
                float soilNoise = fbm(wpos * _SoilScale, 2);
                float soilHeight = 0.06 * soilNoise;

                float grassNoise = fbm(wpos * _GrassScale, 3);
                float grassMask = smoothstep(1.0 - _GrassCoverage - 0.15, 1.0 - _GrassCoverage + 0.15, grassNoise) * topMask;
                float grassHeight = 0.45 + 0.25 * grassNoise;

                float2 vFlower = voronoi(wpos * _FlowerScale);
                float flowerRadius = _FlowerSize * 0.35;
                float flowerMask = smoothstep(flowerRadius, flowerRadius * 0.3, vFlower.x);
                flowerMask *= step(1.0 - _FlowerDensity, vFlower.y);
                flowerMask *= grassMask;
                float flowerHeight = 0.85 + 0.15 * (1.0 - saturate(vFlower.x / max(flowerRadius, 0.001)));

                float h = lerp(soilHeight, grassHeight, grassMask);
                h = lerp(h, flowerHeight, flowerMask);
                return h;
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

                // --- 1. Gleba (Soil) ---
                float soilNoise = fbm(wpos * _SoilScale, 3);
                float3 soilColor = lerp(_SoilColorDark.rgb, _SoilColorLight.rgb, soilNoise);

                // --- 2. Bujna Trawa (Lush Grass) ---
                float grassNoise = fbm(wpos * _GrassScale, 4);
                float grassMask = smoothstep(1.0 - _GrassCoverage - 0.15, 1.0 - _GrassCoverage + 0.15, grassNoise) * topMask;
                float grassTone = fbm(wpos * (_GrassScale * 2.2) + 5.7, 2);
                float3 grassColor = lerp(_GrassColorDeep.rgb, _GrassColorLush.rgb, grassTone);

                // --- 3. Kwiaty (Flowers) ---
                float2 vFlower = voronoi(wpos * _FlowerScale);
                float dist = vFlower.x;
                float flowerId = vFlower.y;
                float flowerRadius = _FlowerSize * 0.35;
                float flowerMask = smoothstep(flowerRadius, flowerRadius * 0.3, dist);
                flowerMask *= step(1.0 - _FlowerDensity, flowerId);
                flowerMask *= grassMask; // Kwiaty rosną tylko na bujnej trawie

                float3 flowerColor = _FlowerColor1.rgb;
                if (flowerId > 0.66)
                    flowerColor = _FlowerColor3.rgb;
                else if (flowerId > 0.33)
                    flowerColor = _FlowerColor2.rgb;

                // --- 4. Złożenie Albedo & Smoothness ---
                float3 finalAlbedo = lerp(soilColor, grassColor, grassMask);
                finalAlbedo = lerp(finalAlbedo, flowerColor, flowerMask);

                float finalSmoothness = lerp(_SoilSmoothness, _GrassSmoothness, grassMask);
                finalSmoothness = lerp(finalSmoothness, 0.1, flowerMask);

                // --- 5. Wypukłości 3D (Procedural Normal / Bump) ---
                float delta = 0.04;
                float hL = EvaluateHeight(wpos - float2(delta, 0.0), topMask);
                float hR = EvaluateHeight(wpos + float2(delta, 0.0), topMask);
                float hD = EvaluateHeight(wpos - float2(0.0, delta), topMask);
                float hU = EvaluateHeight(wpos + float2(0.0, delta), topMask);

                float3 bumpOffset = float3((hL - hR) * _BumpStrength, 0.0, (hD - hU) * _BumpStrength);
                float3 normalWS = normalize(input.normalWS + bumpOffset * topMask);

                // --- 6. URP PBR Lighting ---
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

        // Shadow Caster Pass
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

        // DepthOnly Pass
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
                UNITY_TRANSFER_INSTANCE_ID(input, output);
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
