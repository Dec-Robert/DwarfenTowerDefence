#ifndef FOG_FUNCTIONS_INCLUDED
#define FOG_FUNCTIONS_INCLUDED

// ===================================================================================
// PARAMETRY GRANICY MGŁY (wszystko w jednostkach świata, ~ 1 = rozmiar heksa)
// Tu stroisz wygląd granicy chunk do odkrycia <-> chunk za daleko
// ===================================================================================
#define FOG_WARP_AMOUNT     3.0   // jak mocno falują/wygryzają się krawędzie (0 = jak dawniej)
#define FOG_WARP_FREQ       0.07  // skala dużych "zatok" i "półwyspów" (mniej = większe kształty)
#define FOG_SOFTNESS        1.4   // promień rozmycia krawędzi (0 = ostra)
#define FOG_DETAIL_STRENGTH 0.55  // drobne postrzępienie / wypustki przy samej krawędzi
#define FOG_DETAIL_FREQ     0.55  // skala tego postrzępienia (więcej = drobniejsze)

// ===================================================================================
// Szum 2D (bez tekstur)
// ===================================================================================
float FogHash2D(float2 p)
{
    // hash bez widocznej siatki (stary dawał blokowe artefakty)
    float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float FogNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = FogHash2D(i);
    float b = FogHash2D(i + float2(1.0, 0.0));
    float c = FogHash2D(i + float2(0.0, 1.0));
    float d = FogHash2D(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// 4 oktawy, każda obrócona -> brak widocznej siatki, organiczny kształt
float FogFBM(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;
    float norm = 0.0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        sum += FogNoise2D(p) * amp;
        norm += amp;
        p = float2(0.8 * p.x - 0.6 * p.y, 0.6 * p.x + 0.8 * p.y) * 2.03 + 17.1;
        amp *= 0.5;
    }
    return sum / norm; // ~0..1, średnio 0.5
}

float FogSampleMask(UnityTexture2D FogMask, float2 uv)
{
    return SAMPLE_TEXTURE2D(FogMask.tex, FogMask.samplerstate, saturate(uv)).r;
}

// ===================================================================================
// Funkcja 1: kolor mgły w pikselu
// ===================================================================================
void CalculateFog_float(
    float3 WorldPos,
    float4 WorldBounds,
    UnityTexture2D FogMask,
    float FadeMin,
    float FadeMax,
    float3 PurpleColor,
    float3 DarkColor,
    float3 VoronoiFlashes,
    out float3 FinalColor,
    out float OutWildernessFactor
)
{
    float4 bounds = (length(WorldBounds.zw) > 0.001) ? WorldBounds : _FogWorldBounds;
    float2 size   = max(bounds.zw, float2(0.0001, 0.0001));

    float2 p     = WorldPos.xz;
    float  time  = _Time.y;
    float2 drift1 = float2(time * 0.035, time * 0.02);
    float2 drift2 = float2(-time * 0.02, time * 0.035);

    // 1. DOMAIN WARPING – przesuwamy punkt, w którym czytamy maskę, o wektor z szumu.
    //    Dzięki temu sama granica (a nie tylko wartość maski) robi się nieregularna,
    //    niezależnie od tego jak stromo maska przechodzi z 0 do 1.
    float2 warp;
    warp.x = FogFBM(p * FOG_WARP_FREQ + drift1);
    warp.y = FogFBM(p * FOG_WARP_FREQ + drift2 + float2(37.2, 11.7));
    warp = (warp - 0.5) * 3.0 * FOG_WARP_AMOUNT;

    // drobna, szybsza warstwa wypustek
    float2 warpFine;
    warpFine.x = FogNoise2D(p * 0.9 + drift2 * 2.0);
    warpFine.y = FogNoise2D(p * 0.9 - drift1 * 2.0 + float2(8.3, 21.9));
    warp += (warpFine - 0.5) * 1.2;

    float2 uv = (p + warp - bounds.xy) / size;

    // 2. MIĘKKA, NIEJEDNOLITA KRAWĘDŹ – 5 próbek maski z promieniem zależnym od szumu
    float  r  = FOG_SOFTNESS * (0.4 + FogNoise2D(p * 0.3 + drift1)) / size.x;
    float2 ro = float2(r, r * size.x / size.y);

    float m  = FogSampleMask(FogMask, uv) * 0.36;
    m += FogSampleMask(FogMask, uv + ro * float2( 1.0,  0.6)) * 0.16;
    m += FogSampleMask(FogMask, uv + ro * float2(-0.6,  1.0)) * 0.16;
    m += FogSampleMask(FogMask, uv + ro * float2(-1.0, -0.6)) * 0.16;
    m += FogSampleMask(FogMask, uv + ro * float2( 0.6, -1.0)) * 0.16;

    // 3. POSTRZĘPIENIE – szum działa tylko w pasie przejścia (m ~ 0.5), więc
    //    głębokie wnętrza chunków pozostają stabilne, a granica dostaje "chmurowe" wypustki
    float detail = FogFBM(p * FOG_DETAIL_FREQ + drift2 * 2.0 + float2(15.7, 39.2));
    float band   = saturate(4.0 * m * (1.0 - m));
    float dynamicMask = saturate(m + (detail - 0.5) * 2.0 * FOG_DETAIL_STRENGTH * band);

    // 4. Przejście do ciemnej mgły
    float fMin = min(FadeMin, 0.25);
    float fMax = max(FadeMax, 0.80);
    float wilderness = smoothstep(fMin, fMax, dynamicMask);

    FinalColor = lerp(PurpleColor, DarkColor + VoronoiFlashes * wilderness, wilderness);
    OutWildernessFactor = wilderness;
}

void CalculateFog_half(
    half3 WorldPos,
    half4 WorldBounds,
    UnityTexture2D FogMask,
    half FadeMin,
    half FadeMax,
    half3 PurpleColor,
    half3 DarkColor,
    half3 VoronoiFlashes,
    out half3 FinalColor,
    out half OutWildernessFactor
)
{
    // szum i warping wymagają precyzji float, więc liczymy w float
    float3 col;
    float  w;
    CalculateFog_float((float3)WorldPos, (float4)WorldBounds, FogMask,
                       (float)FadeMin, (float)FadeMax,
                       (float3)PurpleColor, (float3)DarkColor, (float3)VoronoiFlashes,
                       col, w);
    FinalColor = (half3)col;
    OutWildernessFactor = (half)w;
}

// ===================================================================================
// Funkcja 2: wierzchołki – bez zmian
// ===================================================================================
void CalculateFogSpill_float(
    float3 ObjectPos,
    float3 WorldPos,
    float4 WorldBounds,
    UnityTexture2D FogMask,
    float Time,
    float SpillStrength,
    float SpillSpeed,
    out float3 OutPosition
)
{
    OutPosition = ObjectPos;
}

void CalculateFogSpill_half(
    half3 ObjectPos,
    half3 WorldPos,
    half4 WorldBounds,
    UnityTexture2D FogMask,
    half Time,
    half SpillStrength,
    half SpillSpeed,
    out half3 OutPosition
)
{
    OutPosition = ObjectPos;
}

#endif
