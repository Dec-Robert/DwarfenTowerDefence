using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Manages the runtime statistics, combat interactions, and damage resolution pipeline for an enemy unit.
/// Key Responsibilities:
/// - Initializes base attributes (Health, Armor, Magic Resist, Dodge, Speed) from EnemyData ScriptableObject.
/// - Processes incoming attacks through active skills, dodge checks, penetration mitigations, and damage types.
/// - Handles visual feedback for damage and dodges using renderer color tints.
/// - Dispatches health update and death lifecycle events to subscribers (UI, Spawner, Session Managers).
/// </summary>
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
        currentArmor = data.armor;
        currentMagicResist = data.magicResist;
        currentDodge = data.dodgeChance;

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

        float dodgeRoll = Random.Range(0f, 100f);
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
            if (gameObject == null) return;
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

    float CalculateFinalDamage(DamageType type, float raw, float ap, float mp, bool crit, float critMult)
    {
        float armor = Mathf.Max(0, currentArmor - ap);
        float mr = Mathf.Max(0, currentMagicResist - mp);

        switch (type)
        {
            case DamageType.Physical: 
                raw *= (1f - armor / 100f); 
                break;
            case DamageType.Magic: 
                raw *= (1f - mr / 100f); 
                break;
            case DamageType.True: 
                break;
        }

        if (crit) 
            raw *= critMult / 100f;

        return raw;
    }

    public float GetMaxHealth() => maxHealth;

    public void SetHealthManually(float amount)
    {
        maxHealth = amount;
        currentHealth = amount;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }
}