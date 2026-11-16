using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SpellStatModifier
{
    public SpellUpgradeStatType statType;
    public float value;
}

[Serializable]
public class SpellUpgradeNodeData
{
    public string nodeName;
    [TextArea] public string description;
    public Sprite icon;
    public List<ResourceCost> costs = new List<ResourceCost>();
    public List<SpellStatModifier> modifiers = new List<SpellStatModifier>();
}

[Serializable]
public class SpellSpecializationData
{
    public string specializationId;
    public string specializationName;
    [TextArea] public string description;
    public Sprite icon;
    public List<ResourceCost> costs = new List<ResourceCost>();
    public List<SpellStatModifier> modifiers = new List<SpellStatModifier>();

    [Header("Efekty Specjalne")]
    public bool spawnsGroundZone;
    public float groundZoneDps;
    public float groundZoneSlowPercent;
    public float groundZoneDuration = 4f;
    public float bonusSlowPercent;
    public float bonusSlowDuration;
    public bool appliesFreeze;
}

[CreateAssetMenu(fileName = "NewSpellData", menuName = "Game/Spells/Spell Data")]
public class SpellDataSO : ScriptableObject
{
    [Header("Identyfikacja")]
    public string spellId;
    public string spellName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Wizualia")]
    public GameObject vfxPrefab;
    public GameObject groundZonePrefab;
    public Color indicatorColor = new Color(1f, 0.4f, 0.1f, 0.35f);

    [Header("Tryb Rzucania")]
    public SpellCastLimitMode castLimitMode = SpellCastLimitMode.CooldownOnly;
    [Min(1)] public int maxUsesPerNight = 2;
    [Min(1)] public int maxCharges = 2;
    [Min(1f)] public float chargeRechargeSeconds = 20f;
    [Min(1f)] public float cooldownSeconds = 18f;

    [Header("Bazowe Statystyki")]
    public SpellEffectType primaryEffect = SpellEffectType.AreaDamage;
    public DamageType damageType = DamageType.Magic;
    public float baseDamage = 80f;
    public float baseRadius = 3.5f;
    [Range(0f, 1f)] public float baseSlowPercent = 0f;
    public float baseSlowDuration = 0f;
    public float baseDotDps = 0f;
    public float baseDotDuration = 0f;

    [Header("Drzewko Rozwoju (4 Poziomy Liniowe + 5. Specjalizacja A/B/C)")]
    public List<SpellUpgradeNodeData> linearTiers = new List<SpellUpgradeNodeData>();
    public List<SpellSpecializationData> specializations = new List<SpellSpecializationData>();

    [Header("Money Sink (Nieskończony Rozwój)")]
    public int moneySinkBasePoints = 100;
    public int moneySinkPointsStep = 50;
    public float moneySinkDamageMultiplierPerLevel = 0.10f;
    public float moneySinkRadiusPerLevel = 0.35f;

    public int GetMoneySinkRequiredPoints(int currentTotalSinkLevels)
    {
        return Mathf.Max(10, moneySinkBasePoints + currentTotalSinkLevels * moneySinkPointsStep);
    }

    public static SpellDataSO CreateDefaultFireball()
    {
        var spell = CreateInstance<SpellDataSO>();
        spell.name = "Spell_Fireball";
        spell.spellId = "fireball";
        spell.spellName = "Kula Ognia";
        spell.description = "Przywołuje ognistą kulę, która zadaje obrażenia magiczne wszystkim wrogom w obszarze.";
        spell.indicatorColor = new Color(1f, 0.35f, 0.1f, 0.4f);
        spell.castLimitMode = SpellCastLimitMode.CooldownOnly;
        spell.cooldownSeconds = 16f;
        spell.primaryEffect = SpellEffectType.AreaDamage;
        spell.damageType = DamageType.Magic;
        spell.baseDamage = 100f;
        spell.baseRadius = 3.5f;

        spell.linearTiers = new List<SpellUpgradeNodeData>
        {
            new SpellUpgradeNodeData
            {
                nodeName = "Większy Płomień",
                description = "+1.0 do promienia wybuchu.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 25 }, new ResourceCost { type = ResourceType.Ether, amount = 15 } },
                modifiers = new List<SpellStatModifier> { new SpellStatModifier { statType = SpellUpgradeStatType.RadiusFlat, value = 1.0f } }
            },
            new SpellUpgradeNodeData
            {
                nodeName = "Skoncentrowany Żar",
                description = "+40% obrażeń czaru.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Coal, amount = 20 }, new ResourceCost { type = ResourceType.Ether, amount = 25 } },
                modifiers = new List<SpellStatModifier> { new SpellStatModifier { statType = SpellUpgradeStatType.DamageMultiplier, value = 0.40f } }
            },
            new SpellUpgradeNodeData
            {
                nodeName = "Szybka Inkantacja",
                description = "-4 sekundy czasu odnowienia.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Iron, amount = 15 }, new ResourceCost { type = ResourceType.Ether, amount = 30 } },
                modifiers = new List<SpellStatModifier> { new SpellStatModifier { statType = SpellUpgradeStatType.CooldownReduction, value = 4f } }
            },
            new SpellUpgradeNodeData
            {
                nodeName = "Piekielny Wybuch",
                description = "+80 bazowych obrażeń i +0.5 zasięgu.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Stone, amount = 30 }, new ResourceCost { type = ResourceType.Ether, amount = 45 } },
                modifiers = new List<SpellStatModifier>
                {
                    new SpellStatModifier { statType = SpellUpgradeStatType.DamageFlat, value = 80f },
                    new SpellStatModifier { statType = SpellUpgradeStatType.RadiusFlat, value = 0.5f }
                }
            }
        };

        spell.specializations = new List<SpellSpecializationData>
        {
            new SpellSpecializationData
            {
                specializationId = "fireball_burst",
                specializationName = "A: Niszcząca Eksplozja",
                description = "Podwaja moc wybuchu (+100% obrażeń).",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Ether, amount = 80 }, new ResourceCost { type = ResourceType.Iron, amount = 30 } },
                modifiers = new List<SpellStatModifier> { new SpellStatModifier { statType = SpellUpgradeStatType.DamageMultiplier, value = 1.0f } }
            },
            new SpellSpecializationData
            {
                specializationId = "fireball_scorched",
                specializationName = "B: Spalona Ziemia",
                description = "Pozostawia płonące pole na 5s, zadając 35 DPS wrogom w środku.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Ether, amount = 80 }, new ResourceCost { type = ResourceType.Coal, amount = 40 } },
                spawnsGroundZone = true,
                groundZoneDps = 35f,
                groundZoneDuration = 5f
            },
            new SpellSpecializationData
            {
                specializationId = "fireball_cataclysm",
                specializationName = "C: Kataklizm",
                description = "+2.0 do promienia oraz -3s czasu odnowienia.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Ether, amount = 80 }, new ResourceCost { type = ResourceType.Stone, amount = 50 } },
                modifiers = new List<SpellStatModifier>
                {
                    new SpellStatModifier { statType = SpellUpgradeStatType.RadiusFlat, value = 2.0f },
                    new SpellStatModifier { statType = SpellUpgradeStatType.CooldownReduction, value = 3f }
                }
            }
        };

        return spell;
    }

    public static SpellDataSO CreateDefaultFrostNova()
    {
        var spell = CreateInstance<SpellDataSO>();
        spell.name = "Spell_FrostNova";
        spell.spellId = "frost_nova";
        spell.spellName = "Fala Mrozu";
        spell.description = "Uderza mrozem w wybrany obszar, zadając lekkie obrażenia i spowalniając wrogów.";
        spell.indicatorColor = new Color(0.2f, 0.75f, 1f, 0.4f);
        spell.castLimitMode = SpellCastLimitMode.RechargeableCharges;
        spell.maxCharges = 2;
        spell.chargeRechargeSeconds = 20f;
        spell.primaryEffect = SpellEffectType.AreaSlow;
        spell.damageType = DamageType.Magic;
        spell.baseDamage = 40f;
        spell.baseRadius = 4.0f;
        spell.baseSlowPercent = 0.45f;
        spell.baseSlowDuration = 4.0f;

        spell.linearTiers = new List<SpellUpgradeNodeData>
        {
            new SpellUpgradeNodeData
            {
                nodeName = "Rozległy Chłód",
                description = "+1.0 do promienia działania.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 25 }, new ResourceCost { type = ResourceType.Ether, amount = 15 } },
                modifiers = new List<SpellStatModifier> { new SpellStatModifier { statType = SpellUpgradeStatType.RadiusFlat, value = 1.0f } }
            },
            new SpellUpgradeNodeData
            {
                nodeName = "Ostre Kryształy",
                description = "+50% obrażeń Fali Mrozu.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Stone, amount = 25 }, new ResourceCost { type = ResourceType.Ether, amount = 25 } },
                modifiers = new List<SpellStatModifier> { new SpellStatModifier { statType = SpellUpgradeStatType.DamageMultiplier, value = 0.50f } }
            },
            new SpellUpgradeNodeData
            {
                nodeName = "Dodatkowy Ładunek",
                description = "+1 maksymalny ładunek zaklęcia.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Iron, amount = 20 }, new ResourceCost { type = ResourceType.Ether, amount = 35 } },
                modifiers = new List<SpellStatModifier> { new SpellStatModifier { statType = SpellUpgradeStatType.BonusCharges, value = 1f } }
            },
            new SpellUpgradeNodeData
            {
                nodeName = "Wieczna Zima",
                description = "-5s czasu ładowania i +40 bazowych obrażeń.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Coal, amount = 25 }, new ResourceCost { type = ResourceType.Ether, amount = 45 } },
                modifiers = new List<SpellStatModifier>
                {
                    new SpellStatModifier { statType = SpellUpgradeStatType.CooldownReduction, value = 5f },
                    new SpellStatModifier { statType = SpellUpgradeStatType.DamageFlat, value = 40f }
                }
            }
        };

        spell.specializations = new List<SpellSpecializationData>
        {
            new SpellSpecializationData
            {
                specializationId = "frost_freeze",
                specializationName = "A: Głębokie Zamrożenie",
                description = "Zatrzymuje wrogów w miejscu na 2.5 sekundy (100% spowolnienia).",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Ether, amount = 80 }, new ResourceCost { type = ResourceType.Iron, amount = 30 } },
                appliesFreeze = true,
                bonusSlowDuration = 2.5f
            },
            new SpellSpecializationData
            {
                specializationId = "frost_zone",
                specializationName = "B: Lodowa Pułapka",
                description = "Tworzy lodową strefę na 6s, która spowalnia o 60% i zadaje 15 DPS.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Ether, amount = 80 }, new ResourceCost { type = ResourceType.Stone, amount = 40 } },
                spawnsGroundZone = true,
                groundZoneDps = 15f,
                groundZoneSlowPercent = 0.60f,
                groundZoneDuration = 6f
            },
            new SpellSpecializationData
            {
                specializationId = "frost_shatter",
                specializationName = "C: Lodowy Odłamek",
                description = "+120% obrażeń i +1.5 promienia Fali Mrozu.",
                costs = new List<ResourceCost> { new ResourceCost { type = ResourceType.Ether, amount = 80 }, new ResourceCost { type = ResourceType.Coal, amount = 35 } },
                modifiers = new List<SpellStatModifier>
                {
                    new SpellStatModifier { statType = SpellUpgradeStatType.DamageMultiplier, value = 1.2f },
                    new SpellStatModifier { statType = SpellUpgradeStatType.RadiusFlat, value = 1.5f }
                }
            }
        };

        return spell;
    }
}
