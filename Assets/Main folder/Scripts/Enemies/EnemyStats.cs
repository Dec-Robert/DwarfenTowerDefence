using UnityEngine;
using System.Collections.Generic;

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

    // Lista umiejêtnoœci
    private List<EnemySkill> skills = new List<EnemySkill>();

    [Header("Wizualizacja")]
    public Renderer meshRenderer;
    private Color originalColor;

    // --- ZMIENIONA METODA INITIALIZE ---
    // Teraz przyjmuje modyfikatory globalne (np. z Beacona)
    public void Initialize(EnemyData _data, float globalHpMod = 1f, float globalSpeedMod = 1f, float globalEliteChanceMod = 1f)
    {
        data = _data;

        // 1. Losowanie Rangi (z uwzglêdnieniem modyfikatora Beacona)
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

        // 2. Ustawianie Statystyk
        // Baza * Modyfikator globalny (Beacon)
        maxHealth = data.baseHp * globalHpMod;
        float speed = data.moveSpeed * globalSpeedMod;

        currentArmor = data.armor;
        currentMagicResist = data.magicResist;
        currentDodge = data.dodgeChance;

        // Jeœli Elita -> Nak³adamy dodatkowe mno¿niki
        if (rank == EnemyRank.Elite)
        {
            maxHealth *= data.eliteHpMultiplier;
            speed *= data.eliteSpeedMultiplier;

            // Wizualne odró¿nienie elity
            transform.localScale *= 1.2f;
            if (meshRenderer) meshRenderer.material.color = Color.red;
        }
        else if (rank == EnemyRank.Boss)
        {
            // Logika bossa (mo¿na dodaæ póŸniej)
        }

        currentHealth = maxHealth;

        // Przekazanie prêdkoœci do Walkera
        var walker = GetComponent<EnemyWalker>();
        if (walker) walker.speed = speed;

        // 3. Dodawanie Umiejêtnoœci
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

        // Pobranie renderera do flashowania (jeœli nie przypisany)
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null) originalColor = meshRenderer.material.color;
    }

    public void TakeDamage(float rawDamage, DamageType type)
    {
        // 1. Unik
        if (currentDodge > 0 && Random.Range(0f, 100f) < currentDodge)
        {
            // Debug.Log($"{name} UNIKN¥£ ataku!");
            return;
        }

        // 2. Umiejêtnoœci: Before Damage (np. Hiperpancerz)
        foreach (var skill in skills)
        {
            rawDamage = skill.OnBeforeDamageCalculation(rawDamage, type);
            if (rawDamage <= 0) return; // Zablokowane ca³kowicie
        }

        // 3. Redukcja pancerzem/magi¹
        float mitigation = (type == DamageType.Physical) ? currentArmor : currentMagicResist;
        // Wzór: Procentowa redukcja (Armor 20 = -20% dmg)
        float finalDamage = rawDamage * (1f - (mitigation / 100f));

        if (finalDamage < 1) finalDamage = 1; // Min 1 dmg

        // 4. Aplikacja obra¿eñ
        currentHealth -= finalDamage;

        // Flash efekt
        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.white;
            Invoke("ResetColor", 0.1f);
        }

        // 5. Umiejêtnoœci: On Damage (np. Blob split)
        for (int i = skills.Count - 1; i >= 0; i--)
        {
            if (this == null) return;
            skills[i].OnDamageTaken(finalDamage, currentHealth);
        }

        if (this != null && currentHealth <= 0)
        {
            Die();
        }
    }

    void ResetColor()
    {
        if (meshRenderer != null) meshRenderer.material.color = (rank == EnemyRank.Elite) ? Color.red : originalColor;
    }

    void Die()
    {
        // Loot System
        if (rank == EnemyRank.Elite)
        {
            if (Random.Range(0, 100) < 50) Debug.Log("<color=magenta>DROP: Runa (z Elity)</color>");
        }
        else if (rank == EnemyRank.Boss)
        {
            Debug.Log("<color=magenta>DROP: Runa GWARANTOWANA (z Bossa)</color>");
            if (Random.Range(0, 100) < 20) Debug.Log("<color=magenta>DROP: Druga Runa (Bonus)</color>");
        }

        foreach (var skill in skills) skill.OnDeath();

        Destroy(gameObject);
    }

    public float GetMaxHealth() => maxHealth;

    public void SetHealthManually(float amount)
    {
        maxHealth = amount;
        currentHealth = amount;
    }
}