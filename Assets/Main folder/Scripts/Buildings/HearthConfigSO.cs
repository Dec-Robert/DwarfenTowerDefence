using System;
using UnityEngine;

[Serializable]
public class HearthLevelData
{
    [Header("Podstawowe informacje")]
    public int levelIndex = 1;
    public string title = "Poziom Paleniska";
    public string tagText = "NEUTRALNY";
    public Color accentColor = new Color(0.28f, 0.56f, 0.78f, 1f);
    public Sprite levelIcon;

    [Header("Koszt rozpalenia")]
    [Min(0)]
    public int coalCost = 0;

    [Header("Modyfikatory Nocne (np. -0.20 = -20%, +0.35 = +35%)")]
    [Tooltip("Modyfikator obrażeń wszystkich wież w nocy (TowerStatType.Damage)")]
    public float towerDamageBonus = 0f;

    [Tooltip("Modyfikator zasięgu i szybkostrzelności wież w nocy (TowerStatType.Range i FireRate)")]
    public float towerRangeAndRateBonus = 0f;

    [Tooltip("Modyfikator PŻ (HP) oraz prędkości ruchu wrogów w nocy (+0.15 = wrogowie silniejsi o 15%, -0.15 = wrogowie słabsi o 15%)")]
    public float enemyStatsBonus = 0f;

    [Tooltip("Modyfikator szansy na drop runy oraz wag rzadkich run w nocy (+0.35 = +35% więcej/rzadszych run)")]
    public float runeDropAndRarityBonus = 0f;

    [Header("Modyfikatory Następnego Dnia (np. -0.25 = -25%, +0.35 = +35%)")]
    [Tooltip("Globalny modyfikator produkcji wszystkich budynków ekonomicznych następnego dnia")]
    public float nextDayProductionBonus = 0f;
}

[CreateAssetMenu(fileName = "HearthConfig", menuName = "RTS/Buildings/Hearth Config")]
public class HearthConfigSO : ScriptableObject
{
    public HearthLevelData[] levels = new HearthLevelData[5];

    private void OnEnable()
    {
        EnsureDefaultLevels();
    }

    private void OnValidate()
    {
        EnsureDefaultLevels();
    }

    public void EnsureDefaultLevels()
    {
        if (levels == null || levels.Length != 5)
        {
            levels = new HearthLevelData[5];
        }

        if (levels[0] == null || string.IsNullOrEmpty(levels[0].title))
        {
            levels[0] = new HearthLevelData
            {
                levelIndex = 1,
                title = "Wygasły Popiół",
                tagText = "RYZYKO / ŁUPY",
                accentColor = new Color(0.58f, 0.36f, 0.82f, 1f),
                coalCost = 0,
                towerDamageBonus = -0.20f,
                towerRangeAndRateBonus = -0.20f,
                enemyStatsBonus = 0.15f,
                runeDropAndRarityBonus = 0.35f,
                nextDayProductionBonus = -0.25f
            };
        }

        if (levels[1] == null || string.IsNullOrEmpty(levels[1].title))
        {
            levels[1] = new HearthLevelData
            {
                levelIndex = 2,
                title = "Tlący się Żar",
                tagText = "OSZCZĘDNOŚĆ",
                accentColor = new Color(0.75f, 0.42f, 0.32f, 1f),
                coalCost = 15,
                towerDamageBonus = -0.10f,
                towerRangeAndRateBonus = -0.10f,
                enemyStatsBonus = 0.05f,
                runeDropAndRarityBonus = 0.15f,
                nextDayProductionBonus = -0.10f
            };
        }

        if (levels[2] == null || string.IsNullOrEmpty(levels[2].title))
        {
            levels[2] = new HearthLevelData
            {
                levelIndex = 3,
                title = "Zrównoważony Płomień",
                tagText = "NEUTRALNY",
                accentColor = new Color(0.28f, 0.56f, 0.78f, 1f),
                coalCost = 35,
                towerDamageBonus = 0f,
                towerRangeAndRateBonus = 0f,
                enemyStatsBonus = 0f,
                runeDropAndRarityBonus = 0f,
                nextDayProductionBonus = 0f
            };
        }

        if (levels[3] == null || string.IsNullOrEmpty(levels[3].title))
        {
            levels[3] = new HearthLevelData
            {
                levelIndex = 4,
                title = "Jasny Ogień",
                tagText = "WZMOCNIENIE",
                accentColor = new Color(0.85f, 0.62f, 0.18f, 1f),
                coalCost = 65,
                towerDamageBonus = 0.15f,
                towerRangeAndRateBonus = 0.15f,
                enemyStatsBonus = -0.05f,
                runeDropAndRarityBonus = -0.10f,
                nextDayProductionBonus = 0.15f
            };
        }

        if (levels[4] == null || string.IsNullOrEmpty(levels[4].title))
        {
            levels[4] = new HearthLevelData
            {
                levelIndex = 5,
                title = "Palenisko Magmy",
                tagText = "MAKS. MOC",
                accentColor = new Color(0.95f, 0.36f, 0.12f, 1f),
                coalCost = 110,
                towerDamageBonus = 0.35f,
                towerRangeAndRateBonus = 0.35f,
                enemyStatsBonus = -0.15f,
                runeDropAndRarityBonus = -0.25f,
                nextDayProductionBonus = 0.35f
            };
        }
    }

    public HearthLevelData GetLevelData(int levelIndex)
    {
        EnsureDefaultLevels();
        int idx = Mathf.Clamp(levelIndex - 1, 0, levels.Length - 1);
        return levels[idx];
    }
}
