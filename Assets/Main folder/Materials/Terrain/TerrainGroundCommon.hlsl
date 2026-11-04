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
half4 CalculateTerrainLitPBR(
    float3 positionWS,
    float3 normalWS,
    float4 positionCS,
    float fogFactor,
    float3 albedo,
    float smoothness,
    float ambientBoost)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalize(normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), fogFactor);

    // Podloga ambientowa (Ambient Floor) wzbogacona o _AmbientBoost:
    // Zapewnia prawidlowy odbior chlodnego zmierzchu i nocy bez czarnych plam
    half3 ambientGI = SampleSH(inputData.normalWS);
    half3 ambientFloor = half3(0.040, 0.045, 0.042) * (1.0 + ambientBoost * 0.85);
    inputData.bakedGI = max(ambientGI, ambientFloor);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.metallic = 0.0;
    surfaceData.specular = half3(0.04, 0.04, 0.04);
    surfaceData.smoothness = smoothness;
    surfaceData.normalTS = half3(0, 0, 1);
    surfaceData.emission = half3(0, 0, 0);
    surfaceData.occlusion = 1.0;
    surfaceData.alpha = 1.0;

    half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
    finalColor.rgb = MixFog(finalColor.rgb, inputData.fogCoord);
    return finalColor;
}

#endif // TERRAIN_GROUND_COMMON_INCLUDED
