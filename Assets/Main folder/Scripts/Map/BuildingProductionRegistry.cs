using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class BuildingProductionRegistry
{
    private class HexCellReferenceComparer : IEqualityComparer<HexCell>
    {
        public static readonly HexCellReferenceComparer Instance = new HexCellReferenceComparer();
        public bool Equals(HexCell x, HexCell y) => ReferenceEquals(x, y);
        public int GetHashCode(HexCell obj) => obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }

    private static readonly Dictionary<HexCell, int> resourceUsageCount = new Dictionary<HexCell, int>(HexCellReferenceComparer.Instance);

    public static void RegisterUsage(HexCell cell)
    {
        if (ReferenceEquals(cell, null)) return;

        if (!resourceUsageCount.ContainsKey(cell)) resourceUsageCount[cell] = 0;
        resourceUsageCount[cell]++;
    }

    public static void UnregisterUsage(HexCell cell)
    {
        if (!ReferenceEquals(cell, null) && resourceUsageCount.ContainsKey(cell))
        {
            resourceUsageCount[cell]--;
            if (resourceUsageCount[cell] <= 0) resourceUsageCount.Remove(cell);
        }
    }

    public static int GetUsageCount(HexCell cell)
    {
        if (ReferenceEquals(cell, null)) return 0;
        return resourceUsageCount.ContainsKey(cell) ? resourceUsageCount[cell] : 0;
    }

    public static void Clear() => resourceUsageCount.Clear();
}
