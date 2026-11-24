Shader "Custom/MistHLSL"
{
    // =======================================================================================
    // MIST V2 - RETRO CHAOS
    //
    // Gameplay (maska odkrywania, granica, wilderness) jest przeniesiony 1:1 z wersji V1.
    // Zmieniony jest WYLACZNIE wyglad: wzor mgly, paleta, retro stylizacja i blyski.
    //
    // _FogMask, _FogWorldBounds i _UnscaledTime sa globalne (ustawiane przez C#) i celowo
    // NIE wystepuja w Properties ani w UnityPerMaterial - lokalny Material nie moze ich
    // przeslonic.
    //
    // Kolory w Properties podajemy tak jak w Inspectorze (sRGB). Unity sam konwertuje je do
    // Linear w projekcie Linear, a w projekcie Gamma przekazuje bez zmian - dlatego V2 nie
    // wykonuje wlasnych konwersji sRGB<->Linear (brak podwojnej korekcji).
    // =======================================================================================
    Properties
    {
        [Header(Mist Pixel Grid and Shape)]
        _MistTexelDensity      ("Texel Density (per hex width)", Range(8.0, 64.0)) = 24.0
        _HexRadius             ("Hex Radius", Float) = 1.0
        _MistMacroScale        ("Macro Scale (mniejsze = wieksze masy)", Range(0.05, 1.5)) = 0.42
        _MistSwirlScale        ("Swirl Scale (klyby i wiry)", Range(0.1, 2.5)) = 0.85
        _MistWarpStrength      ("Domain Warp Strength", Range(0.0, 3.5)) = 1.75
        _MistDetailScale       ("Detail Scale (wieloskalowy dym)", Range(0.2, 4.0)) = 1.80
        _MistDetailStrength    ("Detail Strength", Range(0.0, 0.8)) = 0.30
        _MistQuantizeSteps     ("Quantize Steps (poziomy tonow)", Range(4.0, 24.0)) = 14.0
        _MistContrast          ("Field Contrast", Range(0.5, 2.5)) = 0.95
        _MistAnimationSpeed    ("Animation Speed", Range(0.0, 3.0)) = 0.85
        [Toggle(_MIST_LEGACY_LOOK)] _MistLegacyLook ("Legacy V1 Look (porownanie)", Float) = 0

        [Header(Discoverable Borderlands Palette sRGB)]
        _DiscColor0            ("0: Gleboki cien (#291B36)", Color) = (0.16078432, 0.10588235, 0.21176471, 1.0)
        _DiscColor1            ("1: Ciemny fiolet (#361C48)", Color) = (0.21176471, 0.10980392, 0.28235295, 1.0)
        _DiscColor2            ("2: Bazowy fiolet (#552B70)", Color) = (0.33333334, 0.16862746, 0.4392157, 1.0)
        _DiscColor3            ("3: Jasny fiolet (#683585)", Color) = (0.40784314, 0.20784314, 0.52156866, 1.0)
        _DiscColor4            ("4: Magiczny akcent (#7E439C)", Color) = (0.49411765, 0.2627451, 0.6117647, 1.0)
        _DiscoverableBrightness ("Discoverable Brightness (albedo)", Range(0.0, 3.0)) = 0.85
        _DiscoverableEmission   ("Discoverable Emission", Range(0.0, 3.0)) = 0.55

        [Header(Blocked Wilderness Temporary)]
        _BlockedColorDark      ("Blocked Color Dark",  Color) = (0.07843138, 0.0627451, 0.10980392, 1.0)
        _BlockedColorLight     ("Blocked Color Light", Color) = (0.18431373, 0.11372549, 0.23921569, 1.0)
        _BlockedBrightness     ("Blocked Brightness (albedo)", Range(0.0, 3.0)) = 0.62
        _BlockedEmission       ("Blocked Emission", Range(0.0, 3.0)) = 0.34

        [Header(Frontier Boundary)]
        _BoundaryWarpAmplitude ("Boundary Warp Amplitude", Range(0.0, 1.5)) = 0.65
        _BoundaryDetailStrength("Boundary Detail Strength", Range(0.0, 1.5)) = 0.55
        _BoundaryAnimationSpeed("Boundary Animation Speed", Range(0.0, 3.0)) = 1.0
        [PerRendererData] _LogicalWilderness ("Logical Wilderness (0=Borderlands, 1=Wilderness)", Float) = 0.0
        _DebugBoundaryMode     ("Debug Boundary (0:Off, 1:State, 2:Frontier, 3:Dist, 4:Final)", Range(0, 4)) = 0

        [Header(Mist Geometry and Lighting)]
        _MistSideDrop          ("Side Drop (zejscie bokow w dol)", Range(0.0, 5.0)) = 2.5
        _MistSideDarkening     ("Side Bottom Darkening", Range(0.0, 0.8)) = 0.30
        _MistAlpha             ("Mist Alpha (przezroczystosc)", Range(0.0, 1.0)) = 1.0
        _MistAlphaMaskSmoothness ("Alpha Mask Smoothness (brak kwadratowych plam)", Range(0.0, 1.0)) = 0.82
        _MistAlphaVariation    ("Alpha Local Variation (rozpraszanie dymu)", Range(0.0, 1.0)) = 0.72
        _AmbientBoost          ("Ambient Boost (Trilight GI)", Range(0.0, 1.0)) = 0.55

        [Header(Surface and Blending)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0.0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0.0

        [Header(Chaos Sparks)]
        [HDR] _SparkColor      ("Color", Color) = (0.85, 0.50, 0.95, 1.0)
        _SparkDensity          ("Density (odsetek komorek z iskra)", Range(0.0, 1.0)) = 0.12
        _SparkSize             ("Size (promien, jednostki swiata)", Range(0.01, 0.2)) = 0.05
        _SparkIntensity        ("Intensity", Range(0.0, 3.0)) = 0.65
        _SparkBlinkSpeed       ("Blink Speed", Range(0.1, 4.0)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        // ===================================================================================
        // 1. PARAMETRY MATERIALU (SRP Batcher) - ten sam CBUFFER w kazdym passie
        // ===================================================================================
        CBUFFER_START(UnityPerMaterial)
            float  _MistTexelDensity;
            float  _HexRadius;
            float  _MistMacroScale;
            float  _MistSwirlScale;
            float  _MistWarpStrength;
            float  _MistDetailScale;
            float  _MistDetailStrength;
            float  _MistQuantizeSteps;
            float  _MistContrast;
            float  _MistAnimationSpeed;
            float  _MistLegacyLook;
            float  _MistSideDrop;
            float  _MistSideDarkening;
            float  _MistAlpha;
            float  _MistAlphaMaskSmoothness;
            float  _MistAlphaVariation;
            float  _AmbientBoost;

            float  _BoundaryWarpAmplitude;
            float  _BoundaryDetailStrength;
            float  _BoundaryAnimationSpeed;
            float  _LogicalWilderness;
            float  _DebugBoundaryMode;

            float4 _DiscColor0;
            float4 _DiscColor1;
            float4 _DiscColor2;
            float4 _DiscColor3;
            float4 _DiscColor4;
            float  _DiscoverableBrightness;
            float  _DiscoverableEmission;

            float4 _BlockedColorDark;
            float4 _BlockedColorLight;
            float  _BlockedBrightness;
            float  _BlockedEmission;

            float4 _SparkColor;
            float  _SparkDensity;
            float  _SparkSize;
            float  _SparkIntensity;
            float  _SparkBlinkSpeed;
        CBUFFER_END

        // ===================================================================================
        // 2. GLOBALNE PARAMETRY USTAWIANE PRZEZ C# (FogOfWarManager, GlobalShaderTime)
        //    Poza CBUFFER materialu - bez zmian wzgledem V1.
        // ===================================================================================
        TEXTURE2D(_FogMask);
        SAMPLER(sampler_FogMask);
        float4 _FogWorldBounds;
        float  _FogTransitionWidthWorld;
        float  _UnscaledTime;

        // ===================================================================================
        // 3. STALE GRANICY MGLY (z FogFunctions.hlsl) - GAMEPLAY, NIE ZMIENIAC
        // ===================================================================================
        #define FOG_WARP_AMOUNT     3.0   // jak mocno faluja/wygryzaja sie krawedzie
        #define FOG_WARP_FREQ       0.07  // skala duzych "zatok" i "polwyspow"
        #define FOG_SOFTNESS        1.4   // promien rozmycia krawedzi
        #define FOG_DETAIL_STRENGTH 0.55  // drobne postrzepienie / wypustki przy samej krawedzi
        #define FOG_DETAIL_FREQ     0.55  // skala tego postrzepienia

        // ===================================================================================
        // 4. STALE WYGLADU MIST V2 (nie sa parametrami materialu - zmieniaj tutaj, jesli trzeba)
        // ===================================================================================
        #define MIST_FIELD_CONTRAST     2.1                    // kontrast makrostruktury (skupiska)
        #define MIST_MID_RATIO          2.17                   // czestotliwosc warstwy srodkowej wzgl. macro
        #define MIST_BLOCKED_CONTRAST   0.6                    // Blocked ma mniej kontrastowy wzor niz Discoverable
        #define MIST_PINK_TINT          float3(1.40, 1.20, 0.92) // rozowo-fioletowy akcent w jasnych skupiskach
        #define MIST_SPARK_CELL         0.9                    // rozmiar siatki iskier (jednostki swiata)
        #define MIST_SPARK_FIRE_CHANCE  0.6                    // szansa, ze iskra odpali w danym cyklu

        // ===================================================================================
        // 5. DETERMINISTYCZNE FUNKCJE HASHUJACE I SZUMY SHADER GRAPHA (Gradient Noise, Voronoi)
        //    Zachowane bez zmian - uzywa ich wersja Legacy oraz V2 (hash).
        // ===================================================================================
        void Mist_Hash_Tchou_2_1_uint(uint2 v, out uint o)
        {
            v.y ^= 1103515245U;
            v.x += v.y;
            v.x *= v.y;
            v.x ^= v.x >> 5u;
            v.x *= 0x27d4eb2du;
            o = v.x;
        }

        void Mist_Hash_Tchou_2_1_float(float2 i, out float o)
        {
            uint r;
            uint2 v = (uint2)(int2)round(i);
            Mist_Hash_Tchou_2_1_uint(v, r);
            o = (r >> 8) * (1.0 / float(0x00ffffff));
        }

        void Mist_Hash_Tchou_2_2_uint(uint2 v, out uint2 o)
        {
            v.y ^= 1103515245U;
            v.x += v.y;
            v.x *= v.y;
            v.x ^= v.x >> 5u;
            v.x *= 0x27d4eb2du;
            v.y ^= (v.x << 3u);
            o = v;
        }

        void Mist_Hash_Tchou_2_2_float(float2 i, out float2 o)
        {
            uint2 r;
            uint2 v = (uint2)(int2)round(i);
            Mist_Hash_Tchou_2_2_uint(v, r);
            o = (r >> 8) * (1.0 / float(0x00ffffff));
        }

        float2 Mist_TilingAndOffset(float2 uv, float2 tiling, float2 offset)
        {
            return uv * tiling + offset;
        }

        float Mist_Remap(float val, float2 inMinMax, float2 outMinMax)
        {
            return outMinMax.x + (val - inMinMax.x) * (outMinMax.y - outMinMax.x) / (inMinMax.y - inMinMax.x);
        }

        float2 Mist_GradientNoise_Deterministic_Dir(float2 p)
        {
            float x;
            Mist_Hash_Tchou_2_1_float(p, x);
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }

        float Mist_GradientNoise_Deterministic(float2 uv, float scale)
        {
            float2 p = uv * scale;
            float2 ip = floor(p);
            float2 fp = frac(p);
            float d00 = dot(Mist_GradientNoise_Deterministic_Dir(ip), fp);
            float d01 = dot(Mist_GradientNoise_Deterministic_Dir(ip + float2(0.0, 1.0)), fp - float2(0.0, 1.0));
            float d10 = dot(Mist_GradientNoise_Deterministic_Dir(ip + float2(1.0, 0.0)), fp - float2(1.0, 0.0));
            float d11 = dot(Mist_GradientNoise_Deterministic_Dir(ip + float2(1.0, 1.0)), fp - float2(1.0, 1.0));
            fp = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);
            return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
        }

        float2 Mist_Voronoi_RandomVector_Deterministic(float2 uv, float offset)
        {
            Mist_Hash_Tchou_2_2_float(uv, uv);
            return float2(sin(uv.y * offset), cos(uv.x * offset)) * 0.5 + 0.5;
        }

        void Mist_Voronoi_Deterministic(float2 uv, float angleOffset, float cellDensity, out float outDist, out float outCells)
        {
            float2 g = floor(uv * cellDensity);
            float2 f = frac(uv * cellDensity);
            float3 res = float3(8.0, 0.0, 0.0);
            outDist = 8.0;
            outCells = 0.0;

            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 lattice = float2(x, y);
                    float2 offset = Mist_Voronoi_RandomVector_Deterministic(lattice + g, angleOffset);
                    float d = distance(lattice + offset, f);
                    if (d < res.x)
                    {
                        res = float3(d, offset.x, offset.y);
                        outDist = res.x;
                        outCells = res.y;
                    }
                }
            }
        }

        // ===================================================================================
        // 6. LEGACY (V1) - oryginalne lancuchy wizualne, uzywane tylko gdy wlaczony
        //    jest przelacznik "Legacy V1 Look" (_MIST_LEGACY_LOOK). Sluza do porownania.
        // ===================================================================================
        bool Mist_IsGammaSpace()
        {
            #ifdef UNITY_COLORSPACE_GAMMA
                return true;
            #else
                return false;
            #endif
        }

        float EvaluateMistDualGradientNoise(float2 worldXZ, float unscaledTime)
        {
            // Warstwa 1: Tiling=(1,1), Offset = UnscaledTime * 0.1, Scale = 1.0
            float2 uv1 = Mist_TilingAndOffset(worldXZ, float2(1.0, 1.0), (unscaledTime * 0.1).xx);
            float noise1 = Mist_GradientNoise_Deterministic(uv1, 1.0);

            // Warstwa 2: Tiling=(1,1), Offset = UnscaledTime * -0.4, Scale = 0.5
            float2 uv2 = Mist_TilingAndOffset(worldXZ, float2(1.0, 1.0), (unscaledTime * -0.4).xx);
            float noise2 = Mist_GradientNoise_Deterministic(uv2, 0.5);

            return noise1 + noise2;
        }

        float3 EvaluatePurpleColor(float noiseSum)
        {
            float3 colA = Mist_IsGammaSpace()
                ? LinearToSRGB(float3(0.05364122, 0.0, 0.14117648))
                : float3(0.05364122, 0.0, 0.14117648);

            float3 colB = Mist_IsGammaSpace()
                ? LinearToSRGB(float3(0.12420164, 0.0, 0.25098041))
                : float3(0.12420164, 0.0, 0.25098041);

            // Lerp bez klamrowania T (zachowuje oryginalny kontrast)
            return lerp(colA, colB, noiseSum);
        }

        float3 EvaluateDarkColor(float noiseSum)
        {
            float3 colA = Mist_IsGammaSpace()
                ? float3(0.14455269, 0.0, 0.17924529)
                : SRGBToLinear(float3(0.14455269, 0.0, 0.17924529));

            float3 colB = Mist_IsGammaSpace()
                ? LinearToSRGB(float3(0.03839650, 0.00400498, 0.05660376))
                : float3(0.03839650, 0.00400498, 0.05660376);

            return lerp(colA, colB, noiseSum);
        }

        float3 EvaluateVoronoiFlashes(float2 worldXZ, float unscaledTime)
        {
            float sineVal = sin(unscaledTime * 0.02);
            float remappedSine = Mist_Remap(sineVal, float2(-1.0, 1.0), float2(0.0, 1.0));
            float timePulse = pow(max(0.0, remappedSine), 2.0);

            float voronoiDist;
            float voronoiCells;
            Mist_Voronoi_Deterministic(worldXZ, unscaledTime * 2.0, 5.0, voronoiDist, voronoiCells);

            float gateNoise = Mist_GradientNoise_Deterministic(worldXZ, 3.0);
            float gateStep = step(0.99, gateNoise);

            float gatedVoronoi = voronoiDist * gateStep;
            float voronoiPeak = pow(max(0.0, gatedVoronoi), 40.0);

            float flashFactor = timePulse * voronoiPeak;

            float3 flashColor = Mist_IsGammaSpace()
                ? LinearToSRGB(float3(2.41825628, 0.0, 1.30196655))
                : float3(2.41825628, 0.0, 1.30196655);

            return clamp(flashColor * flashFactor, 0.0, 0.5);
        }

        // ===================================================================================
        // 7. MIST V3 - LIVING PS1/PS2 SUPERNATURAL MIST
        //    Stabilna siatka texeli (32 / szer. heksa), animowany szum, brak rozciagania na bokach.
        // ===================================================================================

        float2 MistV2_GradientDir(float2 p)
        {
            float h;
            Mist_Hash_Tchou_2_1_float(p, h);
            float x = h * 2.0 - 1.0;
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }

        float MistV2_GradientNoise(float2 p)
        {
            float2 ip = floor(p);
            float2 fp = frac(p);
            float d00 = dot(MistV2_GradientDir(ip), fp);
            float d01 = dot(MistV2_GradientDir(ip + float2(0.0, 1.0)), fp - float2(0.0, 1.0));
            float d10 = dot(MistV2_GradientDir(ip + float2(1.0, 0.0)), fp - float2(1.0, 0.0));
            float d11 = dot(MistV2_GradientDir(ip + float2(1.0, 1.0)), fp - float2(1.0, 1.0));
            fp = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);
            return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
        }

        // Wyznacza dyskretna wspolrzedna srodka texela w przestrzeni swiata (wyrównana z terenem)
        // oraz niweluje rozciaganie na pionowych sciankach bocznych mgly.
        float2 EvaluateMistPixelCoord(float2 worldXZ, float3 normalWS, float normalOSY, float sideDepth, out float detailFade, out float2 smoothSampleSpace)
        {
            float wallWeight = 1.0 - saturate((normalOSY - 0.35) / 0.30);
            float2 sideDir = normalize(normalWS.xz + float2(1e-5, 0.0));
            float2 tangentDir = float2(-sideDir.y, sideDir.x);

            // Przy gornej krawedzi (sideDepth == 0) wspolrzedna jest identyczna z worldXZ -> pelna ciaglosc.
            // Schodzac w dol po pionowej scianie przesuwa sie 1:1 z wysokoscia, dajac kwadratowe texele!
            smoothSampleSpace = worldXZ
                - sideDir * (sideDepth * wallWeight)
                + tangentDir * (sideDepth * wallWeight * 0.25);

            float texelsPerUnit = max(1.0, _MistTexelDensity) / max(0.001, 2.0 * _HexRadius);
            float2 texelCoords = smoothSampleSpace * texelsPerUnit;

            float2 dTex = fwidth(texelCoords);
            float texelSpan = max(dTex.x, dTex.y);
            detailFade = 1.0 - smoothstep(0.95, 2.15, texelSpan);

            return (floor(texelCoords) + 0.5) / texelsPerUnit;
        }

        // Miekka maska gestosci dla przezroczystych wariantow (Surveying / Ground):
        // ogranicza kwadratowe plamy przy zachowaniu sredniego krycia i tego samego ruchu co wiry.
        float EvaluateMistSmoothAlphaMask(float2 smoothPos, float time)
        {
            float t = time * _MistAnimationSpeed;
            float2 driftMacro = t * float2(-0.032,  0.041);
            float2 driftSwirl = t * float2( 0.062, -0.036);

            float macroSmooth = MistV2_GradientNoise((smoothPos + driftMacro) * _MistMacroScale);
            float swirlSmooth = MistV2_GradientNoise((smoothPos + driftSwirl) * (_MistSwirlScale * 0.9) + float2(17.4, 42.8));

            float smoothField = macroSmooth * 0.62 + swirlSmooth * 0.38;
            // Symetryczne rozciagniecie wokol 0.5 -> srednia wartosc pozostaje ~0.5 (zachowane srednie krycie)
            return saturate(0.5 + (smoothField - 0.5) * 1.85);
        }

        // Pole gestosci mgly [0..1] wspolne dla koloru i maski gestosci.
        // Siatka texeli stoi w miejscu; animuja sie wylacznie warstwy noise i wiry.
        float EvaluateMistDensityField(float2 pixelPos, float time, float detailFade, out float swirlSignal)
        {
            float t = time * _MistAnimationSpeed;

            // Warstwy organicznego ruchu o roznych kierunkach i niskich predkosciach
            float2 driftWarp  = t * float2( 0.048,  0.022);
            float2 driftMacro = t * float2(-0.032,  0.041);
            float2 driftSwirl = t * float2( 0.062, -0.036);
            float2 driftGas1  = t * float2(-0.052, -0.029);
            float2 driftGas2  = t * float2( 0.038,  0.047);

            // 1. Domain Warping - organiczne zakrzywienia chmur mgly
            float warpX = MistV2_GradientNoise((pixelPos + driftWarp) * (_MistMacroScale * 1.15) + float2(7.3, 19.1)) - 0.5;
            float warpY = MistV2_GradientNoise((pixelPos - driftWarp * 0.8) * (_MistMacroScale * 1.15) + float2(31.7, 5.9)) - 0.5;
            float2 warpVec = float2(warpX, warpY) * _MistWarpStrength;

            // 2. Organiczne chmuropodobne masy (Macro Noise)
            float macroMass = MistV2_GradientNoise((pixelPos + driftMacro + warpVec) * _MistMacroScale);

            // 3. Mniejsze kleby i wiry w obrebie skupisk
            float2 swirlPos = (pixelPos + driftSwirl + warpVec * 1.25) * _MistSwirlScale + float2(17.4, 42.8);
            swirlSignal = MistV2_GradientNoise(swirlPos);

            // 4. Wieloskalowy Detail Noise (rozbija jednolite plamy na gazowa strukture)
            float detail1 = MistV2_GradientNoise((pixelPos + driftGas1 + warpVec * 0.65) * _MistDetailScale + float2(63.1, 11.7));
            float detail2 = MistV2_GradientNoise((pixelPos + driftGas2 + warpVec * 0.35) * (_MistDetailScale * 2.15) + float2(91.4, 28.3));
            float multiDetail = detail1 * 0.65 + detail2 * 0.35;

            float baseField = macroMass * 0.54 + swirlSignal * 0.46;
            float contrasted = saturate(0.5 + (baseField - 0.5) * _MistContrast);
            float withGas = contrasted + (multiDetail - 0.5) * _MistDetailStrength * lerp(0.35, 1.0, detailFade);

            return saturate(withGas);
        }

        float3 EvaluateMistDiscRamp5(float t)
        {
            if (t < 0.27) return lerp(_DiscColor0.rgb, _DiscColor1.rgb, saturate(t / 0.27));
            if (t < 0.54) return lerp(_DiscColor1.rgb, _DiscColor2.rgb, saturate((t - 0.27) / 0.27));
            if (t < 0.80) return lerp(_DiscColor2.rgb, _DiscColor3.rgb, saturate((t - 0.54) / 0.26));
            return lerp(_DiscColor3.rgb, _DiscColor4.rgb, saturate((t - 0.80) / 0.20));
        }

        // Ewaluacja palety Borderlands (Discoverable) z 12-16 poziomami kwantyzacji:
        // ostre granice texeli, miekka gradacja gazu (zero poziomic), ~20-30% jasniejszych skupisk.
        void EvaluateDiscoverableMist(float density, float swirlSignal, float detailFade, out float3 outColor, out float outEmissionFactor)
        {
            float shaped = saturate(density + (swirlSignal - 0.5) * 0.14);

            // 12-16 dyskretnych poziomow tonalnych (miekka gazowa substancja bez plaskich poziomic)
            float steps = max(4.0, _MistQuantizeSteps);
            float qTone = floor(shaped * (steps - 1.0) + 0.5) / (steps - 1.0);

            outColor = EvaluateMistDiscRamp5(qTone);
            outEmissionFactor = lerp(0.30, 1.25, qTone * qTone);
        }

        // Tymczasowa, uporzadkowana ewaluacja Blocked (Wilderness) na wspolnej siatce texeli przed kolejnym etapem
        float3 EvaluateBlockedMistTemp(float density)
        {
            float mB = saturate(0.5 + (density - 0.5) * 0.55);
            float qB = floor(mB * 4.0 + 0.5) / 4.0;
            return lerp(_BlockedColorDark.rgb, _BlockedColorLight.rgb, qB);
        }

        // ===================================================================================
        // 8. CHAOS SPARKS
        //    Siatka komorek w przestrzeni swiata, max. jedna iskra na komorke. Kazda komorka ma
        //    deterministyczna (hash ID komorki) pozycje, okres, faze i jasnosc; kazdy cykl ma
        //    wlasny hash (czy odpala, kiedy, jak jasno). Zero losowosci per-klatke.
        //    Koszt: ~6 hashy PCG + kilka ALU, i to tylko w Blocked (dynamiczna galaz).
        // ===================================================================================
        uint Mist_PCG(uint v)
        {
            uint state = v * 747796405u + 2891336453u;
            uint word  = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
            return (word >> 22u) ^ word;
        }

        float Mist_U01(uint h)
        {
            return float(h >> 8) * (1.0 / 16777216.0);
        }

        // pxWidth = rozmiar piksela w jednostkach swiata (fwidth) - iskra nigdy nie jest
        // mniejsza niz ~1.5 px, wiec nie migocze podczas ruchu kamery.
        float3 EvaluateChaosSparks(float2 worldXZ, float time, float pxWidth)
        {
            float2 cp    = worldXZ / MIST_SPARK_CELL;
            float2 cell  = floor(cp);
            float2 cellFrac = cp - cell;

            uint2 uid = (uint2)(int2)cell;
            uint h0 = Mist_PCG(uid.x + Mist_PCG(uid.y));
            uint h1 = Mist_PCG(h0 ^ 0xA511E9B3u);
            uint h2 = Mist_PCG(h1);
            uint h3 = Mist_PCG(h2);
            uint h4 = Mist_PCG(h3);
            uint h5 = Mist_PCG(h4);

            float rHost   = Mist_U01(h0);
            float rJx     = Mist_U01(h1);
            float rJy     = Mist_U01(h2);
            float rPeriod = Mist_U01(h3);
            float rPhase  = Mist_U01(h4);
            float rLife   = Mist_U01(h5);

            // Czy ta komorka w ogole gosci iskre (monotonicznie z _SparkDensity)
            float host = (rHost < _SparkDensity) ? 1.0 : 0.0;

            // Pozycja iskry: z dala od krawedzi komorki, wiec nie trzeba sprawdzac sasiadow
            float2 center = 0.25 + 0.5 * float2(rJx, rJy);

            // Czas: niezalezny okres (6-20 s) i faza dla kazdej komorki
            float period   = lerp(6.0, 20.0, rPeriod);
            float cyc      = time * _SparkBlinkSpeed / period + rPhase;
            float cycIdx   = floor(cyc);
            float cycFrac  = cyc - cycIdx;

            // Hash per cykl: czy odpala, kiedy w cyklu, jak jasno
            uint hc  = Mist_PCG(h5 + (uint)cycIdx * 2654435761u);
            uint hc2 = Mist_PCG(hc);
            uint hc3 = Mist_PCG(hc2);
            float rFire   = Mist_U01(hc);
            float rStart  = Mist_U01(hc2);
            float rBright = Mist_U01(hc3);

            float fire  = (rFire < MIST_SPARK_FIRE_CHANCE) ? 1.0 : 0.0;
            float life  = lerp(0.03, 0.12, rLife);            // ulamek okresu (~0.2-2.4 s)
            float start = rStart * (1.0 - life);
            float u     = saturate((cycFrac - start) / life); // poza oknem u = 0 lub 1 => env = 0

            // Szybkie pojawienie sie, dluzsze wygasanie
            float env = smoothstep(0.0, 0.15, u) * (1.0 - smoothstep(0.35, 1.0, u));
            float brightness = lerp(0.6, 1.0, rBright);       // niewielkie roznice jasnosci

            // Ksztalt: maly, ostry punkt. Retro tu zawsze kwadratowy texel zgodny z estetyka PS1/PS2.
            float2 dv = abs(cellFrac - center) * MIST_SPARK_CELL;
            float  d  = max(dv.x, dv.y);

            float rr    = min(max(_SparkSize, pxWidth * 0.75), MIST_SPARK_CELL * 0.24);
            float soft  = max(pxWidth * 0.75, rr * 0.2);
            float shape = saturate((rr - d) / max(soft, 1e-4));

            // Delikatna zmiennosc barwy miedzy rozem a fioletem
            float3 col = lerp(_SparkColor.rgb, _SparkColor.rgb * float3(0.8, 0.9, 1.2), rPhase);

            return col * (host * fire * env * brightness * shape * _SparkIntensity);
        }

        // ===================================================================================
        // 9. GRANICA MGLY I WILDERNESS (z FogFunctions.hlsl) - GAMEPLAY, LOGIKA 1:1 Z V1
        //    Jedyna zmiana: funkcja zwraca tylko wspolczynnik wilderness. Mieszanie kolorow
        //    zostalo przeniesione do fragment shadera (patrz MistFrag).
        // ===================================================================================
        float FogHash2D(float2 p)
        {
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
            return sum / norm;
        }

        float4 SampleFogMaskRaw(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_FogMask, sampler_FogMask, saturate(uv));
        }

        float FogSampleMask(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_FogMask, sampler_FogMask, saturate(uv)).r;
        }

        float CalculateFogWilderness(
            float3 WorldPos,
            float4 WorldBounds,
            out float outRawDistMask,
            out float outFrontierLine,
            out float outLogicalMask
        )
        {
            // Etap 1: Odczyt rzeczywistego stanu fragmentu (PerRendererData z MaterialPropertyBlock)
            float logicalWilderness = saturate(_LogicalWilderness);
            outLogicalMask = logicalWilderness;

            float4 bounds = (length(WorldBounds.zw) > 0.001) ? WorldBounds : _FogWorldBounds;
            float2 size   = max(bounds.zw, float2(0.0001, 0.0001));
            float2 p      = WorldPos.xz;
            float2 rawUV  = (p - bounds.xy) / size;

            float4 rawSample = SampleFogMaskRaw(rawUV);
            outRawDistMask  = rawSample.r;
            outFrontierLine = rawSample.b;

            // Fragment na chunku Wilderness nigdy nie moze otrzymac jasnego wariantu Discoverable
            if (logicalWilderness >= 0.999)
            {
                return 1.0;
            }

            // Etap 2: Organiczne, animowane przejscie wylacznie wewnatrz Borderlands na bazie maski odleglosci od Frontier
            float transitionWidth = _FogTransitionWidthWorld > 0.01 ? _FogTransitionWidthWorld : 1.9;
            float warpAmp = transitionWidth * _BoundaryWarpAmplitude;

            float time = _Time.y * _BoundaryAnimationSpeed;
            float2 drift1 = float2(time * 0.035, time * 0.02);
            float2 drift2 = float2(-time * 0.02, time * 0.035);

            // 1. DOMAIN WARPING (proporcjonalny do szerokosci pasa przejsciowego)
            float2 warp;
            warp.x = FogFBM(p * FOG_WARP_FREQ + drift1);
            warp.y = FogFBM(p * FOG_WARP_FREQ + drift2 + float2(37.2, 11.7));
            warp = (warp - 0.5) * 2.0 * warpAmp;

            float2 warpFine;
            warpFine.x = FogNoise2D(p * 0.9 + drift2 * 2.0);
            warpFine.y = FogNoise2D(p * 0.9 - drift1 * 2.0 + float2(8.3, 21.9));
            warp += (warpFine - 0.5) * (0.45 * warpAmp);

            // Ograniczamy deformacje do strefy blisko granicy Frontier
            float nearFrontier = smoothstep(0.0, 0.25, rawSample.r + 0.12);
            float2 uv = (p + warp * nearFrontier - bounds.xy) / size;

            // 2. MIEKKA, NIEJEDNOLITA KRAWEDZ - 5 probek maski odleglosci
            float softnessWorld = min(FOG_SOFTNESS, transitionWidth * 0.55);
            float  r  = softnessWorld * (0.4 + FogNoise2D(p * 0.3 + drift1)) / size.x;
            float2 ro = float2(r, r * size.x / size.y);

            float m  = FogSampleMask(uv) * 0.36;
            m += FogSampleMask(uv + ro * float2( 1.0,  0.6)) * 0.16;
            m += FogSampleMask(uv + ro * float2(-0.6,  1.0)) * 0.16;
            m += FogSampleMask(uv + ro * float2(-1.0, -0.6)) * 0.16;
            m += FogSampleMask(uv + ro * float2( 0.6, -1.0)) * 0.16;

            // 3. POSTRZEPIENIE - szum dziala tylko w pasie przejscia (m ~ 0.5)
            float detail = FogFBM(p * FOG_DETAIL_FREQ + drift2 * 2.0 + float2(15.7, 39.2));
            float band   = saturate(4.0 * m * (1.0 - m));
            float dynamicMask = saturate(m + (detail - 0.5) * 2.0 * _BoundaryDetailStrength * band);

            float dynamicWilderness = smoothstep(0.18, 0.85, dynamicMask);

            // Plynne domkniecie na samej krawedzi z Wilderness do wariantu Blocked
            float edgeSeal = smoothstep(0.72, 0.97, rawSample.r);
            dynamicWilderness = max(dynamicWilderness, edgeSeal);

            // Etap 3: Twarde ograniczenie do rzeczywistego stanu logicznego
            return max(logicalWilderness, dynamicWilderness);
        }
        ENDHLSL

        // ===================================================================================
        // PASS 1: UniversalForward (URP Lit, Opaque, Double-Sided, No Shadows)
        // ===================================================================================
        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MistVert
            #pragma fragment MistFrag

            // Przelacznik porownania z poprzednia wersja (V1)
            #pragma shader_feature_local_fragment _MIST_LEGACY_LOOK

            // Brak odbierania cieni (zgodnie z m_ReceiveShadows: false w Mist.shadergraph)
            #define _RECEIVE_SHADOWS_OFF 1

            // Slowa kluczowe URP Forward / Forward+
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS       : POSITION;
                float3 normalOS         : NORMAL;
                float4 tangentOS        : TANGENT;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS               : SV_POSITION;
                float3 positionWS               : TEXCOORD0;
                float3 normalWS                 : TEXCOORD1;
                half4  fogFactorAndVertexLight  : TEXCOORD2;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 3);
                float  normalOSY                : TEXCOORD4;
                float  sideDepth                : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings MistVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Spod heksa (normalOS.y < -0.5) nigdy nie jest renderowany
                if (input.normalOS.y < -0.5)
                {
                    output.positionCS = float4(0.0, 0.0, 0.0, 0.0);
                    return output;
                }

                float3 posOS = input.positionOS.xyz;
                float sideDepth = 0.0;
                if (input.normalOS.y < 0.5)
                {
                    if (posOS.y < -0.05)
                    {
                        posOS.y -= _MistSideDrop;
                    }
                    else
                    {
                        posOS.y -= 0.002;
                    }
                    sideDepth = max(0.0, -posOS.y);
                }
                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);

                float3 normOS = input.normalOS;
                if (normOS.y < 0.5)
                {
                    // Lagodne pochylenie normalnej ku gorze, aby boczne sciany mgly
                    // nie byly oswietlane jak bialy mur, lecz mialy ten sam odcien co gora mgly
                    normOS = normalize(float3(normOS.x * 0.25, 0.75, normOS.z * 0.25));
                }
                VertexNormalInputs normalInput = GetVertexNormalInputs(normOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.normalOSY = input.normalOS.y;
                output.sideDepth = sideDepth;

                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(normalInput.normalWS, output.vertexSH);

                return output;
            }

            half4 MistFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);



                float2 worldXZ = input.positionWS.xz;

                // 1. GAMEPLAY / GRANICA: 3-etapowe CalculateFogWilderness
                float rawDistMask;
                float frontierLine;
                float logicalMask;
                float wilderness = CalculateFogWilderness(
                    input.positionWS,
                    _FogWorldBounds,
                    rawDistMask,
                    frontierLine,
                    logicalMask
                );

                // Tryby debugowania granicy (_DebugBoundaryMode)
                int debugBoundary = (int)(_DebugBoundaryMode + 0.5);
                if (debugBoundary == 1)
                {
                    float3 stateCol = lerp(float3(0.15, 0.78, 0.28), float3(0.85, 0.18, 0.22), logicalMask);
                    return half4(stateCol, 1.0);
                }
                else if (debugBoundary == 2)
                {
                    float3 baseCol = lerp(float3(0.12, 0.42, 0.22), float3(0.32, 0.10, 0.14), logicalMask);
                    float3 fCol = lerp(baseCol, float3(1.0, 0.90, 0.15), step(0.35, frontierLine));
                    return half4(fCol, 1.0);
                }
                else if (debugBoundary == 3)
                {
                    return half4(rawDistMask, rawDistMask, rawDistMask, 1.0);
                }
                else if (debugBoundary == 4)
                {
                    return half4(wilderness, wilderness, wilderness, 1.0);
                }

                // 2. WYGLAD: kolor bazowy (albedo) i emisja sa teraz niezalezne
                float3 albedoColor;
                float3 emissionColor;
                float  mist = 0.5;
                float2 smoothAlphaCoord = worldXZ;

                #if defined(_MIST_LEGACY_LOOK)
                    // --- Legacy V1: identyczny wynik jak poprzedni shader ---
                    float  noiseSum      = EvaluateMistDualGradientNoise(worldXZ, _UnscaledTime);
                    float3 purpleColor   = EvaluatePurpleColor(noiseSum);
                    float3 darkColor     = EvaluateDarkColor(noiseSum);
                    float3 voronoiFlashes = EvaluateVoronoiFlashes(worldXZ, _UnscaledTime);

                    float3 fogFinalColor = lerp(purpleColor, darkColor + voronoiFlashes * wilderness, wilderness);
                    albedoColor   = fogFinalColor;
                    emissionColor = fogFinalColor;
                #else
                    // --- Mist V3: Living PS1/PS2 Mist (Borderlands / Discoverable standard) ---
                    float3 normWS = NormalizeNormalPerPixel(input.normalWS);
                    float detailFade;
                    float2 pixelPos = EvaluateMistPixelCoord(worldXZ, normWS, input.normalOSY, input.sideDepth, detailFade, smoothAlphaCoord);

                    float swirlSignal;
                    float density = EvaluateMistDensityField(pixelPos, _UnscaledTime, detailFade, swirlSignal);
                    mist = density;

                    float3 discoverableColor;
                    float  discEmissionFactor;
                    EvaluateDiscoverableMist(density, swirlSignal, detailFade, discoverableColor, discEmissionFactor);

                    float3 blockedColor = EvaluateBlockedMistTemp(density);

                    // Delikatne przyciemnienie podstawy pionowych scianek bocznych dla glebi
                    float sideDarken = 1.0 - saturate(input.sideDepth / max(0.01, _MistSideDrop)) * _MistSideDarkening;
                    discoverableColor *= sideDarken;
                    blockedColor *= sideDarken;

                    albedoColor   = lerp(discoverableColor * _DiscoverableBrightness,
                                         blockedColor      * _BlockedBrightness, wilderness);
                    emissionColor = lerp(discoverableColor * (_DiscoverableEmission * discEmissionFactor),
                                         blockedColor      * _BlockedEmission, wilderness);

                    // Chaos Sparks: tylko Blocked, plynnie wygaszane na granicy stanow.
                    float2 fw = fwidth(worldXZ);
                    float pxWidth = max(fw.x, fw.y);
                    float sparkGate = smoothstep(0.55, 0.95, wilderness);
                    [branch]
                    if (sparkGate > 0.0)
                    {
                        emissionColor += EvaluateChaosSparks(worldXZ, _UnscaledTime, pxWidth) * sparkGate;
                    }
                #endif

                // 3. Konfiguracja InputData (real-time Trilight z DayNightCycle + fallback)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;

                half3 trilightGI = lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb, saturate(inputData.normalWS.y));
                trilightGI = lerp(trilightGI, unity_AmbientGround.rgb, saturate(-inputData.normalWS.y));
                half trilightSum = trilightGI.r + trilightGI.g + trilightGI.b;
                half3 baseAmbient = trilightSum > 0.0001 ? trilightGI : SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                half3 ambientGI = baseAmbient * (1.0 + _AmbientBoost * 0.22);
                half3 ambientFloor = half3(0.008, 0.010, 0.015) * (1.0 + _AmbientBoost * 0.35);
                inputData.bakedGI = max(ambientGI, ambientFloor);

                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo              = albedoColor;
                surfaceData.metallic            = 0.0;
                surfaceData.specular            = half3(0.0, 0.0, 0.0);
                surfaceData.smoothness          = 0.0;
                surfaceData.normalTS            = half3(0.0, 0.0, 1.0);
                surfaceData.emission            = emissionColor;
                surfaceData.occlusion           = 1.0;
                float finalAlpha = _MistAlpha;
                if (_MistAlpha < 0.999)
                {
                    // Lagodne przejscie maski przezroczystosci (ogranicza kwadratowe plamy przy zachowaniu sredniego krycia)
                    float smoothAlphaMask = EvaluateMistSmoothAlphaMask(smoothAlphaCoord, _UnscaledTime);
                    float blendedMask = lerp(mist, smoothAlphaMask, saturate(_MistAlphaMaskSmoothness));
                    float alphaMultiplier = lerp(1.0 - _MistAlphaVariation, 1.0 + _MistAlphaVariation, blendedMask);
                    finalAlpha = saturate(_MistAlpha * alphaMultiplier);
                }
                surfaceData.alpha               = finalAlpha;
                surfaceData.clearCoatMask       = 0.0;
                surfaceData.clearCoatSmoothness = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = surfaceData.alpha;
                return color;
            }
            ENDHLSL
        }

        // ===================================================================================
        // PASS 2: DepthOnly (Zapis glebokosci URP, brak ShadowCaster bo m_CastShadows = false)
        // ===================================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                if (input.normalOS.y < -0.5)
                {
                    output.positionCS = float4(0.0, 0.0, 0.0, 0.0);
                    return output;
                }

                float3 posOS = input.positionOS.xyz;
                if (input.normalOS.y < 0.5)
                {
                    if (posOS.y < -0.05)
                    {
                        posOS.y -= _MistSideDrop;
                    }
                    else
                    {
                        posOS.y -= 0.002;
                    }
                }

                output.positionCS = TransformObjectToHClip(posOS);
                return output;
            }

            half4 DepthOnlyFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        // ===================================================================================
        // PASS 3: DepthNormals (Wymagane przez SSAO w PC_Renderer)
        // ===================================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                if (input.normalOS.y < -0.5)
                {
                    output.positionCS = float4(0.0, 0.0, 0.0, 0.0);
                    return output;
                }

                float3 posOS = input.positionOS.xyz;
                if (input.normalOS.y < 0.5)
                {
                    if (posOS.y < -0.05)
                    {
                        posOS.y -= _MistSideDrop;
                    }
                    else
                    {
                        posOS.y -= 0.002;
                    }
                }

                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);
                output.positionCS = vertexInput.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(input.normalWS);
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
