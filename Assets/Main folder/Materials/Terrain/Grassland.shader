Shader "Custom/Grassland"
{
    Properties
    {
        [Header(Debug Diagnostics)]
        _DebugGrassMode("Debug (0:Norm, 1:Grid, 2:Idx, 3:DrySoil, 4:Albedo)", Range(0, 4)) = 0

        [Header(Terrain Palette sRGB)]
        _Color0("0: Gleboka zielen (#2E3F31)", Color) = (0.18039216, 0.24705882, 0.19215687, 1.0)
        _Color1("1: Ciemna zielen (#3B4F37)", Color) = (0.23137255, 0.30980393, 0.21568628, 1.0)
        _Color2("2: Bazowa zielen (#475D3E)", Color) = (0.2784314, 0.3647059, 0.24313726, 1.0)
        _Color3("3: Jasniejsza zielen (#566D48)", Color) = (0.3372549, 0.42745098, 0.28235295, 1.0)
        _Color4("4: Sucha oliwka (#687148)", Color) = (0.40784314, 0.44313726, 0.28235295, 1.0)
        _Color5("5: Brudny braz (#544B3C)", Color) = (0.32941177, 0.29411766, 0.23529412, 1.0)

        [Header(Retro Texel Grid)]
        _TexelDensity("Texel Density (per hex width)", Float) = 32.0
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0
        _Seed("Seed wzoru - Seed", Float) = 7.0

        [Header(Procedural Grass Structure)]
        _ClusterSize("Cluster Size (3-6 texels)", Range(2.0, 10.0)) = 4.5
        _MacroNoiseScale("Macro Noise Scale", Float) = 0.25
        _MacroNoiseStrength("Macro Noise Strength", Range(0.0, 1.0)) = 0.22
        _MicroNoiseScale("Micro Noise Scale", Float) = 0.90
        _MicroNoiseStrength("Micro Noise Strength", Range(0.0, 0.40)) = 0.14
        _Contrast("Contrast (niski-sredni)", Range(0.4, 2.0)) = 0.85

        [Header(Dry Grass and Exposed Soil)]
        _DetailAmount("Detail Amount (sucha trawa)", Range(0.0, 0.50)) = 0.12
        _DirtAmount("Dirt Amount (przebijajaca ziemia)", Range(0.0, 0.50)) = 0.08

        [Header(Hex Grid Readability)]
        _HexBorderColor("Kolor granicy heksa - Border Color", Color) = (0.38431373, 0.5019608, 0.35686275, 1.0)
        _HexBorderWidth("Szerokosc granicy heksa - Border Width", Range(0.01, 0.20)) = 0.07
        _HexBorderStrength("Widocznosc granicy - Border Strength", Range(0.0, 1.0)) = 0.88
        _HexBorderNoiseStrength("Nieregularnosc granicy - Border Noise", Range(0.0, 1.0)) = 0.35

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
                float4 _HexBorderColor;
                float  _DebugGrassMode;
                float  _TexelDensity;
                float  _HexRadius;
                float  _Seed;
                float  _ClusterSize;
                float  _MacroNoiseScale;
                float  _MacroNoiseStrength;
                float  _MicroNoiseScale;
                float  _MicroNoiseStrength;
                float  _Contrast;
                float  _DetailAmount;
                float  _DirtAmount;
                float  _HexBorderWidth;
                float  _HexBorderStrength;
                float  _HexBorderNoiseStrength;
                float  _AmbientBoost;
                float  _Smoothness;
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
                float4 positionCS    : SV_POSITION;
                float3 positionWS    : TEXCOORD0;
                float3 normalWS      : TEXCOORD1;
                float  fogFactor     : TEXCOORD2;
                float2 positionOS_XZ : TEXCOORD3;
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

                // --- 2. Spokojne przebarwienia makro + nieregularne skupiska zieleni (3-6 texeli) ---
                float safeCluster = max(2.0, _ClusterSize);
                float clusterField = GradientNoise2D((grid.cell + seedOff * 9.0) / safeCluster * 1.15);
                float macroField   = GradientNoise2D((grid.pixelPos + seedOff) * _MacroNoiseScale);

                // Drobna struktura kepkowatej darni (2-3 texele) + kontrolowany Micro Noise
                float turfClump = ValueNoise2D(grid.cell * float2(0.58, 0.72) + seedOff * 2.1);
                float fineNoise = ValueNoise2D(grid.cell * _MicroNoiseScale + seedOff * 0.6);
                float cellHash  = Hash21(grid.cell + seedOff * 3.1);
                float micro     = ((fineNoise * 0.65 + cellHash * 0.35) - 0.5) * _MicroNoiseStrength * grid.microFade;

                float baseTone = 0.50
                    + (clusterField - 0.5) * 0.42
                    + (macroField - 0.5) * _MacroNoiseStrength
                    + (turfClump - 0.5) * 0.30
                    + micro;
                baseTone = saturate(0.50 + (baseTone - 0.50) * _Contrast);

                // --- 3. Dyskretny wybor podstawowej zieleni (spokojne ~70% bazy + ~25% detali) ---
                int idx = 2; // Bazowa zielen (#414D36)
                if (baseTone < 0.16)      idx = 0; // Gleboka zielen (#29332A) - zaglebienia
                else if (baseTone < 0.39) idx = 1; // Ciemna zielen (#364230) - ciemniejsze skupiska
                else if (baseTone < 0.74) idx = 2; // Bazowa zielen (#414D36) - dominujaca
                else                      idx = 3; // Jasniejsza zielen (#505C40) - kepki trawy

                // Subtelne podbicie drobnych 2-3 pikselowych kepek w wyzszych skupiskach
                float tuftSpot = ValueNoise2D(grid.cell * 0.88 + seedOff * 4.7);
                if (idx == 2 && tuftSpot > 0.76 && baseTone > 0.56)
                {
                    idx = 3;
                }

                // Bardzo subtelna, niskokontrastowa struktura drobnych texeli pomiedzy skupiskami (max +/-1 ton zieleni)
                float fineTexelPattern = ValueNoise2D(grid.cell * (_MicroNoiseScale * 1.15) + seedOff * 7.3) * 0.65 + cellHash * 0.35;
                if (grid.microFade > 0.20)
                {
                    if (idx == 2 && fineTexelPattern < 0.22)
                    {
                        idx = 1;
                    }
                    else if (idx == 2 && fineTexelPattern > 0.82 && baseTone > 0.48)
                    {
                        idx = 3;
                    }
                    else if (idx == 1 && fineTexelPattern > 0.80)
                    {
                        idx = 2;
                    }
                }

                // --- 4. Rzadkie fragmenty wysuszonej trawy (#656A46) i przebijajacej ziemi (#51483A) (~5%) ---
                float dryZone = GradientNoise2D((grid.pixelPos + seedOff * 1.9) * (_MacroNoiseScale * 1.9) + float2(47.2, 13.9));
                float dryClump = ValueNoise2D(grid.cell * 0.62 + seedOff * 5.3);
                float dryScore = dryZone * 0.52 + dryClump * 0.36 + cellHash * 0.12 * grid.microFade;
                float dryThreshold = 1.0 - saturate(_DetailAmount * 1.15);
                bool isDry = (_DetailAmount > 0.001 && dryScore > dryThreshold && baseTone > 0.46);

                float dirtZone = GradientNoise2D((grid.pixelPos + seedOff * 2.7) * (_MacroNoiseScale * 1.6) + float2(83.1, 51.4));
                float dirtClump = Hash21(floor(grid.cell * 0.55) + seedOff * 6.1);
                float dirtScore = dirtZone * 0.60 + dirtClump * 0.40;
                float dirtThreshold = 1.0 - saturate(_DirtAmount * 1.10);
                bool isDirt = (_DirtAmount > 0.001 && dirtScore > dirtThreshold && baseTone < 0.44);

                if (isDry)
                {
                    idx = (grid.microFade > 0.15) ? 4 : 3;
                }
                else if (isDirt)
                {
                    idx = (grid.microFade > 0.15) ? 5 : 1;
                }

                // --- Tryby diagnostyczne (_DebugGrassMode) ---
                int debugMode = (int)(_DebugGrassMode + 0.5);
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
                    return half4(isDry ? 1.0 : 0.0, isDirt ? 1.0 : 0.0, clusterField, 1.0);
                }

                // --- 5. Gladka, czytelna granica heksa (antyalising fwidth, bez pikselizacji) ---
                float borderMask = EvaluateHexBorder(
                    input.positionOS_XZ, worldXZ,
                    _HexRadius, _HexBorderWidth, _HexBorderStrength, _HexBorderNoiseStrength);

                float3 baseTopColor = PickPalette6(
                    idx,
                    _Color0.rgb, _Color1.rgb, _Color2.rgb,
                    _Color3.rgb, _Color4.rgb, _Color5.rgb);
                float3 finalTopColor = lerp(baseTopColor, _HexBorderColor.rgb, borderMask);

                // --- 6. Sciany boczne heksa (fallback dla pojedynczych meshy) ---
                float topMask = saturate((normWS.y - 0.25) / 0.45);
                float3 sideColor = EvaluateSideStrataSoil(input.positionWS, normWS, _Color0.rgb, _Color5.rgb);
                float3 finalAlbedo = lerp(sideColor, finalTopColor, topMask);

                if (debugMode == 4)
                {
                    return half4(finalAlbedo, 1.0);
                }

                // --- 7. URP PBR Lighting (surowy mat: Metallic = 0, Smoothness = 0) ---
                return CalculateTerrainLitPBR(
                    input.positionWS, normWS, input.positionCS, input.fogFactor,
                    finalAlbedo, _Smoothness, _AmbientBoost);
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
