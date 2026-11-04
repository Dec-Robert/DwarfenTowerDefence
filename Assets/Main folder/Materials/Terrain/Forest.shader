Shader "Custom/Terrain/Forest"
{
    Properties
    {
        [Header(Forest Floor Palette)]
        _ForestBaseColor("Ciemna gleba lesna - Forest Soil", Color) = (0.161, 0.169, 0.125, 1.0)
        _LeafLitterColor("Opadle liscie / sciolka - Leaf Litter", Color) = (0.255, 0.220, 0.165, 1.0)
        _MossColor("Lesny mech - Forest Moss", Color) = (0.275, 0.314, 0.224, 1.0)
        _DryNeedlesColor("Suche igliwie - Dry Needles", Color) = (0.475, 0.392, 0.282, 1.0)

        [Header(Macro Variation and Features)]
        _ForestFloorScale("Skala gleby lesnej - Floor Scale", Float) = 0.45
        _LitterAmount("Ilosc sciolki i igliwia - Litter Amount", Range(0.0, 0.50)) = 0.22
        _LitterScale("Skala skupisk sciolki - Litter Scale", Float) = 1.4
        _MossAmount("Ilosc mchu - Moss Amount", Range(0.0, 0.40)) = 0.18
        _MossScale("Skala plam mchu - Moss Scale", Float) = 0.85
        _RootsStrength("Widocznosc korzeni - Roots Strength", Range(0.0, 0.50)) = 0.15

        [Header(Retro Stylized Detail)]
        _TexelDensity("Gestosc pikseli retro - Texel Density", Float) = 16.0
        _DetailScale("Skala faktury sciolki - Detail Scale", Float) = 2.2
        _DetailStrength("Sila faktury retro - Detail Strength", Range(0.0, 1.0)) = 0.45
        _QuantizeSteps("Kwantyzacja odcieni - Color Steps", Range(2.0, 8.0)) = 4.0

        [Header(Hex Grid Readability)]
        _HexBorderWidth("Szerokosc granicy heksa - Border Width", Range(0.01, 0.20)) = 0.07
        _HexBorderStrength("Widocznosc granicy - Border Strength", Range(0.0, 1.0)) = 0.35
        _HexBorderNoiseStrength("Nieregularnosc granicy - Border Noise", Range(0.0, 1.0)) = 0.35
        _HexRadius("Promien heksa - Hex Radius", Float) = 1.0

        [Header(Low Light Readability)]
        _AmbientBoost("Jasnosc w polmroku i nocy - Ambient Boost", Range(0.0, 1.0)) = 0.50

        [Header(Surface Properties)]
        _Smoothness("Gladkosc sciolki - Smoothness", Range(0.0, 1.0)) = 0.04
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
                float4 _ForestBaseColor;
                float4 _LeafLitterColor;
                float4 _MossColor;
                float4 _DryNeedlesColor;
                float  _ForestFloorScale;
                float  _LitterAmount;
                float  _LitterScale;
                float  _MossAmount;
                float  _MossScale;
                float  _RootsStrength;
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

                // --- 2. Warstwa A: Gleba lesna (Forest Soil) ---
                float floorNoise = ValueNoise2D(worldXZ * _ForestFloorScale + float2(15.2, 49.7));
                float3 floorColor = lerp(_ForestBaseColor.rgb, _LeafLitterColor.rgb * 0.90, floorNoise * 0.65);

                // Subtelne korzenie przy powierzchni (Subtle Roots)
                float rootNoise = abs(ValueNoise2D(worldXZ * 1.8 + float2(9.1, 77.3)) - 0.5) * 2.0;
                float rootStrand = smoothstep(0.12, 0.0, rootNoise);
                floorColor = lerp(floorColor, _ForestBaseColor.rgb * 0.65, rootStrand * _RootsStrength);

                // Ziarnistosc retro scioly
                float dirtGrain = ValueNoise2D(retroUV * _DetailScale + float2(42.1, 13.9));
                float steppedGrain = floor(dirtGrain * _QuantizeSteps + 0.5) / _QuantizeSteps;
                float grainMod = (steppedGrain - 0.5) * _DetailStrength * detailFade;
                floorColor = saturate(floorColor + grainMod * 0.08);

                // Zabezpieczenie minimalnego albedo: lesne dno nigdy nie jest zupelnie czarne
                floorColor = max(floorColor, _ForestBaseColor.rgb * 0.85);

                // --- 3. Warstwa B: Mech lesny (Forest Moss) ---
                float mossNoise = ValueNoise2D(worldXZ * _MossScale + float2(63.1, 31.8));
                float mossMask = smoothstep(1.0 - saturate(_MossAmount * 2.2), 1.0 - saturate(_MossAmount * 2.2) + 0.15, mossNoise);
                float3 floorWithMoss = lerp(floorColor, _MossColor.rgb, mossMask * 0.85);

                // --- 4. Warstwa C: Opadle liscie i igliwie (Leaf Litter & Dry Needles) ---
                float litterNoise = ValueNoise2D(worldXZ * _LitterScale + float2(21.4, 88.5));
                float litterMask = smoothstep(1.0 - saturate(_LitterAmount * 2.0), 1.0 - saturate(_LitterAmount * 2.0) + 0.12, litterNoise);

                float litterDetail = ValueNoise2D(retroUV * 3.8 + float2(3.7, 18.2));
                float3 litterCol = lerp(_LeafLitterColor.rgb, _DryNeedlesColor.rgb, saturate(litterDetail * 1.6 - 0.3));
                float3 surfaceColor = lerp(floorWithMoss, litterCol, litterMask * smoothstep(0.30, 0.70, litterDetail));

                // --- 5. Subtelne granice heksa ---
                float borderMask = EvaluateHexBorder(input.positionOS_XZ, worldXZ, _HexRadius, _HexBorderWidth, _HexBorderStrength, _HexBorderNoiseStrength);
                float3 borderTone = lerp(_ForestBaseColor.rgb * 0.80, _LeafLitterColor.rgb * 0.70, 0.30);
                float3 finalTopColor = lerp(surfaceColor, borderTone, borderMask);

                // --- 6. Scianki boczne heksa ---
                float topMask = saturate((normWS.y - 0.25) / 0.45);
                float3 sideColor = EvaluateSideStrataSoil(input.positionWS, normWS, _ForestBaseColor.rgb, _LeafLitterColor.rgb);
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
