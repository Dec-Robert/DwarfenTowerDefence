using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChunkReconIntel
{
    public bool hasIntel;
    public bool hasEnemyPath;
    public bool isCrossroads;
    public Dictionary<HexFeatureType, ReconAbundance> features = new Dictionary<HexFeatureType, ReconAbundance>();

    public ReconAbundance GetAbundance(HexFeatureType feature)
    {
        return features != null && features.TryGetValue(feature, out var ab) ? ab : ReconAbundance.None;
    }

    public static string GetAbundanceLabel(ReconAbundance abundance)
    {
        switch (abundance)
        {
            case ReconAbundance.Scarce: return "Znikome";
            case ReconAbundance.Moderate: return "Umiarkowane";
            case ReconAbundance.Abundant: return "Obfite";
            default: return "Brak";
        }
    }

    public static string GetFeatureDisplayName(HexFeatureType feature)
    {
        switch (feature)
        {
            case HexFeatureType.Forest: return "Las";
            case HexFeatureType.Mountain: return "Góra";
            case HexFeatureType.Hill: return "Wzgórze";
            case HexFeatureType.FertileSoil: return "Żyzna gleba";
            case HexFeatureType.Sinkhole: return "Zapadlina";
            default: return feature.ToString();
        }
    }
}

[Serializable]
public class ChunkStateData
{
    public Vector2Int chunkCoord;
    public ChunkState baseState = ChunkState.Wilderness;
    public OutpostEntity activeOutpost;
    public bool isRoadChunk;

    /// <summary>
    /// Lista chunków poprzedzających (bliżej bazy) wzdłuż drogi.
    /// W topologii pętli i rozdroży chunk może mieć wielu poprzedników (np. Rozdroże 2 ma nitkę główną i nitkę pętli).
    /// </summary>
    public List<Vector2Int> roadPredecessors = new List<Vector2Int>();

    /// <summary>
    /// Właściwość wstecznej kompatybilności zwracająca pierwszego poprzednika lub sentinel (-999, -999).
    /// Setter czyści listę i dodaje nową wartość (jeśli nie jest sentinelem).
    /// </summary>
    public Vector2Int roadPredecessor
    {
        get => roadPredecessors != null && roadPredecessors.Count > 0
            ? roadPredecessors[0]
            : new Vector2Int(-999, -999);
        set
        {
            if (roadPredecessors == null)
                roadPredecessors = new List<Vector2Int>();
            else
                roadPredecessors.Clear();

            if (value.x != -999 || value.y != -999)
            {
                roadPredecessors.Add(value);
            }
        }
    }

    /// <summary>
    /// Właściwość określająca, czy chunk ma zdefiniowanego przynajmniej jednego poprzednika drogowego.
    /// Setter zachowany dla wstecznej kompatybilności z instrukcjami przypisania (np. data.hasRoadPredecessor = ...).
    /// </summary>
    public bool hasRoadPredecessor
    {
        get => roadPredecessors != null && roadPredecessors.Count > 0;
        set
        {
            // Setter no-op: stan wynika dynamicznie z roadPredecessors.Count > 0.
        }
    }

    public int surveyDaysRemaining;
    public int outpostSettlementDaysRemaining;

    // --- Zwiad (Recon) i Misje ---
    public ChunkReconIntel reconIntel = new ChunkReconIntel();
    public bool isReconActive;

    public bool hasReconIntel => reconIntel != null && reconIntel.hasIntel;
    public bool isSurveyActive => baseState == ChunkState.Surveying;
    public bool hasActiveMission => isReconActive || isSurveyActive;

    public ChunkState EffectiveState
    {
        get
        {
            if (baseState == ChunkState.Outskirts && activeOutpost != null)
            {
                return ChunkState.Settled;
            }
            return baseState;
        }
    }

    public bool CanBuildDefense => EffectiveState == ChunkState.Outskirts || EffectiveState == ChunkState.Settled;

    public bool CanBuildEconomic => EffectiveState == ChunkState.Settled;

    /// <summary>Bezpiecznie dodaje poprzednika drogowego, o ile nie jest sentinelem.</summary>
    public void AddRoadPredecessor(Vector2Int pred)
    {
        if (pred.x == -999 && pred.y == -999) return;
        if (roadPredecessors == null) roadPredecessors = new List<Vector2Int>();
        if (!roadPredecessors.Contains(pred)) roadPredecessors.Add(pred);
    }
}