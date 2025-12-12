using UnityEngine;
using System;

// Typy terenu/obiektów na mapie
public enum HexFeatureType
{
    None,           // Pusta trawa
    Forest,         // Las (wymagany do Tartaku)
    Mountain,       // Góra (blokuje wizjê, wymagana do Kopalni)
    Hill,           // Wzgórze (bonus do zasiêgu)
    Sinkhole,       // Zapadlina (bonus dla Archeologa)
    FertileSoil,    // ¯yzna gleba (bonus dla Farmy)
    Base,           // Kapitol
    Beacon,         // Beacon of Hope
    Wall            // Mur (generowany na granicy)
}

// Typy biomów dla Chunków
public enum BiomeType
{
    Plains,     // Domyœlny (Zbalansowany)
    Forest,     // Du¿o lasów, ma³o gór
    Mountains,  // Du¿o gór i wzgórz
    Volcano,    // P³asko (brak os³on)
    Permafrost  // (Do zdefiniowania póŸniej)
}

// Klasa przechowuj¹ca dane pojedynczego pola
[Serializable]
public class HexCellData
{
    public Vector2Int chunkCoord;   // W którym chunku jest ten heks
    public Vector2Int localCoord;   // Koordynaty q,r wewn¹trz chunku
    public Vector2Int gridCoord;    // Globalne koordynaty (opcjonalne, do ³atwiejszego dostêpu)

    public HexFeatureType feature = HexFeatureType.None;
    public int featureLevel = 0;    // Dla Wzgórz (+1 do +5) i Zapadlin (-1 do -5)

    public bool isPath = false;     // Czy to jest droga wroga?

    // Tu bêdziemy trzymaæ budynek, jeœli zostanie zbudowany
    // public Building constructedBuilding; 
}