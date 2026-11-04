Shader "Custom/Grassland"
{
    Properties
    {
        [Header(Grass Color Palette)]
        _BaseGrassColor("Trawa bazowa - Base Grass", Color) = (0.245, 0.335, 0.200, 1.0)
        _DarkVegColor("Ciemna roslinnosc - Dark Vegetation", Color) = (0.180, 0.252, 0.155, 1.0)
        _LightGrassColor("Jasna trawa - Light Grass", Color) = (0.365, 0.445, 0.265, 1.0)
        _DryGrassColor("Wysuszona trawa - Dry Grass", Color) = (0.470, 0.445, 0.295, 1.0)
        _SoilColor("Odslonieta gleba - Exposed Soil", Color) = (0.340, 0.280, 0.200, 1.0)

        [Header(Macro Variation)]
        _MacroScale("Skala duzych plam - Macro Scale", Float) = 0.35
        _MacroContrast("Kontrast plam - Macro Contrast", Range(0.2, 2.0)) = 0.85

        [Header(Retro Stylized Detail)]
        _TexelDensity("Gestosc pikseli retro - Texel Density", Float) = 16.0
        _DetailScale("Skala drobnego detalu - Detail Scale", Float) = 1.8
        _DetailStrength("Sila detalu retro - Detail Strength", Range(0.0, 1.0)) = 0.60
        _QuantizeSteps("Kwantyzacja odcieni - Color Steps", Range(2.0, 8.0)) = 4.0

        [Header(Dry Vegetation)]
        _DryAmount("Ilosc suchej trawy - Dry Amount", Range(0.0, 0.25)) = 0.08
        _DryIntensity("Intensywnosc wysuszenia - Dry Intensity", Range(0.0, 1.0)) = 0.85
        _DryScale("Skala skupisk suchej trawy - Dry Scale", Float) = 0.65

        [Header(Exposed Soil)]
        _SoilAmount("Ilosc odslonietej gleby - Soil Amount", Range(0.0, 0.15)) = 0.035
        _SoilScale("Skala plam gleby - Soil Scale", Float) = 0.75

        [Header(Hex Grid Readability)]
        _HexBorderWidth("Szerokosc granicy heksa - Border Width", Range(0.01, 0.20)) = 0.07
        _HexBorderStrength("Widocznosc granicy - Border Strength", Range(0.0, 1.0)) = 0.45
        _HexBorderNoiseStrength("Nieregularnosc granicy - Border Noise", Range(0.0, 1.0)) = 0.35
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0

        [Header(Low Light Readability)]
        _AmbientBoost("Jasnosc w polmroku i nocy - Ambient Boost", Range(0.0, 1.0)) = 0.50

        [Header(Surface Properties)]
        _Smoothness("Gladkosc - Smoothness", Range(0.0, 1.0)) = 0.04
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
                float4 _BaseGrassColor;
                float4 _DarkVegColor;
                float4 _LightGrassColor;
                float4 _DryGrassColor;
                float4 _SoilColor;
                float  _MacroScale;
                float  _MacroContrast;
                float  _TexelDensity;
                float  _DetailScale;
                float  _DetailStrength;
                float  _QuantizeSteps;
                float  _DryAmount;
                float  _DryIntensity;
                float  _DryScale;
                float  _SoilAmount;
                float  _SoilScale;
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

            // Szybki 2D Hash zwracajacy skalar [0, 1]
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Wydajny 2D Value Noise (0 zewnetrznych tekstur, plynna interpolacja Hermite'a)
            float ValueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Uniwersalna funkcja obliczajaca maske [0, 1] krawedzi wewnatrz heksa (pointy-top)
            float EvaluateHexBorderMask(float2 localPosXZ, float2 worldXZ, float hexRadius, float borderWidth, float borderStrength, float noiseStrength)
            {
                float rIn = hexRadius * 0.8660254; // apotema: R * cos(30 deg)
                float2 p = abs(localPosXZ);
                float hexD = max(p.x, 0.5 * p.x + 0.8660254 * p.y);
                float distFromEdge = max(0.0, rIn - hexD);

                float edgePixelSpan = length(fwidth(distFromEdge));
                float borderNoise = ValueNoise2D(worldXZ * 3.5 + float2(5.2, 9.1)) - 0.5;
                float effectiveWidth = max(borderWidth * (1.0 + borderNoise * noiseStrength), edgePixelSpan * 1.2);

                return smoothstep(effectiveWidth, 0.0, distFromEdge) * borderStrength;
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

                // --- 1. Filtrowanie i ochrona antyaliasingowa (fwidth) ---
                float texelPixelSpan = length(fwidth(worldXZ * _TexelDensity));
                float detailFade = saturate(1.0 - (texelPixelSpan - 0.6) * 1.6);

                // Drobne znieksztalcenie siatki texeli, zapobiegajace sztucznym kwadratom
                float2 warpOffset = (float2(Hash21(floor(worldXZ * 2.0)), Hash21(floor(worldXZ * 2.0) + 17.5)) - 0.5) * 0.04;
                float2 warpedWorld = worldXZ + warpOffset;

                // Kwantyzacja wspolrzednych do siatki wirtualnych texeli w stylu PSX / 256px
                float2 quantizedUV = floor(warpedWorld * _TexelDensity + 0.5) / _TexelDensity;
                float2 retroUV = lerp(quantizedUV, worldXZ, saturate(texelPixelSpan * 0.8));

                // --- 2. Warstwa A: Duze organiczne plamy (Macro Variation) ---
                float2 macroUV = worldXZ * _MacroScale;
                float macroWarp = (ValueNoise2D(macroUV * 0.6 + float2(3.1, 7.8)) - 0.5) * 0.5;
                float macroVal = ValueNoise2D(macroUV + macroWarp);

                float macroTone = saturate(0.5 + (macroVal - 0.5) * _MacroContrast);
                float3 macroColor = lerp(_DarkVegColor.rgb, _BaseGrassColor.rgb, saturate(macroTone * 1.8));
                macroColor = lerp(macroColor, _LightGrassColor.rgb, saturate((macroTone - 0.55) * 2.0));

                // --- 3. Warstwa B: Stylizowany detal powierzchni (Retro Hand-Painted / PSX Detail) ---
                float detailNoise = ValueNoise2D(retroUV * _DetailScale + float2(11.3, 19.7));
                float steppedDetail = floor(detailNoise * _QuantizeSteps + 0.5) / _QuantizeSteps;
                float detailMod = (steppedDetail - 0.5) * _DetailStrength * detailFade;

                float3 grassColor = macroColor;
                if (detailMod > 0.0)
                {
                    grassColor = lerp(grassColor, _LightGrassColor.rgb, saturate(detailMod * 1.8));
                }
                else
                {
                    // Lagodniejsze przyciemnienie - chroni przed powstawaniem czarnych plam
                    grassColor = lerp(grassColor, _DarkVegColor.rgb, saturate(-detailMod * 1.4) * 0.75);
                }

                // Drobny dithering o lagodnym zakresie
                float microDither = (Hash21(floor(warpedWorld * _TexelDensity)) - 0.5) * 0.04 * _DetailStrength * detailFade;
                grassColor = saturate(grassColor + microDither);

                // Zabezpieczenie minimalnego albedo: roslinnosc nigdy nie spada do czerni
                grassColor = max(grassColor, _DarkVegColor.rgb * 0.85);

                // --- 4. Warstwa C: Wysuszona roslinnosc (Dry / Wilted Grass, 5-12%) ---
                float dryCluster = ValueNoise2D(worldXZ * _DryScale + float2(47.2, 13.9));
                float dryThreshold = 1.0 - saturate(_DryAmount * 1.8);
                float dryMask = smoothstep(dryThreshold, dryThreshold + 0.12, dryCluster);

                float dryDetail = ValueNoise2D(retroUV * 3.5 + float2(2.1, 8.4));
                dryMask *= smoothstep(0.3, 0.7, dryDetail) * _DryIntensity;

                float3 withDry = lerp(grassColor, _DryGrassColor.rgb, saturate(dryMask));

                // --- 5. Warstwa D: Odslonieta gleba (Exposed Soil, rzadkie plamy) ---
                float soilNoise = ValueNoise2D(worldXZ * _SoilScale + float2(83.1, 51.4));
                float soilThreshold = 1.0 - saturate(_SoilAmount * 2.2);
                float soilMask = smoothstep(soilThreshold, soilThreshold + 0.10, soilNoise);

                float soilRough = (ValueNoise2D(retroUV * 4.0) - 0.5) * 0.12 * detailFade;
                float3 soilWithTexel = saturate(_SoilColor.rgb + soilRough);

                float3 topColor = lerp(withDry, soilWithTexel, saturate(soilMask));

                // --- 6. Subtelne granice heksa (Hex Border Readability) ---
                float borderMask = EvaluateHexBorderMask(input.positionOS_XZ, worldXZ, _HexRadius, _HexBorderWidth, _HexBorderStrength, _HexBorderNoiseStrength);

                // Naturalna ubita / suchsza darn wzdluz krawedzi (delikatnie ciemniejsza/bardziej zuzyta)
                float3 borderTone = lerp(_DarkVegColor.rgb * 0.82, _SoilColor.rgb * 0.88, 0.45);
                borderTone = lerp(borderTone, _DryGrassColor.rgb * 0.78, 0.30);
                float3 finalTopColor = lerp(topColor, borderTone, borderMask);

                // --- 7. Sciany boczne heksa (Fallback dla starszych / pojedynczych meshy) ---
                float topMask = saturate((normWS.y - 0.25) / 0.45);
                float horizontalTangent = dot(input.positionWS.xz, float2(normWS.z, -normWS.x));
                float2 wallCoord = float2(horizontalTangent, input.positionWS.y);
                float sideStrata = sin(input.positionWS.y * 6.0 + ValueNoise2D(wallCoord * 2.0) * 2.0) * 0.15;
                float3 sideSoilColor = lerp(_SoilColor.rgb * 0.75, _DarkVegColor.rgb * 0.85, saturate(0.5 + sideStrata));
                float3 finalAlbedo = lerp(sideSoilColor, finalTopColor, topMask);

                // --- 8. URP PBR Lighting z ochrona czytelnosci w polmroku i nocy ---
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);

                // Podloga ambientowa (Ambient Floor) wzbogacona o regulacje _AmbientBoost:
                // Zapewnia odbior chlodnego/cieplego ambientu w Dusk i Night przez roslinnosc
                half3 ambientGI = SampleSH(normWS);
                half3 ambientFloor = half3(0.040, 0.048, 0.045) * (1.0 + _AmbientBoost * 0.85);
                inputData.bakedGI = max(ambientGI, ambientFloor);

                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo;
                surfaceData.metallic = 0.0;
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.smoothness = _Smoothness;
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
