using UnityEngine;
using System;
using System.Collections.Generic;

public class DebugCheatManager : MonoBehaviour
{
    void Update()
    {
        // F1 - Zabij wszystkich wrogów
        if (Input.GetKeyDown(KeyCode.F1))
        {
            KillAllEnemies();
        }

        // F2 - Dodaj surowce
        if (Input.GetKeyDown(KeyCode.F2))
        {
            AddAllResources();
        }

        // F3 + I - Odblokuj WSZYSTKIE pola (Locked, Unlocked, Scouting)
        if (Input.GetKeyDown(KeyCode.F3) && Input.GetKey(KeyCode.I))
        {
            CheatUnlockAll();
            return; 
        }

        // F3 - Odblokuj tylko pola w stanie Scouting lub Unlocked (za darmo)
        if (Input.GetKeyDown(KeyCode.F3))
        {
            CheatUnlockScouting();
        }

        // --- NOWE: F4 - SYSTEM RUN ---
        HandleRuneCheats();
    }

    // =========================================================================
    // F1 – Zabij wrogów
    // =========================================================================

    void KillAllEnemies()
    {
        EnemyStats[] allEnemies = FindObjectsOfType<EnemyStats>();

        foreach (var enemy in allEnemies)
        {
            if (enemy != null)
                enemy.TakeDamage(999999f, DamageType.Physical, 100, 100, true, 1000);
        }

        Debug.Log($"<color=red>[DEBUG] Zabito {allEnemies.Length} wrogów.</color>");
    }

    // =========================================================================
    // F2 – Dodaj surowce
    // =========================================================================

    void AddAllResources()
    {
        if (ResourceManager.Instance == null) return;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            ResourceManager.Instance.AddResource(type, 200);

        Debug.Log("<color=green>[DEBUG] Dodano po 200 sztuk każdego surowca.</color>");
    }

    // =========================================================================
    // F3 – Odblokuj pola w stanie Scouting i Unlocked
    // =========================================================================

    /// <summary>
    /// Natychmiastowo kończy wszystkie aktywne misje zwiadowcze (Scouting)
    /// oraz odkrywa za darmo wszystkie pola w stanie Unlocked.
    /// Pola Locked pozostają bez zmian.
    /// </summary>
    void CheatUnlockScouting()
    {
        var expansion = FindObjectOfType<MapExpansionManager>();
        if (expansion == null)
        {
            Debug.LogWarning("[DEBUG] Brak MapExpansionManager na scenie.");
            return;
        }

        var fogManager = expansion.fogManager;
        int count = 0;

        // Kopia – będziemy modyfikować słownik pośrednio przez CompleteMission
        var chunksCopy = new List<KeyValuePair<Vector2Int, ChunkStateData>>(expansion.activeChunks);

        foreach (var kvp in chunksCopy)
        {
            var coord = kvp.Key;
            var data  = kvp.Value;

            // Pola w trakcie scoutingu – zakończ misję natychmiastowo
            if (data.state == ChunkState.Scouting)
            {
                ForceCompleteChunk(expansion, fogManager, coord, data);
                count++;
            }
            // Pola gotowe do scoutingu (Unlocked) – odblokuj za darmo
            else if (data.state == ChunkState.Unlocked)
            {
                ForceCompleteChunk(expansion, fogManager, coord, data);
                count++;
            }
        }

        // Anuluj aktywne misje (zwiadowcy wracają, centra zwalniają się)
        foreach (var mission in new List<ScoutingMission>(expansion.activeMissions))
            mission.sourceCenter?.OnMissionComplete(mission);
        expansion.activeMissions.Clear();

        Debug.Log($"<color=cyan>[DEBUG F3] Odblokowano {count} pól (Scouting + Unlocked).</color>");
    }

    // =========================================================================
    // F3 + I – Odblokuj WSZYSTKIE pola (w tym Locked)
    // =========================================================================

    /// <summary>
    /// Natychmiastowo ustawia wszystkie znane chunki (w tym Locked) na FullyUnlocked
    /// i odkrywa mgłę nad nimi.
    /// </summary>
    void CheatUnlockAll()
    {
        var expansion = FindObjectOfType<MapExpansionManager>();
        if (expansion == null)
        {
            Debug.LogWarning("[DEBUG] Brak MapExpansionManager na scenie.");
            return;
        }

        var fogManager = expansion.fogManager;
        int count = 0;

        var chunksCopy = new List<KeyValuePair<Vector2Int, ChunkStateData>>(expansion.activeChunks);

        foreach (var kvp in chunksCopy)
        {
            var coord = kvp.Key;
            var data  = kvp.Value;

            if (data.state == ChunkState.FullyUnlocked) continue; // już odblokowane

            ForceCompleteChunk(expansion, fogManager, coord, data);
            count++;
        }

        // Anuluj aktywne misje
        foreach (var mission in new List<ScoutingMission>(expansion.activeMissions))
            mission.sourceCenter?.OnMissionComplete(mission);
        expansion.activeMissions.Clear();

        Debug.Log($"<color=magenta>[DEBUG F3+I] Odblokowano {count} pól (wszystkie stany).</color>");
    }

    // =========================================================================
    // F4 – Dodawanie Run (Ekwipunek)
    // =========================================================================

    void HandleRuneCheats()
    {
        // Sprawdzamy czy wciśnięto F4
        if (Input.GetKeyDown(KeyCode.F4))
        {
            // Sprawdzamy, czy wciśnięto dodatkowo jakiś modyfikator
            // (Najwygodniej jest trzymać literę i kliknąć F4)
            if (Input.GetKey(KeyCode.Y))
                AddDebugRunes(5, RuneRarity.Common);
            else if (Input.GetKey(KeyCode.U))
                AddDebugRunes(5, RuneRarity.Uncommon);
            else if (Input.GetKey(KeyCode.I))
                AddDebugRunes(5, RuneRarity.Rare);
            else if (Input.GetKey(KeyCode.O))
                AddDebugRunes(5, RuneRarity.Legendary);
            else if (Input.GetKey(KeyCode.P))
                AddDebugRunes(5, RuneRarity.Cursed);
            else
                AddDebugRunes(5, null); // Samo F4 = całkowicie losowe wg bazowych szans
        }
    }

    void AddDebugRunes(int count, RuneRarity? forcedRarity)
    {
        if (RuneManager.Instance == null)
        {
            Debug.LogWarning("[DEBUG] Brak RuneManager na scenie.");
            return;
        }

        int addedCount = 0;

        for (int i = 0; i < count; i++)
        {
            // Generujemy runę, używając wymuszonej rzadkości (lub null, żeby wylosować z puli)
            RuneItem newRune = RuneManager.Instance.GenerateRandomRune(forcedRarity);
            
            if (newRune != null)
            {
                RuneManager.Instance.playerRunes.Add(newRune);
                addedCount++;
            }
        }

        // Powiadamiamy interfejs (np. Menu Kuźni), żeby się odświeżył, jeśli jest otwarty
        RuneManager.Instance.NotifyInventoryChanged();

        string rarityStr = forcedRarity.HasValue ? forcedRarity.Value.ToString() : "Mieszanych (Losowych)";
        Debug.Log($"<color=magenta>[DEBUG F4] Wygenerowano i dodano do ekwipunku {addedCount} run typu: {rarityStr}.</color>");
    }
    
    // =========================================================================
    // Pomocnicza – wymusza FullyUnlocked na konkretnym chunku
    // =========================================================================

    private void ForceCompleteChunk(MapExpansionManager expansion,
                                    FogOfWarManager fogManager,
                                    Vector2Int coord,
                                    ChunkStateData data)
    {
        data.state                    = ChunkState.FullyUnlocked;
        data.settlementDaysRemaining  = 0;
        data.discoveredOnDay          = GameManager.Instance?.waveNumber ?? 0;

        fogManager?.RevealChunk(coord);
    }
}