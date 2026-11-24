#ifndef TERRAIN_GROUND_COMMON_INCLUDED
#define TERRAIN_GROUND_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Szybki 2D Hash zwracajacy skalar [0, 1]
float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Szybki 2D Hash zwracajacy wektor float2 [0, 1]
float2 Hash22(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453123);
}

// Wydajny 2D Value Noise (0 tekstur, gladka interpolacja Hermite'a)
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

// Struktura i funkcja 2D Voronoi zwracajaca F1, F2, pekniecia (F2-F1) oraz losowy hash komorki
struct VoronoiResult
{
    float f1;          // Odleglosc do najblizszego punktu
    float f2;          // Odleglosc do drugiego najblizszego punktu
    float crack;       // f2 - f1: odleglosc od krawedzi / szczeliny komorkowej
    float cellHash;    // Identyfikator odcienia komorki
    float2 cellCenter; // Srodek komorki
};

VoronoiResult FastVoronoi2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    float minD1 = 8.0;
    float minD2 = 8.0;
    float bestHash = 0.0;
    float2 bestCenter = float2(0, 0);

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbor = float2(float(x), float(y));
            float2 cell = i + neighbor;
            float2 h = Hash22(cell);
            float2 pt = neighbor + 0.15 + 0.70 * h;
            float2 diff = f - pt;
            float d = length(diff);

            if (d < minD1)
            {
                minD2 = minD1;
                minD1 = d;
                bestHash = Hash21(cell);
                bestCenter = cell + pt;
            }
            else if (d < minD2)
            {
                minD2 = d;
            }
        }
    }

    VoronoiResult res;
    res.f1 = minD1;
    res.f2 = minD2;
    res.crack = minD2 - minD1;
    res.cellHash = bestHash;
    res.cellCenter = bestCenter;
    return res;
}

// Filtrowanie i wirtualna siatka texeli retro PSX
void EvaluateRetroTexel(float2 worldXZ, float texelDensity, out float detailFade, out float2 retroUV)
{
    float texelPixelSpan = length(fwidth(worldXZ * texelDensity));
    detailFade = saturate(1.0 - (texelPixelSpan - 0.6) * 1.6);

    float2 warpOffset = (float2(Hash21(floor(worldXZ * 2.0)), Hash21(floor(worldXZ * 2.0) + 17.5)) - 0.5) * 0.04;
    float2 warpedWorld = worldXZ + warpOffset;

    float2 quantizedUV = floor(warpedWorld * texelDensity + 0.5) / texelDensity;
    retroUV = lerp(quantizedUV, worldXZ, saturate(texelPixelSpan * 0.8));
}

// Subtelne granice heksa (pointy-top) spójne z Grassland
float EvaluateHexBorder(float2 localPosXZ, float2 worldXZ, float hexRadius, float borderWidth, float borderStrength, float noiseStrength)
{
    float rIn = hexRadius * 0.8660254; // Apotema: R * cos(30 deg)
    float2 p = abs(localPosXZ);
    float hexD = max(p.x, 0.5 * p.x + 0.8660254 * p.y);
    float distFromEdge = max(0.0, rIn - hexD);

    float edgePixelSpan = length(fwidth(distFromEdge));
    float borderNoise = ValueNoise2D(worldXZ * 3.5 + float2(5.2, 9.1)) - 0.5;
    float effectiveWidth = max(borderWidth * (1.0 + borderNoise * noiseStrength), edgePixelSpan * 1.2);

    return smoothstep(effectiveWidth, 0.0, distFromEdge) * borderStrength;
}

// Warstwy ziemi na sciankach bocznych heksa (fallback dla uniwersalnych meshy)
float3 EvaluateSideStrataSoil(float3 positionWS, float3 normalWS, float3 darkColor, float3 baseColor)
{
    float horizontalTangent = dot(positionWS.xz, float2(normalWS.z, -normalWS.x));
    float2 wallCoord = float2(horizontalTangent, positionWS.y);
    float sideStrata = sin(positionWS.y * 6.0 + ValueNoise2D(wallCoord * 2.0) * 2.0) * 0.15;
    return lerp(darkColor * 0.85, baseColor * 0.80, saturate(0.5 + sideStrata));
}

// Standardowy URP PBR fragment lighting z bezpieczna podloga ambientowa
half4 CalculateTerrainLitPBREmissive(
    float3 positionWS,
    float3 normalWS,
    float4 positionCS,
    float fogFactor,
    float3 albedo,
    float smoothness,
    float ambientBoost,
    half3 emission)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalize(normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), fogFactor);

    // Real-time Trilight z DayNightCycle jako glowne GI terenu (bez zawyzania przez statyczne SampleSH skyboxa)
    half3 trilightGI = lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb, saturate(inputData.normalWS.y));
    trilightGI = lerp(trilightGI, unity_AmbientGround.rgb, saturate(-inputData.normalWS.y));
    half trilightSum = trilightGI.r + trilightGI.g + trilightGI.b;
    half3 baseAmbient = trilightSum > 0.0001 ? trilightGI : SampleSH(inputData.normalWS);
    half3 ambientGI = baseAmbient * (1.0 + ambientBoost * 0.22);
    half3 ambientFloor = half3(0.008, 0.010, 0.015) * (1.0 + ambientBoost * 0.35);
    inputData.bakedGI = max(ambientGI, ambientFloor);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.metallic = 0.0;
    surfaceData.specular = half3(0.04, 0.04, 0.04);
    surfaceData.smoothness = smoothness;
    surfaceData.normalTS = half3(0, 0, 1);
    surfaceData.emission = emission;
    surfaceData.occlusion = 1.0;
    surfaceData.alpha = 1.0;

    half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
    finalColor.rgb = MixFog(finalColor.rgb, inputData.fogCoord);
    return finalColor;
}

half4 CalculateTerrainLitPBR(
    float3 positionWS,
    float3 normalWS,
    float4 positionCS,
    float fogFactor,
    float3 albedo,
    float smoothness,
    float ambientBoost)
{
    return CalculateTerrainLitPBREmissive(
        positionWS, normalWS, positionCS, fogFactor,
        albedo, smoothness, ambientBoost, half3(0, 0, 0));
}

// ============================================================================
// PS1 / PS2 RETRO TERRAIN SYSTEM (Ostra siatka texeli + dyskretne palety)
// ============================================================================

struct RetroTexelGrid
{
    float2 cell;         // Wspolrzedne calkowite texela w swiecie (stabilne, wspolne dla wszystkich terenow)
    float2 pixelPos;     // Srodek texela w jednostkach swiata (do probkowania bez rozmycia)
    float  texelsPerUnit;
    float  texelSpan;    // Szerokosc texela w pikselach ekranu (fwidth)
    float  microFade;    // Wygaszanie wylacznie najdrobniejszego szumu przy oddaleniu kamery
};

// Kwantyzuje pozycje World XZ do stale ostrej siatki texeli (domyslnie ~32 texele na szerokosc heksa)
RetroTexelGrid EvaluateDiscreteTexelGrid(float2 worldXZ, float hexRadius, float texelsPerHex)
{
    float hexWidth = max(0.001, 2.0 * hexRadius);
    float texelsPerUnit = max(1.0, texelsPerHex) / hexWidth;
    float2 texelCoords = worldXZ * texelsPerUnit;

    float2 dTex = fwidth(texelCoords);
    float texelSpan = max(dTex.x, dTex.y);

    RetroTexelGrid grid;
    grid.cell = floor(texelCoords);
    grid.pixelPos = (grid.cell + 0.5) / texelsPerUnit;
    grid.texelsPerUnit = texelsPerUnit;
    grid.texelSpan = texelSpan;
    // Siatka texeli zostaje ZAWSZE ostra; wygaszamy wylacznie pojedynczy micro-noise przy silnym oddaleniu
    grid.microFade = 1.0 - smoothstep(0.85, 1.85, texelSpan);
    return grid;
}

float2 SeedOffset2D(float seed)
{
    return float2(seed * 17.31 + 3.7, seed * 43.17 + 11.3);
}

// Izotropowy 2D Gradient Noise w zakresie [0, 1] bez kierunkowych smug
float GradientNoise2D(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);

    float2 g00 = normalize(Hash22(ip) * 2.0 - 1.0 + 0.0001);
    float2 g10 = normalize(Hash22(ip + float2(1.0, 0.0)) * 2.0 - 1.0 + 0.0001);
    float2 g01 = normalize(Hash22(ip + float2(0.0, 1.0)) * 2.0 - 1.0 + 0.0001);
    float2 g11 = normalize(Hash22(ip + float2(1.0, 1.0)) * 2.0 - 1.0 + 0.0001);

    float d00 = dot(g00, fp);
    float d10 = dot(g10, fp - float2(1.0, 0.0));
    float d01 = dot(g01, fp - float2(0.0, 1.0));
    float d11 = dot(g11, fp - float2(1.0, 1.0));

    float2 u = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);
    return saturate(lerp(lerp(d00, d10, u.x), lerp(d01, d11, u.x), u.y) * 0.7 + 0.5);
}

// Nieregularne skupiska pikseli (Pixel Clusters) bez oblych plam i bez koncentrycznych warstw
float PixelClusterField(float2 cell, float clusterSize, float2 seedOffset, out float clusterInterior)
{
    float safeSize = max(1.5, clusterSize);
    float2 p = (cell + seedOffset * 8.0) / safeSize;

    // Organiczne zaburzenie ksztaltu klastrów na skali pod-klastrowej + drobne poszarpanie krawedzi per texel
    float2 warp = float2(
        ValueNoise2D(p * 1.35 + float2(4.1, 13.7)),
        ValueNoise2D(p * 1.35 + float2(21.3, 6.9))
    ) - 0.5;
    float2 texelJitter = (Hash22(cell + seedOffset * 3.0) - 0.5) * 0.46;

    float2 warpedP = p + warp * 0.80 + (texelJitter / safeSize) * 1.4;
    float2 clusterCell = floor(warpedP);
    float2 clusterFrac = frac(warpedP) - 0.5;

    clusterInterior = saturate(1.0 - max(abs(clusterFrac.x), abs(clusterFrac.y)) * 2.0);
    return Hash21(clusterCell);
}

// Dyskretny wybor koloru z 6-stopniowej palety bez interpolacji Albedo
float3 PickPalette6(int idx, float3 c0, float3 c1, float3 c2, float3 c3, float3 c4, float3 c5)
{
    int i = clamp(idx, 0, 5);
    if (i == 0) return c0;
    if (i == 1) return c1;
    if (i == 2) return c2;
    if (i == 3) return c3;
    if (i == 4) return c4;
    return c5;
}

// Ostra, skwantyzowana do texeli maska granicy heksa (pointy-top)
float EvaluateDiscreteHexBorder(float2 localPosXZ, float2 pixelWorldXZ, float texelsPerUnit, float hexRadius, float borderWidth, float borderStrength, float noiseStrength)
{
    if (borderStrength <= 0.001) return 0.0;

    // Srodek aktualnego texela w ukladzie lokalnym heksa (zabezpiecza ciaglosc na skosnych krawedziach 60 deg)
    float2 texelStepLocal = (floor(localPosXZ * texelsPerUnit) + 0.5) / texelsPerUnit;
    float rIn = hexRadius * 0.8660254;
    float2 p = abs(texelStepLocal);
    float hexD = max(p.x, 0.5 * p.x + 0.8660254 * p.y);
    float distFromEdge = max(0.0, rIn - hexD);

    float borderNoise = (Hash21(floor(pixelWorldXZ * texelsPerUnit)) - 0.5) * 2.0;
    float effectiveWidth = max(borderWidth * (1.0 + borderNoise * noiseStrength), 0.72 / max(1.0, texelsPerUnit));

    return (distFromEdge <= effectiveWidth) ? borderStrength : 0.0;
}

#endif // TERRAIN_GROUND_COMMON_INCLUDED
