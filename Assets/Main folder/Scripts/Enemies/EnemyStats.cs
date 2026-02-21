using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyStats : MonoBehaviour
{
    [Header("Dane (Tylko podgląd)")]
    public EnemyData data;
    public EnemyRank rank = EnemyRank.Normal;

    [SerializeField] private float currentHealth;
    [SerializeField] private float maxHealth;

    private float currentArmor;
    private float currentMagicResist;
    private float currentDodge;

    private List<EnemySkill> skills = new List<EnemySkill>();

    [Header("Wizualizacja")]
    public Renderer meshRenderer;
    private Color originalColor;

    // EVENT DLA UI
    public event Action<float, float> OnHealthChanged;

    // Referencja do Echo – ustawiana przez EnemySpawner po Initialize()
    // (żeby Walker mógł powiadomić Echo gdy dotrze do bazy)
    [HideInInspector] public EnemyData cachedData;

    // =========================================================================
    // INICJALIZACJA – nowa sygnatura przyjmuje gotowe ScaledEnemyStats
    // =========================================================================

    public void Initialize(EnemyData _data, ScaledEnemyStats scaled)
    {
        data       = _data;
        cachedData = _data;

        // Wizualizacja
        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<Renderer>();

        // Ranga (ustalona przez EnemyStatScaler)
        rank = scaled.isElite ? EnemyRank.Elite : EnemyRank.Normal;

        // Statystyki z gotowego zestawu
        maxHealth          = scaled.finalHp;
        currentHealth      = maxHealth;
        currentArmor       = scaled.finalArmor;
        currentMagicResist = scaled.finalMagicResist;
        currentDodge       = scaled.finalDodge;

        // Prędkość do Walkera
        var walker = GetComponent<EnemyWalker>();
        if (walker) walker.speed = scaled.finalSpeed;

        // Wygląd Elity
        if (rank == EnemyRank.Elite)
        {
            transform.localScale *= 1.2f;
            if (meshRenderer) meshRenderer.material.color = Color.red;
        }

        // Inicjalizacja skillów
        if (_data.skillPrefabs != null)
        {
            foreach (var prefab in _data.skillPrefabs)
            {
                GameObject skillObj = Instantiate(prefab, transform);
                EnemySkill skill = skillObj.GetComponent<EnemySkill>();
                if (skill != null)
                {
                    skill.Initialize(this);
                    skills.Add(skill);
                }
            }
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // =========================================================================
    // OTRZYMYWANIE OBRAŻEŃ
    // =========================================================================

    public void TakeDamage(float rawDamage, DamageType type, float armourPiercing, float magicPiercing, bool isCritical, float criticalMultiplier)
    {
        foreach (var skill in skills)
        {
            rawDamage = skill.OnBeforeDamageCalculation(rawDamage, type);
            if (rawDamage <= 0) return;
        }

        float dodgeRoll   = Random.Range(0f, 100f);
        float finalDamage = CalculateFinalDamage(type, rawDamage, armourPiercing, magicPiercing, isCritical, criticalMultiplier);

        if (dodgeRoll < currentDodge && !isCritical && type != DamageType.True)
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = Color.yellow;
                Invoke(nameof(ResetColor), 0.1f);
            }
            return;
        }

        currentHealth -= finalDamage;

        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.white;
            Invoke(nameof(ResetColor), 0.1f);
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        for (int i = skills.Count - 1; i >= 0; i--)
        {
            if (gameObject == null) return; // poprawna wersja null checka
            skills[i].OnDamageTaken(finalDamage, currentHealth);
        }

        if (currentHealth <= 0)
            Die();
    }

    void ResetColor()
    {
        if (meshRenderer == null) return;
        meshRenderer.material.color = (rank == EnemyRank.Elite) ? Color.red : originalColor;
    }

    void Die()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(ResourceType.Artifacts, 1);

            if (ResourceLogger.Instance != null)
                ResourceLogger.Instance.LogSingleEvent($"Śmierć Wroga: {data.enemyName}", ResourceType.Artifacts, 1);
        }

        foreach (var skill in skills) skill.OnDeath();
        Destroy(gameObject);
    }

    float CalculateFinalDamage(DamageType type, float raw, float ap, float mp, bool crit, float critMult)
    {
        float armor = Mathf.Max(0, currentArmor      - ap);
        float mr    = Mathf.Max(0, currentMagicResist - mp);

        switch (type)
        {
            case DamageType.Physical: raw *= (1f - armor / 100f); break;
            case DamageType.Magic:    raw *= (1f - mr    / 100f); break;
            case DamageType.True:     break;
        }

        // critMult jako procenty
        if (crit) raw *= critMult/100;

        return raw;
    }

    // Helpery dla skillów (BlobSplit)
    public float GetMaxHealth() => maxHealth;

    public void SetHealthManually(float amount)
    {
        maxHealth     = amount;
        currentHealth = amount;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}