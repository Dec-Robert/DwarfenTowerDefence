Shader "Custom/Terrain/Meadow"
{
    Properties
    {
        [Header(Debug Diagnostics)]
        _DebugMeadowMode("Tryb debugowania (0:Norm, 1:Grid, 2:Detail, 3:Tufts)", Range(0, 3)) = 0

        [Header(Meadow Green Palette)]
        _MeadowBaseColor("Podstawowa zielen laki - Base Green", Color) = (0.349, 0.431, 0.271, 1.0)
        _MeadowDarkColor("Ciemniejszy ton dla glebi - Dark Green", Color) = (0.247, 0.322, 0.204, 1.0)
        _MeadowLightColor("Jasna bujna trawa - Light Grass", Color) = (0.506, 0.584, 0.380, 1.0)
        _MeadowBorderColor("Jasna zielona granica heksa - Border Green", Color) = (0.549, 0.635, 0.420, 1.0)

        [Header(Optional Soil Undertone)]
        _SoilCoverage("Subtelny undertone gleby (domyslnie 0) - Soil Undertone", Range(0.0, 0.20)) = 0.00
        _MeadowSoilColor("Kolor undertone gleby - Soil Color", Color) = (0.400, 0.320, 0.225, 1.0)

        [Header(Grass Tufts Distribution)]
        _PatchScale("Skala kep bujnej trawy - Patch Scale", Float) = 0.52
        _PatchContrast("Kontrast kep bujnej trawy - Patch Contrast", Range(0.5, 2.0)) = 0.75

        [Header(Retro Stylized Detail)]
        _TexelDensity("Gestosc pikseli na heks - Texel Density", Float) = 16.0
        _DetailScale("Skala struktury roslinnej - Detail Scale", Float) = 2.2
        _DetailStrength("Sila struktury roslinnej - Detail Strength", Range(0.0, 1.0)) = 0.30
        _QuantizeSteps("Kwantyzacja odcieni - Color Steps", Range(2.0, 8.0)) = 4.0

        [Header(Hex Grid Readability)]
        _HexBorderWidth("Szerokosc granicy heksa - Border Width", Range(0.01, 0.20)) = 0.07
        _HexBorderStrength("Widocznosc jasnej granicy - Border Strength", Range(0.0, 1.0)) = 0.35
        _HexBorderNoiseStrength("Nieregularnosc granicy - Border Noise", Range(0.0, 1.0)) = 0.35
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0

        [Header(Lighting and Surface)]
        _AmbientBoost("Jasnosc w polmroku i nocy - Ambient Boost", Range(0.0, 1.0)) = 0.50
        _Smoothness("Matowosc podloza - Smoothness", Range(0.0, 1.0)) = 0.02
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
                float4 _MeadowBaseColor;
                float4 _MeadowDarkColor;
                float4 _MeadowLightColor;
                float4 _MeadowBorderColor;
                float4 _MeadowSoilColor;
                float  _DebugMeadowMode;
                float  _SoilCoverage;
                float  _PatchScale;
                float  _PatchContrast;
                float  _TexelDensity;
                float  _DetailScale;
                float  _DetailStrength;
                float  _QuantizeSteps;
                float  _HexBorderWidth;
                float  _HexBorderStrength;
                float  _HexBorderNoiseStrength;
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

                // =========================================================================
                // 1. WIRTUALNA SIATKA TEXELI (TEXEL DENSITY)
                // =========================================================================
                float hexDiameter = max(0.001, 2.0 * _HexRadius);
                float density = max(1.0, _TexelDensity);

                float2 texelCoords = (worldXZ / hexDiameter) * density;
                float2 gridCell = floor(texelCoords);
                float2 quantizedGrid = gridCell + 0.5;
                float2 samplePos = (quantizedGrid / density) * hexDiameter;

                // Antyaliasing na oddaleniu kamery
                float2 dTexel = fwidth(texelCoords);
                float texelScreenSpan = max(dTexel.x, dTexel.y);
                float aaBlend = smoothstep(0.75, 1.5, texelScreenSpan);
                float detailFade = 1.0 - smoothstep(1.0, 2.5, texelScreenSpan);

                float2 retroUV = lerp(samplePos, worldXZ, aaBlend);

                // =========================================================================
                // 2. BAZA TRAWY ŁĄKOWEJ + SUBTELNE ZRÓŻNICOWANIE MAKRO
                // =========================================================================
                float macroNoise = ValueNoise2D(worldXZ * (_PatchScale * 0.6) + float2(12.3, 45.6));
                float macroDepth = smoothstep(0.60, 0.95, macroNoise);
                float3 grassBase = lerp(_MeadowBaseColor.rgb, _MeadowDarkColor.rgb, macroDepth * 0.25);

                // =========================================================================
                // 3. KĘPY JAŚNIEJSZEJ TRAWY (TUFTS OF LIGHTER GRASS)
                // Zastępuje plamy gleby - tworzy miękkie, nieregularne skupiska bujniejszej zieleni
                // =========================================================================
                float tuftNoiseA = ValueNoise2D(worldXZ * _PatchScale + float2(38.4, 71.2));
                float tuftNoiseB = ValueNoise2D(worldXZ * (_PatchScale * 1.8) + float2(89.1, 14.7));
                float combinedTufts = tuftNoiseA * 0.65 + tuftNoiseB * 0.35;

                float tuftContrastAdjusted = saturate(0.5 + (combinedTufts - 0.5) * _PatchContrast);
                float tuftMask = smoothstep(0.35, 0.75, tuftContrastAdjusted);

                float3 surfaceWithTufts = lerp(grassBase, _MeadowLightColor.rgb, tuftMask * 0.70);

                // =========================================================================
                // 4. RETRO STRUKTURA DROBNYCH ŹDŹBEŁ / KĘPEK (PROWADZONA PRZEZ retroUV)
                // =========================================================================
                float2 bladeCoord = retroUV * _DetailScale;
                float fineBladeNoise = ValueNoise2D(bladeCoord * float2(1.8, 1.1) + float2(31.2, 74.9));
                float fineClump = ValueNoise2D(bladeCoord * 0.8 + float2(84.1, 19.3));
                float bladeStructure = fineBladeNoise * 0.70 + fineClump * 0.30;

                float steppedBlade = floor(bladeStructure * _QuantizeSteps + 0.5) / _QuantizeSteps;
                float bladeDelta = (steppedBlade - 0.5) * 2.0; // [-1.0 .. +1.0]

                float3 bladeHighlight = lerp(surfaceWithTufts, _MeadowLightColor.rgb, 0.75);
                float3 bladeShadow    = lerp(surfaceWithTufts, _MeadowDarkColor.rgb, 0.60);

                float detailFactor = _DetailStrength * detailFade;
                float3 surfaceColor = surfaceWithTufts;
                if (bladeDelta > 0.0)
                {
                    surfaceColor = lerp(surfaceWithTufts, bladeHighlight, saturate(bladeDelta * detailFactor * 1.4));
                }
                else
                {
                    surfaceColor = lerp(surfaceWithTufts, bladeShadow, saturate(-bladeDelta * detailFactor * 1.4));
                }

                // =========================================================================
                // 5. OPCJONALNY SUBTELNY UNDERTONE GLEBY (domyślnie _SoilCoverage = 0.00)
                // =========================================================================
                if (_SoilCoverage > 0.001)
                {
                    float soilNoise = ValueNoise2D(worldXZ * (_PatchScale * 2.2) + float2(44.2, 81.7));
                    float soilCutoff = 1.0 - saturate(_SoilCoverage * 1.5);
                    float soilMask = smoothstep(soilCutoff, soilCutoff + 0.08, soilNoise);
                    surfaceColor = lerp(surfaceColor, _MeadowSoilColor.rgb, soilMask * 0.65);
                }

                // =========================================================================
                // TRYBY DIAGNOSTYCZNE (_DebugMeadowMode)
                // =========================================================================
                int debugMode = (int)(_DebugMeadowMode + 0.5);
                if (debugMode == 1)
                {
                    float checker = (fmod(abs(gridCell.x) + abs(gridCell.y), 2.0) < 1.0) ? 0.05 : 0.95;
                    return half4(checker, checker, checker, 1.0);
                }
                else if (debugMode == 2)
                {
                    return half4(steppedBlade, steppedBlade, steppedBlade, 1.0);
                }
                else if (debugMode == 3)
                {
                    // Tryb 3: Rozmieszczenie kęp bujniejszej trawy (tuftMask)
                    return half4(tuftMask, tuftMask, tuftMask, 1.0);
                }

                // =========================================================================
                // 6. ROZJAŚNIONA ZIELONA GRANICA HEKSÓW (BRIGHTER MEADOW HEX BORDER)
                // Obrys heksa jest czytelny, organiczny i JAŚNIEJSZY niż środek heksa
                // =========================================================================
                float borderMask = EvaluateHexBorder(input.positionOS_XZ, worldXZ, _HexRadius, _HexBorderWidth, _HexBorderStrength, _HexBorderNoiseStrength);
                float3 finalTopColor = lerp(surfaceColor, _MeadowBorderColor.rgb, borderMask);

                // Ścianki boczne heksa (uskoki terenu)
                float topMask = saturate((normWS.y - 0.25) / 0.45);
                float3 sideColor = EvaluateSideStrataSoil(input.positionWS, normWS, _MeadowDarkColor.rgb, _MeadowBaseColor.rgb);
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
