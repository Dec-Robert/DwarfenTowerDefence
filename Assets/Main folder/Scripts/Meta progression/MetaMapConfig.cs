using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MetaMapConfig", menuName = "Map/Meta Map Config")]
public class MetaMapConfig : ScriptableObject
{
    [Header("Baza (0,0)")]
    public ChunkLayoutSO startingChunkBase;

    [Header("Chunki Ekspansji (Odblokowywanie terenu)")]
    public List<ExpansionChunkLink> expansions;

    [Header("Modyfikatory Terenu (Nak³adki)")]
    // To definiuje zmiany na istniej¹cych chunkach (np. wiêcej lasu na -1,0)
    public List<ChunkModifierLink> globalModifiers;

    [System.Serializable]
    public struct ExpansionChunkLink
    {
        public Vector2Int chunkCoordinate;
        public MetaUpgradeSO requiredUpgrade;
        public ChunkLayoutSO baseLayout;
    }

    [System.Serializable]
    public struct ChunkModifierLink
    {
        public string name; // Dla czytelnoœci w edytorze
        public Vector2Int targetChunk;        // Na którym chunku to na³o¿yæ (np. -1, 0)
        public MetaUpgradeSO requiredUpgrade; // Jakie ulepszenie to aktywuje
        public ChunkLayoutSO modifierLayout;  // Co zmieniamy (zawiera tylko heksy do zmiany)
    }
}