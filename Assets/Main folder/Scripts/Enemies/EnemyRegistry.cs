using System.Collections.Generic;
using UnityEngine;

public static class EnemyRegistry
{
    private static readonly List<EnemyStats> s_ActiveEnemies = new List<EnemyStats>(128);

    public static List<EnemyStats> ActiveEnemies => s_ActiveEnemies;

    public static void Register(EnemyStats enemy)
    {
        if (enemy == null) return;
        if (!s_ActiveEnemies.Contains(enemy))
        {
            s_ActiveEnemies.Add(enemy);
        }
    }

    public static void Unregister(EnemyStats enemy)
    {
        if (enemy == null) return;
        s_ActiveEnemies.Remove(enemy);
    }

    public static void Clear()
    {
        s_ActiveEnemies.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_ActiveEnemies.Clear();
    }
}
