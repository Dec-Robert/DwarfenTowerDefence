using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject opisujący jedno meta-ulepszenie.
///
/// ── POLA PODSTAWOWE ─────────────────────────────────────────────────────────
///   id            – unikalny string (np. "wall_s1", "wood_storage_1")
///   upgradeName   – wyświetlana nazwa
///   description   – opis dla UI
///   cost          – koszt w Artefaktach
///   isUnlocked    – czy kupione (zarządzane przez SaveManager)
///   prerequisites – lista wymaganych wcześniejszych upgradów
///
/// ── POLA EFEKTU ─────────────────────────────────────────────────────────────
///   effectType      – co robi ten upgrade (MetaEffectType)
///   effectValue     – główny parametr numeryczny efektu
///   effectValue2    – opcjonalny drugi parametr (np. od której fali)
///   targetBuilding  – wymagany dla efektów specyficznych dla budynku
///                     (SpecificBuildingUpgradeCostReduction, BuildingTerrainBonusMultiplier)
///
/// ── KONWENCJA NADPISYWANIA ───────────────────────────────────────────────────
///   Jeśli kilka odblokowanych upgradów ma ten sam effectType,
///   MetaUpgradeManager bierze OSTATNIĄ wartość (ostatni w liście allUpgrades).
///   Dlatego kolejność w allUpgrades ma znaczenie dla "tiered" upgradów
///   (np. Magazyn Drewna I → II → III).
/// </summary>
[CreateAssetMenu(fileName = "NewMetaUpgrade", menuName = "Game/Meta Progression/Meta Upgrade")]
public class MetaUpgradeSO : ScriptableObject
{
    [Header("── Identyfikacja ────────────────────────")]
    public string id;
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    public int cost;

    public string DisplayName => LocalizationManager.Get($"META_{GetLocalizationId()}_NAME", upgradeName);
    public string DisplayDescription => LocalizationManager.Get($"META_{GetLocalizationId()}_DESC", description);

    public string GetLocalizationId()
    {
        string raw = !string.IsNullOrEmpty(id) ? id : (!string.IsNullOrEmpty(upgradeName) ? upgradeName : name);
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

    [Header("── Pozycja w grafie ─────────────────────")]
    public Vector2 nodePosition;

    [Header("── Stan ─────────────────────────────────")]
    public bool isUnlocked = false;

    [Header("── Wymagania ────────────────────────────")]
    public List<MetaUpgradeSO> prerequisites;

    [Header("── Efekt ────────────────────────────────")]
    [Tooltip("Co robi ten upgrade. None = tylko wizualny / zarezerwowany.")]
    public MetaEffectType effectType = MetaEffectType.None;

    [Tooltip("Główna wartość efektu.\n" +
             "Surowce: ile dodać (np. 50)\n" +
             "Mnożniki: wartość mnożnika (np. 0.1 = +10%)\n" +
             "Redukcje: procent redukcji (np. 0.2 = -20%)\n" +
             "HP: ile dodać (np. 5)\n" +
             "Brak wartości dla WallSystem efektów.")]
    public float effectValue = 0f;

    [Tooltip("Opcjonalny drugi parametr.\n" +
             "EliteChanceBoost: od której fali boost działa.")]
    public float effectValue2 = 0f;

    [Tooltip("Wymagany dla: SpecificBuildingUpgradeCostReduction, BuildingTerrainBonusMultiplier.\n" +
             "Wskazuje konkretny budynek którego dotyczy efekt.")]
    public BuildingData targetBuilding;

    [Header("── Warianty A/B (Opcjonalne) ───────────────")]
    [Tooltip("Czy ten węzeł posiada wzajemnie wykluczające się warianty specjalizacji A/B.")]
    public bool hasVariants = false;

    [Tooltip("Czy ten asset jest opcją wariantu podpiętą pod węzeł nadrzędny (niewyświetlany bezpośrednio na grafie 2D).")]
    public bool isVariantOption = false;

    [Tooltip("Referencja do wariantu A")]
    public MetaUpgradeSO variantA;

    [Tooltip("Referencja do wariantu B")]
    public MetaUpgradeSO variantB;

    // =========================================================================
    // HELPERS (tylko edytor + runtime)
    // =========================================================================

    /// <summary>Czy ten upgrade wymaga wskazania konkretnego budynku?</summary>
    public bool RequiresTargetBuilding =>
        effectType == MetaEffectType.SpecificBuildingUpgradeCostReduction ||
        effectType == MetaEffectType.BuildingTerrainBonusMultiplier||
        effectType == MetaEffectType.SpecificBuildingProductionBonus;

    /// <summary>
    /// Zwraca aktywny wariant ("A", "B" lub null) na podstawie listy odblokowanych ID.
    /// </summary>
    public string GetActiveVariant(List<string> unlockedIDs)
    {
        if (!hasVariants || unlockedIDs == null) return null;
        if (variantB != null && unlockedIDs.Contains(variantB.id)) return "B";
        if (variantA != null && unlockedIDs.Contains(variantA.id)) return "A";
        return null;
    }

    /// <summary>Czy wszystkie wymagania są spełnione?</summary>
    public bool ArePrerequisitesMet()
    {
        if (prerequisites == null) return true;
        foreach (var req in prerequisites)
            if (!ReferenceEquals(req, null) && !req.isUnlocked) return false;
        return true;
    }
}
