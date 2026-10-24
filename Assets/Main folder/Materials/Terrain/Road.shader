Shader "Custom/Road"
{
    Properties
    {
        [Header(Trakt Ziemny (Dirt))]
        _DirtColorDark("Ciemna gleba (Dark Dirt)", Color) = (0.38, 0.25, 0.16, 1)
        _DirtColorLight("Jasny ubity piasek (Light Dirt)", Color) = (0.58, 0.42, 0.28, 1)
        _DirtScale("Skala ziemi (Dirt Scale)", Float) = 1.5

        [Header(Kamyki i Zwir (Pebbles and Gravel))]
        _PebbleColorLight("Jasne kamyczki (Light Pebbles)", Color) = (0.72, 0.64, 0.53, 1)
        _PebbleColorDark("Ciemne kamyczki (Dark Pebbles)", Color) = (0.42, 0.36, 0.31, 1)
        _GravelScale("Skala drobnego zwiru (Fine Gravel Scale)", Float) = 16.0
        _PebbleScale("Skala kamyczkow (Pebble Scale)", Float) = 6.0
        _PebbleCoverage("Pokrycie kamykami (Pebble Coverage)", Range(0.0, 1.0)) = 0.65
        _CreviceAO("Cienie w szczelinach (Crevice AO)", Range(0.0, 1.0)) = 0.45

        [Header(Zmiekczenie i Relief 3D (Softness and Bump))]
        _Softness("Sila rozmycia / zmiekczenia (Softness)", Range(0.0, 1.0)) = 0.35
        _BumpStrength("Sila wypuklosci (Bump Strength)", Range(0.0, 3.0)) = 0.85
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
                float4 _DirtColorDark;
                float4 _DirtColorLight;
                float4 _PebbleColorLight;
                float4 _PebbleColorDark;
                float _DirtScale;
                float _GravelScale;
                float _PebbleScale;
                float _PebbleCoverage;
                float _CreviceAO;
                float _Softness;
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

            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            float hash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
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

            float fbm(float2 p, int octaves)
            {
                float val = 0.0;
                float amp = 0.5;
                float freq = 1.0;
                for (int i = 0; i < octaves; i++)
                {
                    val += amp * gradientNoise(p * freq);
                    freq *= 2.0;
                    amp *= 0.5;
                }
                return val;
            }

            struct PebbleData
            {
                float dist;         // Normalized distance from center (faceted/chipped shape)
                float cellHash;     // Random identifier for coverage & type
                float cellHash2;    // Random identifier for size & tone
            };

            PebbleData voronoiPebbles(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float minNormDist = 999.0;
                float bestHash1 = 0.0;
                float bestHash2 = 0.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(float(x), float(y));
                        float2 cellCoord = i + neighbor;
                        float2 h = hash2(cellCoord);
                        float2 center = neighbor + 0.15 + 0.70 * h;
                        float2 delta = f - center;

                        float h1 = hash1(cellCoord);
                        float h2 = hash1(cellCoord + float2(19.37, 7.81));

                        // Random orientation angle for each pebble
                        float angle = h1 * 6.283185;
                        float s = sin(angle);
                        float c = cos(angle);
                        float2 rotDelta = float2(delta.x * c - delta.y * s, delta.x * s + delta.y * c);

                        // Subtle oblong stretch (natural crushed stone aspect ratio)
                        float aspect = lerp(1.10, 1.35, h2);
                        rotDelta.y *= aspect;

                        // Chipped facets: blend radial distance with diamond/box facets
                        float rRadial = length(rotDelta);
                        float rFacet = (abs(rotDelta.x) + abs(rotDelta.y)) * 0.72;
                        float shapedR = lerp(rRadial, max(rRadial, rFacet), 0.45);

                        // Stone radius
                        float radius = lerp(0.22, 0.42, h2);
                        float normDist = shapedR / radius;

                        if (normDist < minNormDist)
                        {
                            minNormDist = normDist;
                            bestHash1 = h1;
                            bestHash2 = h2;
                        }
                    }
                }

                PebbleData o;
                o.dist = minNormDist;
                o.cellHash = bestHash1;
                o.cellHash2 = bestHash2;
                return o;
            }

            struct RoadSurface
            {
                float3 albedo;
                float height;
            };

            RoadSurface SampleRoadSurface(float2 wpos)
            {
                RoadSurface o;

                // 1. Podloze gliniasto-ziemne (Compacted Earth)
                float dirtFbm = fbm(wpos * _DirtScale, 3);
                float3 dirtColor = lerp(_DirtColorDark.rgb, _DirtColorLight.rgb, dirtFbm);

                // 2. Drobny zwirek i piasek (Micro Gravel & Sand)
                float mg1 = gradientNoise(wpos * _GravelScale);
                float mg2 = gradientNoise(wpos * (_GravelScale * 2.1));
                float microGravel = mg1 * 0.6 + mg2 * 0.4;
                dirtColor = lerp(dirtColor * 0.88, dirtColor * 1.12, microGravel);

                // Drobne jasne okruszki zwiru w ziemi
                float speckle = smoothstep(0.68, 0.78, mg2);
                dirtColor = lerp(dirtColor, _PebbleColorLight.rgb * 0.85, speckle * 0.45);

                // 3. Kamyczki (Pebbles & Stones)
                PebbleData pebble = voronoiPebbles(wpos * _PebbleScale);
                float isPebble = step(1.0 - _PebbleCoverage, pebble.cellHash);

                // Edge chip perturbation on pebble perimeter
                float chipNoise = (gradientNoise(wpos * (_PebbleScale * 2.4)) - 0.5) * 0.08;
                float pDist = pebble.dist + chipNoise;

                // Pebble mask (smooth anti-aliased edge)
                float pebbleMask = smoothstep(1.0, 0.80, pDist) * isPebble;

                // Barwa kamyczka
                float3 pColor = lerp(_PebbleColorDark.rgb, _PebbleColorLight.rgb, pebble.cellHash2);
                float specialType = hash1(float2(pebble.cellHash, pebble.cellHash2) * 13.37);

                // Limestone / pale sandstone variation (matches reference)
                float isLightStone = smoothstep(0.80, 0.86, specialType);
                pColor = lerp(pColor, float3(0.84, 0.80, 0.74), isLightStone * 0.65);

                // Dark slate / flint variation (matches reference)
                float isDarkStone = smoothstep(0.20, 0.14, specialType);
                pColor = lerp(pColor, pColor * 0.55, isDarkStone);

                // Internal stone grain and subtle edge shading
                float sGrain = gradientNoise(wpos * (_GravelScale * 1.4));
                pColor *= lerp(0.92, 1.08, sGrain);
                pColor *= lerp(0.85, 1.05, saturate(1.0 - pDist * 0.7));

                // Contact shadow on the pebble rim
                pColor *= lerp(0.80, 1.0, smoothstep(1.0, 0.65, pDist));

                // 4. Okluzja w szczelinach (Crevice AO)
                // Applied strictly around existing pebbles, preventing Voronoi artifacts on bare dirt!
                float pebbleEdgeAO = smoothstep(1.35, 0.95, pDist) * smoothstep(0.60, 0.95, pDist) * isPebble;
                float ao = 1.0 - (_CreviceAO * 0.70 * pebbleEdgeAO);
                dirtColor *= ao;

                // 5. Profil wysokosci (Smooth plateaued stone profile with zero-slope boundary)
                float x = saturate(1.0 - pDist);
                float stoneProfile = x * x * (3.0 - 2.0 * x); // Hermite smoothstep
                float pebbleH = stoneProfile * 0.22 * isPebble;
                float baseH = dirtFbm * 0.03 + microGravel * 0.015;

                o.albedo = lerp(dirtColor, pColor, pebbleMask);
                o.height = baseH + pebbleH;
                return o;
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

                // --- 1. Filtr Zmiekczajacy (Softening / Anti-Harshness Multi-Sample) ---
                float2 dpos_dx = ddx(wpos);
                float2 dpos_dy = ddy(wpos);
                float pixelFootprint = length(dpos_dx) + length(dpos_dy);
                float blurRadius = max((_Softness * 0.035) + (pixelFootprint * _Softness * 0.6), 0.015);

                RoadSurface centerSample = SampleRoadSurface(wpos);
                float3 finalAlbedo = centerSample.albedo;
                float3 normalWS = input.normalWS;

                if (_Softness > 0.01)
                {
                    // 4-point symmetric isotropic sampling pattern
                    float2 u1 = float2( 0.866025,  0.500000);
                    float2 u2 = float2(-0.500000,  0.866025);

                    float2 o1 =  u1 * blurRadius;
                    float2 o2 =  u2 * blurRadius;
                    float2 o3 = -u1 * blurRadius;
                    float2 o4 = -u2 * blurRadius;

                    RoadSurface s1 = SampleRoadSurface(wpos + o1);
                    RoadSurface s2 = SampleRoadSurface(wpos + o2);
                    RoadSurface s3 = SampleRoadSurface(wpos + o3);
                    RoadSurface s4 = SampleRoadSurface(wpos + o4);

                    // Softened albedo blend
                    finalAlbedo = centerSample.albedo * 0.36 + (s1.albedo + s2.albedo + s3.albedo + s4.albedo) * 0.16;

                    // Unified softened bump: isotropic gradient from the 4 symmetric samples
                    float g1 = (s1.height - s3.height) / (2.0 * blurRadius);
                    float g2 = (s2.height - s4.height) / (2.0 * blurRadius);

                    float dh_dx = g1 * u1.x + g2 * u2.x;
                    float dh_dz = g1 * u1.y + g2 * u2.y;

                    float3 bumpOffset = float3(-dh_dx, 0.0, -dh_dz) * (_BumpStrength * 0.04);
                    normalWS = normalize(input.normalWS + bumpOffset * topMask);
                }
                else
                {
                    // Fast path when softness is 0: central difference with small delta
                    float delta = 0.02;
                    RoadSurface sR = SampleRoadSurface(wpos + float2(delta, 0.0));
                    RoadSurface sU = SampleRoadSurface(wpos + float2(0.0, delta));

                    float dh_dx = (sR.height - centerSample.height) / delta;
                    float dh_dz = (sU.height - centerSample.height) / delta;

                    float3 bumpOffset = float3(-dh_dx, 0.0, -dh_dz) * (_BumpStrength * 0.04);
                    normalWS = normalize(input.normalWS + bumpOffset * topMask);
                }

                // Przyciemnienie pionowych scianek kafelkow
                finalAlbedo *= lerp(0.75, 1.0, topMask);

                // --- 2. URP Soft Stylized Lit (Matte PBR Lighting) ---
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
                surfaceData.specular = half3(0.0, 0.0, 0.0);
                surfaceData.smoothness = 0.0;
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

            CBUFFER_START(UnityPerMaterial)
                float4 _DirtColorDark;
                float4 _DirtColorLight;
                float4 _PebbleColorLight;
                float4 _PebbleColorDark;
                float _DirtScale;
                float _GravelScale;
                float _PebbleScale;
                float _PebbleCoverage;
                float _CreviceAO;
                float _Softness;
                float _BumpStrength;
            CBUFFER_END

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

            CBUFFER_START(UnityPerMaterial)
                float4 _DirtColorDark;
                float4 _DirtColorLight;
                float4 _PebbleColorLight;
                float4 _PebbleColorDark;
                float _DirtScale;
                float _GravelScale;
                float _PebbleScale;
                float _PebbleCoverage;
                float _CreviceAO;
                float _Softness;
                float _BumpStrength;
            CBUFFER_END

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
