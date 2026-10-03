using UnityEngine;
using System;
using System.Collections.Generic;

public class DebugCheatManager : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            KillAllEnemies();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            AddAllResources();
        }

        if (Input.GetKeyDown(KeyCode.F3) && Input.GetKey(KeyCode.I))
        {
            CheatUnlockAll();
            return;
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            CheatUnlockScouting();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            AdvancePhaseNormally();
        }

        if (Input.GetKeyDown(KeyCode.F7))
        {
            ForceAdvancePhase();
        }

        HandleRuneCheats();
    }

    private void AdvancePhaseNormally()
    {
        if (TimePhaseManager.Instance == null) return;

        if (TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Morning ||
            TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Dusk)
        {
            TimePhaseManager.Instance.ChangeCurrentPhase();
        }
    }

    private void ForceAdvancePhase()
    {
        if (TimePhaseManager.Instance == null) return;

        if (TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night)
        {
            KillAllEnemies();
            var spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.StopAllCoroutines();
            }
        }

        TimePhaseManager.Instance.ChangeCurrentPhase();
    }

    private void KillAllEnemies()
    {
        EnemyStats[] allEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);

        foreach (var enemy in allEnemies)
        {
            if (enemy != null)
            {
                enemy.TakeDamage(999999f, DamageType.Physical, 100, 100, true, 1000);
            }
        }
    }

    private void AddAllResources()
    {
        if (ResourceManager.Instance == null) return;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            ResourceManager.Instance.AddResource(type, 200);
        }
    }

    private void CheatUnlockScouting()
    {
        var expansion = MapExpansionManager.Instance;
        if (expansion == null) return;

        var coords = new List<Vector2Int>(expansion.activeChunks.Keys);
        foreach (var coord in coords)
        {
            var data = expansion.activeChunks[coord];
            if (data.baseState == ChunkState.Surveying || data.baseState == ChunkState.Borderlands)
            {
                expansion.SetChunkBaseState(coord, ChunkState.Outskirts);
            }
        }
    }

    private void CheatUnlockAll()
    {
        var expansion = MapExpansionManager.Instance;
        if (expansion == null) return;

        var coords = new List<Vector2Int>(expansion.activeChunks.Keys);
        foreach (var coord in coords)
        {
            expansion.SetChunkBaseState(coord, ChunkState.Settled);
        }
    }

    private void HandleRuneCheats()
    {
        if (Input.GetKeyDown(KeyCode.F4))
        {
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
                AddDebugRunes(5, null);
        }
    }

    private void AddDebugRunes(int count, RuneRarity? forcedRarity)
    {
        if (RuneManager.Instance == null) return;

        for (int i = 0; i < count; i++)
        {
            RuneItem newRune = RuneManager.Instance.GenerateRandomRune(forcedRarity);
            if (newRune != null)
            {
                RuneManager.Instance.playerRunes.Add(newRune);
            }
        }

        RuneManager.Instance.NotifyInventoryChanged();
    }
}