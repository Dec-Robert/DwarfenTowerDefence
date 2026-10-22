Shader "Custom/CastleStone"
{
    Properties
    {
        [Header(Kamienne Plyty Posadzkowe (Stone Tiles))]
        _TileColorDark("Ciemny bazalt / spoina (Dark Stone)", Color) = (0.18, 0.18, 0.20, 1)
        _TileColorLight("Ciosany granit (Light Stone)", Color) = (0.38, 0.38, 0.40, 1)
        _TileScale("Rozmiar plyt (Tile Scale)", Float) = 3.2
        _MortarWidth("Szerokosc spoin (Mortar Width)", Range(0.01, 0.2)) = 0.08
        _TileSmoothness("Gladkosc posadzki (Tile Smoothness)", Range(0.0, 1.0)) = 0.32

        [Header(Rzezba i Fazowanie 3D (Bevel and Bump))]
        _BumpStrength("Wyrazistosc krawedzi (Bump Strength)", Range(0.0, 5.0)) = 2.4
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
                float4 _TileColorDark;
                float4 _TileColorLight;
                float _TileScale;
                float _MortarWidth;
                float _TileSmoothness;
                float _BumpStrength;
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
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float  fogFactor    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            float gradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float2 g00 = hash2(i + float2(0.0, 0.0)) * 2.0 - 1.0;
                float2 g10 = hash2(i + float2(1.0, 0.0)) * 2.0 - 1.0;
                float2 g01 = hash2(i + float2(0.0, 1.0)) * 2.0 - 1.0;
                float2 g11 = hash2(i + float2(1.0, 1.0)) * 2.0 - 1.0;

                float n00 = dot(g00, f - float2(0.0, 0.0));
                float n10 = dot(g10, f - float2(1.0, 0.0));
                float n01 = dot(g01, f - float2(0.0, 1.0));
                float n11 = dot(g11, f - float2(1.0, 1.0));

                return 0.5 + 0.5 * lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
            }

            float EvaluateTileHeight(float2 wpos)
            {
                float2 grid = wpos * _TileScale;
                grid.x += step(0.5, frac(grid.y * 0.5)) * 0.5;
                float2 tileCoord = floor(grid);
                float2 tileUV = frac(grid);

                float edgeDist = min(min(tileUV.x, 1.0 - tileUV.x), min(tileUV.y, 1.0 - tileUV.y));
                float bevel = smoothstep(0.0, _MortarWidth, edgeDist);
                float tileVariation = hash1(tileCoord) * 0.1;
                float grain = gradientNoise(wpos * (_TileScale * 4.0)) * 0.04;

                return (bevel * (0.8 + tileVariation)) + grain;
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

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 wpos = input.positionWS.xz;
                float topMask = smoothstep(0.2, 0.6, input.normalWS.y);

                // --- 1. Wzór Płyt Bruku Krasnoludzkiego (Ashlar Masonry) ---
                float2 grid = wpos * _TileScale;
                grid.x += step(0.5, frac(grid.y * 0.5)) * 0.5;
                float2 tileCoord = floor(grid);
                float2 tileUV = frac(grid);

                float edgeDist = min(min(tileUV.x, 1.0 - tileUV.x), min(tileUV.y, 1.0 - tileUV.y));
                float mortarMask = smoothstep(_MortarWidth, _MortarWidth * 0.3, edgeDist);

                float tileHash = hash1(tileCoord);
                float grain = gradientNoise(wpos * (_TileScale * 3.5));
                float3 tileCol = lerp(_TileColorDark.rgb, _TileColorLight.rgb, 0.4 + 0.5 * tileHash);
                tileCol *= 0.9 + 0.2 * grain;

                float3 finalAlbedo = lerp(tileCol, _TileColorDark.rgb * 0.6, mortarMask);
                float finalSmoothness = lerp(_TileSmoothness, 0.08, mortarMask);

                // Ścianki boczne – bloki muru
                finalAlbedo *= lerp(0.85, 1.0, topMask);

                // --- 2. Relief 3D (Fazowanie Krawędzi Płyt) ---
                float delta = 0.03;
                float hL = EvaluateTileHeight(wpos - float2(delta, 0.0));
                float hR = EvaluateTileHeight(wpos + float2(delta, 0.0));
                float hD = EvaluateTileHeight(wpos - float2(0.0, delta));
                float hU = EvaluateTileHeight(wpos + float2(0.0, delta));

                float3 bumpOffset = float3((hL - hR) * _BumpStrength, 0.0, (hD - hU) * _BumpStrength);
                float3 normalWS = normalize(input.normalWS + bumpOffset * topMask);

                // --- 3. URP PBR Lighting ---
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo;
                surfaceData.metallic = 0.0;
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.smoothness = finalSmoothness;
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
                UNITY_TRANSFER_INPUT_INSTANCE_ID(input, output);
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
