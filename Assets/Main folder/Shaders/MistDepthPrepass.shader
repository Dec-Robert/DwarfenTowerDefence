Shader "Custom/MistDepthPrepass"
{
    // =======================================================================================
    // Depth prepass gornej tafli przezroczystej mgly (stan Surveying).
    //
    // Material z tym shaderem jest dokladany jako DRUGI material na renderery chunka mgly
    // w stanie Surveying. Renderuje sie w kolejce Transparent-1 (2999), czyli PRZED wlasciwa
    // mgla (3000), i zapisuje wylacznie glebokosc gornych scian heksow (bez koloru).
    //
    // Efekt: boczne scianki mgly, ktore gracz widzialby "przez" polprzezroczysta gorna tafle,
    // nie przechodza ZTest i nie sa rysowane. Scianki zewnetrzne (niezasloniete tafla) dalej
    // sa widoczne. Koszt: jeden tani draw bez fragment shadera koloru.
    // =======================================================================================
    Properties { }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-1"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MistTopDepth"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Tylko gorne sciany (normal w gore). Boki i spod -> zdegenerowane trojkaty.
                if (input.normalOS.y < 0.5)
                {
                    output.positionCS = float4(0.0, 0.0, 0.0, 0.0);
                    return output;
                }

                // Ta sama transformacja co w Mist.shader dla gornej tafli (brak _MistSideDrop
                // dla gory), wiec glebokosc jest identyczna i gorna tafla przechodzi ZTest LEqual.
                output.positionCS = GetVertexPositionInputs(input.positionOS.xyz).positionCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
