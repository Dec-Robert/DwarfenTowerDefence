using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RuneSystemConfig", menuName = "Tower Defense/Rune System Config")]
public class RuneSystemConfigSO : ScriptableObject
{
    [Header("── Szanse na Drop z Wrogów (w %) ──")]
    [Range(0f, 100f)] public float dropChanceNormal = 2f;
    [Range(0f, 100f)] public float dropChanceElite = 100f;
    public int minRunesFromBoss = 2;
    public int maxRunesFromBoss = 4;

    [Header("── Wagi Rzadkości (Bazowe) ──")]
    public float weightCommon = 60f;
    public float weightUncommon = 20f;
    public float weightRare = 10f;
    public float weightLegendary = 6f;
    public float weightCursed = 4f;

    [Header("── Pule Run do Losowania ──")]
    public List<RuneDefinitionSO> commonRunes;
    public List<RuneDefinitionSO> uncommonRunes;
    public List<RuneDefinitionSO> rareRunes;
    public List<RuneDefinitionSO> legendaryRunes;
    public List<RuneDefinitionSO> cursedRunes;
}