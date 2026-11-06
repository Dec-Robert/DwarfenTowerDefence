using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Wave_Day_X", menuName = "Game/Enemies & Waves/Predefined Wave")]
public class WaveDefinition : ScriptableObject
{
    [Header("Konfiguracja Dnia i Komunikatu")]
    public int dayNumber = 1;
    public string waveMessage;

    [Header("Ekonomia Fali (Ether)")]
    [Tooltip("Maksymalna pula eteru do zdobycia ze zwykłych wrogów w tej fali (np. Fala 1 = 3, Fala 2 = 4, Fala 3 = 6)")]
    public int etherPool = 3;

    [Header("Parametry Spawnu i Skalowania Fali")]
    [Tooltip("Jeśli <= 0, używany jest domyślny nightSpawnInterval w EnemySpawner")]
    public float customSpawnInterval = 0f;

    [Tooltip("Legacy: w zdefiniowanych falach statystyki wynikają z wybranego Tieru. Fale po ostatniej zdefiniowanej używają skalowania 1.1^a")]
    public float hpMultiplier = 1f;

    [Tooltip("Legacy: w zdefiniowanych falach statystyki wynikają z wybranego Tieru")]
    public float speedMultiplier = 1f;

    [Tooltip("Czy wymieszać kolejność spawnowania wrogów ze wszystkich grup")]
    public bool shuffleEnemies = false;

    [Tooltip("Czy pozwalać na losowe pojawianie się Elit zgodnie z szansą w EnemyData")]
    public bool allowRandomElites = false;

    [Header("Kontrola Elit")]
    [Tooltip("Maksymalna liczba elit w fali (0 = bez limitu, zgodnie z szansą)")]
    public int maxElitesCount = 0;

    [System.Serializable]
    public struct WaveEntry
    {
        public EnemyData enemy;

        [Range(1, 4)]
        [Tooltip("Tier wroga (1 - 4) określający jego statystyki, prefab i ikonkę")]
        public int tier;

        public int count;

        [Tooltip("Wymusza rangę Elite dla całej tej grupy")]
        public bool forceElite;

        /// <summary>
        /// Zwraca poprawny numer tieru (1 - 4), domyślnie 1 gdy pole nie zostało zainicjalizowane.
        /// </summary>
        public int Tier => tier > 0 ? Mathf.Clamp(tier, 1, 4) : 1;
    }

    [System.Serializable]
    public class SpawnGroup
    {
        public string groupName = "Vanguard";
        [Tooltip("Czas w sekundach oczekiwania przed rozpoczęciem spawnowania tej grupy")]
        public float delayBefore = 0f;
        [Tooltip("Odstęp między spawnami wewnątrz grupy (jeśli <= 0, użyty zostanie interwał fali)")]
        public float spawnInterval = 0f;
        [Range(0f, 1f)]
        [Tooltip("Losowe odchylenie interwału (+/- jitter) wewnątrz grupy")]
        public float intervalJitter = 0.2f;
        [Tooltip("Czy przemieszać jednostki wewnątrz tej grupy")]
        public bool shuffleInside = false;

        public List<WaveEntry> enemies = new List<WaveEntry>();
    }

    [Header("Skład Fali (Kompatybilność / Płaska Lista)")]
    public List<WaveEntry> enemies = new List<WaveEntry>();

    [Header("Zaawansowane Grupy Spawnu (Sekwencje)")]
    [Tooltip("Jeśli lista zawiera grupy, Spawner przetwarza je sekwencyjnie. Jeśli pusta, używa listy 'enemies'.")]
    public List<SpawnGroup> spawnGroups = new List<SpawnGroup>();

    /// <summary>
    /// Zwraca listę wszystkich wpisów jednostek (z grup jeśli zdefiniowane, lub z listy płaskiej).
    /// </summary>
    public List<WaveEntry> GetAllEntries()
    {
        if (spawnGroups != null && spawnGroups.Count > 0)
        {
            var list = new List<WaveEntry>();
            foreach (var group in spawnGroups)
            {
                if (group != null && group.enemies != null)
                {
                    list.AddRange(group.enemies);
                }
            }
            return list;
        }
        return enemies ?? new List<WaveEntry>();
    }

    /// <summary>
    /// Synchronizuje płaską listę 'enemies' na podstawie zawartości 'spawnGroups' dla pełnej kompatybilności wstecznej.
    /// </summary>
    public void SyncFlattenedEnemiesList()
    {
        if (spawnGroups != null && spawnGroups.Count > 0)
        {
            enemies = GetAllEntries();
        }
    }

    /// <summary>
    /// Oblicza łączny Threat Budget dla fali.
    /// Wzór: sum(BaseThreatCost * TierMultiplier * EliteMultiplier * SpecialAbilityMultiplier)
    /// </summary>
    public float CalculateThreatBudget(float t1Mult = 1.0f, float t2Mult = 2.2f, float t3Mult = 4.5f, float t4Mult = 8.0f, float eliteMult = 2.0f)
    {
        float total = 0f;
        var all = GetAllEntries();
        foreach (var entry in all)
        {
            if (entry.enemy == null || entry.count <= 0) continue;
            float baseThreat = entry.enemy.threatCost > 0 ? entry.enemy.threatCost : 1f;

            float tierMult = t1Mult;
            switch (entry.Tier)
            {
                case 1: tierMult = t1Mult; break;
                case 2: tierMult = t2Mult; break;
                case 3: tierMult = t3Mult; break;
                case 4: tierMult = t4Mult; break;
            }

            // Specjalna zdolność: Blob dzieli się na 2 mniejsze jednostki (dodatkowy mnożnik ~1.35)
            float abilityMult = 1f;
            if (!string.IsNullOrEmpty(entry.enemy.name) && entry.enemy.name.IndexOf("Blob", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                abilityMult = 1.35f;
            }

            float eMult = entry.forceElite ? eliteMult : 1f;
            total += baseThreat * tierMult * eMult * abilityMult * entry.count;
        }

        return total;
    }

    /// <summary>
    /// Oblicza średni tier jednostek w fali.
    /// </summary>
    public float GetAverageTier()
    {
        var all = GetAllEntries();
        int totalUnits = 0;
        int weightedTier = 0;
        foreach (var entry in all)
        {
            if (entry.enemy == null || entry.count <= 0) continue;
            totalUnits += entry.count;
            weightedTier += entry.Tier * entry.count;
        }
        return totalUnits > 0 ? (float)weightedTier / totalUnits : 1.0f;
    }

    /// <summary>
    /// Szacuje całkowity czas trwania spawnowania fali w sekundach.
    /// </summary>
    public float GetEstimatedSpawnDuration()
    {
        if (spawnGroups != null && spawnGroups.Count > 0)
        {
            float duration = 0f;
            foreach (var g in spawnGroups)
            {
                if (g == null) continue;
                duration += Mathf.Max(0f, g.delayBefore);
                int count = 0;
                if (g.enemies != null)
                {
                    foreach (var e in g.enemies) count += Mathf.Max(0, e.count);
                }
                float interval = g.spawnInterval > 0f ? g.spawnInterval : (customSpawnInterval > 0f ? customSpawnInterval : 1.0f);
                duration += count * interval;
            }
            return duration;
        }

        int total = 0;
        if (enemies != null)
        {
            foreach (var e in enemies) total += Mathf.Max(0, e.count);
        }
        float defInterval = customSpawnInterval > 0f ? customSpawnInterval : 1.0f;
        return total * defInterval;
    }
}
