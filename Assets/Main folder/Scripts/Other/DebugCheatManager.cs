using UnityEngine;
using System; // Potrzebne do iterowania po Enumach

public class DebugCheatManager : MonoBehaviour
{
    void Update()
    {
        // F1 - Zabij wszystkich wrogów
        if (Input.GetKeyDown(KeyCode.F1))
        {
            KillAllEnemies();
        }

        // F2 - Dodaj surowce
        if (Input.GetKeyDown(KeyCode.F2))
        {
            AddAllResources();
        }
    }

    void KillAllEnemies()
    {
        // Znajdujemy wszystkich wrogów na scenie
        EnemyStats[] allEnemies = FindObjectsOfType<EnemyStats>();

        foreach (var enemy in allEnemies)
        {
            if (enemy != null)
            {
                // Zadajemy obra¿enia "nieskoñczone", ¿eby na pewno zgin¹³
                // U¿ywamy DamageType.Physical (lub dowolnego innego), system i tak to przetworzy
                enemy.TakeDamage(999999f, DamageType.Physical,100,100,true,1000);
            }
        }

        Debug.Log($"<color=red>[DEBUG] Zabito {allEnemies.Length} wrogów.</color>");
    }

    void AddAllResources()
    {
        if (ResourceManager.Instance == null) return;

        // Iterujemy po wszystkich typach surowców z Enuma ResourceType
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            ResourceManager.Instance.AddResource(type, 200);
        }

        Debug.Log("<color=green>[DEBUG] Dodano po 200 sztuk ka¿dego surowca.</color>");
    }
}