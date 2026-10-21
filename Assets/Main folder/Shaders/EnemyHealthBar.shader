Shader "Custom/EnemyHealthBar"
{
    Properties
    {
        _BarColor ("Bar Color", Color) = (0.85, 0.15, 0.15, 1.0)
        _BgColor ("Background Color", Color) = (0.12, 0.12, 0.12, 0.95)
        _BorderColor ("Border Color", Color) = (0.02, 0.02, 0.02, 1.0)
        _DividerColor ("Divider Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _FillRatio ("Fill Ratio", Range(0, 1)) = 1.0
        _SegmentCount ("Segment Count", Float) = 1.0
        _BorderThickness ("Border Thickness", Float) = 2.0
        _DividerThickness ("Divider Thickness", Float) = 1.5
        _BarPixelSize ("Bar Pixel Size", Vector) = (96, 24, 0, 0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BarColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BgColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BorderColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DividerColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BarPixelSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _FillRatio)
                UNITY_DEFINE_INSTANCED_PROP(float, _SegmentCount)
                UNITY_DEFINE_INSTANCED_PROP(float, _BorderThickness)
                UNITY_DEFINE_INSTANCED_PROP(float, _DividerThickness)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 barCol = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BarColor);
                float4 bgCol = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BgColor);
                float4 borderCol = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BorderColor);
                float4 dividerCol = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DividerColor);
                float2 barSize = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BarPixelSize).xy;
                float fill = saturate(UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FillRatio));
                float segCount = max(0.001, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SegmentCount));
                float borderThick = max(0.5, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BorderThickness));
                float divThick = max(0.5, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DividerThickness));

                float2 pixel = input.uv * barSize;

                // Obramowanie zewnętrzne paska
                if (pixel.x < borderThick || pixel.x > barSize.x - borderThick ||
                    pixel.y < borderThick || pixel.y > barSize.y - borderThick)
                {
                    return borderCol;
                }

                // Wnętrze paska
                float innerX = pixel.x - borderThick;
                float innerWidth = max(0.001, barSize.x - 2.0 * borderThick);
                float innerU = innerX / innerWidth;

                // Jeśli poza wypełnioną częścią -> tło utraconego zdrowia
                if (innerU > fill)
                {
                    return bgCol;
                }

                // Wypełniona część paska: sprawdź linie podziału segmentów
                if (segCount > 1.0)
                {
                    float segPos = innerU * segCount;
                    float nearestLine = round(segPos);

                    // Linie podziału tylko wewnętrzne (od 1 do floor(segCount))
                    if (nearestLine >= 1.0 && nearestLine < segCount - 0.01)
                    {
                        float distToLineInPx = abs(segPos - nearestLine) * (innerWidth / segCount);
                        float halfDiv = divThick * 0.5;
                        if (distToLineInPx <= halfDiv)
                        {
                            return dividerCol;
                        }
                    }
                }

                return barCol;
            }
            ENDHLSL
        }
    }

    // Fallback dla potoku Built-in / testów Editora
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100" 
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BarColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BgColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BorderColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DividerColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BarPixelSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _FillRatio)
                UNITY_DEFINE_INSTANCED_PROP(float, _SegmentCount)
                UNITY_DEFINE_INSTANCED_PROP(float, _BorderThickness)
                UNITY_DEFINE_INSTANCED_PROP(float, _DividerThickness)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                fixed4 barCol = UNITY_ACCESS_INSTANCED_PROP(Props, _BarColor);
                fixed4 bgCol = UNITY_ACCESS_INSTANCED_PROP(Props, _BgColor);
                fixed4 borderCol = UNITY_ACCESS_INSTANCED_PROP(Props, _BorderColor);
                fixed4 dividerCol = UNITY_ACCESS_INSTANCED_PROP(Props, _DividerColor);
                float2 barSize = UNITY_ACCESS_INSTANCED_PROP(Props, _BarPixelSize).xy;
                float fill = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _FillRatio));
                float segCount = max(0.001, UNITY_ACCESS_INSTANCED_PROP(Props, _SegmentCount));
                float borderThick = max(0.5, UNITY_ACCESS_INSTANCED_PROP(Props, _BorderThickness));
                float divThick = max(0.5, UNITY_ACCESS_INSTANCED_PROP(Props, _DividerThickness));

                float2 pixel = i.uv * barSize;

                if (pixel.x < borderThick || pixel.x > barSize.x - borderThick ||
                    pixel.y < borderThick || pixel.y > barSize.y - borderThick)
                {
                    return borderCol;
                }

                float innerX = pixel.x - borderThick;
                float innerWidth = max(0.001, barSize.x - 2.0 * borderThick);
                float innerU = innerX / innerWidth;

                if (innerU > fill)
                {
                    return bgCol;
                }

                if (segCount > 1.0)
                {
                    float segPos = innerU * segCount;
                    float nearestLine = round(segPos);

                    if (nearestLine >= 1.0 && nearestLine < segCount - 0.01)
                    {
                        float distToLineInPx = abs(segPos - nearestLine) * (innerWidth / segCount);
                        float halfDiv = divThick * 0.5;
                        if (distToLineInPx <= halfDiv)
                        {
                            return dividerCol;
                        }
                    }
                }

                return barCol;
            }
            ENDCG
        }
    }
    FallBack Off
}
