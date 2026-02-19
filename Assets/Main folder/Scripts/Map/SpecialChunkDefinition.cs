using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enum typów chunków – rozszerza istniejący system o Special.
/// </summary>
public enum ChunkType
{
    Normal,
    Spawner,      // Chunk startowy wrogów
    MetaSafe,     // Bezpieczna strefa (baza, ekspansje meta)
    Special       // Specjalny chunk z unikalną mechaniką
}

/// <summary>
/// Definiuje specjalny chunk jako ScriptableObject.
/// Tworzysz jeden plik per typ (np. "Ruins", "BanditCamp", "Treasure").
///
/// UŻYCIE: Dodaj do listy specialChunkTypes w HexMapGenerator.
/// </summary>
[CreateAssetMenu(fileName = "NewSpecialChunk", menuName = "City Builder/Special Chunk Definition")]
public class SpecialChunkDefinition : ScriptableObject
{
    [Header("Identyfikacja")]
    public string chunkId;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Warunki Spawnu")]
    [Tooltip("Szansa na pojawienie się (0-100)")]
    [Range(0, 100)] public int spawnChance = 15;

    [Tooltip("Minimalna i maksymalna odległość od bazy (w chunkach)")]
    public Vector2Int distanceRange = new Vector2Int(3, 8);

    [Tooltip("W jakich biomach może się pojawić (puste = wszystkie)")]
    public List<BiomeType> allowedBiomes = new List<BiomeType>();

    [Tooltip("Czy może pojawić się tylko raz na mapie?")]
    public bool uniquePerMap = true;

    [Header("Układ Terenu")]
    [Tooltip("Opcjonalny layout nadpisujący teren (jak MetaChunk)")]
    public ChunkLayoutSO layoutOverride;

    [Header("Zasoby Startowe")]
    [Tooltip("Predefiniowane zasoby zawsze obecne w tym chunku")]
    public List<ResourceDeposit> guaranteedResources = new List<ResourceDeposit>();

    [Header("Mechaniki")]
    public SpecialChunkMechanics mechanics;

    // =========================================================================
    // Struktury pomocnicze
    // =========================================================================

    [System.Serializable]
    public class ResourceDeposit
    {
        public HexFeatureType featureType;
        [Tooltip("Ilość hexów z tym zasobem")]
        [Range(1, 10)] public int count = 3;
        public bool avoidPaths = true;
    }

    [System.Serializable]
    public class SpecialChunkMechanics
    {
        [Tooltip("Czy chunk wymaga ekspedycji żeby odblokować zasoby?")]
        public bool requiresExpedition = false;

        [Tooltip("Czy zawiera jednorazowy łup do zebrania?")]
        public bool hasOneShotLoot = false;

        [Tooltip("Zasoby do zdobycia jednorazowo")]
        public List<BuildingData.ResourceCost> lootTable = new List<BuildingData.ResourceCost>();

        [Tooltip("Czy modyfikuje wrogów w tym chunku?")]
        public bool hasEnemyModifier = false;
        public float enemyHpMultiplier = 1f;
        public float enemyCountMultiplier = 1f;
    }
}