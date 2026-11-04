Shader "Custom/Terrain/Hill"
{
    Properties
    {
        [Header(Debug Diagnostics)]
        _DebugHillMode("Tryb debugowania (0:Norm, 1:Grid, 2:RockMask, 3:CracksOnly)", Range(0, 3)) = 0

        [Header(Hill Level System)]
        _HillLevel("Poziom wzgorza (0:Grassland, 1..5:Hill)", Range(0.0, 5.0)) = 3.0
        _LevelRockBoost("Mnoznik ekspozycji skaly - Rock Boost", Range(0.5, 2.0)) = 1.15
        _LevelGrassReduction("Tempo zanikania trawy - Grass Fade", Range(0.5, 2.0)) = 1.10

        [Header(Grass Palette)]
        _GrassBaseColor("Baza trawy wyzynnej - Grass Base", Color) = (0.275, 0.329, 0.220, 1.0)
        _GrassLightColor("Jasna bujna trawa - Grass Light", Color) = (0.408, 0.482, 0.325, 1.0)
        _GrassDarkColor("Ciemna trawa w zaglebieniach - Grass Dark", Color) = (0.184, 0.227, 0.149, 1.0)

        [Header(Dry Soil and Transition Palette)]
        _DrySoilColor("Sucha pylista ziemia - Dry Soil", Color) = (0.412, 0.357, 0.278, 1.0)
        _TransitionColor("Lagodne przejscie - Soil Transition", Color) = (0.322, 0.278, 0.220, 1.0)

        [Header(Rock and Cracks Palette)]
        _RockLightColor("Jasna lita plyta skalna - Rock Light", Color) = (0.482, 0.459, 0.404, 1.0)
        _RockDarkColor("Ciemniejszy lit kamien - Rock Dark", Color) = (0.325, 0.310, 0.275, 1.0)
        _CrackColor("Szczelina pekniecia - Crack Color", Color) = (0.169, 0.153, 0.137, 1.0)
        _HexBorderColor("Jasna granica heksa - Border Color", Color) = (0.541, 0.518, 0.455, 1.0)

        [Header(Organic Rock Structure)]
        _RockChunkScale("Skala wielkich plyt skalnych - Chunk Scale", Float) = 0.45
        _RockIrregularity("Nieregularnosc pekniec - Warp Strength", Range(0.0, 1.0)) = 0.35
        _CrackScale("Gestosc szczelin - Crack Scale", Float) = 1.8
        _CrackWidth("Szerokosc pekniec (ostre) - Crack Width", Range(0.01, 0.12)) = 0.045
        _DrySoilCoverage("Szerokosc bufora pylu - Soil Fringe", Range(0.05, 0.40)) = 0.22
        _PatchScale("Skala makro-skupisk - Patch Scale", Float) = 0.50
        _PatchContrast("Kontrast makro-skupisk - Patch Contrast", Range(0.5, 2.0)) = 1.0

        [Header(Retro Stylization)]
        _TexelDensity("Gestosc pikseli na heks - Texel Density", Float) = 16.0
        _ColorSteps("Kwantyzacja odcieni - Color Steps", Range(2.0, 8.0)) = 4.0
        _DetailStrength("Sila retro-faktury - Detail Strength", Range(0.0, 1.0)) = 0.35

        [Header(Hex Grid Readability)]
        _HexBorderWidth("Szerokosc granicy heksa - Border Width", Range(0.01, 0.20)) = 0.07
        _HexBorderStrength("Widocznosc granicy - Border Strength", Range(0.0, 1.0)) = 0.32
        _HexBorderNoise("Nieregularnosc granicy - Border Noise", Range(0.0, 1.0)) = 0.35
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0

        [Header(Lighting and Surface)]
        _AmbientBoost("Jasnosc w polmroku i nocy - Ambient Boost", Range(0.0, 1.0)) = 0.50
        _Smoothness("Matowosc skaly i gleby - Smoothness", Range(0.0, 1.0)) = 0.04
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

            #include "TerrainGroundCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GrassBaseColor;
                float4 _GrassLightColor;
                float4 _GrassDarkColor;
                float4 _DrySoilColor;
                float4 _TransitionColor;
                float4 _RockLightColor;
                float4 _RockDarkColor;
                float4 _CrackColor;
                float4 _HexBorderColor;
                float  _DebugHillMode;
                float  _HillLevel;
                float  _LevelRockBoost;
                float  _LevelGrassReduction;
                float  _RockChunkScale;
                float  _RockIrregularity;
                float  _CrackScale;
                float  _CrackWidth;
                float  _DrySoilCoverage;
                float  _PatchScale;
                float  _PatchContrast;
                float  _TexelDensity;
                float  _ColorSteps;
                float  _DetailStrength;
                float  _HexBorderWidth;
                float  _HexBorderStrength;
                float  _HexBorderNoise;
                float  _HexRadius;
                float  _AmbientBoost;
                float  _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float2 uv1          : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float3 positionWS    : TEXCOORD0;
                float3 normalWS      : TEXCOORD1;
                float  fogFactor     : TEXCOORD2;
                float2 positionOS_XZ : TEXCOORD3;
                float  hillLevelAttr : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

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

                // Odczyt poziomu wzgórza z siatki (uv1.y)
                output.hillLevelAttr = input.uv1.y;

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normWS = normalize(input.normalWS);
                float2 worldXZ = input.positionWS.xz;

                // ---------------------------------------------------------------------
                // 1. WIRTUALNA SIATKA TEXELI (SPÓJNA Z MEADOW / PSX STYLE)
                // ---------------------------------------------------------------------
                float hexDiameter = max(0.001, 2.0 * _HexRadius);
                float density = max(1.0, _TexelDensity);

                float2 texelCoords = (worldXZ / hexDiameter) * density;
                float2 gridCell = floor(texelCoords);
                float2 quantizedGrid = gridCell + 0.5;
                float2 samplePos = (quantizedGrid / density) * hexDiameter;

                float2 dTexel = fwidth(texelCoords);
                float texelScreenSpan = max(dTexel.x, dTexel.y);
                float aaBlend = smoothstep(0.75, 1.5, texelScreenSpan);
                float detailFade = 1.0 - smoothstep(1.0, 2.5, texelScreenSpan);

                float2 retroUV = lerp(samplePos, worldXZ, aaBlend);

                // ---------------------------------------------------------------------
                // 2. POZIOM WZGÓRZA (LEVEL 0..5 LOGIC)
                // ---------------------------------------------------------------------
                float currentLevel = _HillLevel;
                if (input.hillLevelAttr > 0.001)
                {
                    currentLevel = input.hillLevelAttr <= 1.0 ? input.hillLevelAttr * 5.0 : input.hillLevelAttr;
                }
                currentLevel = clamp(currentLevel, 0.0, 5.0);
                float levelNorm = currentLevel / 5.0; // 0.0 .. 1.0

                // Progi ekspozycji geologicznej
                float rockTarget = saturate(levelNorm * _LevelRockBoost);
                float grassTarget = saturate(1.0 - levelNorm * _LevelGrassReduction);

                // ---------------------------------------------------------------------
                // 3. PŁYTY SKALNE (ORGANIC MACRO SLABS - ZERO BRUKU)
                // ---------------------------------------------------------------------
                float2 warpOffset = (float2(ValueNoise2D(worldXZ * 1.5 + float2(11.2, 34.5)),
                                            ValueNoise2D(worldXZ * 1.5 + float2(56.7, 89.1))) - 0.5) * _RockIrregularity;
                float macroRockNoise = ValueNoise2D((worldXZ + warpOffset) * _RockChunkScale + float2(42.1, 19.8));
                macroRockNoise = saturate(0.5 + (macroRockNoise - 0.5) * _PatchContrast);

                // Odsłonięcie skały sterowane poziomem wzgórza
                float rockCutoff = 1.0 - saturate(rockTarget * 0.95);
                float rockSlabMask = smoothstep(rockCutoff, rockCutoff + 0.10, macroRockNoise);

                // Odcień fasetek płyty skalnej z kwantyzacją (PSX retro look)
                VoronoiResult slabV = FastVoronoi2D(retroUV * (_RockChunkScale * 1.8) + float2(7.3, 19.8));
                float steppedTone = floor(slabV.cellHash * _ColorSteps + 0.5) / _ColorSteps;
                float3 slabColor = lerp(_RockDarkColor.rgb, _RockLightColor.rgb, steppedTone);

                // ---------------------------------------------------------------------
                // 4. NATURALNE PĘKNIĘCIA SKAŁY (ORGANIC THIN CRACKS)
                // ---------------------------------------------------------------------
                float2 crackCoord = retroUV * _CrackScale + warpOffset * 2.0;
                VoronoiResult crackV = FastVoronoi2D(crackCoord);
                
                // Pęknięcie to wyłącznie wąska szczelina przy krawędzi komórki (nie fuga bruku!)
                float crackMask = 1.0 - smoothstep(0.0, _CrackWidth, crackV.crack);
                crackMask *= rockSlabMask;

                // Zastosowanie pęknięć do koloru skały
                float3 rockComposite = lerp(slabColor, _CrackColor.rgb, crackMask * 0.85);

                // ---------------------------------------------------------------------
                // 5. SUCHA PYLISTA ZIEMIA (DRY SOIL FRINGE BUFFER)
                // ---------------------------------------------------------------------
                float soilThreshold = max(0.0, rockCutoff - _DrySoilCoverage);
                float soilFringeMask = smoothstep(soilThreshold, rockCutoff + 0.04, macroRockNoise) * (1.0 - rockSlabMask * 0.90);
                
                float soilGrain = ValueNoise2D(retroUV * 3.5 + float2(91.2, 34.7));
                float3 soilColor = lerp(_TransitionColor.rgb, _DrySoilColor.rgb, soilGrain * 0.55);

                float3 rockAndSoil = lerp(soilColor, rockComposite, rockSlabMask);

                // ---------------------------------------------------------------------
                // 6. TRAWA WYŻYNNA (GRASS COVERAGE - LEVEL 0..5)
                // ---------------------------------------------------------------------
                float grassNoise = ValueNoise2D(worldXZ * _PatchScale + float2(88.3, 51.2));
                float grassCutoff = 1.0 - saturate(grassTarget * 1.15);
                float grassMask = smoothstep(grassCutoff, grassCutoff + 0.12, grassNoise);
                
                grassMask *= saturate(1.0 - rockSlabMask * 1.15 + crackMask * 0.40);

                float grassTuftNoise = ValueNoise2D(retroUV * 2.0 + float2(22.3, 61.8));
                float steppedGrass = floor(grassTuftNoise * _ColorSteps + 0.5) / _ColorSteps;
                float3 grassColor = lerp(_GrassBaseColor.rgb, _GrassLightColor.rgb, steppedGrass * 0.65);
                grassColor = lerp(grassColor, _GrassDarkColor.rgb, (1.0 - steppedGrass) * 0.30);

                float3 surfaceColor = lerp(rockAndSoil, grassColor, grassMask);

                // ---------------------------------------------------------------------
                // 7. TRYBY DIAGNOSTYCZNE (_DebugHillMode)
                // ---------------------------------------------------------------------
                int debugMode = (int)(_DebugHillMode + 0.5);
                if (debugMode == 1)
                {
                    float checker = (fmod(abs(gridCell.x) + abs(gridCell.y), 2.0) < 1.0) ? 0.05 : 0.95;
                    return half4(checker, checker, checker, 1.0);
                }
                else if (debugMode == 2)
                {
                    return half4(rockSlabMask, rockSlabMask, rockSlabMask, 1.0);
                }
                else if (debugMode == 3)
                {
                    return half4(crackMask, crackMask, crackMask, 1.0);
                }

                // ---------------------------------------------------------------------
                // 8. CZYTELNA GRANICA HEKSA I ŚCIANKI BOCZNE
                // ---------------------------------------------------------------------
                float borderMask = EvaluateHexBorder(input.positionOS_XZ, worldXZ, _HexRadius, _HexBorderWidth, _HexBorderStrength, _HexBorderNoise);
                float3 finalTopColor = lerp(surfaceColor, _HexBorderColor.rgb, borderMask);

                // Ścianki boczne heksa (uskoki elewacji do niższych sąsiadów)
                float topMask = saturate((normWS.y - 0.25) / 0.45);
                float3 sideColor = EvaluateSideStrataSoil(input.positionWS, normWS, _DrySoilColor.rgb, _RockDarkColor.rgb);
                float3 finalAlbedo = lerp(sideColor, finalTopColor, topMask);

                // Oświetlenie URP PBR
                return CalculateTerrainLitPBR(input.positionWS, normWS, input.positionCS, input.fogFactor, finalAlbedo, _Smoothness, _AmbientBoost);
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
