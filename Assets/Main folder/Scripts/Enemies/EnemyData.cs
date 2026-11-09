using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EnemyTierStats
{
    [Header("Wygląd Tieru")]
    [Tooltip("Dedykowany prefab dla tego tieru (jeśli puste, użyty zostanie domyślny prefab wroga)")]
    public GameObject prefab;

    [Tooltip("Dedykowana ikonka dla tego tieru (jeśli puste, użyta zostanie domyślna ikonka wroga)")]
    public Sprite sprite;

    [Header("Umiejętności Tieru")]
    [Tooltip("Czy ten tier posiada własne (nadpisane) umiejętności")]
    public bool overrideSkills = false;

    [Tooltip("Dedykowane prefaby umiejętności dla tego tieru. Jeśli lista jest pusta i overrideSkills jest wyłączone, użyte zostaną umiejętności domyślne.")]
    public List<GameObject> skillPrefabs = new List<GameObject>();

    [Header("Statystyki Bazowe Tieru")]
    public float baseHp = 100f;
    public float moveSpeed = 3f;
    [Range(0, 100)] public float armor = 0f;
    [Range(0, 100)] public float magicResist = 0f;
    [Range(0, 100)] public float dodgeChance = 0f;

    /// <summary>
    /// Oblicza efektywne zdrowie (Physical Effective HP) z uwzględnieniem pancerza fizycznego i szansy na unik.
    /// Wzór: baseHp / ((1 - Armor) * (1 - Unik))
    /// </summary>
    public float CalculatePhysicalEHP()
    {
        float a = Mathf.Clamp01(armor <= 1f ? armor : armor / 100f);
        float d = Mathf.Clamp01(dodgeChance <= 1f ? dodgeChance : dodgeChance / 100f);
        float factor = (1f - a) * (1f - d);
        if (factor <= 0.0001f) return float.PositiveInfinity;
        return baseHp / factor;
    }

    /// <summary>
    /// Oblicza efektywne zdrowie (Magic Effective HP) z uwzględnieniem odporności na magię i szansy na unik.
    /// Wzór: baseHp / ((1 - MagicResist) * (1 - Unik))
    /// </summary>
    public float CalculateMagicEHP()
    {
        float m = Mathf.Clamp01(magicResist <= 1f ? magicResist : magicResist / 100f);
        float d = Mathf.Clamp01(dodgeChance <= 1f ? dodgeChance : dodgeChance / 100f);
        float factor = (1f - m) * (1f - d);
        if (factor <= 0.0001f) return float.PositiveInfinity;
        return baseHp / factor;
    }
}

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Game/Enemies & Waves/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Wygląd (Domyślny / Fallback)")]
    public GameObject prefab;
    public Sprite sprite;

    [Header("Balans Fali")]
    public int threatCost = 1;

    [Header("Podstawowe")]
    public string enemyName;

    public string DisplayName => LocalizationManager.Get($"ENEMY_{GetLocalizationId()}_NAME", enemyName);

    public string GetLocalizationId()
    {
        string raw = !string.IsNullOrEmpty(enemyName) ? enemyName : name;
        var sb = new System.Text.StringBuilder();
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            else if (c == ' ' || c == '_' || c == '-') sb.Append('_');
        }
        string clean = sb.ToString();
        while (clean.Contains("__")) clean = clean.Replace("__", "_");
        return clean.Trim('_');
    }

    [Header("Tiery Wroga (1 - 4)")]
    [Tooltip("Statystyki dla 4 tierów tego wroga. Każdy tier może posiadać inny prefab, ikonkę oraz statystyki.")]
    public EnemyTierStats[] tiers = new EnemyTierStats[4];

    // Pola bazowe zachowane jako [SerializeField] dla pełnej kompatybilności wstecznej (dane z YAML i testy)
    [SerializeField, HideInInspector] public float baseHp = 100f;
    [SerializeField, HideInInspector] public float moveSpeed = 3f;
    [SerializeField, HideInInspector] public float armor = 0f;
    [SerializeField, HideInInspector] public float magicResist = 0f;
    [SerializeField, HideInInspector] public float dodgeChance = 0f;

    [Header("Ranga")]
    public EnemyRank rank = EnemyRank.Normal;
    public bool isBoss => rank == EnemyRank.Boss;
    public bool canBeElite = true;
    public float eliteSpawnChance = 10f;
    public float eliteHpMultiplier = 2.0f;
    public float eliteSpeedMultiplier = 1.3f;

    [Header("Drop Etheru (Niezależny od puli fali)")]
    [Tooltip("Gwarantowany Ether za pokonanie tego wroga w wersji Elitarnej (nie zużywa limitu puli fali)")]
    public int etherDropElite = 1;

    [Tooltip("Gwarantowany Ether za pokonanie tego wroga w wersji Bossa (nie zużywa limitu puli fali)")]
    public int etherDropBoss = 5;

    [Header("Umiejętności")]
    public List<GameObject> skillPrefabs;

    public EnemyTierStats GetTier(int tierNumber)
    {
        EnsureTiers();
        int index = Mathf.Clamp(tierNumber - 1, 0, 3);
        return tiers[index];
    }

    public GameObject GetPrefabForTier(int tierNumber)
    {
        var t = GetTier(tierNumber);
        return (t != null && t.prefab != null) ? t.prefab : prefab;
    }

    public Sprite GetSpriteForTier(int tierNumber)
    {
        var t = GetTier(tierNumber);
        return (t != null && t.sprite != null) ? t.sprite : sprite;
    }

    public List<GameObject> GetSkillPrefabsForTier(int tierNumber)
    {
        var t = GetTier(tierNumber);
        if (t != null)
        {
            if (t.overrideSkills)
            {
                return t.skillPrefabs != null ? t.skillPrefabs : new List<GameObject>();
            }
            if (t.skillPrefabs != null && t.skillPrefabs.Count > 0)
            {
                return t.skillPrefabs;
            }
        }
        return skillPrefabs != null ? skillPrefabs : new List<GameObject>();
    }

    public float CalculatePhysicalEHP(int tierNumber = 1)
    {
        var t = GetTier(tierNumber);
        return t != null ? t.CalculatePhysicalEHP() : baseHp;
    }

    public float CalculateMagicEHP(int tierNumber = 1)
    {
        var t = GetTier(tierNumber);
        return t != null ? t.CalculateMagicEHP() : baseHp;
    }

    public void EnsureTiers()
    {
        if (tiers == null || tiers.Length != 4)
        {
            var oldTiers = tiers;
            tiers = new EnemyTierStats[4];
            if (oldTiers != null)
            {
                for (int i = 0; i < Mathf.Min(oldTiers.Length, 4); i++)
                {
                    tiers[i] = oldTiers[i];
                }
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (tiers[i] == null)
            {
                tiers[i] = new EnemyTierStats
                {
                    prefab = prefab,
                    sprite = sprite,
                    baseHp = baseHp > 0f ? baseHp : 100f,
                    moveSpeed = moveSpeed > 0f ? moveSpeed : 3f,
                    armor = armor,
                    magicResist = magicResist,
                    dodgeChance = dodgeChance,
                    overrideSkills = false,
                    skillPrefabs = new List<GameObject>()
                };
            }
            else
            {
                if (tiers[i].skillPrefabs == null)
                {
                    tiers[i].skillPrefabs = new List<GameObject>();
                }
                if (i == 0 && baseHp > 0f && tiers[0].baseHp == 100f && baseHp != 100f)
                {
                    tiers[0].baseHp = baseHp;
                    tiers[0].moveSpeed = moveSpeed;
                    tiers[0].armor = armor;
                    tiers[0].magicResist = magicResist;
                    tiers[0].dodgeChance = dodgeChance;
                }
                if (tiers[i].prefab == null && prefab != null) tiers[i].prefab = prefab;
                if (tiers[i].sprite == null && sprite != null) tiers[i].sprite = sprite;
            }
        }
    }

    private void OnEnable()
    {
        EnsureTiers();
    }

    private void OnValidate()
    {
        EnsureTiers();
        if (tiers != null && tiers.Length > 0 && tiers[0] != null)
        {
            baseHp = tiers[0].baseHp;
            moveSpeed = tiers[0].moveSpeed;
            armor = tiers[0].armor;
            magicResist = tiers[0].magicResist;
            dodgeChance = tiers[0].dodgeChance;
        }
    }
}
