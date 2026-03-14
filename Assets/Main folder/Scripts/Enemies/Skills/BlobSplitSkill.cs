using UnityEngine;

public class BlobSplitSkill : EnemySkill
{
    [Header("Konfiguracja Splitu")]
    public int amountOfChildren = 2;
    [Tooltip("Procent max HP oryginalnego wroga, jaki otrzymają małe bloby (np. 0.3 = 30%)")]
    public float childHpPercent = 0.3f; 
    public float scaleReduction = 0.6f;

    // Kiedy zadziała? np. po śmierci, ale możemy odpalić to przed nią
    private bool hasSplit = false;

    public override void OnDeath()
    {
        if (hasSplit) return; // Zabezpieczenie przed pętlą

        hasSplit = true;
        Debug.Log($"<color=orange>{name} dzieli się na {amountOfChildren} małych wrogów!</color>");

        for (int i = 0; i < amountOfChildren; i++)
        {
            GameObject childObj = Instantiate(stats.gameObject, transform.position, transform.rotation);

            // Kopiujemy postęp na ścieżce, by dzieci nie szły od początku mapy
            EnemyWalker childWalker = childObj.GetComponent<EnemyWalker>();
            if (walker != null && childWalker != null)
            {
                childWalker.CopyProgressFrom(walker);
            }

            // Ustawiamy nowe, małe HP
            EnemyStats childStats = childObj.GetComponent<EnemyStats>();
            if (childStats != null)
            {
                float newMaxHp = stats.GetMaxHealth() * childHpPercent;
                childStats.SetHealthManually(newMaxHp);
            }

            childObj.transform.localScale *= scaleReduction;

            // Przesuwamy lekko w bok, by się nie zablokowały w sobie
            Vector2 randCircle = Random.insideUnitCircle * 2f;
            childObj.transform.position += new Vector3(randCircle.x, 0, randCircle.y);

            // Odbieramy dziecku ten skill, żeby nie dzielił się w nieskończoność (opcjonalnie)
            BlobSplitSkill childSkill = childObj.GetComponentInChildren<BlobSplitSkill>();
            if (childSkill != null)
            {
                Destroy(childSkill); // Dzieci nie będą się już dzielić!
            }
        }
    }
}