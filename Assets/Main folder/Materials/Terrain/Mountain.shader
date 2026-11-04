Shader "Custom/Terrain/Mountain"
{
    Properties
    {
        [Header(Rock Color Palette)]
        _RockBaseColor("Bazowa skala - Rock Base", Color) = (0.318, 0.282, 0.255, 1.0)
        _RockDarkColor("Ciemna szczelina/cienie - Rock Dark", Color) = (0.200, 0.180, 0.173, 1.0)
        _RockLightColor("Jasna zwietrzala skala - Rock Light", Color) = (0.439, 0.400, 0.365, 1.0)
        _RockAccentColor("Akcent mineralny/ochra - Rock Accent", Color) = (0.384, 0.349, 0.302, 1.0)

        [Header(Macro Variation)]
        _MacroScale("Skala makro-plam - Macro Scale", Float) = 0.55
        _MacroContrast("Kontrast makro - Macro Contrast", Range(0.5, 2.0)) = 1.10

        [Header(Medium and Micro Detail)]
        _DetailScale("Skala fasetek skalnych - Detail Scale", Float) = 1.80
        _DetailStrength("Sila retro-faktury - Detail Strength", Range(0.0, 1.0)) = 0.45

        [Header(Retro Stylization)]
        _TexelDensity("Gestosc pikseli na heks - Texel Density", Float) = 16.0
        _QuantizeSteps("Kwantyzacja odcieni - Quantize Steps", Range(2.0, 8.0)) = 4.0

        [Header(Hex Grid Readability)]
        _HexBorderColor("Kolor granicy heksa - Border Color", Color) = (0.439, 0.400, 0.365, 1.0)
        _HexBorderWidth("Szerokosc granicy - Border Width", Range(0.01, 0.20)) = 0.06
        _HexBorderStrength("Widocznosc granicy - Border Strength", Range(0.0, 1.0)) = 0.28
        _HexBorderNoise("Nieregularnosc granicy - Border Noise", Range(0.0, 1.0)) = 0.35
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0

        [Header(Lighting and Surface)]
        _AmbientBoost("Rozjasnienie polmroku i cieni - Ambient Boost", Range(0.0, 1.0)) = 0.50
        _Smoothness("Matowosc skaly - Smoothness", Range(0.0, 1.0)) = 0.05
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
            #include "TerrainGroundCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RockBaseColor;
                float4 _RockDarkColor;
                float4 _RockLightColor;
                float4 _RockAccentColor;
                float4 _HexBorderColor;
                float  _MacroScale;
                float  _MacroContrast;
                float  _DetailScale;
                float  _DetailStrength;
                float  _TexelDensity;
                float  _QuantizeSteps;
                float  _HexBorderWidth;
                float  _HexBorderStrength;
                float  _HexBorderNoise;
                float  _HexRadius;
                float  _AmbientBoost;
                float  _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 worldXZ = input.positionWS.xz;

                // ---------------------------------------------------------------------
                // 1. WIRTUALNA SIATKA TEXELI (TEXEL DENSITY & RETRO SNAP)
                // ---------------------------------------------------------------------
                float hexDiameter = max(0.001, 2.0 * _HexRadius);
                float density = max(1.0, _TexelDensity);

                float2 texelCoords = (worldXZ / hexDiameter) * density;
                float2 gridCell = floor(texelCoords);

                // Antyaliasing na krawedzi texela ze stabilnym fwidth
                float2 dTex = fwidth(texelCoords);
                float pixelSpan = max(dTex.x, dTex.y);
                float snapWeight = saturate(1.0 - pixelSpan * 1.2);

                float2 quantizedGrid = gridCell + 0.5;
                float2 samplePos = (quantizedGrid / density) * hexDiameter;

                // Zblizenie do kamery -> wyrazny retro piksel; oddalenie -> plynny brak aliasingu
                float2 retroUV = lerp(worldXZ, samplePos, snapWeight);

                // ---------------------------------------------------------------------
                // 2. MACRO VARIATION (ORGANICZNE PLAMY GEOLOGICZNE - ZERO BRUKU)
                // ---------------------------------------------------------------------
                float2 warpOffset = (float2(ValueNoise2D(retroUV * (_MacroScale * 1.5) + float2(12.3, 45.6)),
                                            ValueNoise2D(retroUV * (_MacroScale * 1.5) + float2(78.9, 32.1))) - 0.5) * 0.45;

                float macroNoise = ValueNoise2D((retroUV + warpOffset) * _MacroScale + float2(21.4, 63.8));
                macroNoise = saturate(0.5 + (macroNoise - 0.5) * _MacroContrast);

                // ---------------------------------------------------------------------
                // 3. MEDIUM DETAIL: BEDROCK FACETS (KWANTYZOWANE FASETKI SKALNE)
                // ---------------------------------------------------------------------
                // Uwaga: Uzywamy losowego odcienia fasetki (cellHash) z kwantyzacja steps,
                // bez zadnych obrysow szczelin (f2 - f1), dzieki czemu skala jest lita!
                VoronoiResult facetV = FastVoronoi2D(retroUV * _DetailScale + warpOffset);
                float quantSteps = max(1.0, _QuantizeSteps);
                float steppedTone = floor(facetV.cellHash * quantSteps + 0.5) / quantSteps;

                // Stopniowanie kolorystyczne zaleznie od makro-plam i fasetek:
                float3 darkToMid = lerp(_RockDarkColor.rgb, _RockBaseColor.rgb, macroNoise);
                float3 midToLight = lerp(_RockBaseColor.rgb, _RockLightColor.rgb, macroNoise);
                float3 rockComposite = lerp(darkToMid, midToLight, steppedTone);

                // Cieplejszy akcent mineralny / ochra na wybranych partiach fasetek:
                float accentNoise = ValueNoise2D(retroUV * (_DetailScale * 0.75) + float2(33.7, 71.2));
                float accentMask = smoothstep(0.68, 0.88, accentNoise);
                rockComposite = lerp(rockComposite, _RockAccentColor.rgb, accentMask * 0.38);

                // ---------------------------------------------------------------------
                // 4. MICRO DETAIL: WZBURZENIE POWIERZCHNI I ZWIETRZELINA (CHISELED GRAIN)
                // ---------------------------------------------------------------------
                float microNoise = (ValueNoise2D(retroUV * (_DetailScale * 4.8) + float2(7.3, 19.1)) - 0.5);
                rockComposite += microNoise * (_DetailStrength * 0.28) * snapWeight;

                // ---------------------------------------------------------------------
                // 5. CZYTELNOSC SIATKI HEKSOW (HEX BORDER)
                // ---------------------------------------------------------------------
                float borderMask = EvaluateHexBorder(input.positionOS.xz, input.positionWS.xz, _HexRadius, _HexBorderWidth, _HexBorderStrength, _HexBorderNoise);
                float3 finalAlbedo = lerp(rockComposite, _HexBorderColor.rgb, borderMask);

                // ---------------------------------------------------------------------
                // 6. OSWIETLENIE URP PBR
                // ---------------------------------------------------------------------
                return CalculateTerrainLitPBR(
                    input.positionWS,
                    input.normalWS,
                    input.positionCS,
                    input.fogFactor,
                    finalAlbedo,
                    _Smoothness,
                    _AmbientBoost
                );
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
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));

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
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
