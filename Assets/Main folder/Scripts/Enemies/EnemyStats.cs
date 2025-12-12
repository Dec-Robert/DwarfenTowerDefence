using UnityEngine;

public class EnemyStats : MonoBehaviour
{
    [Header("Statystyki")]
    public float maxHealth = 10f;
    private float currentHealth;

    [Header("Wizualizacja")]
    public Renderer meshRenderer; // Przypisz tu MeshRenderer kulki
    private Color originalColor;

    void Start()
    {
        currentHealth = maxHealth;
        if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null) originalColor = meshRenderer.material.color;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        // Prosty efekt "flash" przy trafieniu
        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.red;
            Invoke("ResetColor", 0.1f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void ResetColor()
    {
        if (meshRenderer != null) meshRenderer.material.color = originalColor;
    }

    void Die()
    {
        Debug.Log("Przeciwnik zniszczony!");
        ResourceManager.Instance.AddResource(ResourceType.Gold, 2);
        Destroy(gameObject);
    }
}