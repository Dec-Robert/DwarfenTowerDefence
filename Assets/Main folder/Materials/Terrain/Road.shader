Shader "Custom/Road"
{
    Properties
    {
        [Header(Trakt Ziemny (Dirt Path))]
        _DirtColorDark("Ciemna ziemia (Dark Dirt)", Color) = (0.36, 0.24, 0.15, 1)
        _DirtColorLight("Jasny ubity piasek (Light Dirt)", Color) = (0.54, 0.39, 0.26, 1)
        _DirtScale("Skala ziemi (Dirt Scale)", Float) = 1.3
        _DirtSmoothness("Wilgotnosc traktu (Dirt Smoothness)", Range(0.0, 1.0)) = 0.35

        [Header(Kamienie Brukowe (Cobblestones))]
        _StoneColorDark("Ciemny kamien (Dark Stone)", Color) = (0.34, 0.31, 0.28, 1)
        _StoneColorLight("Jasny kamien (Light Stone)", Color) = (0.50, 0.46, 0.42, 1)
        _CobbleScale("Zageszczenie kamieni (Cobble Scale)", Float) = 2.0
        _CobbleCoverage("Ilosc bruku (Cobble Coverage)", Range(0.0, 1.0)) = 0.52
        _StoneSmoothness("Gladkosc kamienia (Stone Smoothness)", Range(0.0, 1.0)) = 0.15

        [Header(Zwir i Wypuklosci 3D (Gravel and Bump))]
        _GravelScale("Skala zwiru (Gravel Scale)", Float) = 5.5
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
                float4 _DirtColorDark;
                float4 _DirtColorLight;
                float _DirtScale;
                float _DirtSmoothness;

                float4 _StoneColorDark;
                float4 _StoneColorLight;
                float _CobbleScale;
                float _CobbleCoverage;
                float _StoneSmoothness;

                float _GravelScale;
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
                        float2 pointInCell = neighbor + 0.15 + 0.7 * h;
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

            float EvaluateHeight(float2 wpos, float topMask)
            {
                float dirtNoise = fbm(wpos * _DirtScale, 2);
                float gravelNoise = gradientNoise(wpos * _GravelScale);
                float baseHeight = 0.1 * dirtNoise + 0.05 * gravelNoise;

                float2 vCobble = voronoi(wpos * _CobbleScale);
                float stoneRadius = 0.38;
                float stoneMask = smoothstep(stoneRadius, stoneRadius * 0.4, vCobble.x);
                stoneMask *= step(1.0 - _CobbleCoverage, vCobble.y) * topMask;

                float stoneHeight = 0.45 + 0.15 * (1.0 - saturate(vCobble.x / stoneRadius));
                return lerp(baseHeight, stoneHeight, stoneMask);
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

                // --- 1. Podłoże Gliniasto-Ziemne ---
                float dirtNoise = fbm(wpos * _DirtScale, 3);
                float gravel = gradientNoise(wpos * _GravelScale);
                float3 dirtColor = lerp(_DirtColorDark.rgb, _DirtColorLight.rgb, dirtNoise);
                dirtColor = lerp(dirtColor, dirtColor * 0.85, gravel * 0.5);

                // --- 2. Kamienie Brukowe (Voronoi) ---
                float2 vCobble = voronoi(wpos * _CobbleScale);
                float dist = vCobble.x;
                float cellId = vCobble.y;
                float stoneRadius = 0.38;
                float stoneMask = smoothstep(stoneRadius, stoneRadius * 0.4, dist);
                stoneMask *= step(1.0 - _CobbleCoverage, cellId) * topMask;

                float stoneTone = hash1(float2(cellId, 3.71));
                float3 stoneColor = lerp(_StoneColorDark.rgb, _StoneColorLight.rgb, stoneTone);
                // Cieniowanie ku krawędziom kamienia
                stoneColor *= 0.8 + 0.25 * (1.0 - saturate(dist / stoneRadius));

                // --- 3. Kompozycja Albedo i Smoothness ---
                float3 finalAlbedo = lerp(dirtColor, stoneColor, stoneMask);
                float finalSmoothness = lerp(_DirtSmoothness, _StoneSmoothness, stoneMask);

                // --- 4. Proceduralny Relief 3D (Normal/Bump) ---
                float delta = 0.04;
                float hL = EvaluateHeight(wpos - float2(delta, 0.0), topMask);
                float hR = EvaluateHeight(wpos + float2(delta, 0.0), topMask);
                float hD = EvaluateHeight(wpos - float2(0.0, delta), topMask);
                float hU = EvaluateHeight(wpos + float2(0.0, delta), topMask);

                float3 bumpOffset = float3((hL - hR) * _BumpStrength, 0.0, (hD - hU) * _BumpStrength);
                float3 normalWS = normalize(input.normalWS + bumpOffset * topMask);

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
