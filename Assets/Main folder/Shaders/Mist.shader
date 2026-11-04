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
        [Header(Mist Pattern)]
        _MistMacroScale        ("Macro Scale (wieksza = drobniejsze masy)",  Range(0.05, 1.5)) = 0.28
        _MistDetailScale       ("Detail Scale",                              Range(0.2, 4.0))  = 1.1
        _MistDetailStrength    ("Detail Strength",                           Range(0.0, 1.0))  = 0.22
        _MistTexelDensity      ("Texel Density (teksle / jednostke swiata)", Range(0.5, 16.0)) = 4.0
        [IntRange] _MistQuantizeSteps ("Quantize Steps (poziomy tonow)",     Range(3, 8))      = 5
        _MistRetroStrength     ("Retro Strength (0 = gladko)",               Range(0.0, 1.0))  = 0.65
        _MistAnimationSpeed    ("Animation Speed (tylko wzor)",              Range(0.0, 3.0))  = 1.0
        [Toggle(_MIST_LEGACY_LOOK)] _MistLegacyLook ("Legacy V1 Look (porownanie)", Float)     = 0

        [Header(Discoverable Mist)]
        _DiscoverableColorDark  ("Color Dark",  Color) = (0.293, 0.039, 0.461, 1)
        _DiscoverableColorLight ("Color Light", Color) = (0.517, 0.190, 0.735, 1)
        _DiscoverableBrightness ("Brightness (albedo)", Range(0.0, 3.0)) = 1.0
        _DiscoverableEmission   ("Emission",            Range(0.0, 3.0)) = 1.0

        [Header(Blocked Mist)]
        _BlockedColorDark  ("Color Dark",  Color) = (0.100, 0.051, 0.235, 1)
        _BlockedColorLight ("Color Light", Color) = (0.272, 0.078, 0.349, 1)
        _BlockedBrightness ("Brightness (albedo)", Range(0.0, 3.0)) = 1.0
        _BlockedEmission   ("Emission",            Range(0.0, 3.0)) = 1.0

        [Header(Chaos Sparks)]
        [HDR] _SparkColor ("Color", Color) = (1.0, 0.537, 0.931, 1)
        _SparkDensity    ("Density (odsetek komorek z iskra)", Range(0.0, 1.0))  = 0.18
        _SparkSize       ("Size (promien, jednostki swiata)",  Range(0.01, 0.2)) = 0.05
        _SparkIntensity  ("Intensity",                         Range(0.0, 3.0))  = 0.8
        _SparkBlinkSpeed ("Blink Speed",                       Range(0.1, 4.0))  = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
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
            float  _MistMacroScale;
            float  _MistDetailScale;
            float  _MistDetailStrength;
            float  _MistTexelDensity;
            float  _MistQuantizeSteps;
            float  _MistRetroStrength;
            float  _MistAnimationSpeed;
            float  _MistLegacyLook;

            float4 _DiscoverableColorDark;
            float4 _DiscoverableColorLight;
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
        // 7. MIST V2 - MACRO MIST + RETRO DETAIL
        // ===================================================================================

        // Gradient noise V2: ta sama konstrukcja co w V1, ale kierunki gradientow sa liczone
        // z hasha przeskalowanego do [-1,1] (jak w oryginalnym nodzie Unity). W V1 hash [0,1]
        // dawal kierunki tylko w dwoch cwiartkach, czyli diagonalne smugi (anizotropia).
        float2 MistV2_GradientDir(float2 p)
        {
            float h;
            Mist_Hash_Tchou_2_1_float(p, h);
            float x = h * 2.0 - 1.0;
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }

        // p jest juz przeskalowane. Zwraca ~[0,1], srednia ~0.5.
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

        // Miekka kwantyzacja: N tonow, ale przejscia miedzy poziomami sa wygladzone
        // (plateau ~60% pasa, przejscie ~40%), zeby nie powstawaly ostre, plaskie pasy.
        float MistV2_SoftQuantize(float v, float steps)
        {
            float levels = max(steps - 1.0, 1.0);
            float x  = v * levels;
            float fl = floor(x);
            float fr = x - fl;
            return (fl + smoothstep(0.3, 0.7, fr)) / levels;
        }

        // Pole wizualne Mist w [0,1]. Wspolne dla Discoverable i Blocked (jedno obliczenie).
        // detailNoise (nieskwantyzowany) sluzy tylko do akcentow koloru.
        // Koszt: 3 gradient-noise (+ 4 sin/cos dla nieregularnego dryfu). Zadnych FBM.
        float EvaluateMistField(float2 worldXZ, float time, out float detailNoise)
        {
            float retro = saturate(_MistRetroStrength);

            // --- A. Pikselizacja w przestrzeni swiata (stabilna: zalezy tylko od worldXZ) ---
            // Siatka tekseli jest nieruchoma w swiecie; wzor plynie "przez" tekstle.
            // Probkujemy srodek tekselu. Retro < 1 plynnie wraca do gladkiego wzoru.
            float  density     = max(_MistTexelDensity, 0.01);
            float2 texelCenter = (floor(worldXZ * density) + 0.5) / density;
            float2 pos         = lerp(worldXZ, texelCenter, retro);

            // --- B. Ruch: powolny dryf + wolne, niewspolbiezne wahania (brak widocznej petli) ---
            // Kazda warstwa ma wlasny kierunek, predkosc i fazy wahan, wiec warstwy powoli
            // "przesuwaja sie wzgledem siebie". Predkosci ~0.03-0.09 jednostki swiata / s.
            float t = time * _MistAnimationSpeed;
            float2 drift1 = t * float2( 0.060,  0.025) + float2(sin(t * 0.071 + 1.3), cos(t * 0.053      )) * 0.9;
            float2 drift2 = t * float2(-0.040,  0.052) + float2(cos(t * 0.083 + 0.4), sin(t * 0.061 + 2.1)) * 0.7;
            float2 driftD = t * float2( 0.026, -0.034) + float2(sin(t * 0.097 + 4.2), cos(t * 0.067 + 0.9)) * 0.5;

            // --- C. Warstwa MACRO: duze masy + warstwa srednia zwarpowana przez macro ---
            float macroS = _MistMacroScale;
            float n1 = MistV2_GradientNoise((pos + drift1) * macroS);

            float2 pm = (pos + drift2) * (macroS * MIST_MID_RATIO) + (n1 - 0.5) * float2(1.9, -1.4) + 17.3;
            float  n2 = MistV2_GradientNoise(pm);

            // --- D. Warstwa DETAIL: jedna probka, zwarpowana przez n2 (zadnych drobnych plam) ---
            float2 pd = (pos + driftD) * _MistDetailScale + (n2 - 0.5) * float2(-1.2, 1.6) + 41.7;
            detailNoise = MistV2_GradientNoise(pd);

            float baseN = n1 * 0.64 + n2 * 0.36;
            float field = (baseN - 0.5) * MIST_FIELD_CONTRAST + 0.5
                        + (detailNoise - 0.5) * _MistDetailStrength;
            field = saturate(field);

            // --- E. Kwantyzacja tonow - tylko wizualnego pola, nigdy maski/wildernessu ---
            float q = MistV2_SoftQuantize(field, _MistQuantizeSteps);
            return lerp(field, q, retro);
        }

        // Palety. Wszystkie kolory z Properties sa uzywane bez konwersji (patrz naglowek).
        void EvaluateMistPalette(float mist, float detailNoise, out float3 discoverable, out float3 blocked)
        {
            // Discoverable: ciemniejszy fiolet wokol, jasne skupiska w makrostrukturze.
            float  mD   = smoothstep(0.12, 0.92, mist);
            float3 disc = lerp(_DiscoverableColorDark.rgb, _DiscoverableColorLight.rgb, mD);

            // Subtelny rozowo-fioletowy akcent - tylko w najjasniejszych skupiskach, niejednolicie.
            float accent = smoothstep(0.62, 1.0, mist) * saturate((detailNoise - 0.35) * 2.5);
            disc *= lerp(float3(1.0, 1.0, 1.0), MIST_PINK_TINT, accent * 0.8);
            discoverable = disc;

            // Blocked: ten sam wzor, ale mniejszy kontrast (spokojniejszy) i ciemna paleta.
            float mB = saturate(0.5 + (mist - 0.5) * MIST_BLOCKED_CONTRAST);
            blocked = lerp(_BlockedColorDark.rgb, _BlockedColorLight.rgb, mB);
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

            // Ksztalt: maly, ostry punkt. Retro Strength laczy kolo (0) z kwadratowym teksem (1).
            float2 dv = abs(cellFrac - center) * MIST_SPARK_CELL;
            float  d  = lerp(length(dv), max(dv.x, dv.y), saturate(_MistRetroStrength));

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

        float FogSampleMask(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_FogMask, sampler_FogMask, saturate(uv)).r;
        }

        float CalculateFogWilderness(
            float3 WorldPos,
            float4 WorldBounds,
            float FadeMin,
            float FadeMax
        )
        {
            float4 bounds = (length(WorldBounds.zw) > 0.001) ? WorldBounds : _FogWorldBounds;
            float2 size   = max(bounds.zw, float2(0.0001, 0.0001));

            float2 p      = WorldPos.xz;
            float  time   = _Time.y;
            float2 drift1 = float2(time * 0.035, time * 0.02);
            float2 drift2 = float2(-time * 0.02, time * 0.035);

            // 1. DOMAIN WARPING
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

            // 2. MIEKKA, NIEJEDNOLITA KRAWEDZ - 5 probek maski z promieniem zaleznym od szumu
            float  r  = FOG_SOFTNESS * (0.4 + FogNoise2D(p * 0.3 + drift1)) / size.x;
            float2 ro = float2(r, r * size.x / size.y);

            float m  = FogSampleMask(uv) * 0.36;
            m += FogSampleMask(uv + ro * float2( 1.0,  0.6)) * 0.16;
            m += FogSampleMask(uv + ro * float2(-0.6,  1.0)) * 0.16;
            m += FogSampleMask(uv + ro * float2(-1.0, -0.6)) * 0.16;
            m += FogSampleMask(uv + ro * float2( 0.6, -1.0)) * 0.16;

            // 3. POSTRZEPIENIE - szum dziala tylko w pasie przejscia (m ~ 0.5)
            float detail = FogFBM(p * FOG_DETAIL_FREQ + drift2 * 2.0 + float2(15.7, 39.2));
            float band   = saturate(4.0 * m * (1.0 - m));
            float dynamicMask = saturate(m + (detail - 0.5) * 2.0 * FOG_DETAIL_STRENGTH * band);

            // 4. Przejscie do ciemnej mgly (wilderness: 0 = Discoverable, 1 = Blocked)
            float fMin = min(FadeMin, 0.25);
            float fMax = max(FadeMax, 0.80);
            return smoothstep(fMin, fMax, dynamicMask);
        }
        ENDHLSL

        // ===================================================================================
        // PASS 1: UniversalForward (URP Lit, Opaque, Double-Sided, No Shadows)
        // ===================================================================================
        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Off

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings MistVert(Attributes input)
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

                // 1. GAMEPLAY: wspolczynnik wilderness z maski (FadeMin = 0.55, FadeMax = 0.88
                //    jak w nodzie CalculateFog). Nie kwantyzowany, nie pikselizowany.
                float wilderness = CalculateFogWilderness(
                    input.positionWS,
                    _FogWorldBounds,
                    0.55,
                    0.88
                );

                // 2. WYGLAD: kolor bazowy (albedo) i emisja sa teraz niezalezne
                float3 albedoColor;
                float3 emissionColor;

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
                    // --- Mist V2: Retro Chaos ---
                    // Pochodne liczone poza galeziami dynamicznymi (wymog derivatives).
                    float2 fw = fwidth(worldXZ);
                    float pxWidth = max(fw.x, fw.y);

                    float detailNoise;
                    float mist = EvaluateMistField(worldXZ, _UnscaledTime, detailNoise);

                    float3 discoverableColor;
                    float3 blockedColor;
                    EvaluateMistPalette(mist, detailNoise, discoverableColor, blockedColor);

                    // Ten sam wspolczynnik wilderness miesza oba stany, jak w V1.
                    albedoColor   = lerp(discoverableColor * _DiscoverableBrightness,
                                         blockedColor      * _BlockedBrightness, wilderness);
                    emissionColor = lerp(discoverableColor * _DiscoverableEmission,
                                         blockedColor      * _BlockedEmission, wilderness);

                    // Chaos Sparks: tylko Blocked, plynnie wygaszane na granicy stanow.
                    // Dodawane wylacznie do emisji (nie sa oswietlane, nie rozjasniaja albedo).
                    float sparkGate = smoothstep(0.55, 0.95, wilderness);
                    [branch]
                    if (sparkGate > 0.0)
                    {
                        emissionColor += EvaluateChaosSparks(worldXZ, _UnscaledTime, pxWidth) * sparkGate;
                    }
                #endif

                // 3. Konfiguracja InputData i SurfaceData identyczna z Universal Lit Shader Graph
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
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
                surfaceData.alpha               = 1.0;
                surfaceData.clearCoatMask       = 0.0;
                surfaceData.clearCoatSmoothness = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, false);
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
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
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

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
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
            Cull Off

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

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
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
