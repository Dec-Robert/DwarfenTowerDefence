using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WaveGenerator50
{
    private const string WavesFolderPath = "Assets/Main folder/ScriptableObjects/PredefinedWaves";
    private const string EnemiesFolderPath = "Assets/Main folder/ScriptableObjects/Enemies";

    public struct EnemyAssets
    {
        public EnemyData skeleton;
        public EnemyData arcanist;
        public EnemyData abberacja;
        public EnemyData blob;
        public EnemyData mistStalker;
        public EnemyData herald;
    }

    [MenuItem("Tools/Enemies & Waves/Generate 50 Balanced Waves", priority = 32)]
    public static void GenerateAll50WavesMenuItem()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Generowanie 50 Fal",
            "Czy na pewno chcesz wygenerować 50 predefiniowanych nocy zgodnie z planem balansu?\n\nOperacja utworzy lub zaktualizuje assety WaveDefinition w folderze:\n" + WavesFolderPath,
            "Tak, generuj",
            "Anuluj"
        );

        if (!confirm) return;

        int count = GenerateAll50Waves();
        EditorUtility.DisplayDialog("Sukces", $"Pomyślnie wygenerowano i zaktualizowano {count} assetów fal (Dzień 1 - 50)!", "OK");
    }

    public static void GenerateAll50WavesBatch()
    {
        GenerateAll50Waves();
    }

    public static int GenerateAll50Waves()
    {
        if (!Directory.Exists(WavesFolderPath))
        {
            Directory.CreateDirectory(WavesFolderPath);
        }

        EnemyAssets enemies = LoadEnemyAssets();
        if (enemies.skeleton == null || enemies.arcanist == null || enemies.abberacja == null ||
            enemies.blob == null || enemies.mistStalker == null || enemies.herald == null)
        {
            Debug.LogError("[WaveGenerator50] Nie znaleziono wszystkich wymaganych assetów EnemyData w: " + EnemiesFolderPath);
            return 0;
        }

        int generatedCount = 0;

        for (int day = 1; day <= 50; day++)
        {
            string assetPath = $"{WavesFolderPath}/Wave day {day}.asset";
            WaveDefinition wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(assetPath);
            bool isNew = false;

            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveDefinition>();
                isNew = true;
            }

            ConfigureWaveForDay(wave, day, enemies);

            if (isNew)
            {
                AssetDatabase.CreateAsset(wave, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(wave);
            }

            generatedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SyncSpawnerIfSceneOpen();

        Debug.Log($"<color=green>[WaveGenerator50] Pomyślnie wygenerowano {generatedCount} assetów fal (WaveDefinition Dzień 1..50).</color>");
        return generatedCount;
    }

    public static void SyncSpawnerIfSceneOpen()
    {
        string scenePath = "Assets/Main folder/_Scenes/Game scene.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        var spawner = UnityEngine.Object.FindFirstObjectByType<EnemySpawner>();
        if (spawner != null)
        {
            string[] waveGuids = AssetDatabase.FindAssets("t:WaveDefinition", new[] { WavesFolderPath });
            var list = new List<WaveDefinition>();
            foreach (var g in waveGuids)
            {
                var w = AssetDatabase.LoadAssetAtPath<WaveDefinition>(AssetDatabase.GUIDToAssetPath(g));
                if (w != null) list.Add(w);
            }
            list = list.OrderBy(w => w.dayNumber).ToList();
            Undo.RecordObject(spawner, "Sync EnemySpawner 50 Waves");
            spawner.predefinedWaves = list;
            EditorUtility.SetDirty(spawner);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=green>[WaveGenerator50] Zsynchronizowano {list.Count} fal z EnemySpawner w scenie {scenePath}.</color>");
        }
    }

    public static EnemyAssets LoadEnemyAssets()
    {
        EnemyAssets a = new EnemyAssets();
        string[] guids = AssetDatabase.FindAssets("t:EnemyData", new[] { EnemiesFolderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null) continue;

            string name = data.name.ToLowerInvariant();
            if (name.Contains("skeleton")) a.skeleton = data;
            else if (name.Contains("arcanist")) a.arcanist = data;
            else if (name.Contains("abber")) a.abberacja = data;
            else if (name.Contains("blob")) a.blob = data;
            else if (name.Contains("stalker")) a.mistStalker = data;
            else if (name.Contains("herald")) a.herald = data;
        }
        return a;
    }

    public static int CalculateEtherPool(int day)
    {
        // Nowa zbalansowana skala eteru:
        // Fale 1-10: 1 + (1 co 2 fale) -> 1, 2, 2, 3, 3, 4, 4, 5, 5, 6
        // Fale 11-20: 6 + (3 co 4 fale) -> 7, 7, 8, 8, 9, 10, 10, 11, 11, 12
        // Fale 21-40: 12 + (1 co 2 fale) -> fala 40 = 22
        // Fale 41-45: +2 na falę -> 24, 26, 28, 30, 32
        // Fale 46-50: stała wartość z fali 45 (32)
        if (day <= 1) return 1;
        if (day <= 10) return 1 + day / 2;
        if (day <= 20) return Mathf.RoundToInt(6f + (day - 10) * 0.6f);
        if (day <= 40) return 12 + (day - 20) / 2;
        if (day <= 45) return 22 + (day - 40) * 2;
        return 32;
    }

    public struct TierSplit
    {
        public int tierOld;
        public int countOld;
        public int tierNew;
        public int countNew;
    }

    /// <summary>
    /// Dzieli liczbę jednostek na stary i nowy tier wg 4-etapowego schematu:
    /// step 0: 100% stary
    /// step 1: 90% stary, 10% nowy (min. 1 jednostka nowego tieru)
    /// step 2: 70% stary, 30% nowy
    /// step 3: 50% stary, 50% nowy
    /// step 4: 20% stary, 80% nowy
    /// step 5: 100% nowy
    /// </summary>
    public static TierSplit SplitByTierStep(int totalCount, int baseTier, int step)
    {
        if (totalCount <= 0)
        {
            return new TierSplit { tierOld = baseTier, countOld = 0, tierNew = baseTier, countNew = 0 };
        }

        if (step <= 0 || baseTier >= 4)
        {
            return new TierSplit { tierOld = baseTier, countOld = totalCount, tierNew = baseTier, countNew = 0 };
        }

        if (step >= 5)
        {
            int nextTier = Mathf.Min(4, baseTier + 1);
            return new TierSplit { tierOld = nextTier, countOld = totalCount, tierNew = nextTier, countNew = 0 };
        }

        float newRatio = 0.10f;
        switch (step)
        {
            case 1: newRatio = 0.10f; break;
            case 2: newRatio = 0.30f; break;
            case 3: newRatio = 0.50f; break;
            case 4: newRatio = 0.80f; break;
        }

        int countNew = Mathf.RoundToInt(totalCount * newRatio);
        if (countNew <= 0 && totalCount > 0) countNew = 1;
        countNew = Mathf.Clamp(countNew, 1, totalCount);
        int countOld = totalCount - countNew;

        return new TierSplit
        {
            tierOld = baseTier,
            countOld = countOld,
            tierNew = Mathf.Min(4, baseTier + 1),
            countNew = countNew
        };
    }

    public static void AddTierEntries(List<WaveDefinition.WaveEntry> list, EnemyData enemy, TierSplit split)
    {
        if (split.countOld > 0)
        {
            list.Add(new WaveDefinition.WaveEntry { enemy = enemy, tier = split.tierOld, count = split.countOld, forceElite = false });
        }
        if (split.countNew > 0 && split.tierNew != split.tierOld)
        {
            list.Add(new WaveDefinition.WaveEntry { enemy = enemy, tier = split.tierNew, count = split.countNew, forceElite = false });
        }
    }

    public static void ConfigureWaveForDay(WaveDefinition wave, int day, EnemyAssets e)
    {
        wave.dayNumber = day;
        wave.etherPool = CalculateEtherPool(day);
        wave.allowRandomElites = day >= 20;
        wave.maxElitesCount = day < 20 ? 0 : (day <= 25 ? 2 : (day <= 35 ? 4 : (day <= 45 ? 6 : 8)));
        wave.hpMultiplier = 1.0f;
        wave.speedMultiplier = 1.0f;
        wave.shuffleEnemies = false;

        // Określenie tempa i czasu fali: dla fal wielkich (200-500) zmniejszamy interwał
        if (day <= 15) wave.customSpawnInterval = 0.9f;
        else if (day <= 30) wave.customSpawnInterval = 0.6f;
        else if (day <= 40) wave.customSpawnInterval = 0.38f;
        else wave.customSpawnInterval = 0.22f;

        wave.spawnGroups = new List<WaveDefinition.SpawnGroup>();
        wave.waveMessage = GetClimaticMessageForDay(day);

        // Liczba wrogów na falę (orientacyjna krzywa z dekompresją po bossach i skokami na 24 i 38)
        int totalEnemies = GetTargetEnemyCount(day);

        // Harmonogramy tierów (stadium blendowania)
        // Skeleton: debiut 1 (T1). 11..14 (T1->T2). 15 boss hold. 16..25 (T2). 26..29 (T2->T3). 30 boss hold. 31..35 (T3). 36..39 (T3->T4). 40..50 (T4).
        int skelBaseTier = 1;
        int skelStep = 0;
        if (day < 11) { skelBaseTier = 1; skelStep = 0; }
        else if (day <= 14) { skelBaseTier = 1; skelStep = day - 10; }
        else if (day == 15) { skelBaseTier = 1; skelStep = 4; } // Hold na bossie
        else if (day <= 25) { skelBaseTier = 2; skelStep = 0; }
        else if (day <= 29) { skelBaseTier = 2; skelStep = day - 25; }
        else if (day == 30) { skelBaseTier = 2; skelStep = 4; } // Hold na bossie
        else if (day <= 35) { skelBaseTier = 3; skelStep = 0; }
        else if (day <= 39) { skelBaseTier = 3; skelStep = day - 35; }
        else { skelBaseTier = 4; skelStep = 0; }

        // Arcanist: debiut 5 (T1). 15 boss hold. 16..19 (T1->T2). 20..29 (T2). 30 boss hold. 31..34 (T2->T3). 35..39 (T3). 40..43 (T3->T4). 44..50 (T4).
        int arcBaseTier = 1;
        int arcStep = 0;
        if (day < 16) { arcBaseTier = 1; arcStep = 0; }
        else if (day <= 19) { arcBaseTier = 1; arcStep = day - 15; }
        else if (day <= 30) { arcBaseTier = 2; arcStep = 0; }
        else if (day <= 34) { arcBaseTier = 2; arcStep = day - 30; }
        else if (day <= 39) { arcBaseTier = 3; arcStep = 0; }
        else if (day <= 43) { arcBaseTier = 3; arcStep = day - 39; }
        else { arcBaseTier = 4; arcStep = 0; }

        // Abberacja: teaser 5 (T1). Powrót 10 (T1). 20..23 (T1->T2). 24..33 (T2). 34..37 (T2->T3). 38..42 (T3). 43..46 (T3->T4). 47..50 (T4).
        int abbBaseTier = 1;
        int abbStep = 0;
        if (day < 20) { abbBaseTier = 1; abbStep = 0; }
        else if (day <= 23) { abbBaseTier = 1; abbStep = day - 19; }
        else if (day <= 33) { abbBaseTier = 2; abbStep = 0; }
        else if (day <= 37) { abbBaseTier = 2; abbStep = day - 33; }
        else if (day <= 42) { abbBaseTier = 3; abbStep = 0; }
        else if (day <= 46) { abbBaseTier = 3; abbStep = day - 42; }
        else { abbBaseTier = 4; abbStep = 0; }

        // Blob: debiut 20 (T1). 30 boss hold. 31..34 (T1->T2). 35..44 (T2). 45..48 (T2->T3). 49..50 (T3).
        int blobBaseTier = 1;
        int blobStep = 0;
        if (day < 31) { blobBaseTier = 1; blobStep = 0; }
        else if (day <= 34) { blobBaseTier = 1; blobStep = day - 30; }
        else if (day <= 44) { blobBaseTier = 2; blobStep = 0; }
        else if (day <= 48) { blobBaseTier = 2; blobStep = day - 44; }
        else { blobBaseTier = 3; blobStep = 0; }

        // Mist Stalker: teaser 18..19 (T1). Główny debiut 25 (T1). 35..38 (T1->T2). 39..48 (T2). 49..50 (T2->T3).
        int stalkBaseTier = 1;
        int stalkStep = 0;
        if (day < 35) { stalkBaseTier = 1; stalkStep = 0; }
        else if (day <= 38) { stalkBaseTier = 1; stalkStep = day - 34; }
        else if (day <= 48) { stalkBaseTier = 2; stalkStep = 0; }
        else if (day == 49) { stalkBaseTier = 2; stalkStep = 1; }
        else { stalkBaseTier = 2; stalkStep = 2; }

        // Fale specjalne z Bossami: 15, 30, 50
        if (day == 15)
        {
            BuildBossWave15(wave, e, skelBaseTier, skelStep, arcBaseTier, arcStep, abbBaseTier, abbStep);
        }
        else if (day == 30)
        {
            BuildBossWave30(wave, e, skelBaseTier, skelStep, arcBaseTier, arcStep, abbBaseTier, abbStep, blobBaseTier, blobStep, stalkBaseTier, stalkStep);
        }
        else if (day == 50)
        {
            BuildBossWave50(wave, e, skelBaseTier, skelStep, arcBaseTier, arcStep, abbBaseTier, abbStep, blobBaseTier, blobStep, stalkBaseTier, stalkStep);
        }
        else
        {
            BuildStandardWave(wave, day, totalEnemies, e,
                skelBaseTier, skelStep,
                arcBaseTier, arcStep,
                abbBaseTier, abbStep,
                blobBaseTier, blobStep,
                stalkBaseTier, stalkStep);
        }

        wave.SyncFlattenedEnemiesList();
    }

    private static int GetTargetEnemyCount(int day)
    {
        // 1-15: 6..30
        if (day <= 15)
        {
            int[] early = { 6, 8, 10, 12, 15, 15, 17, 19, 21, 22, 23, 24, 25, 27, 30 };
            return early[day - 1];
        }
        // 16-30: 25..105 (fala 16 dekompresja 25, fala 24 skok 78, fala 30 boss 105)
        if (day <= 30)
        {
            int[] mid = { 25, 30, 36, 42, 48, 54, 60, 66, 78, 82, 86, 90, 94, 98, 105 };
            return mid[day - 16];
        }
        // 31-40: 95..210 (fala 31 dekompresja 95, fala 38 skok 188, fala 40: 210)
        if (day <= 40)
        {
            int[] late = { 95, 108, 120, 132, 144, 156, 168, 188, 198, 210 };
            return late[day - 31];
        }
        // 41-50: 200..500
        int[] endgame = { 220, 245, 275, 310, 350, 380, 410, 440, 470, 500 };
        return endgame[day - 41];
    }

    private static string GetClimaticMessageForDay(int day)
    {
        switch (day)
        {
            case 1: return "Cienie z mgły podchodzą pod bramę...";
            case 5: return "Arkaniści Mgły dołączają do szturmu! Uważaj na ich tarcze przeciw pociskom.";
            case 10: return "Z mgły wyłaniają się ciężko opancerzone Abberacje!";
            case 15: return "BOSS: Zwiastun Mgły przybył! Zgładź bestię zanim skruszy bramę!";
            case 16: return "Mgła rzednie po uderzeniu bossa. Czas na regenerację i rozbudowę obrony.";
            case 18: return "Szybkie sylwetki mignęły we mgle... Bądź czujny!";
            case 20: return "Wypaczone Bloby zalewają dolinę! Po śmierci dzielą się na mniejsze poczwary.";
            case 24: return "Potężne uderzenie hordy! Nadciągają ulepszone Abberacje drugiego tieru!";
            case 25: return "Watahy Prześladowców Mgły atakują z zabójczą prędkością i zwinnością!";
            case 30: return "BOSS: Zwiastun Mgły powraca w potężniejszej, zahartowanej formie!";
            case 31: return "Zwycięstwo nad bestią daje wytchnienie. Przeciwnik przegrupowuje siły.";
            case 38: return "Mroźny wicher przynosi potężną hordę wrogów trzeciego tieru!";
            case 40: return "Pradawne szkielety osiągają szczyt swej potęgi!";
            case 50: return "OSTATECZNE STARCIE: Prastary Zwiastun Mgły na czele legionu cieni!";
            default: return "";
        }
    }

    private static void BuildStandardWave(WaveDefinition wave, int day, int totalCount, EnemyAssets e,
        int skelTier, int skelStep,
        int arcTier, int arcStep,
        int abbTier, int abbStep,
        int blobTier, int blobStep,
        int stalkTier, int stalkStep)
    {
        // 1. Podział proporcji jednostek w zależności od etapu i debiutów
        int countSkel = 0;
        int countArc = 0;
        int countAbb = 0;
        int countBlob = 0;
        int countStalk = 0;

        if (day < 5)
        {
            // Fale 1..4: 100% Skeletons
            countSkel = totalCount;
        }
        else if (day == 5)
        {
            // Fala 5: Debiut Arcanist + 1 Abberacja teaser
            countAbb = 1;
            countArc = 7;
            countSkel = totalCount - countAbb - countArc;
        }
        else if (day < 10)
        {
            // Fale 6..9: Skeletons + Arcanists (Abberacja absent)
            countArc = Mathf.RoundToInt(totalCount * 0.35f);
            countSkel = totalCount - countArc;
        }
        else if (day == 10)
        {
            // Fala 10: Debiut stały Abberacji (fokus)
            countAbb = Mathf.RoundToInt(totalCount * 0.45f);
            countArc = Mathf.RoundToInt(totalCount * 0.25f);
            countSkel = totalCount - countAbb - countArc;
        }
        else if (day < 18)
        {
            // Fale 11..17: Szkielety (T1/T2), Arkaniści, Abberacje
            countAbb = Mathf.RoundToInt(totalCount * 0.25f);
            countArc = Mathf.RoundToInt(totalCount * 0.25f);
            countSkel = totalCount - countAbb - countArc;
        }
        else if (day == 18)
        {
            // Teaser Mist Stalker (1 unit)
            countStalk = 1;
            countAbb = Mathf.RoundToInt(totalCount * 0.25f);
            countArc = Mathf.RoundToInt(totalCount * 0.25f);
            countSkel = totalCount - countStalk - countAbb - countArc;
        }
        else if (day == 19)
        {
            // Teaser Mist Stalker (2 units)
            countStalk = 2;
            countAbb = Mathf.RoundToInt(totalCount * 0.25f);
            countArc = Mathf.RoundToInt(totalCount * 0.25f);
            countSkel = totalCount - countStalk - countAbb - countArc;
        }
        else if (day == 20)
        {
            // Fala 20: Debiut Blob (fokus)
            countBlob = Mathf.RoundToInt(totalCount * 0.40f);
            countAbb = Mathf.RoundToInt(totalCount * 0.20f);
            countArc = Mathf.RoundToInt(totalCount * 0.15f);
            countSkel = totalCount - countBlob - countAbb - countArc;
        }
        else if (day < 25)
        {
            // Fale 21..24: Skel, Arc, Abb, Blob
            countBlob = Mathf.RoundToInt(totalCount * 0.25f);
            countAbb = Mathf.RoundToInt(totalCount * 0.25f);
            countArc = Mathf.RoundToInt(totalCount * 0.20f);
            countSkel = totalCount - countBlob - countAbb - countArc;
        }
        else if (day == 25)
        {
            // Fala 25: Debiut Mist Stalker (fokus)
            countStalk = Mathf.RoundToInt(totalCount * 0.35f);
            countBlob = Mathf.RoundToInt(totalCount * 0.20f);
            countAbb = Mathf.RoundToInt(totalCount * 0.20f);
            countArc = Mathf.RoundToInt(totalCount * 0.10f);
            countSkel = totalCount - countStalk - countBlob - countAbb - countArc;
        }
        else
        {
            // Fale 26..49: Pełna zróżnicowana armia cieni
            countStalk = Mathf.RoundToInt(totalCount * 0.18f);
            countBlob = Mathf.RoundToInt(totalCount * 0.20f);
            countAbb = Mathf.RoundToInt(totalCount * 0.22f);
            countArc = Mathf.RoundToInt(totalCount * 0.15f);
            countSkel = totalCount - countStalk - countBlob - countAbb - countArc;
        }

        // Korekta sumaryczna: szkielety pochłaniają ewentualne zaokrąglenie
        int currentSum = countSkel + countArc + countAbb + countBlob + countStalk;
        countSkel += (totalCount - currentSum);

        // 2. Podział na tiery z gwarancją minimum 1 dla debiutów tieru
        TierSplit skelSplit = SplitByTierStep(countSkel, skelTier, skelStep);
        TierSplit arcSplit = SplitByTierStep(countArc, arcTier, arcStep);
        TierSplit abbSplit = SplitByTierStep(countAbb, abbTier, abbStep);
        TierSplit blobSplit = SplitByTierStep(countBlob, blobTier, blobStep);
        TierSplit stalkSplit = SplitByTierStep(countStalk, stalkTier, stalkStep);

        // 3. Budowa 3 grup taktycznych: Straż Przednia (Vanguard), Siły Główne (Main Body), Odwody / Szarża (Rearguard)
        var g1 = new WaveDefinition.SpawnGroup
        {
            groupName = "1. Straż Przednia (Vanguard)",
            delayBefore = 0f,
            spawnInterval = wave.customSpawnInterval,
            intervalJitter = 0.25f,
            shuffleInside = true
        };

        var g2 = new WaveDefinition.SpawnGroup
        {
            groupName = "2. Siły Główne (Main Body)",
            delayBefore = day <= 15 ? 1.5f : 2.5f,
            spawnInterval = wave.customSpawnInterval,
            intervalJitter = 0.25f,
            shuffleInside = true
        };

        var g3 = new WaveDefinition.SpawnGroup
        {
            groupName = "3. Szarża Odwodów (Flank & Rear)",
            delayBefore = day <= 15 ? 1.5f : 2.0f,
            spawnInterval = wave.customSpawnInterval,
            intervalJitter = 0.20f,
            shuffleInside = true
        };

        // Rozmieszczenie jednostek w grupach:
        // G1: 40% Szkieletów, 30% Stalkerów
        int g1Skel = Mathf.RoundToInt(skelSplit.countOld * 0.4f);
        int g1SkelNew = Mathf.RoundToInt(skelSplit.countNew * 0.4f);
        int g1Stalk = Mathf.RoundToInt(stalkSplit.countOld * 0.5f);
        int g1StalkNew = Mathf.RoundToInt(stalkSplit.countNew * 0.5f);
        AddTierEntries(g1.enemies, e.skeleton, new TierSplit { tierOld = skelSplit.tierOld, countOld = g1Skel, tierNew = skelSplit.tierNew, countNew = g1SkelNew });
        AddTierEntries(g1.enemies, e.mistStalker, new TierSplit { tierOld = stalkSplit.tierOld, countOld = g1Stalk, tierNew = stalkSplit.tierNew, countNew = g1StalkNew });

        // G2: Abberacje, Bloby, 40% Arkanistów, 30% Szkieletów
        int g2Skel = Mathf.RoundToInt(skelSplit.countOld * 0.35f);
        int g2SkelNew = Mathf.RoundToInt(skelSplit.countNew * 0.35f);
        int g2Arc = Mathf.RoundToInt(arcSplit.countOld * 0.5f);
        int g2ArcNew = Mathf.RoundToInt(arcSplit.countNew * 0.5f);
        AddTierEntries(g2.enemies, e.skeleton, new TierSplit { tierOld = skelSplit.tierOld, countOld = g2Skel, tierNew = skelSplit.tierNew, countNew = g2SkelNew });
        AddTierEntries(g2.enemies, e.abberacja, abbSplit);
        AddTierEntries(g2.enemies, e.blob, blobSplit);
        AddTierEntries(g2.enemies, e.arcanist, new TierSplit { tierOld = arcSplit.tierOld, countOld = g2Arc, tierNew = arcSplit.tierNew, countNew = g2ArcNew });

        // G3: Reszta jednostek (Arkaniści, Stalkerzy, Szkielety)
        int g3Skel = skelSplit.countOld - g1Skel - g2Skel;
        int g3SkelNew = skelSplit.countNew - g1SkelNew - g2SkelNew;
        int g3Arc = arcSplit.countOld - g2Arc;
        int g3ArcNew = arcSplit.countNew - g2ArcNew;
        int g3Stalk = stalkSplit.countOld - g1Stalk;
        int g3StalkNew = stalkSplit.countNew - g1StalkNew;
        AddTierEntries(g3.enemies, e.skeleton, new TierSplit { tierOld = skelSplit.tierOld, countOld = g3Skel, tierNew = skelSplit.tierNew, countNew = g3SkelNew });
        AddTierEntries(g3.enemies, e.arcanist, new TierSplit { tierOld = arcSplit.tierOld, countOld = g3Arc, tierNew = arcSplit.tierNew, countNew = g3ArcNew });
        AddTierEntries(g3.enemies, e.mistStalker, new TierSplit { tierOld = stalkSplit.tierOld, countOld = g3Stalk, tierNew = stalkSplit.tierNew, countNew = g3StalkNew });

        if (g1.enemies.Count > 0) wave.spawnGroups.Add(g1);
        if (g2.enemies.Count > 0) wave.spawnGroups.Add(g2);
        if (g3.enemies.Count > 0) wave.spawnGroups.Add(g3);
    }

    private static void BuildBossWave15(WaveDefinition wave, EnemyAssets e,
        int skelTier, int skelStep,
        int arcTier, int arcStep,
        int abbTier, int abbStep)
    {
        // Fala 15: Boss Herald of the Mist T1 + eskorty (30 jednostek łącznie)
        // Grupa 1: Zwiad (Vanguard): 6 Szkieletów
        var g1 = new WaveDefinition.SpawnGroup
        {
            groupName = "1. Przednia Straż Zwiadu",
            delayBefore = 0f,
            spawnInterval = 0.8f,
            intervalJitter = 0.15f,
            shuffleInside = true
        };
        TierSplit s1 = SplitByTierStep(6, skelTier, skelStep);
        AddTierEntries(g1.enemies, e.skeleton, s1);

        // Grupa 2: Oddział Taktyczny: 4 Abberacje T1 + 4 Arkaniści T1
        var g2 = new WaveDefinition.SpawnGroup
        {
            groupName = "2. Natarcie Przełamania",
            delayBefore = 2.5f,
            spawnInterval = 0.9f,
            intervalJitter = 0.15f,
            shuffleInside = true
        };
        TierSplit abSplit = SplitByTierStep(4, abbTier, abbStep);
        TierSplit arSplit = SplitByTierStep(4, arcTier, arcStep);
        AddTierEntries(g2.enemies, e.abberacja, abSplit);
        AddTierEntries(g2.enemies, e.arcanist, arSplit);

        // Grupa 3: BOSS: Zwiastun Mgły T1 + Gwardia Przyboczna (4 Szkielety)
        var g3 = new WaveDefinition.SpawnGroup
        {
            groupName = "3. BOSS: Zwiastun Mgły (T1) i Gwardia",
            delayBefore = 3.0f,
            spawnInterval = 1.1f,
            intervalJitter = 0.1f,
            shuffleInside = false // Boss wchodzi na czele swojej gwardii
        };
        g3.enemies.Add(new WaveDefinition.WaveEntry { enemy = e.herald, tier = 1, count = 1, forceElite = false });
        TierSplit sGuard = SplitByTierStep(4, skelTier, skelStep);
        AddTierEntries(g3.enemies, e.skeleton, sGuard);

        // Grupa 4: Posłannicy Śmierci (Odwody): 8 Szkieletów + 3 Arkaniści
        var g4 = new WaveDefinition.SpawnGroup
        {
            groupName = "4. Ostatnia Fala Wsparcia Bossa",
            delayBefore = 2.5f,
            spawnInterval = 0.8f,
            intervalJitter = 0.2f,
            shuffleInside = true
        };
        TierSplit s4 = SplitByTierStep(8, skelTier, skelStep);
        TierSplit a4 = SplitByTierStep(3, arcTier, arcStep);
        AddTierEntries(g4.enemies, e.skeleton, s4);
        AddTierEntries(g4.enemies, e.arcanist, a4);

        wave.spawnGroups.Add(g1);
        wave.spawnGroups.Add(g2);
        wave.spawnGroups.Add(g3);
        wave.spawnGroups.Add(g4);
    }

    private static void BuildBossWave30(WaveDefinition wave, EnemyAssets e,
        int skelTier, int skelStep,
        int arcTier, int arcStep,
        int abbTier, int abbStep,
        int blobTier, int blobStep,
        int stalkTier, int stalkStep)
    {
        // Fala 30: Boss Herald of the Mist T2 + 99 jednostek (100 jednostek łącznie)
        var g1 = new WaveDefinition.SpawnGroup
        {
            groupName = "1. Szpica Hordy (Szybki Szturm)",
            delayBefore = 0f,
            spawnInterval = 0.5f,
            intervalJitter = 0.2f,
            shuffleInside = true
        };
        TierSplit s1 = SplitByTierStep(25, skelTier, skelStep);
        TierSplit st1 = SplitByTierStep(10, stalkTier, stalkStep);
        AddTierEntries(g1.enemies, e.skeleton, s1);
        AddTierEntries(g1.enemies, e.mistStalker, st1);

        var g2 = new WaveDefinition.SpawnGroup
        {
            groupName = "2. Ciężki Taran (Abberacje i Bloby)",
            delayBefore = 2.5f,
            spawnInterval = 0.6f,
            intervalJitter = 0.2f,
            shuffleInside = true
        };
        TierSplit ab2 = SplitByTierStep(14, abbTier, abbStep);
        TierSplit bl2 = SplitByTierStep(15, blobTier, blobStep);
        AddTierEntries(g2.enemies, e.abberacja, ab2);
        AddTierEntries(g2.enemies, e.blob, bl2);

        var g3 = new WaveDefinition.SpawnGroup
        {
            groupName = "3. BOSS: Zwiastun Mgły (T2) i Przyboczni",
            delayBefore = 3.0f,
            spawnInterval = 0.7f,
            intervalJitter = 0.1f,
            shuffleInside = false
        };
        g3.enemies.Add(new WaveDefinition.WaveEntry { enemy = e.herald, tier = 2, count = 1, forceElite = false });
        TierSplit ar3 = SplitByTierStep(12, arcTier, arcStep);
        AddTierEntries(g3.enemies, e.arcanist, ar3);

        var g4 = new WaveDefinition.SpawnGroup
        {
            groupName = "4. Fala Przełamująca (Odwody)",
            delayBefore = 2.5f,
            spawnInterval = 0.55f,
            intervalJitter = 0.2f,
            shuffleInside = true
        };
        TierSplit s4 = SplitByTierStep(22, skelTier, skelStep);
        TierSplit bl4 = SplitByTierStep(5, blobTier, blobStep);
        AddTierEntries(g4.enemies, e.skeleton, s4);
        AddTierEntries(g4.enemies, e.blob, bl4);

        wave.spawnGroups.Add(g1);
        wave.spawnGroups.Add(g2);
        wave.spawnGroups.Add(g3);
        wave.spawnGroups.Add(g4);
    }

    private static void BuildBossWave50(WaveDefinition wave, EnemyAssets e,
        int skelTier, int skelStep,
        int arcTier, int arcStep,
        int abbTier, int abbStep,
        int blobTier, int blobStep,
        int stalkTier, int stalkStep)
    {
        // Fala 50: FINAŁ! Boss Herald of the Mist T3 + 499 jednostek (500 łącznie)
        var g1 = new WaveDefinition.SpawnGroup
        {
            groupName = "1. Legion Zagłady (Awangarda)",
            delayBefore = 0f,
            spawnInterval = 0.22f,
            intervalJitter = 0.25f,
            shuffleInside = true
        };
        TierSplit s1 = SplitByTierStep(100, skelTier, skelStep);
        TierSplit st1 = SplitByTierStep(35, stalkTier, stalkStep);
        AddTierEntries(g1.enemies, e.skeleton, s1);
        AddTierEntries(g1.enemies, e.mistStalker, st1);

        var g2 = new WaveDefinition.SpawnGroup
        {
            groupName = "2. Kolumna Tytanów (Abberacje i Bloby)",
            delayBefore = 2.5f,
            spawnInterval = 0.24f,
            intervalJitter = 0.25f,
            shuffleInside = true
        };
        TierSplit ab2 = SplitByTierStep(55, abbTier, abbStep);
        TierSplit bl2 = SplitByTierStep(50, blobTier, blobStep);
        AddTierEntries(g2.enemies, e.abberacja, ab2);
        AddTierEntries(g2.enemies, e.blob, bl2);

        var g3 = new WaveDefinition.SpawnGroup
        {
            groupName = "3. PRASTARY ZWIASTUN MGŁY (T3) i Elitarna Kohorta",
            delayBefore = 3.0f,
            spawnInterval = 0.25f,
            intervalJitter = 0.1f,
            shuffleInside = false
        };
        g3.enemies.Add(new WaveDefinition.WaveEntry { enemy = e.herald, tier = 3, count = 1, forceElite = false });
        TierSplit ar3 = SplitByTierStep(45, arcTier, arcStep);
        TierSplit ab3 = SplitByTierStep(15, abbTier, abbStep);
        AddTierEntries(g3.enemies, e.arcanist, ar3);
        AddTierEntries(g3.enemies, e.abberacja, ab3);

        var g4 = new WaveDefinition.SpawnGroup
        {
            groupName = "4. Nieskończona Ciemność (Ostateczna Horda)",
            delayBefore = 2.5f,
            spawnInterval = 0.20f,
            intervalJitter = 0.25f,
            shuffleInside = true
        };
        TierSplit s4 = SplitByTierStep(120, skelTier, skelStep);
        TierSplit st4 = SplitByTierStep(40, stalkTier, stalkStep);
        TierSplit bl4 = SplitByTierStep(38, blobTier, blobStep);
        AddTierEntries(g4.enemies, e.skeleton, s4);
        AddTierEntries(g4.enemies, e.mistStalker, st4);
        AddTierEntries(g4.enemies, e.blob, bl4);

        wave.spawnGroups.Add(g1);
        wave.spawnGroups.Add(g2);
        wave.spawnGroups.Add(g3);
        wave.spawnGroups.Add(g4);
    }
}
