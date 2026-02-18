using System; // Potrzebne do Action
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random; // ROZWI¥ZANIE PROBLEMU: Wymuszamy u¿ycie Random z Unity

public class EnemyStats : MonoBehaviour
{
    [Header("Dane (Tylko podgl¹d)")]
    public EnemyData data;
    public EnemyRank rank = EnemyRank.Normal;

    [SerializeField] private float currentHealth;
    [SerializeField] private float maxHealth;

    // Aktywne statystyki (po modyfikatorach)
    private float currentArmor;
    private float currentMagicResist;
    private float currentDodge;

    // Lista umiejêtnoœci (instancje)
    private List<EnemySkill> skills = new List<EnemySkill>();

    [Header("Wizualizacja")]
    public Renderer meshRenderer;
    private Color originalColor;

    // EVENT DLA UI (Obecne HP, Max HP)
    public event Action<float, float> OnHealthChanged;

    // --- INICJALIZACJA ---
    public void Initialize(EnemyData _data, float globalHpMod = 1f, float globalSpeedMod = 1f, float globalEliteChanceMod = 1f)
    {
        data = _data;

        // 1. Wizualizacja (Pobranie renderera)
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
        //if (meshRenderer != null) originalColor = meshRenderer.material.color;

        // 2. Losowanie Rangi (z uwzglêdnieniem modyfikatora Beacona)
        float roll = Random.Range(0f, 100f);
        float finalEliteChance = data.eliteSpawnChance * globalEliteChanceMod;

        if (data.canBeElite && roll < finalEliteChance)
        {
            rank = EnemyRank.Elite;
        }
        else
        {
            rank = EnemyRank.Normal;
        }

        // 3. Ustawianie Statystyk
        // Baza * Modyfikator globalny
        maxHealth = data.baseHp * globalHpMod;
        float speed = data.moveSpeed * globalSpeedMod;

        currentArmor = data.armor;
        currentMagicResist = data.magicResist;
        currentDodge = data.dodgeChance;

        // Modyfikatory Elity
        if (rank == EnemyRank.Elite)
        {
            maxHealth *= data.eliteHpMultiplier;
            speed *= data.eliteSpeedMultiplier;

            // Zmiana wygl¹du
            transform.localScale *= 1.2f;
            if (meshRenderer) meshRenderer.material.color = Color.red;
        }
        else if (rank == EnemyRank.Boss)
        {
            // Logika bossa (mo¿na rozwin¹æ w przysz³oœci)
        }

        currentHealth = maxHealth;

        // Przekazanie prêdkoœci do Walkera
        var walker = GetComponent<EnemyWalker>();
        if (walker) walker.speed = speed;

        // 4. Dodawanie Umiejêtnoœci (Skills)
        if (data.skillPrefabs != null)
        {
            foreach (var prefab in data.skillPrefabs)
            {
                // Instancjujemy skill jako dziecko
                GameObject skillObj = Instantiate(prefab, transform);
                EnemySkill skill = skillObj.GetComponent<EnemySkill>();
                if (skill != null)
                {
                    skill.Initialize(this);
                    skills.Add(skill);
                }
            }
        }

        // 5. Powiadomienie UI o startowym zdrowiu
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // --- OTRZYMYWANIE OBRA¯EÑ ---
    public void TakeDamage(float rawDamage, DamageType type, float armourPiercing, float magicPiercing, bool isCritical, float criticalMultiplier)
    {
        // 2. Umiejêtnoœci: Modyfikacja obra¿eñ przed ich zadaniem (np. Hiperpancerz)
        foreach (var skill in skills)
        {
            rawDamage = skill.OnBeforeDamageCalculation(rawDamage, type);
            if (rawDamage <= 0) return; // Zablokowane ca³kowicie
        }

        float dodgeRoll = Random.Range(0f, 100f);
        float finalDamage = ModifieDamage(type, rawDamage, armourPiercing, magicPiercing, isCritical, criticalMultiplier);

        if (dodgeRoll < currentDodge && !isCritical && type != DamageType.True)
        {
            // Flash efekt uniku (wizualny)
            if (meshRenderer != null)
            {
                meshRenderer.material.color = Color.yellow;
                Invoke("ResetColor", 0.1f);
            }
            return; // Unikniêto obra¿eñ
        }

        // 4. Aplikacja obra¿eñ
        currentHealth -= finalDamage;

        // Flash efekt (wizualny)
        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.white;
            Invoke("ResetColor", 0.1f);
        }

        // 5. Aktualizacja UI
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // 6. Umiejêtnoœci: Reakcja na otrzymanie obra¿eñ (np. Podzia³ Bloba)
        // Pêtla odwrotna, bo skill mo¿e zniszczyæ ten obiekt (BlobSplit)
        for (int i = skills.Count - 1; i >= 0; i--)
        {
            if (this == null) return; // Jeœli obiekt zgin¹³/podzieli³ siê w trakcie pêtli
            skills[i].OnDamageTaken(finalDamage, currentHealth);
        }

        // 7. Œmieræ
        if (this != null && currentHealth <= 0)
        {
            Die();
        }
    }

    void ResetColor()
    {
        if (meshRenderer != null)
            meshRenderer.material.color = (rank == EnemyRank.Elite) ? Color.red : originalColor;
    }

    void Die()
    {
        // Loot System (Tymczasowy log, tu podepniemy dodawanie surowców)
        if (rank == EnemyRank.Elite)
        {
            if (Random.Range(0, 100) < 50) Debug.Log("<color=magenta>DROP: Runa (z Elity)</color>");
        }
        else if (rank == EnemyRank.Boss)
        {
            Debug.Log("<color=magenta>DROP: Runa GWARANTOWANA (z Bossa)</color>");
            if (Random.Range(0, 100) < 20) Debug.Log("<color=magenta>DROP: Druga Runa (Bonus)</color>");
        }
        else
        {
            // Zwyk³y wróg - np. ma³a szansa na z³oto lub gwarantowane z³oto
            // ResourceManager.Instance.AddResource(ResourceType.Gold, 5); 
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(ResourceType.Artifacts, 1);

            // LOGOWANIE
            if (ResourceLogger.Instance != null)
            {
                ResourceLogger.Instance.LogSingleEvent($"Œmieræ Wroga: {data.enemyName}", ResourceType.Artifacts, 1);
            }
        }

        // Powiadom skille o œmierci (np. Wybuch po œmierci)
        foreach (var skill in skills) skill.OnDeath();

        Destroy(gameObject);
    }

    float ModifieDamage(DamageType type, float rawDamage, float armourPiercing, float magicPiercing, bool isCritical, float criticalMultiplier)
    {
        float finalArmor = currentArmor;
        float finalMagicResist = currentMagicResist;
        if (finalMagicResist > 0) finalMagicResist = Mathf.Max(0, currentMagicResist - magicPiercing);
        if (finalArmor > 0) finalArmor = Mathf.Max(0, currentArmor - armourPiercing);

        

        switch (type)
        {
            case DamageType.Physical:
               rawDamage *= (1f - finalArmor / 100f); // Zmniejszamy obra¿enia o procent przebicia
                break; // Ignoruje pancerz i odpornoœæ magiczn¹
            case DamageType.Magic:
                rawDamage *= (1f - finalMagicResist / 100f); // Zmniejszamy obra¿enia o procent przebicia
                break; // Ignoruje pancerz i odpornoœæ magiczn¹
            case DamageType.True:
                break; // Ignoruje pancerz i odpornoœæ magiczn¹
        }

        if(isCritical) rawDamage *= criticalMultiplier/100f; // Zastosowanie mno¿nika krytycznego

        return rawDamage; // Domyœlnie zwracamy niezmodyfikowane obra¿enia
    }

    // --- HELPERY DLA UMIEJÊTNOŒCI (Blob) ---
    public float GetMaxHealth() => maxHealth;

    public void SetHealthManually(float amount)
    {
        maxHealth = amount;
        currentHealth = amount;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}