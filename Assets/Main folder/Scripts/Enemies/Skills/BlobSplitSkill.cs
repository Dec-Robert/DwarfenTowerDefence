using UnityEngine;
using System.Collections.Generic;

public class BlobSplitSkill : EnemySkill
{
    [Header("Konfiguracja")]
    public int splitCharges = 1; // Ile razy ta linia mo¿e siê podzieliæ (Blob -> 2 Ma³e -> koniec)
    public float splitThreshold = 0.5f; // 50% HP
    public float childHpPercent = 0.7f; // 70% HP rodzica
    public float scaleReduction = 0.7f; // Dzieci s¹ mniejsze

    private bool hasSplit = false;

    public override void OnDamageTaken(float amount, float currentHp)
    {
        if (splitCharges <= 0 || hasSplit) return;

        float maxHp = stats.GetMaxHealth(); // Potrzebujemy metody w stats

        if (currentHp <= maxHp * splitThreshold)
        {
            Split();
        }
    }

    void Split()
    {
        hasSplit = true;
        Debug.Log($"{name} dzieli siê!");

        // Spawnowanie 2 dzieci
        for (int i = 0; i < 2; i++)
        {
            // POPRAWKA: Klonujemy stats.gameObject (CA£EGO WROGA), a nie gameObject (tylko skrypt skilla)
            GameObject childObj = Instantiate(stats.gameObject, transform.position, transform.rotation);

            // Konfiguracja Walkera
            EnemyWalker childWalker = childObj.GetComponent<EnemyWalker>();
            // Pobieramy walkera rodzica (z tego skryptu mamy do niego referencjê)

            if (walker != null && childWalker != null)
            {
                childWalker.CopyProgressFrom(walker);
            }

            // Konfiguracja Statystyk
            EnemyStats childStats = childObj.GetComponent<EnemyStats>();

            if (childStats != null)
            {
                float newMaxHp = stats.GetMaxHealth() * childHpPercent;
                childStats.SetHealthManually(newMaxHp);
            }

            // Pomniejszamy
            childObj.transform.localScale *= scaleReduction;

            // Przesuwamy lekko
            childObj.transform.position += new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));

            // Zmniejszamy ³adunek podzia³u u dziecka
            // Szukamy komponentu na dziecku (bo skill jest dzieckiem wroga)
            BlobSplitSkill childSkill = childObj.GetComponentInChildren<BlobSplitSkill>();
            if (childSkill != null)
            {
                childSkill.splitCharges = this.splitCharges - 1;
                childSkill.hasSplit = false;

                // WA¯NE: Musimy te¿ zaktualizowaæ referencje w nowym skillu, 
                // bo Instantiate skopiowa³ stare referencje (wskazuj¹ce na martwego rodzica)
                childSkill.Initialize(childStats);
            }
        }

        // Niszczymy rodzica (ca³ego wroga)
        Destroy(stats.gameObject);
    }
}