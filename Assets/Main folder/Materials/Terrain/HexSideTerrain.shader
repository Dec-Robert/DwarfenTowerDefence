Shader "Custom/Terrain/HexSideTerrain"
{
    Properties
    {
        [Header(Soil and Strata Colors)]
        _BaseColor("Kolor bazowy gleby (Base Color)", Color) = (0.38, 0.30, 0.22, 1.0)
        _DarkColor("Ciemna warstwa gleby (Dark Strata Color)", Color) = (0.24, 0.18, 0.13, 1.0)
        _AccentColor("Akcent skalny / mineralny (Accent / Rock Color)", Color) = (0.48, 0.40, 0.30, 1.0)

        [Header(Mountain Side Geological Transition)]
        _IsMountainSide("Wariant Mountain Side (1: On, 0: Off)", Float) = 0.0
        _MountainSideBottomColor("Dolna strefa 0-30% (Bottom Rock)", Color) = (0.239, 0.180, 0.129, 1.0)
        _MountainSideMiddleColor("Srodkowa strefa 30-70% (Middle Rock)", Color) = (0.322, 0.278, 0.239, 1.0)
        _MountainSideTopColor("Gorna strefa 70-100% (Top Rock)", Color) = (0.431, 0.392, 0.353, 1.0)
        _MountainSideTransitionNoise("Nieregularnosc stref (Noise Warp)", Range(0.0, 1.0)) = 0.35
        _MountainSideTransitionScale("Skala szumu stref (Noise Scale)", Float) = 2.0

        [Header(Strata Layers)]
        _StrataScale("Skala warstw pionowych (Strata Scale)", Float) = 5.5
        _StrataContrast("Kontrast warstw (Strata Contrast)", Range(0.1, 5.0)) = 1.6

        [Header(Noise Distortion)]
        _NoiseScale("Skala szumu (Noise Scale)", Float) = 2.5
        _NoiseStrength("Sila znieksztalcenia (Noise Distortion)", Range(0.0, 1.0)) = 0.35

        [Header(Height Strata System)]
        _HeightStep("Wysokosc poziomu (Height Step)", Float) = 0.30
        _BaseHeightY("Poziom bazowy Y (Base Height Y)", Float) = 0.0
        _StrataLineColor("Kolor glowny linii (Line Color)", Color) = (0.784, 0.600, 0.345, 1.0)
        _StrataDarkColor("Ciemna wariacja linii (Dark Line)", Color) = (0.549, 0.384, 0.224, 1.0)
        _StrataLightColor("Jasny akcent linii (Light Accent)", Color) = (0.886, 0.722, 0.471, 1.0)
        _StrataLineWidth("Szerokosc linii (Line Width)", Range(0.005, 0.08)) = 0.024
        _StrataLineStrength("Widocznosc linii (Line Strength)", Range(0.0, 1.0)) = 1.0
        _StrataLineIrregularity("Nieregularnosc retro (Irregularity)", Range(0.0, 1.0)) = 0.25
        _StrataLineBrightness("Jasnosc linii (Line Brightness)", Range(0.5, 3.0)) = 1.6
        _StrataEmissionStrength("Emisja linii (Emission Strength)", Range(0.0, 1.0)) = 0.25
        _TopEdgeWidth("Szerokosc gornej krawedzi (Top Rim Width)", Range(0.005, 0.06)) = 0.020
        _TopEdgeStrength("Widocznosc gornej krawedzi (Top Rim Strength)", Range(0.0, 1.0)) = 0.0

        [Header(Debug Diagnostics)]
        _StrataDebugMode("Tryb debugowania (0:Norm, 1:Levels, 2:Bands)", Range(0, 2)) = 0

        [Header(Lighting and Readability)]
        _AmbientBoost("Rozjasnienie cieni i ambientu (Ambient Boost)", Range(0.0, 1.0)) = 0.65
        _BottomDarkening("Sila zaciemnienia dolu (Bottom Darkening)", Range(0.0, 1.0)) = 0.22
        _Smoothness("Gladkosc (Smoothness)", Range(0.0, 1.0)) = 0.05
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
                float4 _BaseColor;
                float4 _DarkColor;
                float4 _AccentColor;
                float4 _MountainSideBottomColor;
                float4 _MountainSideMiddleColor;
                float4 _MountainSideTopColor;
                float4 _StrataLineColor;
                float4 _StrataDarkColor;
                float4 _StrataLightColor;
                float  _IsMountainSide;
                float  _MountainSideTransitionNoise;
                float  _MountainSideTransitionScale;
                float  _StrataScale;
                float  _StrataContrast;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _HeightStep;
                float  _BaseHeightY;
                float  _StrataLineWidth;
                float  _StrataLineStrength;
                float  _StrataLineIrregularity;
                float  _StrataLineBrightness;
                float  _StrataEmissionStrength;
                float  _TopEdgeWidth;
                float  _TopEdgeStrength;
                float  _StrataDebugMode;
                float  _AmbientBoost;
                float  _BottomDarkening;
                float  _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv0          : TEXCOORD0; // uv0.x = perimeter U, uv0.y = local Y (<= 0)
                float2 uv1          : TEXCOORD1; // uv1: visibleDrop (> 0), edgeKind (0=Cliff, 1=Skirt, 2=MapEdge)
                float2 uv2          : TEXCOORD2; // uv2: visibleDrop (> 0), edgeKind (0=Cliff, 1=Skirt, 2=MapEdge)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 sideInfo     : TEXCOORD2; // x = perimeter U, y = local Y, z = visibleDrop, w = edgeKind
                float  fogFactor    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Szybki, bezgaleziowy 2D Hash (pseudo-losowy ulamek)
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Wydajny 2D Value Noise (0 tekstur, gladka interpolacja Hermite'a)
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

                // Bezpieczny odczyt danych uskoku: bierzemy z uv2 lub uv1
                float2 sideData = length(input.uv2) > 0.0001 ? input.uv2 : input.uv1;
                output.sideInfo = float4(input.uv0.x, input.uv0.y, sideData.x, sideData.y);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normWS = normalize(input.normalWS);

                // --- 1. Metryczne koordynaty w przestrzeni sciany ---
                // Styczna pozioma do sciany w plaszczyznie XZ (niezalezna od obrotu heksa):
                float horizontalTangent = dot(input.positionWS.xz, float2(normWS.z, -normWS.x));
                float2 wallCoord = float2(horizontalTangent, input.positionWS.y);

                // Subtelne przesuniecie swiatowe, by sciany roznych heksow mialy unikalny uklad szumu:
                float2 worldShift = float2(input.positionWS.x * 0.35 + input.positionWS.z * 0.15, 0.0);
                float2 noiseUV = (wallCoord + worldShift) * _NoiseScale;

                // --- 2. Antyaliasing proceduralny przy oddaleniu kamery ---
                float pixelSpan = length(fwidth(noiseUV));
                float detailFade = saturate(1.0 - pixelSpan * 1.5);

                // --- 3. Zroznicowanie powierzchni i zaburzenie warstw ---
                float macroPatch = ValueNoise2D(noiseUV * 0.35);

                float warp1 = ValueNoise2D(noiseUV);
                float warp2 = ValueNoise2D(noiseUV * 2.1 + float2(3.1, 7.7));
                float combinedWarp = (warp1 * 0.7 + warp2 * 0.3 - 0.5) * _NoiseStrength * detailFade;

                // --- 4. Nieregularne uwarstwienie (Strata) ---
                float strataY = input.positionWS.y * _StrataScale + combinedWarp * 3.0;

                float band1 = sin(strataY);
                float band2 = sin(strataY * 2.3 + 1.1) * 0.5 * detailFade;
                float band3 = sin(strataY * 0.45 - 0.7) * 0.3;
                float rawStrata = saturate(0.5 + 0.38 * band1 + 0.25 * band2 + 0.12 * band3);

                float strataVal = saturate(pow(rawStrata, _StrataContrast));

                // --- 5. Mieszanie kolorow bazowych gleby lub Mountain Side ---
                float3 albedo;
                if (_IsMountainSide > 0.5)
                {
                    // Znormalizowana wysokosc widocznej sciany: 0.0 na dole, 1.0 na samej gorze
                    float sideHeight01 = saturate(1.0 + (input.sideInfo.y / max(0.001, input.sideInfo.z)));

                    // Organiczne zaburzenie przejscia stref szumem o niskiej czestotliwosci
                    float transNoise = (ValueNoise2D(wallCoord * _MountainSideTransitionScale + worldShift) - 0.5) * _MountainSideTransitionNoise;
                    float warpedHeight = saturate(sideHeight01 + transNoise);

                    // Trzy strefy geologiczne:
                    // 0–30%: ciemny braz / ziemista skala
                    // 30–70%: mieszanka ciemnego brazu i szarego kamienia
                    // 70–100%: dominujaca szarobrazowa skala
                    float tBottomToMid = smoothstep(0.18, 0.38, warpedHeight);
                    float tMidToTop = smoothstep(0.58, 0.82, warpedHeight);

                    float3 mountainGrad = lerp(_MountainSideBottomColor.rgb, _MountainSideMiddleColor.rgb, tBottomToMid);
                    mountainGrad = lerp(mountainGrad, _MountainSideTopColor.rgb, tMidToTop);

                    // Subtelne wtracenia fasetkowe ze strataVal:
                    mountainGrad *= lerp(0.90, 1.10, strataVal);
                    albedo = mountainGrad;
                }
                else
                {
                    float soilFactor = saturate(strataVal + (macroPatch - 0.5) * 0.28);
                    float3 soilColor = lerp(_DarkColor.rgb, _BaseColor.rgb, soilFactor);

                    // Rzadkie, drobne wtracenia skalne / akcentowe:
                    float rockNoise = ValueNoise2D(noiseUV * 3.8 + float2(17.3, 41.2));
                    float rockThreshold = lerp(0.82, 0.75, 1.0 - strataVal);
                    float rockMask = smoothstep(rockThreshold, rockThreshold + 0.08, rockNoise) * detailFade;
                    albedo = lerp(soilColor, _AccentColor.rgb, rockMask * 0.85);
                }

                // --- 6. Wrazenie glebokosci (Bottom Darkening) ---
                float depthNorm = 0.0;
                if (input.sideInfo.z > 0.001)
                {
                    depthNorm = saturate((-input.sideInfo.y) / input.sideInfo.z);
                }

                float darkening = 1.0 - (_BottomDarkening * depthNorm * depthNorm);
                float3 finalAlbedo = albedo * darkening;

                // ---------------------------------------------------------------------
                // --- 7. HEIGHT STRATA SYSTEM (POZIOME LINIE WYSOKOSCIOWE 0..5) ---
                // ---------------------------------------------------------------------
                float heightStep = max(0.001, _HeightStep);

                // Pozycja w ulamkowych poziomach (0.0 = _BaseHeightY, 1.0 = Level 1, itd.):
                float continuousLevel = (input.positionWS.y - _BaseHeightY) / heightStep;

                // Wyznaczamy absolutny poziom gory tego heksa oraz sasiada (dolu widocznej sciany):
                float hexTopLevel = round(continuousLevel - (input.sideInfo.y / heightStep));
                float hexBottomLevel = round(continuousLevel - ((input.sideInfo.y + input.sideInfo.z) / heightStep));

                // Najblizszy poziom calkowity:
                float nearestLevel = round(continuousLevel);

                // Subtelna nieregularnosc retro (stabilna statycznie, nie migocze):
                float lineWarpNoise = ValueNoise2D(float2(horizontalTangent * 2.2 + input.positionWS.x * 0.1, nearestLevel * 7.13 + 12.4));
                float levelWarp = (lineWarpNoise - 0.5) * (_StrataLineIrregularity * 0.08); // max ~4% poziomu

                // Odleglosc od linii poziomu w metrach (world space):
                float distFromLevelMeters = abs(continuousLevel - (nearestLevel + levelWarp)) * heightStep;

                // Antyaliasing z fwidth dla doskonalej stabilnosci przy oddaleniu kamery:
                float dWS_Y = max(fwidth(input.positionWS.y), 0.0005);
                float effectiveHalfWidth = max(_StrataLineWidth * 0.5, dWS_Y * 0.55);
                float lineFilter = dWS_Y * 0.6;

                float internalLineMask = 1.0 - smoothstep(effectiveHalfWidth - lineFilter, effectiveHalfWidth + lineFilter, distFromLevelMeters);

                // Linia wewnetrzna: musi lezec scisle wewnatrz widocznej sciany:
                // (powyzej dolnego styku z sasiadem i ponizej gornej krawedzi)
                bool isInternalLevel = (nearestLevel > hexBottomLevel + 0.15) && (nearestLevel < hexTopLevel - 0.15);
                internalLineMask *= (isInternalLevel ? 1.0 : 0.0);

                // Odrzucenie fragmentow podziemnych (ukryty skirt ponizej visibleBottom):
                bool isVisibleWall = input.sideInfo.y >= (-input.sideInfo.z - 0.005);
                if (!isVisibleWall)
                {
                    internalLineMask = 0.0;
                }

                // Opcjonalne podkreslenie gornej krawedzi (domyslnie wylaczone / 0.0, aby pominac pasek na samej gorze):
                float topRimMask = 0.0;
                if (_TopEdgeStrength > 0.001 && isVisibleWall && (hexTopLevel > hexBottomLevel + 0.1))
                {
                    float distFromTopMeters = -input.sideInfo.y;
                    float topHalfWidth = max(_TopEdgeWidth * 0.5, dWS_Y * 0.55);
                    topRimMask = (1.0 - smoothstep(topHalfWidth - lineFilter, topHalfWidth + lineFilter, distFromTopMeters)) * _TopEdgeStrength;
                }

                float totalLineMask = saturate(internalLineMask + topRimMask);

                // Kolorystyka linii (miedziano-brazowa / zlotawa paleta z subtelnym zroznicowaniem):
                float lineVarNoise = ValueNoise2D(float2(horizontalTangent * 1.5, nearestLevel * 5.31 + 4.2));
                float3 lineTone = lerp(_StrataLineColor.rgb, _StrataLightColor.rgb, saturate(lineVarNoise * 1.3 - 0.15));
                float darkVarNoise = ValueNoise2D(float2(horizontalTangent * 3.1 + 8.2, nearestLevel * 2.11));
                lineTone = lerp(lineTone, _StrataDarkColor.rgb, darkVarNoise * 0.35);
                lineTone *= _StrataLineBrightness;

                // Jasniejszy akcent dla samej gornej krawedzi:
                float3 topRimColor = _StrataLightColor.rgb * _StrataLineBrightness * 1.05;
                float3 finalLineColor = lerp(lineTone, topRimColor, topRimMask / max(0.001, totalLineMask));

                half3 strataEmission = half3(0, 0, 0);

                // Tryby diagnostyczne / debugowania
                if (_StrataDebugMode > 1.5) // Mode 2: Level Bands
                {
                    int band = (int)floor(continuousLevel);
                    float3 bandCol = float3(0.5, 0.5, 0.5);
                    if (band == 0) bandCol = float3(0.25, 0.65, 0.35); // Zielonkawy (0..1)
                    else if (band == 1) bandCol = float3(0.3, 0.6, 0.9); // Blekitny (1..2)
                    else if (band == 2) bandCol = float3(0.95, 0.8, 0.25); // Zloty (2..3)
                    else if (band == 3) bandCol = float3(0.95, 0.5, 0.18); // Pomaranczowy (3..4)
                    else if (band >= 4) bandCol = float3(0.9, 0.25, 0.55); // Karmazynowy / Fiolet (4..5+)
                    else bandCol = float3(0.2, 0.2, 0.2); // Ponizej zera (zapadlina)

                    finalAlbedo = lerp(finalAlbedo, bandCol, 0.70);
                    finalAlbedo = lerp(finalAlbedo, float3(1.0, 1.0, 1.0), totalLineMask);
                    strataEmission = half3(1.0, 1.0, 1.0) * (totalLineMask * 0.3);
                }
                else if (_StrataDebugMode > 0.5) // Mode 1: Height Levels
                {
                    float3 debugLineCol = internalLineMask > 0.01 ? float3(1.0, 0.15, 0.05) : float3(1.0, 0.85, 0.0);
                    finalAlbedo = lerp(finalAlbedo, debugLineCol, totalLineMask);
                    strataEmission = half3(debugLineCol * (totalLineMask * 0.6));
                }
                else // Mode 0: Normal
                {
                    finalAlbedo = lerp(finalAlbedo, finalLineColor, totalLineMask * _StrataLineStrength);
                    strataEmission = half3(finalLineColor * (totalLineMask * _StrataEmissionStrength));
                }

                // --- 8. URP PBR Lighting ---
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);

                // Bezpieczna podloga ambientowa (Ambient Floor) chroniaca pionowe sciany w cieniu przed zapadaniem sie w czern:
                half3 ambientGI = SampleSH(normWS);
                half3 ambientFloor = half3(0.065, 0.070, 0.068) * (1.0 + _AmbientBoost * 1.5);
                inputData.bakedGI = max(ambientGI, ambientFloor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo;
                surfaceData.metallic = 0.0;
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = strataEmission;
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
