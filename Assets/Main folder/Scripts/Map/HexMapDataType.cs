using UnityEngine;
using System;

// Typy terenu/obiekt�w na mapie
public enum HexFeatureType
{
    None,           // Pusta trawa
    Forest,         // Las (wymagany do Tartaku)
    Mountain,       // G�ra (blokuje wizj�, wymagana do Kopalni)
    Hill,           // Wzg�rze (bonus do zasi�gu)
    Sinkhole,       // Zapadlina (bonus dla Archeologa)
    FertileSoil,    // żyzna gleba (bonus dla Farmy)
    Base,           // Kapitol
    Beacon,         // Beacon of Hope
    Wall            // Mur (generowany na granicy)
}

// Typy biom�w dla Chunk�w
public enum BiomeType
{
    Plains,     // Domy�lny (Zbalansowany)
    Forest,     // Du�o las�w, ma�o g�r
    Mountains,  // Du�o g�r i wzg�rz
    Volcano,    // P�asko (brak os�on)
    Permafrost  // (Do zdefiniowania p�niej)
}

// Klasa przechowuj�ca dane pojedynczego pola
[Serializable]
public class HexCellData
{
    public Vector2Int chunkCoord;   // W kt�rym chunku jest ten heks
    public Vector2Int localCoord;   // Koordynaty q,r wewn�trz chunku
    public Vector2Int gridCoord;    // Globalne koordynaty (opcjonalne, do �atwiejszego dost�pu)

    public HexFeatureType feature = HexFeatureType.None;
    public int featureLevel = 0;    // Dla Wzg�rz (+1 do +5) i Zapadlin (-1 do -5)

    public bool isPath = false;     // Czy to jest droga wroga?

    public BuildingData startingBuilding;
    // Tu b�dziemy trzyma� budynek, je�li zostanie zbudowany
    // public Building constructedBuilding; 
}