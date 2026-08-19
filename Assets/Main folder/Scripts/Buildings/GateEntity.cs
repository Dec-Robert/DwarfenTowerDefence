using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Frontier gate entity acting as the first line of defense.
/// Handles enemy collisions with immediate or true damage based on enemy rank.
/// Can be rebuilt during the day phases using resources.
/// </summary>
public class GateEntity : MonoBehaviour
{
    [Header("HP Bramy")]
    [Tooltip("Maksymalne HP bramy (resetuje się przy odbudowie)")]
    public float maxHP = 5f;

    [Tooltip("Ile HP traci brama przy każdym uderzeniu Elity")]
    public float hpCostPerElite = 2f;

    [Tooltip("Ile HP traci brama przy każdym uderzeniu Bossa")]
    public float hpCostPerBoss = 1f;

    [Header("Koszt Odbudowy")]
    [Tooltip("Bazowy koszt odbudowy w złocie")]
    public float baseRebuildCostGold = 50f;

    [Tooltip("O ile złota rośnie koszt za każdą falę")]
    public float rebuildCostGrowthPerWave = 10f;

    [Header("Wizualizacja")]
    [Tooltip("Obiekt pokazywany gdy brama jest aktywna")]
    public GameObject activeVisual;

    [Tooltip("Obiekt pokazywany gdy brama jest zniszczona (ruina)")]
    public GameObject ruinedVisual;

    [HideInInspector] public Vector2Int chunkA;
    [HideInInspector] public Vector2Int chunkB;

    public bool IsDestroyed { get; private set; }
    public float CurrentHP { get; private set; }
    public float MaxHP => maxHP;

    public event Action<GateEntity> OnGateDestroyed;
    public event Action<GateEntity> OnGateRebuilt;

    private void Awake()
    {
        CurrentHP = maxHP;
        IsDestroyed = false;
        RefreshVisuals();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = IsDestroyed ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 1.5f, 0.3f));

        Handles.Label(
            transform.position + Vector3.up * 2f,
            $"Gate [{chunkA}↔{chunkB}]\nHP: {CurrentHP}/{maxHP}");
    }
#endif

    private void OnTriggerEnter(Collider other)
    {
        if (IsDestroyed) return;

        var stats = other.GetComponent<EnemyStats>();
        if (stats == null) return;

        HandleEnemyCollision(stats);
    }

    private void HandleEnemyCollision(EnemyStats enemy)
    {
        switch (enemy.rank)
        {
            case EnemyRank.Normal:
                ApplyGateDamage(1f);
                KillEnemy(enemy);
                break;

            case EnemyRank.Elite:
                ApplyGateDamage(hpCostPerElite);
                enemy.TakeDamage(
                    enemy.GetMaxHealth() * 0.20f,
                    DamageType.True, 0f, 0f, false, 100f);
                break;

            case EnemyRank.Boss:
                ApplyGateDamage(hpCostPerBoss);
                enemy.TakeDamage(
                    enemy.GetMaxHealth() * 0.05f,
                    DamageType.True, 0f, 0f, false, 100f);
                break;
        }
    }

    private void ApplyGateDamage(float amount)
    {
        if (IsDestroyed) return;

        CurrentHP -= amount;

        if (CurrentHP <= 0f)
        {
            CurrentHP = 0f;
            IsDestroyed = true;
            RefreshVisuals();
            OnGateDestroyed?.Invoke(this);
        }
    }

    private void KillEnemy(EnemyStats enemy)
    {
        enemy.TakeDamage(
            enemy.GetMaxHealth(),
            DamageType.True, 0f, 0f, false, 100f);
    }

    public int GetRebuildCost()
    {
        var wave = TimePhaseManager.Instance != null ? TimePhaseManager.Instance.CurrentDay : 0;
        return Mathf.RoundToInt(baseRebuildCostGold + rebuildCostGrowthPerWave * wave);
    }

    public bool TryRebuild()
    {
        if (!IsDestroyed)
        {
            return false;
        }

        if (TimePhaseManager.Instance != null && 
            TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night)
        {
            return false;
        }

        var cost = GetRebuildCost();
        if (ResourceManager.Instance == null ||
            !ResourceManager.Instance.CanAfford(ResourceType.Gold, cost))
        {
            return false;
        }

        ResourceManager.Instance.SpendResource(ResourceType.Gold, cost);

        CurrentHP = maxHP;
        IsDestroyed = false;
        RefreshVisuals();

        OnGateRebuilt?.Invoke(this);
        return true;
    }

    private void RefreshVisuals()
    {
        if (activeVisual != null) activeVisual.SetActive(!IsDestroyed);
        if (ruinedVisual != null) ruinedVisual.SetActive(IsDestroyed);

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = !IsDestroyed;
    }
}