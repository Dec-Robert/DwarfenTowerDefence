Shader "Custom/RangeIndicator"
{
    Properties
    {
        [Header(Kolory i Poswiata)]
        [HDR] _Color ("Kolor poswiaty - Glow Color", Color) = (1.0, 0.42, 0.04, 1.0)
        [HDR] _RimColor ("Kolor dolnej krawedzi - Base Rim Color", Color) = (1.0, 0.68, 0.16, 1.0)
        _GlowAlpha ("Przezroczystosc poswiaty - Glow Alpha", Range(0.0, 1.0)) = 0.34
        _GlowIntensity ("Intensywnosc poswiaty - Glow Intensity", Range(0.0, 4.0)) = 1.35
        _RimAlpha ("Przezroczystosc dolnej krawedzi - Rim Alpha", Range(0.0, 1.0)) = 0.88
        _RimIntensity ("Intensywnosc dolnej krawedzi - Rim Intensity", Range(0.0, 5.0)) = 1.65

        [Header(Poziom Drogi i Wysokosc)]
        _RoadLevelY ("Poziom drogi (World Y) - Road Level Y", Float) = 0.015
        _GlowHeight ("Wysokosc poswiaty w gore - Glow Height", Range(0.1, 5.0)) = 1.55
        _MeshObjectHeight ("Wysokosc lokalna mesha Z - Mesh Object Z", Float) = 0.01

        [Header(Ksztalt Poswiaty)]
        _VerticalFalloff ("Zanik pionowy - Vertical Falloff", Range(0.5, 6.0)) = 2.1
        _RimThickness ("Grubosc dolnej krawedzi - Base Rim Thickness", Range(0.005, 0.25)) = 0.055
        _FresnelPower ("Ostrosc Fresnela na bokach - Fresnel Power", Range(0.5, 6.0)) = 2.0
        _FresnelBoost ("Wzmocnienie krawedzi - Fresnel Boost", Range(0.0, 1.5)) = 0.35

        [Header(Smugi i Animacja)]
        _CurtainFrequency ("Gestosc smug - Curtain Frequency", Range(1.0, 16.0)) = 4.0
        _CurtainContrast ("Kontrast smug - Curtain Contrast", Range(0.0, 0.8)) = 0.42
        _AnimSpeed ("Predkosc animacji - Animation Speed", Range(0.0, 2.0)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "RangeIndicator"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _RimColor;
                float  _GlowAlpha;
                float  _GlowIntensity;
                float  _RimAlpha;
                float  _RimIntensity;
                float  _RoadLevelY;
                float  _GlowHeight;
                float  _MeshObjectHeight;
                float  _VerticalFalloff;
                float  _RimThickness;
                float  _FresnelPower;
                float  _FresnelBoost;
                float  _CurtainFrequency;
                float  _CurtainContrast;
                float  _AnimSpeed;
            CBUFFER_END

            float _UnscaledTime;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float2 radialDirXZ : TEXCOORD1;
                float  height01    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

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

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 centerWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));

                // Kotwiczenie podstawy wskaznika na poziomie drogi niezaleznie od wysokosci heksa wiezy
                float height01 = saturate(input.positionOS.z / max(_MeshObjectHeight, 0.0001));
                worldPos.y = _RoadLevelY + height01 * _GlowHeight;

                float2 radialVec = worldPos.xz - centerWS.xz;
                float radialLen = length(radialVec);
                float2 radialDirXZ = radialLen > 0.0001 ? (radialVec / radialLen) : float2(1.0, 0.0);

                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.radialDirXZ = radialDirXZ;
                output.height01 = height01;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 dir = normalize(input.radialDirXZ);
                float h = saturate(input.height01);

                // Czas animacji (dziala rowniez w trybie pauzy dzieki _UnscaledTime)
                float t = (_UnscaledTime > 0.0001 ? _UnscaledTime : _Time.y) * _AnimSpeed;

                // Bezszwowy szum smug pionowych wokol calego okregu (bez ciecia na UV 0/1)
                float n1 = ValueNoise2D(dir * _CurtainFrequency + float2(t * 0.35, -t * 0.25));
                float n2 = ValueNoise2D(dir * (_CurtainFrequency * 2.15) + float2(-t * 0.45, t * 0.30) + float2(19.3, 41.7));
                float curtain = saturate(n1 * 0.65 + n2 * 0.35);
                float curtainFactor = lerp(1.0 - _CurtainContrast, 1.0, smoothstep(0.15, 0.85, curtain));

                // Zanik pionowy w gore - jasniejsze smugi siegaja nieco wyzej jak zorza
                float invH = saturate(1.0 - h);
                float dynamicFalloff = lerp(_VerticalFalloff * 1.25, _VerticalFalloff * 0.80, curtain);
                float verticalFade = pow(invH, dynamicFalloff) * smoothstep(1.0, 0.72, h);

                // Subtelny Fresnel na bokach cylindra
                float3 normalWS = float3(dir.x, 0.0, dir.y);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float absNdotV = saturate(abs(dot(normalWS, viewDirWS)));
                float fresnel = pow(1.0 - absNdotV, _FresnelPower);

                float glowAlpha = verticalFade * curtainFactor * _GlowAlpha * (1.0 + fresnel * _FresnelBoost);

                // Wyrazna, antyaliasingowana linia podstawy dokladnie na poziomie zasiegu
                float hGrad = max(fwidth(h), 0.002);
                float rimMask = 1.0 - smoothstep(_RimThickness * 0.35, _RimThickness + hGrad * 1.5, h);
                float rimAlpha = rimMask * _RimAlpha;

                float finalAlpha = saturate(max(glowAlpha, rimAlpha));

                float3 glowRGB = _Color.rgb * _GlowIntensity;
                float3 rimRGB = _RimColor.rgb * _RimIntensity;
                float rimWeight = saturate(rimAlpha / max(finalAlpha, 0.0001));
                float3 finalRGB = lerp(glowRGB, rimRGB, rimWeight);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
