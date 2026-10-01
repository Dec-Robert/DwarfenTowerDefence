using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyStats : MonoBehaviour
{
    [Header("Data Preview")]
    public EnemyData data;
    public EnemyRank rank = EnemyRank.Normal;

    [SerializeField] private float currentHealth;
    [SerializeField] private float maxHealth;

    private float currentArmor;
    private float currentMagicResist;
    private float currentDodge;

    private List<EnemySkill> skills = new List<EnemySkill>();

    [Header("Visuals")]
    public Renderer meshRenderer;
    private Color originalColor;

    public event Action<float, float> OnHealthChanged;
    public event Action<EnemyStats> OnDeath;

    public void Initialize(EnemyData _data)
    {
        data = _data;

        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<Renderer>();

        if (meshRenderer != null)
            originalColor = meshRenderer.material.color;

        rank = EnemyRank.Normal;

        maxHealth = data.baseHp;
        currentHealth = maxHealth;

        currentArmor = data.armor <= 1f ? data.armor : data.armor / 100f;
        currentMagicResist = data.magicResist <= 1f ? data.magicResist : data.magicResist / 100f;
        currentDodge = data.dodgeChance <= 1f ? data.dodgeChance : data.dodgeChance / 100f;

        var walker = GetComponent<EnemyWalker>();
        if (walker) walker.speed = data.moveSpeed;

        if (rank == EnemyRank.Elite)
        {
            transform.localScale *= 1.2f;
            if (meshRenderer) meshRenderer.material.color = Color.red;
        }

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

    public void TakeDamage(float rawDamage, DamageType type, float armourPiercing, float magicPiercing, bool isCritical, float criticalMultiplier, TowerEntity sourceTower = null, bool isAoE = false)
    {
        foreach (var skill in skills)
        {
            rawDamage = skill.OnBeforeDamageCalculation(rawDamage, type, sourceTower, isAoE);
            if (rawDamage <= 0) return;
        }

        if (Random.value < currentDodge && !isCritical && type != DamageType.True)
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = Color.yellow;
                Invoke(nameof(ResetColor), 0.1f);
            }
            return;
        }

        float finalDamage = CalculateFinalDamage(type, rawDamage, armourPiercing, magicPiercing, isCritical, criticalMultiplier);

        currentHealth -= finalDamage;

        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.white;
            Invoke(nameof(ResetColor), 0.1f);
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        for (int i = skills.Count - 1; i >= 0; i--)
        {
            if (gameObject == null) return;
            skills[i].OnDamageTaken(finalDamage, currentHealth);
        }

        if (currentHealth <= 0)
            Die();
    }

    private void ResetColor()
    {
        if (meshRenderer == null) return;
        meshRenderer.material.color = (rank == EnemyRank.Elite) ? Color.red : originalColor;
    }

    private void Die()
    {
        if (HopeSessionManager.Instance != null)
        {
            HopeSessionManager.Instance.OnEnemyKilled(this.rank);
        }

        if (RuneManager.Instance != null)
        {
            RuneManager.Instance.RollRuneDrop(this.rank);
        }

        foreach (var skill in skills) skill.OnDeath();

        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }

    public void GetToBase()
    {
        foreach (var skill in skills) skill.OnDeath();

        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }

    private float CalculateFinalDamage(DamageType type, float raw, float ap, float mp, bool crit, float critMult)
    {
        switch (type)
        {
            case DamageType.Physical:
                float effectiveArmor = Mathf.Clamp01(currentArmor - ap);
                raw *= (1f - effectiveArmor);
                break;

            case DamageType.Magic:
                float effectiveMR = Mathf.Clamp01(currentMagicResist - mp);
                raw *= (1f - effectiveMR);
                break;

            case DamageType.True:
                break;
        }

        if (crit)
            raw *= critMult;

        return raw;
    }

    public float GetMaxHealth() => maxHealth;

    public void SetHealthManually(float amount)
    {
        maxHealth = amount;
        currentHealth = amount;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public float GetCurrentHealth() => currentHealth;
}