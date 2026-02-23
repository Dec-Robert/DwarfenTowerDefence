using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RuneForgeEntity : BuildingEntity
{
    private const string SOURCE_PREFIX = "Forge_";

    [Header("── Konfiguracja Kuźni ────────────────")]
    [Tooltip("Do jakiego typu wież ta kuźnia wysyła moc run? (Można to zmieniać z poziomu UI)")]
    public TowerData infusedTowerData;

    [Header("── Stan (Podgląd) ────────────────────")]
    [SerializeField] private bool dwarfAssigned = false;
    private Citizen reservedDwarf = null;

    // Sloty na runy
    public List<RuneItem> equippedRunes = new List<RuneItem>();
    
    // Unikalne ID źródła dla rejestru modyfikatorów
    private string myRegistrySourceID;

    // =========================================================================
    // INICJALIZACJA I CYKL ŻYCIA
    // =========================================================================

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);
        myRegistrySourceID = SOURCE_PREFIX + gameObject.GetInstanceID();

        TryReserveDwarf();
        RecalculateSlotsAndModifiers();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ReleaseDwarf();
        
        // Zwróć runy do ekwipunku gracza, żeby nie przepadły po zburzeniu kuźni
        if (RuneManager.Instance != null)
        {
            foreach (var rune in equippedRunes)
                RuneManager.Instance.playerRunes.Add(rune);
            RuneManager.Instance.NotifyInventoryChanged(); 
        }

        // Wyczyść bonusy
        GlobalModifierRegistry.Instance?.Unregister(myRegistrySourceID);
    }

    // Nadpisujemy ForceRescan (wywoływane, gdy obok postawi się nowy budynek)
    public new void ForceRescan()
    {
        base.ForceRescan();
        RecalculateSlotsAndModifiers();
    }

    // =========================================================================
    // OBSŁUGA PRACOWNIKA (KRASNOLUD)
    // =========================================================================

    private void TryReserveDwarf()
    {
        if (CitizenManager.Instance == null) return;

        reservedDwarf = CitizenManager.Instance.citizens.Find(
            c => c.race == Race.Dwarves && c.workState == WorkState.Idle);

        if (reservedDwarf != null)
        {
            reservedDwarf.workState = WorkState.Assigned;
            dwarfAssigned = true;
            Debug.Log($"[RuneForge] Krasnolud przypisany do {name}.");
        }
        else
        {
            dwarfAssigned = false;
            Debug.LogWarning($"[RuneForge] Brak wolnych Krasnoludów! {name} nie będzie działać.");
        }
    }

    private void ReleaseDwarf()
    {
        if (reservedDwarf == null || !dwarfAssigned) return;
        reservedDwarf.workState = WorkState.Idle;
        reservedDwarf = null;
        dwarfAssigned = false;
    }

    // =========================================================================
    // SLOTY I SĄSIEDZTWO
    // =========================================================================

    public int GetMaxSlots()
    {
        int baseSlots = 1;
        int runeTowersCount = 0;

        // Szukamy wież runicznych w sąsiednich heksach
        HexCell myCell = GetComponentInParent<HexCell>();
        if (myCell != null && HexMapVisualizer.Instance != null)
        {
            var neighbors = HexGridMath.GetNeighbors(myCell.localCoord);
            foreach (var n in neighbors)
            {
                HexCell neighborCell = HexMapVisualizer.Instance.GetHexCell(myCell.chunkCoord, n);
                if (neighborCell != null)
                {
                    if (neighborCell.GetComponentInChildren<RuneTowerEntity>() != null)
                        runeTowersCount++;
                }
            }
        }

        // Maksymalnie 4 sloty łącznie (1 baza + max 3 wieże)
        return Mathf.Min(4, baseSlots + runeTowersCount);
    }

    // =========================================================================
    // ZARZĄDZANIE RUNAMI (API dla UI)
    // =========================================================================

    public bool CanModifyRunes()
    {
        // Tylko za dnia i tylko jeśli mamy krasnoluda
        return dwarfAssigned && 
               GameManager.Instance != null && 
               GameManager.Instance.currentGameState == GameManager.gameStates.PreparePhase;
    }

    public bool EquipRune(RuneItem rune)
    {
        if (!CanModifyRunes()) return false;
        if (equippedRunes.Count >= GetMaxSlots()) return false;
        if (!RuneManager.Instance.playerRunes.Contains(rune)) return false;

        RuneManager.Instance.playerRunes.Remove(rune);
        equippedRunes.Add(rune);
        
        RecalculateSlotsAndModifiers();
        RuneManager.Instance.NotifyInventoryChanged();
        return true;
    }

    public bool UnequipRune(RuneItem rune)
    {
        if (!CanModifyRunes()) return false;
        if (!equippedRunes.Contains(rune)) return false;

        equippedRunes.Remove(rune);
        RuneManager.Instance.playerRunes.Add(rune);

        RecalculateSlotsAndModifiers();
        RuneManager.Instance.NotifyInventoryChanged();
        return true;
    }

    public void SetInfusedTower(TowerData tower)
    {
        infusedTowerData = tower;
        RecalculateSlotsAndModifiers();
    }

    // =========================================================================
    // PRZELICZANIE I REJESTRACJA MODYFIKATORÓW
    // =========================================================================

    private void RecalculateSlotsAndModifiers()
    {
        // Jeśli limit spadł (ktoś zburzył wieżę runiczną obok), wypluj nadmiarowe runy do plecaka
        int maxSlots = GetMaxSlots();
        while (equippedRunes.Count > maxSlots)
        {
            var runeToEject = equippedRunes.Last();
            equippedRunes.Remove(runeToEject);
            if (RuneManager.Instance != null) RuneManager.Instance.playerRunes.Add(runeToEject);
        }

        if (GlobalModifierRegistry.Instance == null) return;

        // Czyścimy stare wpisy
        GlobalModifierRegistry.Instance.Unregister(myRegistrySourceID);

        // Jeśli kuźnia nie ma celu (nie zinfuzowana) lub brak krasnoluda – nie wysyła buffów
        if (infusedTowerData == null || !dwarfAssigned) return;

        // Sumujemy wartości run
        // Słownik: statystyka -> (suma Flat, suma Percent)
        var sums = new Dictionary<TowerStatType, (float flat, float percent)>();

        foreach (var rune in equippedRunes)
        {
            AddStatToSum(sums, rune.definition.primaryStat, rune.definition.valueType, rune.primaryValue);

            if (rune.hasPenalty)
            {
                AddStatToSum(sums, rune.penaltyStat, rune.penaltyValueType, rune.penaltyValue);
            }
        }

        // Rejestrujemy zsumowane wartości w GlobalModifierRegistry
        foreach (var kvp in sums)
        {
            TowerStatType stat = kvp.Key;
            float totalFlat = kvp.Value.flat;
            float totalPercent = kvp.Value.percent;

            // Tworzymy modyfikator skierowany TYLKO na wskazany typ wieży (PerTowerData)
            // Percent zamieniamy na mnożnik (np. +20% to 1.2f, -30% to 0.7f)
            float multiplier = 1f + (totalPercent / 100f);

            var modifier = new StatModifier(
                myRegistrySourceID, 
                stat, 
                infusedTowerData, 
                multiplier: multiplier, 
                flat: totalFlat);

            GlobalModifierRegistry.Instance.Register(modifier);
        }

        // Wymuś odświeżenie statystyk na wszystkich wieżach przypisanego typu
        foreach (var building in BuildingRegistry.Instance.AllBuildings)
        {
            if (building is TowerEntity tower && tower.data == infusedTowerData)
            {
                tower.RecalculateStats();
            }
        }
    }

    private void AddStatToSum(Dictionary<TowerStatType, (float flat, float pct)> dict, RuneStatType runeStat, RuneValueType type, float value)
    {
        TowerStatType towerStat = MapRuneStatToTowerStat(runeStat);

        if (!dict.ContainsKey(towerStat)) dict[towerStat] = (0f, 0f);

        var current = dict[towerStat];
        if (type == RuneValueType.Flat) current.flat += value;
        else current.pct += value;
        
        dict[towerStat] = current;
    }

    private TowerStatType MapRuneStatToTowerStat(RuneStatType rStat)
    {
        return rStat switch
        {
            RuneStatType.Range => TowerStatType.Range,
            RuneStatType.Damage => TowerStatType.Damage,
            RuneStatType.FireRate => TowerStatType.FireRate,
            RuneStatType.ArmorPenetration => TowerStatType.ArmorPenetration,
            RuneStatType.MagicPenetration => TowerStatType.MagicPenetration,
            RuneStatType.CriticalChance => TowerStatType.CriticalChance,
            RuneStatType.CriticalDamage => TowerStatType.CriticalDamage,
            _ => TowerStatType.Damage // Fallback
        };
    }
}