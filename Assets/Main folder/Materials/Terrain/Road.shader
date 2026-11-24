Shader "Custom/Road"
{
    Properties
    {
        [Header(Debug Diagnostics)]
        _DebugRoadMode("Debug (0:Norm, 1:Grid, 2:Idx, 3:Gravel, 4:Albedo, 5:Shoulder)", Range(0, 5)) = 0
        _DebugEdgeMask("Debug Edge Mask (0 = z mesha, 63 = wszystkie)", Float) = 0.0

        [Header(Terrain Palette sRGB)]
        _Color0("0: Ciemna ziemia (#443430)", Color) = (0.26666668, 0.20392157, 0.1882353, 1.0)
        _Color1("1: Wilgotna ziemia (#544038)", Color) = (0.32941177, 0.2509804, 0.21960784, 1.0)
        _Color2("2: Bazowy braz (#6B5344)", Color) = (0.41960785, 0.3254902, 0.26666668, 1.0)
        _Color3("3: Sucha ziemia (#70574A)", Color) = (0.4392157, 0.34117648, 0.2901961, 1.0)
        _Color4("4: Jasny pyl (#846A54)", Color) = (0.5176471, 0.41568628, 0.32941177, 1.0)
        _Color5("5: Ciemny zwir (#4F443F)", Color) = (0.30980393, 0.26666668, 0.24705882, 1.0)

        [Header(Retro Texel Grid)]
        _TexelDensity("Texel Density (per hex width)", Float) = 32.0
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0
        _Seed("Seed wzoru - Seed", Float) = 23.0

        [Header(Procedural Dirt Structure)]
        _ClusterSize("Cluster Size (4-7 texels)", Range(2.0, 10.0)) = 5.8
        _MacroNoiseScale("Macro Noise Scale", Float) = 0.24
        _MacroNoiseStrength("Macro Noise Strength", Range(0.0, 1.0)) = 0.34
        _MicroNoiseScale("Micro Noise Scale", Float) = 0.85
        _MicroNoiseStrength("Micro Noise Strength", Range(0.0, 0.50)) = 0.13
        _Contrast("Contrast (sredni)", Range(0.4, 2.0)) = 0.86

        [Header(Gravel and Pebbles)]
        _DirtAmount("Gravel Amount (skupiska zwiru)", Range(0.0, 0.50)) = 0.09
        _DetailAmount("Large Pebble Density (kamyki)", Range(0.0, 1.0)) = 0.18

        [Header(Dynamic Shoulders)]
        _ShoulderColor("Kolor pobocza - Shoulder Color", Color) = (0.13725491, 0.09803922, 0.09019608, 1.0)
        _ShoulderWidth("Szerokosc pobocza - Shoulder Width", Range(0.01, 0.20)) = 0.10
        _ShoulderStrength("Sila pobocza - Shoulder Strength", Range(0.0, 1.0)) = 1.00
        _ShoulderNoiseStrength("Nieregularnosc pobocza - Shoulder Noise", Range(0.0, 1.0)) = 0.20
        _ShoulderEmission("Emisja pobocza - Shoulder Emission", Range(0.0, 1.0)) = 0.00
        _ShoulderEmissionColor("Kolor emisji pobocza - Emission Color", Color) = (0.24705882, 0.18039216, 0.15686275, 1.0)

        [Header(Low Light Readability)]
        _AmbientBoost("Jasnosc w polmroku i nocy - Ambient Boost", Range(0.0, 1.0)) = 0.65

        [Header(Surface Properties)]
        _Smoothness("Gladkosc - Smoothness", Range(0.0, 1.0)) = 0.00
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
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog

            #include "TerrainGroundCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color0;
                float4 _Color1;
                float4 _Color2;
                float4 _Color3;
                float4 _Color4;
                float4 _Color5;
                float4 _ShoulderColor;
                float4 _ShoulderEmissionColor;
                float  _DebugRoadMode;
                float  _DebugEdgeMask;
                float  _TexelDensity;
                float  _HexRadius;
                float  _Seed;
                float  _ClusterSize;
                float  _MacroNoiseScale;
                float  _MacroNoiseStrength;
                float  _MicroNoiseScale;
                float  _MicroNoiseStrength;
                float  _Contrast;
                float  _DirtAmount;
                float  _DetailAmount;
                float  _ShoulderWidth;
                float  _ShoulderStrength;
                float  _ShoulderNoiseStrength;
                float  _ShoulderEmission;
                float  _AmbientBoost;
                float  _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float2 uv1          : TEXCOORD1; // UV1.x = 6-bitowa maska krawedzi pobocza
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float3 positionWS    : TEXCOORD0;
                float3 normalWS      : TEXCOORD1;
                float  fogFactor     : TEXCOORD2;
                float2 positionOS_XZ : TEXCOORD3;
                float  roadEdgeMask  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Wektory normalne zewnetrzne 6 krawedzi heksa pointy-top (k = 0..5, katy 0, 60, 120, 180, 240, 300 deg)
            static const float2 HEX_EDGE_NORMALS[6] = {
                float2( 1.0000000,  0.0000000), // k = 0 ( 0 deg, +X)
                float2( 0.5000000,  0.8660254), // k = 1 ( 60 deg)
                float2(-0.5000000,  0.8660254), // k = 2 (120 deg)
                float2(-1.0000000,  0.0000000), // k = 3 (180 deg, -X)
                float2(-0.5000000, -0.8660254), // k = 4 (240 deg)
                float2( 0.5000000, -0.8660254)  // k = 5 (300 deg)
            };

            // Oblicza kontekstowa maske poboczy [0, 1] wylacznie na krawedziach wskazanych przez 6-bitowa edgeMask
            float EvaluateRoadShoulderMask(float2 localPosXZ, float2 worldXZ, float hexRadius, float rawMask, float shoulderWidth, float noiseStrength)
            {
                int mask = (int)round(rawMask);
                if (mask <= 0) return 0.0;

                float rIn = hexRadius * 0.8660254;
                float2 p = localPosXZ;

                float dist[6];
                dist[0] = rIn - dot(p, HEX_EDGE_NORMALS[0]);
                dist[1] = rIn - dot(p, HEX_EDGE_NORMALS[1]);
                dist[2] = rIn - dot(p, HEX_EDGE_NORMALS[2]);
                dist[3] = rIn - dot(p, HEX_EDGE_NORMALS[3]);
                dist[4] = rIn - dot(p, HEX_EDGE_NORMALS[4]);
                dist[5] = rIn - dot(p, HEX_EDGE_NORMALS[5]);

                float shoulderNoise = ValueNoise2D(worldXZ * 3.5 + float2(17.4, 43.8)) - 0.5;
                float shoulderMask = 0.0;

                [unroll]
                for (int k = 0; k < 6; k++)
                {
                    int active_k = (mask >> k) & 1;
                    if (active_k == 0) continue;

                    int prevIdx = (k + 5) % 6;
                    int nextIdx = (k + 1) % 6;
                    int active_prev = (mask >> prevIdx) & 1;
                    int active_next = (mask >> nextIdx) & 1;

                    float cornerFade = 1.0;
                    if (active_prev == 0)
                    {
                        cornerFade *= saturate(dist[prevIdx] / max(dist[k], 0.001));
                    }
                    if (active_next == 0)
                    {
                        cornerFade *= saturate(dist[nextIdx] / max(dist[k], 0.001));
                    }

                    float edgePixelSpan = length(fwidth(dist[k]));
                    float effectiveWidth = max(shoulderWidth * (1.0 + shoulderNoise * noiseStrength), edgePixelSpan * 1.2);

                    float edgeFactor = smoothstep(effectiveWidth, effectiveWidth * 0.22, max(0.0, dist[k])) * cornerFade;
                    shoulderMask = max(shoulderMask, edgeFactor);
                }

                float dustBreak = saturate(0.82 + 0.30 * ValueNoise2D(worldXZ * 1.6 + float2(52.1, 11.7)));
                return shoulderMask * dustBreak;
            }

            // Sporadyczne wieksze kamyki w siatce texeli (0 = brak, 1 = cien pod kamykiem, 2 = ciemny kamyk, 3 = suchy jasniejszy kamyk)
            int EvaluateRoadPebbleMask(float2 cell, float2 seedOff, float pebbleDensity)
            {
                if (pebbleDensity <= 0.001) return 0;

                const float cellSize = 6.0;
                float2 shiftedCell = cell + floor(seedOff * 3.0);
                float2 blockId = floor(shiftedCell / cellSize);
                float2 localTex = shiftedCell - blockId * cellSize;

                float hSpawn = Hash21(blockId + float2(19.3, 47.1));
                if (hSpawn > pebbleDensity) return 0;

                float hPosX  = Hash21(blockId + float2(53.7, 11.9));
                float hPosY  = Hash21(blockId + float2(29.1, 83.4));
                float hShape = Hash21(blockId + float2(71.9, 37.3));
                float hFlip  = Hash21(blockId + float2(13.7, 61.5));
                float hTone  = Hash21(blockId + float2(89.2, 23.8));

                float2 center = float2(1.0 + floor(hPosX * 4.0), 1.0 + floor(hPosY * 4.0));
                int2 rel = int2(round(localTex - center));
                if (hFlip > 0.5) rel.x = -rel.x;

                int variant = (int)floor(hShape * 3.0);
                int stoneTone = (hTone > 0.68) ? 3 : 2;

                if (variant == 0)
                {
                    // Kamyk 2x1 z cieniem kontaktowym pod spodem
                    if (rel.x == 0 && rel.y == -1) return 1;
                    if ((rel.x == 0 || rel.x == 1) && rel.y == 0) return stoneTone;
                }
                else if (variant == 1)
                {
                    // Kanciasty kamien 3-pikselowy L + cien
                    if (rel.x == -1 && rel.y == 0) return 1;
                    if (rel.x == 0 && rel.y == 0)  return stoneTone;
                    if (rel.x == 1 && rel.y == 0)  return stoneTone;
                    if (rel.x == 0 && rel.y == 1)  return stoneTone;
                }
                else
                {
                    // Wiekszy kamyk 2x2 asymetryczny
                    if (rel.x == 0 && rel.y == -1) return 1;
                    if (rel.x == 0 && rel.y == 0)  return stoneTone;
                    if (rel.x == 1 && rel.y == 0)  return stoneTone;
                    if (rel.x == 1 && rel.y == 1)  return 2;
                }

                return 0;
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
                output.positionOS_XZ = input.positionOS.xz;
                output.roadEdgeMask = _DebugEdgeMask > 0.5 ? _DebugEdgeMask : input.uv1.x;

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normWS = normalize(input.normalWS);
                float2 worldXZ = input.positionWS.xz;

                // --- 1. Ostra, wspolna siatka texeli (~32 texele / szerokosc heksa) ---
                RetroTexelGrid grid = EvaluateDiscreteTexelGrid(worldXZ, _HexRadius, _TexelDensity);
                float2 seedOff = SeedOffset2D(_Seed);

                // --- 2. Ubita ziemia + wieksze miekkie plamy i srednie przebarwienia ---
                float safeCluster = max(2.0, _ClusterSize);
                float clusterField = GradientNoise2D((grid.cell + seedOff * 8.0) / safeCluster * 1.15);
                float macroField   = GradientNoise2D((grid.pixelPos + seedOff) * _MacroNoiseScale);
                // Drugie wieksze pasmo wydeptanej ziemi (miekko lamie jednolita powierzchnie)
                float broadPatch   = GradientNoise2D((grid.pixelPos + seedOff * 3.7) * (_MacroNoiseScale * 1.45) + float2(14.8, 82.5));

                float mediumGrain = ValueNoise2D(grid.cell * 0.36 + seedOff * 2.1);
                float fineNoise   = ValueNoise2D(grid.cell * _MicroNoiseScale + seedOff * 0.7);
                float cellHash    = Hash21(grid.cell + seedOff * 3.7);
                float micro       = ((fineNoise * 0.78 + cellHash * 0.22) - 0.5) * _MicroNoiseStrength * grid.microFade;

                float soilTone = 0.51
                    + (clusterField - 0.5) * 0.34
                    + (macroField - 0.5) * _MacroNoiseStrength
                    + (broadPatch - 0.5) * 0.26
                    + (mediumGrain - 0.5) * 0.28
                    + micro;
                soilTone = saturate(0.51 + (soilTone - 0.51) * _Contrast);

                // --- 3. Dyskretny wybor ubitej ziemi (progi dopasowane do rzeczywistego rozkladu szumow) ---
                int idx = 2; // Bazowy braz (#664F44)
                if (soilTone < 0.36)      idx = 0; // Ciemna ziemia (#382B2B) - rzadkie najciemniejsze zaglebienia
                else if (soilTone < 0.45) idx = 1; // Wilgotna ziemia (#493833) - wieksze plamy
                else if (soilTone < 0.57) idx = 2; // Bazowy braz (#664F44) - dominujaca ubita ziemia
                else if (soilTone < 0.64) idx = 3; // Sucha ziemia (#70574A) - miekkie plamy wydeptanej ziemi
                else                      idx = 4; // Jasny pyl (#846A54) - rzadkie suche przetarcia

                // Wieksze, rzadsze skupiska zabrudzen miekko wtopione w podloze
                float stainMacro = GradientNoise2D((grid.pixelPos + seedOff * 2.9) * (_MacroNoiseScale * 1.15) + float2(61.3, 17.9));
                float stainEdge  = ValueNoise2D(grid.cell * 0.28 + seedOff * 5.7);
                float stainScore = stainMacro * 0.72 + stainEdge * 0.28;
                if (stainScore > 0.74)
                {
                    idx = (stainScore > 0.85) ? 0 : min(idx, 1);
                }

                // Bardzo lekka warstwa micro-variation wewnatrz bazy (max +/-1 stopien, bez gestego szumu)
                float fineTexelPattern = ValueNoise2D(grid.cell * (_MicroNoiseScale * 1.1) + seedOff * 6.7) * 0.75 + cellHash * 0.25;
                if (grid.microFade > 0.20)
                {
                    if (idx == 2 && fineTexelPattern < 0.18)
                    {
                        idx = 1;
                    }
                    else if (idx == 2 && fineTexelPattern > 0.82 && soilTone > 0.50)
                    {
                        idx = 3;
                    }
                    else if (idx == 1 && fineTexelPattern > 0.82)
                    {
                        idx = 2;
                    }
                }

                // --- 4. Lepiej zintegrowany drobny zwir (#463E3B) w zwartych skupiskach (bez kropek) ---
                float gravelZone  = GradientNoise2D((grid.pixelPos + seedOff * 1.6) * (_MacroNoiseScale * 1.65) + float2(29.4, 61.2));
                float gravelClump = ValueNoise2D(grid.cell * 0.45 + seedOff * 4.1);
                float gravelScore = gravelZone * 0.58 + gravelClump * 0.42;
                float gravelThreshold = 1.0 - saturate(_DirtAmount * 1.45);
                bool isGravel = (_DirtAmount > 0.001 && gravelScore > gravelThreshold && soilTone < 0.56);
                if (isGravel)
                {
                    idx = (grid.microFade > 0.15) ? 5 : 1;
                }

                // --- 5. Sporadyczne wieksze kamyki bez rozsypanych ciemnych kropek ---
                int pebbleState = EvaluateRoadPebbleMask(grid.cell, seedOff, _DetailAmount);
                if (pebbleState == 2 && grid.microFade > 0.15)
                {
                    idx = 5; // Ciemny zwir/kamyk (#463E3B)
                }
                else if (pebbleState == 3 && grid.microFade > 0.25)
                {
                    idx = 3; // Suchy jasniejszy kamyk (#70574A)
                }

                // --- 6. Dynamiczne ciemniejsze pobocza z lekka emisja (uv1.x) ---
                float shoulderMask = EvaluateRoadShoulderMask(
                    input.positionOS_XZ, worldXZ, _HexRadius,
                    input.roadEdgeMask, _ShoulderWidth, _ShoulderNoiseStrength);

                // --- Tryby diagnostyczne (_DebugRoadMode) ---
                int debugMode = (int)(_DebugRoadMode + 0.5);
                if (debugMode == 1)
                {
                    float checker = (fmod(abs(grid.cell.x) + abs(grid.cell.y), 2.0) < 1.0) ? 0.08 : 0.92;
                    return half4(checker, checker, checker, 1.0);
                }
                else if (debugMode == 2)
                {
                    float v = (float)idx / 5.0;
                    return half4(v, v, v, 1.0);
                }
                else if (debugMode == 3)
                {
                    return half4(isGravel ? 1.0 : 0.0, pebbleState > 1 ? 1.0 : 0.0, clusterField, 1.0);
                }
                else if (debugMode == 5)
                {
                    float3 debugCol = lerp(float3(0.12, 0.65, 0.18), float3(0.92, 0.15, 0.10), shoulderMask);
                    return half4(debugCol, 1.0);
                }

                float3 baseTopColor = PickPalette6(
                    idx,
                    _Color0.rgb, _Color1.rgb, _Color2.rgb,
                    _Color3.rgb, _Color4.rgb, _Color5.rgb);

                float shoulderWeight = shoulderMask * _ShoulderStrength;
                float3 finalTopColor = lerp(baseTopColor, _ShoulderColor.rgb, shoulderWeight);
                half3 shoulderEmission = half3(_ShoulderEmissionColor.rgb * (shoulderWeight * _ShoulderEmission));

                // --- 7. Sciany boczne heksa (fallback dla pojedynczych meshy) ---
                float topMask = saturate((normWS.y - 0.25) / 0.45);
                float3 sideColor = EvaluateSideStrataSoil(input.positionWS, normWS, _Color0.rgb, _Color2.rgb);
                float3 finalAlbedo = lerp(sideColor, finalTopColor, topMask);

                if (debugMode == 4)
                {
                    return half4(finalAlbedo, 1.0);
                }

                // --- 8. URP PBR Lighting (surowy mat: Metallic = 0, Smoothness = 0 + emisja pobocza) ---
                return CalculateTerrainLitPBREmissive(
                    input.positionWS, normWS, input.positionCS, input.fogFactor,
                    finalAlbedo, _Smoothness, _AmbientBoost, shoulderEmission);
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
