using System.Collections.Generic;
using UnityEngine;

public static class BuildingProductionRegistry
{
    // ZMIANA: Kluczem jest teraz konkretny komponent HexCell, a nie pozycja Vector3
    private static Dictionary<HexCell, int> resourceUsageCount = new Dictionary<HexCell, int>();

    public static void RegisterUsage(HexCell cell)
    {
        if (cell == null) return;

        if (!resourceUsageCount.ContainsKey(cell)) resourceUsageCount[cell] = 0;
        resourceUsageCount[cell]++;
    }

    public static void UnregisterUsage(HexCell cell)
    {
        if (cell != null && resourceUsageCount.ContainsKey(cell))
        {
            resourceUsageCount[cell]--;
            if (resourceUsageCount[cell] <= 0) resourceUsageCount.Remove(cell);
        }
    }

    public static int GetUsageCount(HexCell cell)
    {
        if (cell == null) return 0;
        return resourceUsageCount.ContainsKey(cell) ? resourceUsageCount[cell] : 0;
    }

    public static void Clear() => resourceUsageCount.Clear();
}