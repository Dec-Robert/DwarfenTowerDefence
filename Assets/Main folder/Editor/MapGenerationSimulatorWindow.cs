using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Okno symulatora generacji mapy (Tools > Map > Generation Simulator).
/// Umożliwia masowe testowanie N seedów (domyślnie 500) przez BuildMapData(seed),
/// zbieranie statystyk ścieżek, pętli, rozdroży i gwarancji surowców oraz eksport raportu do CSV.
/// </summary>
public class MapGenerationSimulatorWindow : EditorWindow
{
    [Serializable]
    public class SeedSimulationRow
    {
        public int seed;
        public bool pathSuccess;
        public bool guaranteesMet;
        public bool startZoneValid;
        public int mainPathLength;
        public int minStartRoadHexes;
        public int loopsCount;
        public int crossroadsCount;
        public int totalSpawners;
        public int r12Forest;
        public int r12FertileSoil;
        public int r12Hill;
        public int r12Mountain;
        public int r12Sinkhole;
        public int r34Forest;
        public int r34FertileSoil;
        public int r34Hill;
        public int r34Mountain;
        public int r34MountainChunks;
        public int r34Sinkhole;
        public double generationTimeMs;
        public string failureReason = string.Empty;
    }

    [Serializable]
    public class SimulationReport
    {
        public int totalSeeds;
        public int startSeed;

        public int successfulPathGenerations;
        public float pathSuccessRatePercent;

        public int guaranteesMetCount;
        public float guaranteesSuccessRatePercent;

        public int startZoneValidCount;

        public int minPathLength;
        public int maxPathLength;
        public float avgPathLength;

        public int minStartChunkRoadHexes;
        public int maxStartChunkRoadHexes;
        public float avgStartChunkRoadHexes;

        public int minLoops;
        public int maxLoops;
        public float avgLoops;

        public int minCrossroads;
        public int maxCrossroads;
        public float avgCrossroads;

        public int minSpawners;
        public int maxSpawners;
        public float avgSpawners;

        public float avgR12Forest;
        public float avgR12FertileSoil;
        public float avgR12Hill;
        public float avgR12Mountain;
        public float avgR12Sinkhole;

        public float avgR34Forest;
        public float avgR34FertileSoil;
        public float avgR34Hill;
        public float avgR34Mountain;
        public float avgR34MountainChunks;
        public float avgR34Sinkhole;

        public double totalElapsedMs;
        public double avgGenerationTimeMs;

        public List<int> failedPathSeeds = new List<int>();
        public List<int> failedGuaranteeSeeds = new List<int>();
        public List<int> failedStartZoneSeeds = new List<int>();
        public List<SeedSimulationRow> rows = new List<SeedSimulationRow>();

        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Seed,PathSuccess,GuaranteesMet,StartZoneValid,MainPathLength,MinStartRoadHexes,Loops,Crossroads,TotalSpawners,R12_Forest,R12_FertileSoil,R12_Hill,R12_Mountain,R12_Sinkhole,R34_Forest,R34_FertileSoil,R34_Hill,R34_Mountain,R34_MountainChunks,R34_Sinkhole,TimeMs,FailureReason");
            foreach (var r in rows)
            {
                sb.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20:F3},\"{21}\"\n",
                    r.seed,
                    r.pathSuccess,
                    r.guaranteesMet,
                    r.startZoneValid,
                    r.mainPathLength,
                    r.minStartRoadHexes,
                    r.loopsCount,
                    r.crossroadsCount,
                    r.totalSpawners,
                    r.r12Forest,
                    r.r12FertileSoil,
                    r.r12Hill,
                    r.r12Mountain,
                    r.r12Sinkhole,
                    r.r34Forest,
                    r.r34FertileSoil,
                    r.r34Hill,
                    r.r34Mountain,
                    r.r34MountainChunks,
                    r.r34Sinkhole,
                    r.generationTimeMs,
                    r.failureReason ?? string.Empty);
            }
            return sb.ToString();
        }

        public void ExportToCsv(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, ToCsv(), Encoding.UTF8);
        }
    }

    private int seedCount = 500;
    private int startSeed = 1;
    private HexMapGenerator targetGenerator;
    private SimulationReport lastReport;
    private Vector2 scrollPos;
    private string lastCsvExportPath = string.Empty;

    [MenuItem("Tools/Map/Generation Simulator")]
    public static void ShowWindow()
    {
        var window = GetWindow<MapGenerationSimulatorWindow>("Map Simulator");
        window.minSize = new Vector2(560f, 640f);
        window.Show();
    }

    /// <summary>
    /// Uruchamia wsadową symulację N map przez BuildMapData(seed).
    /// Działa zarówno w edytorze Unity, jak i w środowisku bezgłowym CLI (TestHarness).
    /// </summary>
    public static SimulationReport RunBatchSimulation(
        int count = 500,
        int firstSeed = 1,
        HexMapGenerator generatorTemplate = null)
    {
        if (count <= 0) count = 1;

        HexMapGenerator gen = generatorTemplate;
        GameObject tempGo = null;
        bool createdTempGo = false;

        if ((object)gen == null)
        {
            try
            {
                tempGo = new GameObject("__TempMapSimulatorGenerator");
                gen = tempGo.AddComponent<HexMapGenerator>();
                createdTempGo = true;
            }
            catch
            {
                gen = (HexMapGenerator)RuntimeHelpers.GetUninitializedObject(typeof(HexMapGenerator));
                gen.mapWidth = 8;
                gen.mapMinY = -6;
                gen.mapMaxY = 6;
                gen.chunkRadius = 3;
                gen.hexSize = 1f;
                gen.padding = 0.02f;
                gen.minChunkDistance = 11;
                gen.maxGenerationAttempts = 50;
                gen.allowExtraSpawners = true;
                gen.extraSpawnerChances = new int[] { 80, 60, 40, 20 };
                gen.extraSpawnDistanceRange = new Vector2Int(2, 6);
                gen.chanceForMoreBranching = 30;
                gen.globalObstacleChance = 40;
                gen.baseRoadEndLocal = new Vector2Int(3, -2);
                gen.topologySettings = new HexMapGenerator.RoadTopologySettings();
                gen.internalSettings = new HexMapGenerator.ChunkStyleSettings();
                gen.specialChunkTypes = new List<SpecialChunkDefinition>();
            }
        }

        if ((object)gen.terrainConfig == null)
        {
            gen.terrainConfig = TerrainGenerationConfig.CreateDefault();
        }
        if (gen.topologySettings == null)
        {
            gen.topologySettings = new HexMapGenerator.RoadTopologySettings();
        }
        if (gen.internalSettings == null)
        {
            gen.internalSettings = new HexMapGenerator.ChunkStyleSettings();
        }

        var report = new SimulationReport
        {
            totalSeeds = count,
            startSeed = firstSeed,
            minPathLength = int.MaxValue,
            maxPathLength = 0,
            minStartChunkRoadHexes = int.MaxValue,
            maxStartChunkRoadHexes = 0,
            minLoops = int.MaxValue,
            maxLoops = 0,
            minCrossroads = int.MaxValue,
            maxCrossroads = 0,
            minSpawners = int.MaxValue,
            maxSpawners = 0
        };

        var totalSw = Stopwatch.StartNew();

        long sumPathLen = 0;
        long sumStartHexes = 0;
        long startChunksEvaluated = 0;
        long sumLoops = 0;
        long sumCrossroads = 0;
        long sumSpawners = 0;

        long sumR12Forest = 0, sumR12Fertile = 0, sumR12Hill = 0, sumR12Mountain = 0, sumR12Sinkhole = 0;
        long sumR34Forest = 0, sumR34Fertile = 0, sumR34Hill = 0, sumR34Mountain = 0, sumR34MtnChunks = 0, sumR34Sinkhole = 0;

        int safeStartChunksCount = Mathf.Max(1, gen.topologySettings.safeStartChunks);
        int minStartRoadRequired = Mathf.Max(1, gen.topologySettings.minStartRoadLength);
        int minPathDistRequired = Mathf.Max(1, gen.minChunkDistance);

        try
        {
            for (int i = 0; i < count; i++)
            {
                int currentSeed = firstSeed + i;
                var seedSw = Stopwatch.StartNew();
                MapGenerationContext ctx = gen.BuildMapData(currentSeed);
                seedSw.Stop();

                var row = EvaluateContext(
                    ctx,
                    currentSeed,
                    minPathDistRequired,
                    safeStartChunksCount,
                    minStartRoadRequired,
                    seedSw.Elapsed.TotalMilliseconds,
                    out int startChunk1Len,
                    out int startChunk2Len);

                report.rows.Add(row);

                if (row.pathSuccess)
                {
                    report.successfulPathGenerations++;
                }
                else
                {
                    report.failedPathSeeds.Add(currentSeed);
                }

                if (row.guaranteesMet)
                {
                    report.guaranteesMetCount++;
                }
                else
                {
                    report.failedGuaranteeSeeds.Add(currentSeed);
                }

                if (row.startZoneValid)
                {
                    report.startZoneValidCount++;
                }
                else
                {
                    report.failedStartZoneSeeds.Add(currentSeed);
                }

                report.minPathLength = Mathf.Min(report.minPathLength, row.mainPathLength);
                report.maxPathLength = Mathf.Max(report.maxPathLength, row.mainPathLength);
                sumPathLen += row.mainPathLength;

                if (startChunk1Len > 0)
                {
                    report.minStartChunkRoadHexes = Mathf.Min(report.minStartChunkRoadHexes, startChunk1Len);
                    report.maxStartChunkRoadHexes = Mathf.Max(report.maxStartChunkRoadHexes, startChunk1Len);
                    sumStartHexes += startChunk1Len;
                    startChunksEvaluated++;
                }
                if (startChunk2Len > 0)
                {
                    report.minStartChunkRoadHexes = Mathf.Min(report.minStartChunkRoadHexes, startChunk2Len);
                    report.maxStartChunkRoadHexes = Mathf.Max(report.maxStartChunkRoadHexes, startChunk2Len);
                    sumStartHexes += startChunk2Len;
                    startChunksEvaluated++;
                }

                report.minLoops = Mathf.Min(report.minLoops, row.loopsCount);
                report.maxLoops = Mathf.Max(report.maxLoops, row.loopsCount);
                sumLoops += row.loopsCount;

                report.minCrossroads = Mathf.Min(report.minCrossroads, row.crossroadsCount);
                report.maxCrossroads = Mathf.Max(report.maxCrossroads, row.crossroadsCount);
                sumCrossroads += row.crossroadsCount;

                report.minSpawners = Mathf.Min(report.minSpawners, row.totalSpawners);
                report.maxSpawners = Mathf.Max(report.maxSpawners, row.totalSpawners);
                sumSpawners += row.totalSpawners;

                sumR12Forest += row.r12Forest;
                sumR12Fertile += row.r12FertileSoil;
                sumR12Hill += row.r12Hill;
                sumR12Mountain += row.r12Mountain;
                sumR12Sinkhole += row.r12Sinkhole;

                sumR34Forest += row.r34Forest;
                sumR34Fertile += row.r34FertileSoil;
                sumR34Hill += row.r34Hill;
                sumR34Mountain += row.r34Mountain;
                sumR34MtnChunks += row.r34MountainChunks;
                sumR34Sinkhole += row.r34Sinkhole;
            }
        }
        finally
        {
            if (createdTempGo && (object)tempGo != null)
            {
                try { DestroyImmediate(tempGo); } catch { }
            }
        }

        totalSw.Stop();

        if (report.minPathLength == int.MaxValue) report.minPathLength = 0;
        if (report.minStartChunkRoadHexes == int.MaxValue) report.minStartChunkRoadHexes = 0;
        if (report.minLoops == int.MaxValue) report.minLoops = 0;
        if (report.minCrossroads == int.MaxValue) report.minCrossroads = 0;
        if (report.minSpawners == int.MaxValue) report.minSpawners = 0;

        float invN = 1f / count;
        report.pathSuccessRatePercent = report.successfulPathGenerations * 100f * invN;
        report.guaranteesSuccessRatePercent = report.guaranteesMetCount * 100f * invN;

        report.avgPathLength = sumPathLen * invN;
        report.avgStartChunkRoadHexes = startChunksEvaluated > 0 ? (float)sumStartHexes / startChunksEvaluated : 0f;
        report.avgLoops = sumLoops * invN;
        report.avgCrossroads = sumCrossroads * invN;
        report.avgSpawners = sumSpawners * invN;

        report.avgR12Forest = sumR12Forest * invN;
        report.avgR12FertileSoil = sumR12Fertile * invN;
        report.avgR12Hill = sumR12Hill * invN;
        report.avgR12Mountain = sumR12Mountain * invN;
        report.avgR12Sinkhole = sumR12Sinkhole * invN;

        report.avgR34Forest = sumR34Forest * invN;
        report.avgR34FertileSoil = sumR34Fertile * invN;
        report.avgR34Hill = sumR34Hill * invN;
        report.avgR34Mountain = sumR34Mountain * invN;
        report.avgR34MountainChunks = sumR34MtnChunks * invN;
        report.avgR34Sinkhole = sumR34Sinkhole * invN;

        report.totalElapsedMs = totalSw.Elapsed.TotalMilliseconds;
        report.avgGenerationTimeMs = report.totalElapsedMs / count;

        return report;
    }

    private static SeedSimulationRow EvaluateContext(
        MapGenerationContext ctx,
        int seed,
        int minPathDistRequired,
        int safeStartChunksCount,
        int minStartRoadRequired,
        double elapsedMs,
        out int startChunk1Len,
        out int startChunk2Len)
    {
        startChunk1Len = 0;
        startChunk2Len = 0;

        var row = new SeedSimulationRow
        {
            seed = seed,
            generationTimeMs = elapsedMs
        };

        var reasons = new List<string>();

        int seqCount = ctx.generatedChunkSequence != null ? ctx.generatedChunkSequence.Count : 0;
        row.mainPathLength = seqCount;
        row.loopsCount = ctx.loops != null ? ctx.loops.Count : 0;
        row.crossroadsCount = ctx.crossroadsChunks != null ? ctx.crossroadsChunks.Count : 0;
        row.totalSpawners = 1 + (ctx.extraSpawnerChunks != null ? ctx.extraSpawnerChunks.Count : 0);

        bool hasPathLength = seqCount >= minPathDistRequired;
        bool hasGuaranteedLoop = row.loopsCount >= 1;

        // Sprawdzenie strefy ochronnej (pierwsze safeStartChunks od końca generatedChunkSequence, czyli najbliżej bazy)
        bool startZoneNoCrossroads = true;
        int minStartHexes = int.MaxValue;

        if (seqCount >= safeStartChunksCount)
        {
            for (int k = 0; k < safeStartChunksCount; k++)
            {
                Vector2Int startChunk = ctx.generatedChunkSequence[seqCount - 1 - k];
                if (ctx.crossroadsChunks.Contains(startChunk))
                {
                    startZoneNoCrossroads = false;
                }
                if (ctx.chunkPaths.TryGetValue(startChunk, out var pData))
                {
                    if (pData.isCrossroads) startZoneNoCrossroads = false;
                    int hCount = pData.internalPath != null ? pData.internalPath.Count : 0;
                    if (k == 0) startChunk1Len = hCount;
                    if (k == 1) startChunk2Len = hCount;
                    minStartHexes = Mathf.Min(minStartHexes, hCount);
                }
                else
                {
                    minStartHexes = 0;
                }
            }
        }
        else
        {
            minStartHexes = 0;
            startZoneNoCrossroads = false;
        }

        if (minStartHexes == int.MaxValue) minStartHexes = 0;
        row.minStartRoadHexes = minStartHexes;
        row.startZoneValid = startZoneNoCrossroads && minStartHexes >= minStartRoadRequired;

        if (!hasPathLength) reasons.Add($"MainPath<{minPathDistRequired} ({seqCount})");
        if (!hasGuaranteedLoop) reasons.Add("Loops<1");
        if (!startZoneNoCrossroads) reasons.Add("CrossroadsInSafeStart");
        if (minStartHexes < minStartRoadRequired) reasons.Add($"StartRoadHexes<{minStartRoadRequired} ({minStartHexes})");

        row.pathSuccess = hasPathLength && hasGuaranteedLoop && row.startZoneValid;

        // Zliczanie zasobów w pierścieniach 1–2 oraz 3–4
        var r34MtnChunkSet = new HashSet<Vector2Int>();

        foreach (var kvp in ctx.worldData)
        {
            Vector2Int chunkCoord = kvp.Key;
            if (chunkCoord == Vector2Int.zero) continue;

            int ring = HexGridMath.GetDistance(Vector2Int.zero, chunkCoord);
            var cells = kvp.Value;

            foreach (var cell in cells.Values)
            {
                if (cell == null || cell.isPath) continue;

                if (ring >= 1 && ring <= 2)
                {
                    switch (cell.feature)
                    {
                        case HexFeatureType.Forest: row.r12Forest++; break;
                        case HexFeatureType.FertileSoil: row.r12FertileSoil++; break;
                        case HexFeatureType.Hill: row.r12Hill++; break;
                        case HexFeatureType.Mountain: row.r12Mountain++; break;
                        case HexFeatureType.Sinkhole: row.r12Sinkhole++; break;
                    }
                }
                else if (ring >= 3 && ring <= 4)
                {
                    switch (cell.feature)
                    {
                        case HexFeatureType.Forest: row.r34Forest++; break;
                        case HexFeatureType.FertileSoil: row.r34FertileSoil++; break;
                        case HexFeatureType.Hill: row.r34Hill++; break;
                        case HexFeatureType.Mountain:
                            row.r34Mountain++;
                            r34MtnChunkSet.Add(chunkCoord);
                            break;
                        case HexFeatureType.Sinkhole: row.r34Sinkhole++; break;
                    }
                }
            }
        }

        row.r34MountainChunks = r34MtnChunkSet.Count;

        bool gForest = row.r12Forest >= 10;
        bool gFertile = row.r12FertileSoil >= 6;
        bool gHill = row.r12Hill >= 6;
        bool gNoR12Mtn = row.r12Mountain == 0;
        bool gR34Mtn = row.r34Mountain >= 10 && row.r34MountainChunks >= 2;

        if (!gForest) reasons.Add($"R12_Forest={row.r12Forest}<10");
        if (!gFertile) reasons.Add($"R12_Fertile={row.r12FertileSoil}<6");
        if (!gHill) reasons.Add($"R12_Hill={row.r12Hill}<6");
        if (!gNoR12Mtn) reasons.Add($"R12_Mountain={row.r12Mountain}!=0");
        if (!gR34Mtn) reasons.Add($"R34_Mountain={row.r34Mountain}({row.r34MountainChunks} chunks)");

        row.guaranteesMet = gForest && gFertile && gHill && gNoR12Mtn && gR34Mtn;
        row.failureReason = string.Join("; ", reasons);

        return row;
    }

    private void OnEnable()
    {
        if (targetGenerator == null)
        {
            targetGenerator = FindFirstObjectByType<HexMapGenerator>();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Symulator Generacji Mapy (Map Generation Simulator)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Symuluje N map przy użyciu BuildMapData(seed) i weryfikuje:\n" +
            "• Długość Ścieżki A (>= 11 chunków) oraz pętli i rozdroży,\n" +
            "• Strefę ochronną (pierwsze 2 chunki od bazy bez rozdroży, >= 6 heksów drogi na każdym),\n" +
            "• Gwarancje surowców w pierścieniach 1–2 (Las >= 10, Gleba >= 6, Wzgórza >= 6, Góry == 0) oraz 3–4 (Góry >= 10 w min. 2 chunkach).",
            MessageType.Info);

        EditorGUILayout.Space(6f);

        targetGenerator = (HexMapGenerator)EditorGUILayout.ObjectField(
            "HexMapGenerator (Opcjonalny)",
            targetGenerator,
            typeof(HexMapGenerator),
            true);

        seedCount = Mathf.Clamp(EditorGUILayout.IntField("Liczba seedów (N)", seedCount), 1, 5000);
        startSeed = Mathf.Max(1, EditorGUILayout.IntField("Seed początkowy", startSeed));

        EditorGUILayout.Space(6f);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"Uruchom Symulację ({seedCount} map)", GUILayout.Height(30f)))
        {
            lastReport = RunBatchSimulation(seedCount, startSeed, targetGenerator);
            lastCsvExportPath = string.Empty;
        }

        GUI.enabled = lastReport != null;
        if (GUILayout.Button("Eksportuj do CSV...", GUILayout.Height(30f), GUILayout.Width(160f)))
        {
            string defaultPath = Path.Combine(Application.dataPath, "..", "MapSimulationReport.csv");
            string chosen = EditorUtility.SaveFilePanel("Zapisz raport CSV", Path.GetDirectoryName(defaultPath), "MapSimulationReport.csv", "csv");
            if (!string.IsNullOrEmpty(chosen))
            {
                lastReport.ExportToCsv(chosen);
                lastCsvExportPath = chosen;
            }
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(lastCsvExportPath))
        {
            EditorGUILayout.HelpBox($"Zapisano raport CSV: {lastCsvExportPath}", MessageType.None);
        }

        if (lastReport == null) return;

        EditorGUILayout.Space(10f);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawReportSummary(lastReport);

        EditorGUILayout.EndScrollView();
    }

    private void DrawReportSummary(SimulationReport r)
    {
        bool allPassed = r.failedPathSeeds.Count == 0 && r.failedGuaranteeSeeds.Count == 0 && r.failedStartZoneSeeds.Count == 0;
        EditorGUILayout.HelpBox(
            allPassed
                ? $"SUKCES: Wszystkie {r.totalSeeds} mapy spełniły 100% kryteriów ścieżek i zasobów!"
                : $"UWAGA: Wykryto naruszenia (Ścieżki: {r.failedPathSeeds.Count}, Zasoby: {r.failedGuaranteeSeeds.Count}).",
            allPassed ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.LabelField("1. Wskaźniki Ogólne i Wydajność", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Przetestowane seedy:", $"{r.startSeed} .. {r.startSeed + r.totalSeeds - 1} (N = {r.totalSeeds})");
        EditorGUILayout.LabelField("Sukces generacji ścieżek:", $"{r.successfulPathGenerations} / {r.totalSeeds} ({r.pathSuccessRatePercent:F1}%)");
        EditorGUILayout.LabelField("Spełnione gwarancje zasobów:", $"{r.guaranteesMetCount} / {r.totalSeeds} ({r.guaranteesSuccessRatePercent:F1}%)");
        EditorGUILayout.LabelField("Poprawna strefa startowa (2 chunki):", $"{r.startZoneValidCount} / {r.totalSeeds}");
        EditorGUILayout.LabelField("Czas całkowity / średni:", $"{r.totalElapsedMs:F1} ms  ({r.avgGenerationTimeMs:F2} ms / mapę)");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("2. Topologia Ścieżek, Rozdroży i Pętli", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Długość Ścieżki A (chunki):", $"min: {r.minPathLength}  |  śr: {r.avgPathLength:F2}  |  max: {r.maxPathLength}");
        EditorGUILayout.LabelField("Długość drogi w pierwszych 2 chunkach:", $"min: {r.minStartChunkRoadHexes}  |  śr: {r.avgStartChunkRoadHexes:F2}  |  max: {r.maxStartChunkRoadHexes} heksów");
        EditorGUILayout.LabelField("Liczba pętli na mapę:", $"min: {r.minLoops}  |  śr: {r.avgLoops:F2}  |  max: {r.maxLoops}");
        EditorGUILayout.LabelField("Liczba rozdroży na mapę:", $"min: {r.minCrossroads}  |  śr: {r.avgCrossroads:F2}  |  max: {r.maxCrossroads}");
        EditorGUILayout.LabelField("Łączna liczba spawnerów:", $"min: {r.minSpawners}  |  śr: {r.avgSpawners:F2}  |  max: {r.maxSpawners}");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("3. Średni Budżet Zasobów w Pierścieniach 1–2 (Cel: Las>=10, Gleba>=6, Wzgórza>=6, Góry==0)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Las (Forest):", $"{r.avgR12Forest:F1} heksów");
        EditorGUILayout.LabelField("Żyzna Gleba (FertileSoil):", $"{r.avgR12FertileSoil:F1} heksów");
        EditorGUILayout.LabelField("Wzgórza (Hill):", $"{r.avgR12Hill:F1} heksów");
        EditorGUILayout.LabelField("Góry (Mountain):", $"{r.avgR12Mountain:F2} heksów");
        EditorGUILayout.LabelField("Zapadliska (Sinkhole):", $"{r.avgR12Sinkhole:F1} heksów");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("4. Średni Budżet Zasobów w Pierścieniach 3–4 (Cel: Góry>=10 w min. 2 chunkach)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Góry (Mountain):", $"{r.avgR34Mountain:F1} heksów (śr. w {r.avgR34MountainChunks:F1} chunkach)");
        EditorGUILayout.LabelField("Las (Forest):", $"{r.avgR34Forest:F1} heksów");
        EditorGUILayout.LabelField("Żyzna Gleba (FertileSoil):", $"{r.avgR34FertileSoil:F1} heksów");
        EditorGUILayout.LabelField("Wzgórza (Hill):", $"{r.avgR34Hill:F1} heksów");
        EditorGUILayout.LabelField("Zapadliska (Sinkhole):", $"{r.avgR34Sinkhole:F1} heksów");

        if (r.failedPathSeeds.Count > 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Seedy z błędem ścieżki:", string.Join(", ", r.failedPathSeeds.Take(30)));
        }

        if (r.failedGuaranteeSeeds.Count > 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Seedy z naruszeniem gwarancji:", string.Join(", ", r.failedGuaranteeSeeds.Take(30)));
        }
    }
}
