using System;
using UnityEngine;
using UnityEngine.Serialization;

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
    [Tooltip("Modyfikator szybkostrzelności wszystkich wież w nocy (TowerStatType.FireRate)")]
    [FormerlySerializedAs("towerRangeAndRateBonus")]
    public float towerFireRateBonus = 0f;

    [Tooltip("Modyfikator zasięgu wież w nocy (TowerStatType.Range)")]
    public float towerRangeBonus = 0f;

    [FormerlySerializedAs("towerDamageBonus")]
    [HideInInspector]
    public float towerDamageBonus = 0f;

    [Tooltip("Modyfikator PŻ (HP) oraz prędkości ruchu wrogów w nocy (+0.15 = wrogowie silniejsi o 15%, -0.15 = wrogowie słabsi o 15%)")]
    public float enemyStatsBonus = 0f;

    [Tooltip("Modyfikator szansy na drop runy oraz wag rzadkich run w nocy (+0.35 = +35% więcej/rzadszych run)")]
    public float runeDropAndRarityBonus = 0f;

    [Header("Drop Etheru z Fali")]
    [Tooltip("Etykieta szansy na Ether widoczna dla gracza (np. Ogromne, Duże, Średnie, Niskie, Zerowe)")]
    public string etherChanceTierLabel = "Średnie";

    [Tooltip("Gwarantowany procent puli Etheru fali (np. 0.8 = 80%)")]
    [Range(0f, 1f)]
    public float guaranteedEtherPercent = 0.4f;

    [Tooltip("Szansa na naturalny drop pozostałego Etheru z zabitego zwykłego wroga (domyślnie 0.05 = 5%)")]
    [Range(0f, 1f)]
    public float naturalEtherDropChance = 0.05f;

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
                towerDamageBonus = 0f,
                towerFireRateBonus = -0.20f,
                towerRangeBonus = -0.20f,
                enemyStatsBonus = 0.15f,
                runeDropAndRarityBonus = 0.35f,
                etherChanceTierLabel = "Ogromne",
                guaranteedEtherPercent = 0.80f,
                naturalEtherDropChance = 0.05f,
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
                towerDamageBonus = 0f,
                towerFireRateBonus = -0.10f,
                towerRangeBonus = -0.10f,
                enemyStatsBonus = 0.05f,
                runeDropAndRarityBonus = 0.15f,
                etherChanceTierLabel = "Duże",
                guaranteedEtherPercent = 0.60f,
                naturalEtherDropChance = 0.05f,
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
                towerFireRateBonus = 0f,
                towerRangeBonus = 0f,
                enemyStatsBonus = 0f,
                runeDropAndRarityBonus = 0f,
                etherChanceTierLabel = "Średnie",
                guaranteedEtherPercent = 0.40f,
                naturalEtherDropChance = 0.05f,
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
                towerDamageBonus = 0f,
                towerFireRateBonus = 0.15f,
                towerRangeBonus = 0.15f,
                enemyStatsBonus = -0.05f,
                runeDropAndRarityBonus = -0.10f,
                etherChanceTierLabel = "Niskie",
                guaranteedEtherPercent = 0.20f,
                naturalEtherDropChance = 0.05f,
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
                towerDamageBonus = 0f,
                towerFireRateBonus = 0.35f,
                towerRangeBonus = 0.35f,
                enemyStatsBonus = -0.15f,
                runeDropAndRarityBonus = -0.25f,
                etherChanceTierLabel = "Zerowe",
                guaranteedEtherPercent = 0.00f,
                naturalEtherDropChance = 0.05f,
                nextDayProductionBonus = 0.35f
            };
        }

        // Migracja / sanity-check dla istniejących assetów na dysku
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != null)
            {
                levels[i].towerDamageBonus = 0f;
                if (Mathf.Approximately(levels[i].towerRangeBonus, 0f) && !Mathf.Approximately(levels[i].towerFireRateBonus, 0f))
                {
                    levels[i].towerRangeBonus = levels[i].towerFireRateBonus;
                }
            }
        }

        // Uzupełnij brakujące dane Etheru dla istniejących assetów na dysku
        if (levels[0] != null && string.IsNullOrEmpty(levels[0].etherChanceTierLabel))
        {
            levels[0].etherChanceTierLabel = "Ogromne";
            levels[0].guaranteedEtherPercent = 0.80f;
            levels[0].naturalEtherDropChance = 0.05f;
        }
        if (levels[1] != null && string.IsNullOrEmpty(levels[1].etherChanceTierLabel))
        {
            levels[1].etherChanceTierLabel = "Duże";
            levels[1].guaranteedEtherPercent = 0.60f;
            levels[1].naturalEtherDropChance = 0.05f;
        }
        if (levels[2] != null && string.IsNullOrEmpty(levels[2].etherChanceTierLabel))
        {
            levels[2].etherChanceTierLabel = "Średnie";
            levels[2].guaranteedEtherPercent = 0.40f;
            levels[2].naturalEtherDropChance = 0.05f;
        }
        if (levels[3] != null && string.IsNullOrEmpty(levels[3].etherChanceTierLabel))
        {
            levels[3].etherChanceTierLabel = "Niskie";
            levels[3].guaranteedEtherPercent = 0.20f;
            levels[3].naturalEtherDropChance = 0.05f;
        }
        if (levels[4] != null && string.IsNullOrEmpty(levels[4].etherChanceTierLabel))
        {
            levels[4].etherChanceTierLabel = "Zerowe";
            levels[4].guaranteedEtherPercent = 0.00f;
            levels[4].naturalEtherDropChance = 0.05f;
        }
    }

    public HearthLevelData GetLevelData(int levelIndex)
    {
        EnsureDefaultLevels();
        int idx = Mathf.Clamp(levelIndex - 1, 0, levels.Length - 1);
        return levels[idx];
    }
}
