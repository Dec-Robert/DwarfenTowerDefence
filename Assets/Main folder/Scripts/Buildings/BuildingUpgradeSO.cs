using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Game/Buildings/Building Upgrade")]
public class BuildingUpgradeSO : ScriptableObject
{
    [Header("Informacje")] public string upgradeName;

    public Sprite icon;
    [TextArea] public string description;

    public string DisplayName => LocalizationManager.Get($"UPGRADE_{GetLocalizationId()}_NAME", upgradeName);
    public string DisplayDescription => LocalizationManager.Get($"UPGRADE_{GetLocalizationId()}_DESC", description);

    public string GetLocalizationId()
    {
        string raw = !string.IsNullOrEmpty(upgradeName) ? upgradeName : name;
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

    [Header("Koszt")] public List<ResourceCost> cost;

    [Header("Efekty Statystyczne (Addytywne)")]
    // Np. jeśli tartak produkuje 5, a tu wpiszemy Wood: 2, to będzie produkować 7.
    public List<ResourceCost> productionBonus;

    public List<ResourceCost> upkeepIncrease;

    [Tooltip(
        "Globalny mnożnik dodawany do CAŁEJ produkcji budynku (baza + teren). Np. 0.2 to +20% ogólnej wydajności.")]
    public float globalProductionMultiplierBonus;

    [Tooltip("Globalny mnożnik dodawany do CAŁYCH kosztów utrzymania. Np. 0.5 to +50% kosztów węgla i Kryształu Eteru.")]
    public float globalUpkeepMultiplierBonus;

    [Header("Modyfikatory Terenu")]
    [Tooltip("Płaski bonus DODAWANY do wartości każdego sąsiadującego heksa (np. +1.5 drewna za las)")]
    public float terrainFlatBonus;

    [Tooltip("Mnożnik PROCENTOWY zysku z terenu (np. 0.15 = +15% mocy, -0.25 = -25% mocy)")]
    public float terrainPercentBonus;

    // Dodaj te pola do klasy BuildingUpgradeSO:
    [Header("Bonusy do Miejsc Pracy")] public int extraShifts; // Np. +1 Zmiana

    public int extraWorkersPerShift; // Np. +1 Pracownik na zmianę

    [Header("Logika Specjalna")]
    // Np. "AUTO_REPLANT" - ID dla skryptu, żeby wiedział co robić
    public List<string> specialEffectIDs = new();

    [Header("Drzewko Rozwoju")]
    // Jakie ulepszenia staną się dostępne po wykupieniu tego? (To jest ten Tier + 1)
    public List<BuildingUpgradeSO> nextTierOptions;
}
