using UnityEngine;

public class PointDefenseSkill : EnemySkill
{
    [Header("Konfiguracja")]
    [Tooltip("Zasięg wybuchu niszczącego pociski")]
    public float radius = 6f;
    [Tooltip("Co ile sekund odpala się impuls")]
    public float cooldown = 4f;
    public GameObject blastVFX;

    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= cooldown)
        {
            BlastProjectiles();
            timer = 0f;
        }
    }

    private void BlastProjectiles()
    {
        // Uwaga: Wszystkie pociski MUSZĄ mieć przypisany jakiś Tag lub Layer, by Overlap działał wydajnie.
        // Najlepiej stwórz tag "Projectile" i przypisz go do bazowych prefabów pocisków (Simple, Linear, Ground).
        
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        bool hitAnything = false;

        foreach (var hit in hits)
        {
            ProjectileBase bullet = hit.GetComponent<ProjectileBase>();
            if (bullet != null)
            {
                // Hitscan ignorujemy, bo on zadaje DMG natychmiast, a Linear niszczymy przed kontaktem.
                Destroy(bullet.gameObject);
                hitAnything = true;
            }
        }

        if (hitAnything)
        {
            Debug.Log($"<color=cyan>{name} (Area Defence) zniszczył nadlatujące pociski!</color>");
            if (blastVFX != null) Instantiate(blastVFX, transform.position, Quaternion.identity);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}