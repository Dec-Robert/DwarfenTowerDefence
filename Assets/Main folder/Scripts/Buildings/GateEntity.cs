using UnityEngine;
using System;

/// <summary>
/// Brama graniczna między chunkami — pierwsza linia obrony przed falami wrogów.
///
/// ZACHOWANIE:
///   Normal  → ginie natychmiastowo (true dmg = maxHP), brama traci 1 HP
///   Elite   → brama traci hpCostPerElite HP, elita traci 20% maxHP (true dmg)
///   Boss    → brama traci hpCostPerBoss HP,  boss traci 5%  maxHP (true dmg)
///
/// Po zniszczeniu brama nie blokuje wrogów (kolizja wyłączona).
/// Gracz może ją odbudować w fazie dziennej za surowce (rosnący koszt).
///
/// SETUP PREFABU:
///   - Collider ustawiony jako Trigger, obejmujący szerokość ścieżki
///   - Enemy prefab musi mieć Collider (niekoniecznie Trigger) i Rigidbody (kinematic OK)
/// </summary>
public class GateEntity : MonoBehaviour
{
    // =========================================================================
    // KONFIGURACJA
    // =========================================================================

    [Header("HP Bramy")]
    [Tooltip("Maksymalne HP bramy (resetuje się przy odbudowie)")]
    public float maxHP = 5f;

    [Tooltip("Ile HP traci brama przy każdym uderzeniu Elity")]
    public float hpCostPerElite = 2f;

    [Tooltip("Ile HP traci brama przy każdym uderzeniu Bossa")]
    public float hpCostPerBoss  = 1f;

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

    // =========================================================================
    // STAN
    // =========================================================================

    private float currentHP;
    private bool  isDestroyed;

    /// <summary>Chunki między którymi stoi ta brama (A = bliżej bazy, B = frontier).</summary>
    [HideInInspector] public Vector2Int chunkA;
    [HideInInspector] public Vector2Int chunkB;

    // =========================================================================
    // EVENTY
    // =========================================================================

    public event Action<GateEntity> OnGateDestroyed;
    public event Action<GateEntity> OnGateRebuilt;

    // =========================================================================
    // UNITY
    // =========================================================================

    private void Awake()
    {
        currentHP   = maxHP;
        isDestroyed = false;
        RefreshVisuals();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDestroyed) return;

        var stats = other.GetComponent<EnemyStats>();
        if (stats == null) return;

        HandleEnemyCollision(stats);
    }

    // =========================================================================
    // LOGIKA KOLIZJI
    // =========================================================================

    private void HandleEnemyCollision(EnemyStats enemy)
    {
        switch (enemy.rank)
        {
            case EnemyRank.Normal:
                // Brama zabija normalnego wroga natychmiastowo i traci 1 HP
                ApplyGateDamage(1f);
                KillEnemy(enemy);
                break;

            case EnemyRank.Elite:
                // Brama traci X HP, elita traci 20% maxHP (true dmg)
                ApplyGateDamage(hpCostPerElite);
                enemy.TakeDamage(
                    enemy.GetMaxHealth() * 0.20f,
                    DamageType.True, 0f, 0f, false, 100f);
                break;

            case EnemyRank.Boss:
                // Brama traci X HP, boss traci 5% maxHP (true dmg)
                ApplyGateDamage(hpCostPerBoss);
                enemy.TakeDamage(
                    enemy.GetMaxHealth() * 0.05f,
                    DamageType.True, 0f, 0f, false, 100f);
                break;
        }
    }

    private void ApplyGateDamage(float amount)
    {
        if (isDestroyed) return;

        currentHP -= amount;

        if (currentHP <= 0f)
        {
            currentHP   = 0f;
            isDestroyed = true;
            RefreshVisuals();
            OnGateDestroyed?.Invoke(this);
            Debug.Log($"[Gate] Brama między {chunkA} a {chunkB} zniszczona.");
        }
    }

    private void KillEnemy(EnemyStats enemy)
    {
        // True dmg = maxHP gwarantuje śmierć bez omijania skill'ów
        enemy.TakeDamage(
            enemy.GetMaxHealth(),
            DamageType.True, 0f, 0f, false, 100f);
    }

    // =========================================================================
    // ODBUDOWA
    // =========================================================================

    /// <summary>
    /// Zwraca aktualny koszt odbudowy w złocie (rośnie z każdą falą).
    /// </summary>
    public int GetRebuildCost()
    {
        int wave = GameManager.Instance != null ? GameManager.Instance.waveNumber : 0;
        return Mathf.RoundToInt(baseRebuildCostGold + rebuildCostGrowthPerWave * wave);
    }

    /// <summary>
    /// Próbuje odbudować bramę. Zwraca true jeśli się udało.
    /// Odbudowa możliwa tylko w fazie dziennej (PreparePhase).
    /// </summary>
    public bool TryRebuild()
    {
        if (!isDestroyed)
        {
            Debug.LogWarning("[Gate] Brama nie jest zniszczona — odbudowa zbędna.");
            return false;
        }

        // Tylko za dnia
        if (GameManager.Instance == null ||
            GameManager.Instance.currentGameState != GameManager.gameStates.PreparePhase)
        {
            Debug.Log("[Gate] Odbudowa możliwa tylko w fazie dziennej.");
            return false;
        }

        int cost = GetRebuildCost();
        if (ResourceManager.Instance == null ||
            !ResourceManager.Instance.CanAfford(ResourceType.Gold, cost))
        {
            Debug.Log($"[Gate] Za mało złota. Potrzeba: {cost}");
            return false;
        }

        ResourceManager.Instance.SpendResource(ResourceType.Gold, cost);

        currentHP   = maxHP;
        isDestroyed = false;
        RefreshVisuals();

        OnGateRebuilt?.Invoke(this);
        Debug.Log($"[Gate] Brama między {chunkA} a {chunkB} odbudowana. Koszt: {cost} złota.");
        return true;
    }

    // =========================================================================
    // STAN PUBLICZNY
    // =========================================================================

    public bool IsDestroyed => isDestroyed;
    public float CurrentHP  => currentHP;
    public float MaxHP      => maxHP;

    // =========================================================================
    // HELPERS
    // =========================================================================

    private void RefreshVisuals()
    {
        if (activeVisual  != null) activeVisual.SetActive(!isDestroyed);
        if (ruinedVisual  != null) ruinedVisual.SetActive(isDestroyed);

        // Wyłącz trigger gdy zniszczona (wrogowie przechodzą swobodnie)
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = !isDestroyed;
    }

    // =========================================================================
    // GIZMOS
    // =========================================================================

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = isDestroyed ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 1.5f, 0.3f));

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            $"Gate [{chunkA}↔{chunkB}]\nHP: {currentHP}/{maxHP}");
    }
#endif
}