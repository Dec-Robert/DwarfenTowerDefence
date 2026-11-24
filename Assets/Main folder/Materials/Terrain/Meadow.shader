Shader "Custom/Terrain/Meadow"
{
    Properties
    {
        [Header(Debug Diagnostics)]
        _DebugMeadowMode("Debug (0:Norm, 1:Grid, 2:Idx, 3:Tufts, 4:Albedo)", Range(0, 4)) = 0

        [Header(Terrain Palette sRGB)]
        _Color0("0: Najciemniejsza (#465538)", Color) = (0.27450982, 0.33333334, 0.21960784, 1.0)
        _Color1("1: Ciemna baza (#50603D)", Color) = (0.3137255, 0.3764706, 0.23921569, 1.0)
        _Color2("2: Baza chlodniejsza (#586B43)", Color) = (0.34509805, 0.41960785, 0.2627451, 1.0)
        _Color3("3: Glowna baza (#607449)", Color) = (0.3764706, 0.45490196, 0.28627452, 1.0)
        _Color4("4: Baza jasniejsza (#6A7D4F)", Color) = (0.41568628, 0.49019608, 0.30980393, 1.0)
        _Color5("5: Kepki trawy (#748857)", Color) = (0.45490196, 0.53333336, 0.34117648, 1.0)
        _Color6("6: Rzadkie akcenty (#829660)", Color) = (0.50980395, 0.5882353, 0.3764706, 1.0)

        [Header(Retro Texel Grid)]
        _TexelDensity("Texel Density (per hex width)", Float) = 32.0
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0
        _Seed("Seed wzoru - Seed", Float) = 13.0

        [Header(Layer 1 and 2 Base and Subtle Patches)]
        _ClusterSize("Patch Cluster Size (5-10 texels)", Range(3.0, 14.0)) = 7.5
        _MacroNoiseScale("Macro Noise Scale", Float) = 0.22
        _MacroNoiseStrength("Macro Noise Strength", Range(0.0, 1.0)) = 0.28
        _MicroNoiseScale("Micro Noise Scale", Float) = 0.85
        _MicroNoiseStrength("Micro Noise Strength", Range(0.0, 0.35)) = 0.14
        _Contrast("Contrast (lokalny)", Range(0.4, 2.0)) = 1.05

        [Header(Layer 3 Procedural Grass Tufts)]
        _DetailAmount("Tuft Density", Range(0.0, 1.0)) = 0.46
        _AccentChance("Rare Highlight Tips (#829660)", Range(0.0, 1.0)) = 0.16

        [Header(Hex Grid Readability)]
        _HexBorderColor("Kolor granicy heksa - Border Color", Color) = (0.5568628, 0.6313726, 0.41960785, 1.0)
        _HexBorderWidth("Szerokosc granicy heksa - Border Width", Range(0.01, 0.20)) = 0.07
        _HexBorderStrength("Widocznosc granicy - Border Strength", Range(0.0, 1.0)) = 0.85
        _HexBorderNoiseStrength("Nieregularnosc granicy - Border Noise", Range(0.0, 1.0)) = 0.30

        [Header(Lighting and Surface)]
        _AmbientBoost("Jasnosc w polmroku i nocy - Ambient Boost", Range(0.0, 1.0)) = 0.65
        _Smoothness("Matowosc podloza - Smoothness", Range(0.0, 1.0)) = 0.00
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
                float4 _Color6;
                float4 _HexBorderColor;
                float  _DebugMeadowMode;
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
                float  _AccentChance;
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

            float3 PickMeadowPalette7(int idx)
            {
                int i = clamp(idx, 0, 6);
                if (i == 0) return _Color0.rgb;
                if (i == 1) return _Color1.rgb;
                if (i == 2) return _Color2.rgb;
                if (i == 3) return _Color3.rgb;
                if (i == 4) return _Color4.rgb;
                if (i == 5) return _Color5.rgb;
                return _Color6.rgb;
            }

            // Warstwa 3: Asymetryczne, zroznicowane kepki trawy o niskim kontrascie
            // Zwraca: 0 = brak kepki, 1 = subtelny cien u nasady, 2 = przejscie kepki, 3 = glowne zdzbla, 4 = rzadki jasny czubek
            int EvaluateMeadowTuftMask(float2 cell, float2 seedOff, float tuftDensity, float accentChance)
            {
                const float cellSize = 6.0;
                float2 shiftedCell = cell + floor(seedOff * 5.0);
                float2 blockId = floor(shiftedCell / cellSize);
                float2 localTex = shiftedCell - blockId * cellSize; // [0..5]

                float hSpawn = Hash21(blockId + float2(11.3, 27.9));
                if (hSpawn > tuftDensity) return 0;

                float hPosX   = Hash21(blockId + float2(41.7, 13.1));
                float hPosY   = Hash21(blockId + float2(73.2, 59.4));
                float hShape  = Hash21(blockId + float2(91.5, 37.8));
                float hFlipX  = Hash21(blockId + float2(17.9, 83.2));
                float hFlipY  = Hash21(blockId + float2(52.3, 19.7));
                float hAccent = Hash21(blockId + float2(63.1, 47.6));

                // Centrum w [1..4] wewnatrz bloku 6x6 daje nieregularne odstepy (3..9 texeli) bez widocznej siatki
                float2 center = float2(1.0 + floor(hPosX * 4.0), 1.0 + floor(hPosY * 4.0));
                int2 rel = int2(round(localTex - center));

                if (hFlipX > 0.5) rel.x = -rel.x;
                if (hFlipY > 0.65) rel.y = -rel.y;

                int variant = (int)floor(hShape * 5.0); // 0..4, wszystkie asymetryczne

                if (variant == 0)
                {
                    // Drobna 2-pikselowa asymetryczna kepka
                    if (rel.x == 0 && rel.y == 0) return 2;
                    if (rel.x == 1 && rel.y == 1) return (hAccent < accentChance * 0.6) ? 4 : 3;
                }
                else if (variant == 1)
                {
                    // Pochylona 3-pikselowa kepka L
                    if (rel.x == -1 && rel.y == 0) return 1;
                    if (rel.x == 0 && rel.y == 0)  return 2;
                    if (rel.x == 1 && rel.y == 0)  return 3;
                    if (rel.x == 1 && rel.y == 1)  return (hAccent < accentChance) ? 4 : 3;
                }
                else if (variant == 2)
                {
                    // Niska, rozrzucona kepka 3-4 pikseli
                    if (rel.x == 0 && rel.y == -1) return 1;
                    if (rel.x == 0 && rel.y == 0)  return 3;
                    if (rel.x == 1 && rel.y == 0)  return 2;
                    if (rel.x == -1 && rel.y == 1) return 3;
                }
                else if (variant == 3)
                {
                    // Wieksza asymetryczna kepka 4-5 pikseli
                    if (rel.x == -1 && rel.y == -1) return 1;
                    if (rel.x == 0 && rel.y == 0)   return 2;
                    if (rel.x == 1 && rel.y == 0)   return 3;
                    if (rel.x == 0 && rel.y == 1)   return 3;
                    if (rel.x == 1 && rel.y == 2)   return (hAccent < accentChance) ? 4 : 2;
                }
                else
                {
                    // Skosne podwojne zdzblo
                    if (rel.x == 0 && rel.y == 0)  return 3;
                    if (rel.x == 0 && rel.y == 1)  return 2;
                    if (rel.x == 1 && rel.y == -1) return 1;
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

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normWS = normalize(input.normalWS);
                float2 worldXZ = input.positionWS.xz;

                // =========================================================================
                // 0. OSTRA SIATKA TEXELI (~32 TEXELS / HEX WIDTH)
                // =========================================================================
                RetroTexelGrid grid = EvaluateDiscreteTexelGrid(worldXZ, _HexRadius, _TexelDensity);
                float2 seedOff = SeedOffset2D(_Seed);

                // =========================================================================
                // 1. WARSTWA BAZOWA + DELIKATNY MICRO NOISE O NISKIM KONTRASCIE
                // =========================================================================
                float fineNoise = ValueNoise2D(grid.cell * _MicroNoiseScale + seedOff * 0.7);
                float cellHash = Hash21(grid.cell + seedOff * 2.9);
                float cellMicro = ((fineNoise * 0.65 + cellHash * 0.35) - 0.5) * _MicroNoiseStrength * grid.microFade;

                // =========================================================================
                // 2. SUBTELNE ZROZNICOWANIE TRAWY (3-4 ZBLIZONE ODCIENIE SRODKA PALETY)
                // =========================================================================
                float safeCluster = max(3.0, _ClusterSize);
                float patchField = GradientNoise2D((grid.cell + seedOff * 12.0) / safeCluster * 1.15);
                float macroField = GradientNoise2D((grid.pixelPos + seedOff) * _MacroNoiseScale);
                // Srednia faktura trawy (2-4 texele) nadajaca zroznicowanie calej powierzchni
                float turfStructure = ValueNoise2D(grid.cell * 0.52 + seedOff * 2.3);

                float baseTone = 0.53
                    + (patchField - 0.5) * 0.34
                    + (macroField - 0.5) * _MacroNoiseStrength
                    + (turfStructure - 0.5) * 0.28
                    + cellMicro;
                baseTone = saturate(0.53 + (baseTone - 0.53) * _Contrast);

                // Wiekszosc powierzchni korzysta ze srodkowych, zblizonych tonow (1, 2, 3, 4)
                int idx = 3; // Glowna baza (#536344)
                if (baseTone < 0.14)      idx = 0; // (#3D4B34) - oszczednie
                else if (baseTone < 0.30) idx = 1; // (#46563A)
                else if (baseTone < 0.50) idx = 2; // (#4D5D40)
                else if (baseTone < 0.72) idx = 3; // (#536344) - lekko rozjasniony srodek
                else if (baseTone < 0.90) idx = 4; // (#59694A)
                else                      idx = 5; // (#63734E) - rzadkie jasniejsze fragmenty w tle

                // =========================================================================
                // 3. WARSTWA KEPEK TRAWY (MNIEJ O ~30%, NIZSZY KONTRAST, ASYMETRYCZNE)
                // =========================================================================
                int tuftState = EvaluateMeadowTuftMask(grid.cell, seedOff, _DetailAmount, _AccentChance);
                if (tuftState == 1)
                {
                    // Bardzo subtelny cien u nasady - przesuwa tylko o 1 stopien w dol
                    idx = max(0, idx - 1);
                }
                else if (tuftState == 2)
                {
                    // Lagodne przejscie kepki (#59694A)
                    idx = max(idx, 4);
                }
                else if (tuftState == 3)
                {
                    // Glowne zdzbla o obnizonym kontrascie (#63734E, przy oddaleniu #59694A)
                    idx = (grid.microFade > 0.25) ? max(idx, 5) : max(idx, 4);
                }
                else if (tuftState == 4)
                {
                    // Najjasniejsze rzadkie akcenty (#718057)
                    idx = (grid.microFade > 0.45) ? 6 : 5;
                }

                // =========================================================================
                // TRYBY DIAGNOSTYCZNE (_DebugMeadowMode)
                // =========================================================================
                int debugMode = (int)(_DebugMeadowMode + 0.5);
                if (debugMode == 1)
                {
                    float checker = (fmod(abs(grid.cell.x) + abs(grid.cell.y), 2.0) < 1.0) ? 0.08 : 0.92;
                    return half4(checker, checker, checker, 1.0);
                }
                else if (debugMode == 2)
                {
                    float v = (float)idx / 6.0;
                    return half4(v, v, v, 1.0);
                }
                else if (debugMode == 3)
                {
                    float tv = (float)tuftState / 4.0;
                    return half4(tv, tv, tv, 1.0);
                }

                // =========================================================================
                // 4. WYRAZNA, GLADKA GRANICA HEKSA (ANTYALIASING fwidth, BEZ PIKSELIZACJI)
                // =========================================================================
                float borderMask = EvaluateHexBorder(
                    input.positionOS_XZ, worldXZ,
                    _HexRadius, _HexBorderWidth, _HexBorderStrength, _HexBorderNoiseStrength);

                float3 baseTopColor = PickMeadowPalette7(idx);
                float3 finalTopColor = lerp(baseTopColor, _HexBorderColor.rgb, borderMask);

                // Scianki boczne heksa (fallback dla pojedynczych meshy)
                float topMask = saturate((normWS.y - 0.25) / 0.45);
                float3 sideColor = EvaluateSideStrataSoil(input.positionWS, normWS, _Color0.rgb, _Color1.rgb);
                float3 finalAlbedo = lerp(sideColor, finalTopColor, topMask);

                // Tryb 4: Czyste Albedo bez oswietlenia, cieni i mgly dystansowej
                if (debugMode == 4)
                {
                    return half4(finalAlbedo, 1.0);
                }

                // Oswietlenie URP PBR (surowy mat: Metallic = 0, Smoothness = 0)
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
