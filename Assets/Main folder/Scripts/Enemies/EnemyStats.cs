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
    private bool isDead = false;

    [Header("Visuals")]
    public Renderer meshRenderer;
    [SerializeField] private Renderer[] renderers;
    private Color originalColor = Color.white;

    private static MaterialPropertyBlock sharedPropertyBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public EnemyWalker Walker { get; private set; }

    public event Action<float, float> OnHealthChanged;
    public event Action<EnemyStats> OnDeath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sharedPropertyBlock = null;
    }

    private void Awake()
    {
        Walker = GetComponent<EnemyWalker>();
        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnEnable()
    {
        EnemyRegistry.Register(this);
        if (rank == EnemyRank.Elite)
        {
            ApplyVisualColor(Color.red);
        }
        else
        {
            ResetColor();
        }
    }

    private void OnDisable()
    {
        EnemyRegistry.Unregister(this);
        CancelInvoke(nameof(ResetColor));
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }
        }
    }

    public void Initialize(EnemyData _data, float waveHpMult = 1f, float waveSpeedMult = 1f)
    {
        data = _data;

        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<Renderer>();

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();

        if (meshRenderer != null && meshRenderer.sharedMaterial != null)
        {
            if (meshRenderer.sharedMaterial.HasProperty(BaseColorId))
                originalColor = meshRenderer.sharedMaterial.GetColor(BaseColorId);
            else if (meshRenderer.sharedMaterial.HasProperty(ColorId))
                originalColor = meshRenderer.sharedMaterial.GetColor(ColorId);
            else
                originalColor = Color.white;
        }
        else
        {
            originalColor = Color.white;
        }

        if (rank != EnemyRank.Elite && rank != EnemyRank.Boss)
            rank = EnemyRank.Normal;

        float hearthEnemyMult = BeaconEntity.GetActiveNightEnemyMultiplier();
        float eliteHpMult = (rank == EnemyRank.Elite && data.eliteHpMultiplier > 0f) ? data.eliteHpMultiplier : 1f;
        float eliteSpeedMult = (rank == EnemyRank.Elite && data.eliteSpeedMultiplier > 0f) ? data.eliteSpeedMultiplier : 1f;

        maxHealth = Mathf.Max(1f, data.baseHp * hearthEnemyMult * Mathf.Max(0.01f, waveHpMult) * eliteHpMult);
        currentHealth = maxHealth;

        currentArmor = data.armor <= 1f ? data.armor : data.armor / 100f;
        currentMagicResist = data.magicResist <= 1f ? data.magicResist : data.magicResist / 100f;
        currentDodge = data.dodgeChance <= 1f ? data.dodgeChance : data.dodgeChance / 100f;

        if (Walker == null)
            Walker = GetComponent<EnemyWalker>();

        if (Walker != null)
            Walker.speed = Mathf.Max(0.1f, data.moveSpeed * hearthEnemyMult * Mathf.Max(0.01f, waveSpeedMult) * eliteSpeedMult);

        if (rank == EnemyRank.Elite)
        {
            transform.localScale *= 1.2f;
            ApplyVisualColor(Color.red);
        }
        else
        {
            ResetColor();
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

    public void TakeDamage(float rawDamage, DamageType type, float armourPiercing, float magicPiercing, bool isCritical, float criticalMultiplier, TowerEntity sourceTower = null, bool isAoE = false, DeathSource deathSource = DeathSource.Combat)
    {
        if (isDead) return;

        foreach (var skill in skills)
        {
            rawDamage = skill.OnBeforeDamageCalculation(rawDamage, type, sourceTower, isAoE);
            if (rawDamage <= 0) return;
        }

        if (Random.value < currentDodge && !isCritical && type != DamageType.True)
        {
            FlashColor(Color.yellow, 0.1f);
            return;
        }

        float finalDamage = CalculateFinalDamage(type, rawDamage, armourPiercing, magicPiercing, isCritical, criticalMultiplier);

        currentHealth -= finalDamage;

        FlashColor(Color.white, 0.1f);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        for (int i = skills.Count - 1; i >= 0; i--)
        {
            if (gameObject == null || isDead) return;
            skills[i].OnDamageTaken(finalDamage, currentHealth);
        }

        if (currentHealth <= 0)
            Die(deathSource);
    }

    private void FlashColor(Color color, float duration)
    {
        ApplyVisualColor(color);
        CancelInvoke(nameof(ResetColor));
        Invoke(nameof(ResetColor), duration);
    }

    private void ApplyVisualColor(Color color)
    {
        if (renderers == null || renderers.Length == 0) return;
        if (sharedPropertyBlock == null)
            sharedPropertyBlock = new MaterialPropertyBlock();

        sharedPropertyBlock.SetColor(BaseColorId, color);
        sharedPropertyBlock.SetColor(ColorId, color);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r != null)
            {
                r.SetPropertyBlock(sharedPropertyBlock);
            }
        }
    }

    public void ResetColor()
    {
        if (renderers == null || renderers.Length == 0) return;

        if (rank == EnemyRank.Elite)
        {
            ApplyVisualColor(Color.red);
        }
        else
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }
        }
    }

    public void Die(DeathSource source = DeathSource.Combat)
    {
        if (isDead) return;
        isDead = true;

        if (source == DeathSource.Combat)
        {
            if (HopeSessionManager.Instance != null)
            {
                HopeSessionManager.Instance.OnEnemyKilled(this.rank);
            }

            if (RuneManager.Instance != null)
            {
                RuneManager.Instance.RollRuneDrop(this.rank);
            }

            if (EnemyEtherDropManager.Instance != null)
            {
                EnemyEtherDropManager.Instance.HandleEnemyDeath(this, source);
            }
        }

        for (int i = skills.Count - 1; i >= 0; i--)
        {
            if (skills[i] != null)
            {
                skills[i].OnDeath(source);
            }
        }

        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }

    public void GetToBase()
    {
        Die(DeathSource.BaseReached);
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