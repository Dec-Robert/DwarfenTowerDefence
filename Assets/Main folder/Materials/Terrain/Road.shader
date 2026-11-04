Shader "Custom/Road"
{
    Properties
    {
        [Header(Road Dirt Base)]
        _RoadBaseColor("Ubita ziemia bazowa - Base Dirt", Color) = (0.451, 0.353, 0.282, 1.0)
        _RoadDarkColor("Ciemna ubita gleba - Dark Dirt", Color) = (0.286, 0.216, 0.157, 1.0)
        _RoadDustColor("Suchy pyl i przetarcia - Dust & Wear", Color) = (0.565, 0.443, 0.353, 1.0)
        _RoadBodyBrightness("Jasnosc korpusu drogi - Road Brightness", Range(0.7, 1.5)) = 1.0

        [Header(Macro Variation)]
        _MacroScale("Skala przetarc i sciezki - Macro Scale", Float) = 0.40
        _MacroContrast("Kontrast przetarc - Macro Contrast", Range(0.2, 2.0)) = 0.90

        [Header(Retro Stylized Detail)]
        _TexelDensity("Gestosc pikseli retro - Texel Density", Float) = 16.0
        _DetailScale("Skala faktury ziemi - Dirt Texture Scale", Float) = 2.2
        _DetailStrength("Sila faktury retro - Dirt Texture Strength", Range(0.0, 1.0)) = 0.50
        _QuantizeSteps("Kwantyzacja odcieni - Color Steps", Range(2.0, 8.0)) = 4.0

        [Header(Gravel and Pebbles)]
        _PebbleColorLight("Jasny kamyk - Light Pebble", Color) = (0.471, 0.420, 0.369, 1.0)
        _PebbleColorDark("Ciemny lupek - Dark Pebble", Color) = (0.271, 0.243, 0.220, 1.0)
        _PebbleClusterScale("Skala skupisk zwiru - Cluster Scale", Float) = 0.70
        _PebbleDensity("Ilosc skupisk zwiru - Gravel Density", Range(0.0, 0.50)) = 0.18
        _PebbleScale("Rozmiar kamykow - Pebble Frequency", Float) = 8.0
        _PebbleContrast("Kontrast kamykow - Pebble Contrast", Range(0.0, 1.0)) = 0.55

        [Header(Dynamic Shoulders)]
        _ShoulderColor("Kolor pobocza - Shoulder Color", Color) = (0.580, 0.459, 0.365, 1.0)
        _ShoulderWidth("Szerokosc pobocza - Shoulder Width", Range(0.01, 0.20)) = 0.06
        _ShoulderStrength("Sila pobocza - Shoulder Strength", Range(0.0, 1.0)) = 0.25
        _ShoulderNoiseStrength("Nieregularnosc pobocza - Shoulder Noise", Range(0.0, 1.0)) = 0.25
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0

        [Header(Debugging)]
        _DebugShoulders("Tryb debugowania poboczy (1 = wlacz)", Float) = 0.0
        _DebugEdgeMask("Debug Edge Mask (0 = z mesha, 63 = wszystkie)", Float) = 0.0

        [Header(Low Light Readability)]
        _AmbientBoost("Jasnosc w polmroku i nocy - Ambient Boost", Range(0.0, 1.0)) = 0.50

        [Header(Surface Properties)]
        _Smoothness("Gladkosc - Smoothness", Range(0.0, 1.0)) = 0.05
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
                float4 _RoadBaseColor;
                float4 _RoadDarkColor;
                float4 _RoadDustColor;
                float4 _PebbleColorLight;
                float4 _PebbleColorDark;
                float4 _ShoulderColor;
                float  _RoadBodyBrightness;
                float  _MacroScale;
                float  _MacroContrast;
                float  _TexelDensity;
                float  _DetailScale;
                float  _DetailStrength;
                float  _QuantizeSteps;
                float  _PebbleClusterScale;
                float  _PebbleDensity;
                float  _PebbleScale;
                float  _PebbleContrast;
                float  _ShoulderWidth;
                float  _ShoulderStrength;
                float  _ShoulderNoiseStrength;
                float  _HexRadius;
                float  _DebugShoulders;
                float  _DebugEdgeMask;
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

                float rIn = hexRadius * 0.8660254; // Apotema heksa: R * cos(30 deg)
                float2 p = localPosXZ;

                // Odleglosci prostopadle od kazdej z 6 krawedzi do wnetrza heksa
                float dist[6];
                dist[0] = rIn - dot(p, HEX_EDGE_NORMALS[0]);
                dist[1] = rIn - dot(p, HEX_EDGE_NORMALS[1]);
                dist[2] = rIn - dot(p, HEX_EDGE_NORMALS[2]);
                dist[3] = rIn - dot(p, HEX_EDGE_NORMALS[3]);
                dist[4] = rIn - dot(p, HEX_EDGE_NORMALS[4]);
                dist[5] = rIn - dot(p, HEX_EDGE_NORMALS[5]);

                // Szum zaburzajacy szerokosc pobocza w przestrzeni swiata (stabilny przy ruchu kamery)
                float shoulderNoise = ValueNoise2D(worldXZ * 3.5 + float2(17.4, 43.8)) - 0.5;

                float shoulderMask = 0.0;

                [unroll]
                for (int k = 0; k < 6; k++)
                {
                    int active_k = (mask >> k) & 1;
                    if (active_k == 0) continue;

                    // Sasiednie krawedzie k-1 i k+1
                    int prevIdx = (k + 5) % 6;
                    int nextIdx = (k + 1) % 6;
                    int active_prev = (mask >> prevIdx) & 1;
                    int active_next = (mask >> nextIdx) & 1;

                    // Plynne wygaszanie pobocza przy naroznikach prowadzacych do polaczenia drog (Road-Road)
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

                    float edgeFactor = smoothstep(effectiveWidth, 0.0, max(0.0, dist[k])) * cornerFade;
                    shoulderMask = max(shoulderMask, edgeFactor);
                }

                // Organiczne przetarcie pylastego pobocza w przestrzeni swiata
                float dustBreak = saturate(0.70 + 0.50 * ValueNoise2D(worldXZ * 1.6 + float2(52.1, 11.7)));
                return shoulderMask * dustBreak;
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

                // Maska krawedzi: priorytet ma parametr debugowania, a domyslnie UV1.x z mesha
                output.roadEdgeMask = _DebugEdgeMask > 0.5 ? _DebugEdgeMask : input.uv1.x;

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

                // Drobne znieksztalcenie siatki texeli, zapobiegajace nienaturalnym kwadratom
                float2 warpOffset = (float2(Hash21(floor(worldXZ * 2.5)), Hash21(floor(worldXZ * 2.5) + 31.7)) - 0.5) * 0.04;
                float2 warpedWorld = worldXZ + warpOffset;

                // Kwantyzacja wspolrzednych do siatki wirtualnych texeli retro PSX
                float2 quantizedUV = floor(warpedWorld * _TexelDensity + 0.5) / _TexelDensity;
                float2 retroUV = lerp(quantizedUV, worldXZ, saturate(texelPixelSpan * 0.8));

                // --- 2. Warstwa A: Duze organiczne przetarcia traktu (Macro Variation) ---
                float2 macroUV = worldXZ * _MacroScale;
                float macroWarp = (ValueNoise2D(macroUV * 0.6 + float2(3.7, 8.1)) - 0.5) * 0.40;
                float macroVal = ValueNoise2D(macroUV + macroWarp);
                float macroTone = saturate(0.5 + (macroVal - 0.5) * _MacroContrast);

                // Przejscia pomiedzy zaglebionym ubitym bruzdem, baza ziemi a suchym pylem
                float3 dirtColor = lerp(_RoadDarkColor.rgb, _RoadBaseColor.rgb, saturate(macroTone * 1.8));
                dirtColor = lerp(dirtColor, _RoadDustColor.rgb, saturate((macroTone - 0.55) * 2.0));

                // --- 3. Warstwa B: Faktura ubitej ziemi (Retro Hand-Painted / PSX Detail) ---
                float dirtGrain = ValueNoise2D(retroUV * _DetailScale + float2(17.1, 29.4));
                float steppedGrain = floor(dirtGrain * _QuantizeSteps + 0.5) / _QuantizeSteps;
                float grainMod = (steppedGrain - 0.5) * _DetailStrength * detailFade;

                if (grainMod > 0.0)
                {
                    dirtColor = lerp(dirtColor, _RoadDustColor.rgb, saturate(grainMod * 1.6));
                }
                else
                {
                    dirtColor = lerp(dirtColor, _RoadDarkColor.rgb, saturate(-grainMod * 1.4) * 0.75);
                }

                // Drobny mikrodithering o niskiej amplitudzie
                float microDither = (Hash21(floor(warpedWorld * _TexelDensity)) - 0.5) * 0.035 * _DetailStrength * detailFade;
                dirtColor = saturate(dirtColor + microDither);

                // Zabezpieczenie minimalnego albedo: sciezka nigdy nie zapada sie w czern
                dirtColor = max(dirtColor, _RoadDarkColor.rgb * 0.85);

                // --- 4. Warstwa C: Nieregularny zwir i kamyczki w skupiskach (Clustered Pebbles) ---
                // Skupiska zwiru: wystepuja tylko w nieregularnych plamach (~15-20% drogi)
                float clusterNoise = ValueNoise2D(worldXZ * _PebbleClusterScale + float2(29.4, 61.2));
                float clusterThreshold = 1.0 - saturate(_PebbleDensity * 2.2);
                float clusterMask = smoothstep(clusterThreshold, clusterThreshold + 0.12, clusterNoise);

                // Drobny zwirek mikroskopowy wewnatrz skupisk
                float gritVal = ValueNoise2D(retroUV * 3.8 + float2(7.3, 14.5));
                float gritStepped = floor(gritVal * 3.0 + 0.5) / 3.0;
                float gritMod = (gritStepped - 0.5) * 0.12 * clusterMask * detailFade;
                dirtColor = saturate(dirtColor + gritMod);

                // Kanciaste kamyczki generowane na siatce z losowym przesunieciem (Zero petli!)
                float2 pGrid = retroUV * _PebbleScale;
                float2 pCell = floor(pGrid);
                float2 pFrac = frac(pGrid) - 0.5;

                float cellH1 = Hash21(pCell);
                float cellH2 = Hash21(pCell + float2(19.3, 37.1));
                float cellH3 = Hash21(pCell + float2(53.7, 81.9));

                // Tylko wybrane komorki w skupisku zawieraja kamyk (~45%)
                float hasPebble = step(0.55, cellH1) * clusterMask;

                // Losowe przesuniecie srodka w obrebie komorki
                float2 pCenter = (float2(cellH2, cellH3) - 0.5) * 0.52;
                float2 pDelta = pFrac - pCenter;

                // Kanciasty, stylizowany ksztalt kamyczka (low-poly chipped stone)
                float dBox = max(abs(pDelta.x), abs(pDelta.y));
                float dDiag = (abs(pDelta.x) + abs(pDelta.y)) * 0.707;
                float dFacet = max(dBox, dDiag);
                float pShapeDist = lerp(length(pDelta), dFacet, 0.65);

                // Zroznicowany promien kamyczka
                float pRadius = lerp(0.14, 0.34, cellH3);
                float pebbleMask = smoothstep(pRadius, pRadius - 0.07, pShapeDist) * hasPebble;

                // Cien kontaktowy wokol kamyka (wtopienie w ziemie)
                float creviceAO = smoothstep(pRadius + 0.10, pRadius, pShapeDist) * hasPebble * 0.35;
                dirtColor *= (1.0 - creviceAO);

                // Naturalna, przygaszona barwa kamyka (ciemny lupek lub szarobezowy otoczak)
                float3 pColor = lerp(_PebbleColorDark.rgb, _PebbleColorLight.rgb, cellH2);
                float facetLighting = lerp(0.88, 1.12, saturate(0.5 + (pDelta.y - pDelta.x) * 1.5));
                pColor *= facetLighting;

                // Wtopienie kamyczka w podloze z lagodnym kontrastem
                float3 withPebbles = lerp(dirtColor, pColor, pebbleMask * _PebbleContrast);

                // --- 5. Dynamiczne pobocza i jasnosc korpusu drogi (Road V1.2) ---
                float shoulderMask = EvaluateRoadShoulderMask(input.positionOS_XZ, worldXZ, _HexRadius, input.roadEdgeMask, _ShoulderWidth, _ShoulderNoiseStrength);

                // Jasniejsza powierzchnia glowna calej drogi
                float3 roadBodyColor = withPebbles * _RoadBodyBrightness;

                // Wtopienie pylastego pobocza wylacznie na krawedziach z innym terenem
                float3 finalTopColor = lerp(roadBodyColor, _ShoulderColor.rgb, shoulderMask * _ShoulderStrength);

                // Opcjonalny tryb diagnostyczny poboczy
                if (_DebugShoulders > 0.5)
                {
                    float3 debugCol = lerp(float3(0.12, 0.65, 0.18), float3(0.92, 0.15, 0.10), shoulderMask);
                    finalTopColor = debugCol;
                }

                // --- 6. Sciany boczne heksa (Fallback dla starszych / uniwersalnych meshy) ---
                float topMask = saturate((normWS.y - 0.25) / 0.45);
                float horizontalTangent = dot(input.positionWS.xz, float2(normWS.z, -normWS.x));
                float2 wallCoord = float2(horizontalTangent, input.positionWS.y);
                float sideStrata = sin(input.positionWS.y * 6.0 + ValueNoise2D(wallCoord * 2.0) * 2.0) * 0.15;
                float3 sideSoilColor = lerp(_RoadDarkColor.rgb * 0.85, _RoadBaseColor.rgb * 0.80, saturate(0.5 + sideStrata));
                float3 finalAlbedo = lerp(sideSoilColor, finalTopColor, topMask);

                // --- 7. URP PBR Lighting z ochrona czytelnosci w polmroku i nocy ---
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);

                // Podloga ambientowa (Ambient Floor) z regulacja _AmbientBoost:
                // Gwarantuje odbior cieplego zmierzchu i chlodnego granatu nocy bez zapadania sie w czern
                half3 ambientGI = SampleSH(normWS);
                half3 ambientFloor = half3(0.040, 0.045, 0.042) * (1.0 + _AmbientBoost * 0.85);
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
