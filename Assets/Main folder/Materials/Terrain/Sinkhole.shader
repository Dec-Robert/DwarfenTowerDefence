Shader "Custom/Terrain/Sinkhole"
{
    Properties
    {
        [Header(Collapsed Earth Palette)]
        _SinkholeBaseColor("Ciemna martwa gleba - Dead Earth", Color) = (0.188, 0.137, 0.145, 1.0)
        _SinkholeDamagedColor("Uszkodzona bordowa ziemia - Damaged Earth", Color) = (0.318, 0.173, 0.188, 1.0)
        _CrackColor("Szczeliny i pekniecia - Crack Crevices", Color) = (0.098, 0.090, 0.110, 1.0)
        _ErosionColor("Wysuszona erozja szczelin - Crack Erosion", Color) = (0.475, 0.259, 0.278, 1.0)

        [Header(Fissure and Crack Network)]
        _MacroScale("Skala struktury zapadliska - Macro Scale", Float) = 0.50
        _CrackScale("Skala sieci pekniec - Crack Scale", Float) = 2.4
        _CrackWidth("Szerokosc szczelin - Crack Width", Range(0.01, 0.20)) = 0.06
        _CrackErosionWidth("Szerokosc erozji przy szczelinie - Erosion Width", Range(0.02, 0.35)) = 0.14

        [Header(Retro Stylized Detail)]
        _TexelDensity("Gestosc pikseli retro - Texel Density", Float) = 16.0
        _DetailScale("Skala faktury retro - Detail Scale", Float) = 2.2
        _DetailStrength("Sila faktury retro - Detail Strength", Range(0.0, 1.0)) = 0.40
        _QuantizeSteps("Kwantyzacja odcieni - Color Steps", Range(2.0, 8.0)) = 4.0

        [Header(Hex Grid Readability)]
        _HexBorderWidth("Szerokosc granicy heksa - Border Width", Range(0.01, 0.20)) = 0.07
        _HexBorderStrength("Widocznosc granicy - Border Strength", Range(0.0, 1.0)) = 0.35
        _HexBorderNoiseStrength("Nieregularnosc granicy - Border Noise", Range(0.0, 1.0)) = 0.35
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0

        [Header(Low Light Readability)]
        _AmbientBoost("Jasnosc w polmroku i nocy - Ambient Boost", Range(0.0, 1.0)) = 0.50

        [Header(Surface Properties)]
        _Smoothness("Gladkosc martwego gruntu - Smoothness", Range(0.0, 1.0)) = 0.04
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
                float4 _SinkholeBaseColor;
                float4 _SinkholeDamagedColor;
                float4 _CrackColor;
                float4 _ErosionColor;
                float  _MacroScale;
                float  _CrackScale;
                float  _CrackWidth;
                float  _CrackErosionWidth;
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

                // --- 1. Filtrowanie i wirtualna siatka retro texeli (PSX style) ---
                float detailFade;
                float2 retroUV;
                EvaluateRetroTexel(worldXZ, _TexelDensity, detailFade, retroUV);

                // --- 2. Warstwa A: Martwa, bordowo-brunatna gleba (Dead Soil Base) ---
                float macroNoise = ValueNoise2D(worldXZ * _MacroScale + float2(61.8, 27.4));
                float3 baseSoil = lerp(_SinkholeBaseColor.rgb, _SinkholeDamagedColor.rgb, macroNoise);

                // Ziarnistosc retro
                float dirtGrain = ValueNoise2D(retroUV * _DetailScale + float2(19.3, 83.1));
                float steppedGrain = floor(dirtGrain * _QuantizeSteps + 0.5) / _QuantizeSteps;
                float grainMod = (steppedGrain - 0.5) * _DetailStrength * detailFade;
                baseSoil = saturate(baseSoil + grainMod * 0.07);

                // --- 3. Warstwa B & C: Szczeliny zapadliska i ich erozja (Cracks & Erosion) ---
                float2 warp = (float2(ValueNoise2D(worldXZ * 1.5 + float2(5.2, 8.9)), ValueNoise2D(worldXZ * 1.5 + float2(19.4, 71.2))) - 0.5) * 0.35;
                VoronoiResult crackV = FastVoronoi2D((retroUV + warp) * _CrackScale);

                // Maska szczeliny
                float crackNoiseMod = (ValueNoise2D(worldXZ * 3.5 + float2(33.1, 14.8)) - 0.5) * 0.40;
                float effectiveCrackWidth = max(_CrackWidth * (1.0 + crackNoiseMod), 0.01);
                float crackMask = smoothstep(effectiveCrackWidth, effectiveCrackWidth * 0.30, crackV.crack);

                // Maska suchej erozji wokol krawedzi szczelin
                float effectiveErosionWidth = effectiveCrackWidth + _CrackErosionWidth;
                float erosionMask = smoothstep(effectiveErosionWidth, effectiveCrackWidth, crackV.crack) * (1.0 - crackMask);

                float3 surfaceWithErosion = lerp(baseSoil, _ErosionColor.rgb, erosionMask * 0.85);
                float3 surfaceWithCracks = lerp(surfaceWithErosion, _CrackColor.rgb, crackMask);

                // --- 4. Warstwa D: Subtelne cieniowanie zaglebienia (Depth Shading) ---
                float rCenter = length(input.positionOS_XZ) / _HexRadius;
                float depthShading = lerp(0.88, 1.05, smoothstep(0.0, 0.75, rCenter));
                surfaceWithCracks *= depthShading;

                // --- 5. Subtelne granice heksa ---
                float borderMask = EvaluateHexBorder(input.positionOS_XZ, worldXZ, _HexRadius, _HexBorderWidth, _HexBorderStrength, _HexBorderNoiseStrength);
                float3 borderTone = lerp(_SinkholeBaseColor.rgb * 0.80, _CrackColor.rgb * 0.90, 0.35);
                float3 finalTopColor = lerp(surfaceWithCracks, borderTone, borderMask);

                // --- 6. Scianki boczne heksa ---
                float topMask = saturate((normWS.y - 0.25) / 0.45);
                float3 sideColor = EvaluateSideStrataSoil(input.positionWS, normWS, _SinkholeBaseColor.rgb, _SinkholeDamagedColor.rgb);
                float3 finalAlbedo = lerp(sideColor, finalTopColor, topMask);

                // --- 7. Oświetlenie URP PBR ---
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
