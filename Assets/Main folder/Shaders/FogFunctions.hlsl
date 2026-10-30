#ifndef FOG_FUNCTIONS_INCLUDED
#define FOG_FUNCTIONS_INCLUDED

// ===================================================================================
// Funkcje pomocnicze: Bezteksturowy szum 2D oraz animowane kłęby chmur
// ===================================================================================
float FogHash2D(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float FogNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f); // Hermite smoothing

    float a = FogHash2D(i);
    float b = FogHash2D(i + float2(1.0, 0.0));
    float c = FogHash2D(i + float2(0.0, 1.0));
    float d = FogHash2D(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// ===================================================================================
// Funkcja 1: Miksowanie kolorów mgły w pikselu (Fragment)
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
    // Bezpieczny odczyt granic: jeśli parametr w grafie jest (0,0,0,0), bierzemy globalny _FogWorldBounds
    float4 bounds = (length(WorldBounds.zw) > 0.001) ? WorldBounds : _FogWorldBounds;

    // 1. Odczyt maski bazowej (0.0 = Borderlands/Settled, 1.0 = Wilderness)
    float2 baseUV = (WorldPos.xz - bounds.xy) / max(bounds.zw, float2(0.0001, 0.0001));
    baseUV = clamp(baseUV, 0.0, 1.0);
    float rawMask = SAMPLE_TEXTURE2D(FogMask.tex, FogMask.samplerstate, baseUV).r;

    // 2. Kłębiące się, wieloskalowe obłoki ciemnej mgły (Wilderness)
    float time = _Time.y;
    float2 drift1 = float2(time * 0.035, time * 0.02);
    float2 drift2 = float2(-time * 0.02, time * 0.035);

    float n1 = FogNoise2D(WorldPos.xz * 0.16 + drift1);
    float n2 = FogNoise2D(WorldPos.xz * 0.35 + drift2 + float2(15.7, 39.2));
    float darkCloudNoise = n1 * 0.65 + n2 * 0.35; // zakres 0..1

    // 3. Napływ ciemnej mgły na chunki do odkrycia:
    // Przy odkrytym terenie (rawMask bliskie 0) encroachmentFactor = 0 (brzeg z osadą jest czysty i regularny)
    // Im dalej ku Wilderness, tym obłoki ciemnej mgły wpychają mrok głębiej do wnętrza chunka
    float encroachmentFactor = smoothstep(0.08, 0.60, rawMask);
    float dynamicMask = rawMask + (darkCloudNoise - 0.40) * (0.45 * encroachmentFactor);
    dynamicMask = saturate(dynamicMask);

    // 4. Płynny współczynnik przejścia do ciemnej mgły
    float fMin = min(FadeMin, 0.25);
    float fMax = max(FadeMax, 0.80);
    float wilderness = smoothstep(fMin, fMax, dynamicMask);

    // 5. Czysty fiolet bez sztucznego podświetlania środka chunka
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
    half4 bounds = (length(WorldBounds.zw) > 0.001) ? WorldBounds : (half4)_FogWorldBounds;

    half2 baseUV = (WorldPos.xz - bounds.xy) / max(bounds.zw, half2(0.0001, 0.0001));
    baseUV = clamp(baseUV, 0.0, 1.0);
    half rawMask = SAMPLE_TEXTURE2D(FogMask.tex, FogMask.samplerstate, baseUV).r;

    half time = (half)_Time.y;
    half2 drift1 = half2(time * (half)0.035, time * (half)0.02);
    half2 drift2 = half2(-time * (half)0.02, time * (half)0.035);

    half n1 = (half)FogNoise2D((float2)(WorldPos.xz * (half)0.16 + drift1));
    half n2 = (half)FogNoise2D((float2)(WorldPos.xz * (half)0.35 + drift2 + half2(15.7, 39.2)));
    half darkCloudNoise = n1 * (half)0.65 + n2 * (half)0.35;

    half encroachmentFactor = smoothstep((half)0.08, (half)0.60, rawMask);
    half dynamicMask = rawMask + (darkCloudNoise - (half)0.40) * ((half)0.45 * encroachmentFactor);
    dynamicMask = saturate(dynamicMask);

    half fMin = min(FadeMin, (half)0.25);
    half fMax = max(FadeMax, (half)0.80);
    half wilderness = smoothstep(fMin, fMax, dynamicMask);

    FinalColor = lerp(PurpleColor, DarkColor + VoronoiFlashes * wilderness, wilderness);
    OutWildernessFactor = wilderness;
}

// ===================================================================================
// Funkcja 2: Wierzchołki siatki – brak deformacji zapewnia regularne brzegi z terenem
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
    // Zwracamy nienaruszoną pozycję wierzchołka, zachowując regularne krawędzie heksów
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
