using UnityEngine;

public class PointDefenseSkill : EnemySkill
{
    [Header("Konfiguracja")]
    public float range = 3f;
    public float cooldown = 2f;
    public GameObject zapEffect;

    private float timer = 0f;

    void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
            return;
        }

        // Szukamy pocisków w pobli¿u
        // Zak³adamy, ¿e pociski s¹ na warstwie "Projectile" lub maj¹ tag "Bullet"
        // U¿yjemy OverlapSphere
        Collider[] hits = Physics.OverlapSphere(transform.position, range);

        foreach (var hit in hits)
        {
            SimpleBullet bullet = hit.GetComponent<SimpleBullet>();
            if (bullet != null)
            {
                // Zestrzelenie!
                Destroy(bullet.gameObject);
                timer = cooldown;

                if (zapEffect) Instantiate(zapEffect, hit.transform.position, Quaternion.identity);
                Debug.Log($"{name} (Arcanist) zniszczy³ pocisk!");

                return; // Zestrzelamy tylko jeden na raz
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}