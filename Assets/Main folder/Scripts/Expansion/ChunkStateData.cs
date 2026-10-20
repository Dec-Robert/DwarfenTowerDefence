using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChunkReconIntel
{
    public bool hasIntel;
    public bool hasEnemyPath;
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
            case HexFeatureType.Meadow: return "Łąka";
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
    public Vector2Int roadPredecessor = new Vector2Int(-999, -999);
    public bool hasRoadPredecessor;
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
}