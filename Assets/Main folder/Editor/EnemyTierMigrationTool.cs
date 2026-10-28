using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyTierMigrationTool
{
    private const string EnemiesFolderPath = "Assets/Main folder/ScriptableObjects/Enemies";
    private const string WavesFolderPath = "Assets/Main folder/ScriptableObjects/PredefinedWaves";
    private const string MigrationPrefKey = "EnemyTiers_Migrated_v1";

    static EnemyTierMigrationTool()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorPrefs.GetBool(MigrationPrefKey, false))
            {
                EditorPrefs.SetBool(MigrationPrefKey, true);
                int enemies = MigrateEnemies();
                int waves = MigrateWaves();
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>[EnemyTierMigration] Automatyczna migracja w tle: {enemies} wrogów i {waves} fal zaktualizowano do systemu 4 Tierów!</color>");
            }
        };
    }

    [MenuItem("Tools/Waves/Perform Full Tier Migration", priority = 100)]
    public static void MigrateAll()
    {
        int enemiesCount = MigrateEnemies();
        int wavesCount = MigrateWaves();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>[EnemyTierMigration] Pomyślnie zmigrowano {enemiesCount} assetów EnemyData oraz {wavesCount} assetów WaveDefinition do systemu 4 Tierów!</color>");
        EditorUtility.DisplayDialog("Migracja Tierów", $"Zakończono pomyślnie!\n\nZmigrowano:\n- {enemiesCount} wrogów (EnemyData)\n- {wavesCount} fal (WaveDefinition)\n\nWszystkie wpisy fal posiadają teraz domyślny Tier 1, a wrogowie mają 4 tiery statystyk.", "OK");
    }

    public static int MigrateEnemies()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyData", new[] { EnemiesFolderPath });
        if (guids == null || guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("t:EnemyData");
        }

        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null) continue;

            Undo.RecordObject(data, "Migrate Enemy Tiers");
            data.EnsureTiers();

            // Upewnij się, że każdy z 4 tierów ma poprawne wartości
            for (int i = 0; i < 4; i++)
            {
                var t = data.tiers[i];
                if (t.prefab == null && data.prefab != null) t.prefab = data.prefab;
                if (t.sprite == null && data.sprite != null) t.sprite = data.sprite;
                if (t.baseHp <= 0f && data.baseHp > 0f) t.baseHp = data.baseHp;
                if (t.moveSpeed <= 0f && data.moveSpeed > 0f) t.moveSpeed = data.moveSpeed;
            }

            data.baseHp = data.tiers[0].baseHp;
            data.moveSpeed = data.tiers[0].moveSpeed;
            data.armor = data.tiers[0].armor;
            data.magicResist = data.tiers[0].magicResist;
            data.dodgeChance = data.tiers[0].dodgeChance;

            EditorUtility.SetDirty(data);
            count++;
        }

        return count;
    }

    public static int MigrateWaves()
    {
        string[] guids = AssetDatabase.FindAssets("t:WaveDefinition", new[] { WavesFolderPath });
        if (guids == null || guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("t:WaveDefinition");
        }

        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WaveDefinition wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            if (wave == null) continue;

            Undo.RecordObject(wave, "Migrate Wave Entries");
            bool changed = false;

            if (wave.enemies != null)
            {
                for (int i = 0; i < wave.enemies.Count; i++)
                {
                    var entry = wave.enemies[i];
                    if (entry.tier < 1 || entry.tier > 4)
                    {
                        entry.tier = 1;
                        wave.enemies[i] = entry;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(wave);
            }
            count++;
        }

        return count;
    }
}
