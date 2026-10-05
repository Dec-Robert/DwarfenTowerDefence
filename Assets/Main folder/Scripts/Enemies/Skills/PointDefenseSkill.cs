using UnityEngine;

public class PointDefenseSkill : EnemySkill
{
    [Header("Konfiguracja")]
    [Tooltip("Zasięg wybuchu niszczącego pociski")]
    public float radius = 6f;
    [Tooltip("Co ile sekund odpala się impuls")]
    public float cooldown = 4f;
    public GameObject blastVFX;

    [SerializeField] private LayerMask layerMask = ~0;

    private float timer = 0f;
    private static readonly Collider[] hitBuffer = new Collider[64];

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
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, hitBuffer, layerMask);
        bool hitAnything = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null) continue;

            ProjectileBase bullet = hit.GetComponent<ProjectileBase>();
            if (bullet == null) bullet = hit.GetComponentInParent<ProjectileBase>();
            if (bullet == null) bullet = hit.GetComponentInChildren<ProjectileBase>();

            if (bullet != null)
            {
                // Hitscan ignorujemy, bo on zadaje DMG natychmiast, a Linear niszczymy przed kontaktem.
                Destroy(bullet.gameObject);
                hitAnything = true;
            }
        }
        System.Array.Clear(hitBuffer, 0, hitCount);

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