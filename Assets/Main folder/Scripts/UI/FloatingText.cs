using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    public TextMeshPro textMesh; // U�ywamy wersji 3D (World Space), nie UI!

    [Header("Animacja")]
    public float moveSpeed = 2f;
    public float lifeTime = 1.5f;
    public float fadeSpeed = 2f;

    private Color startColor;
    private float timer;

    public void Setup(string text, Color color)
    {
        if (textMesh == null) textMesh = GetComponent<TextMeshPro>();

        textMesh.text = text;
        textMesh.color = color;
        startColor = color;
        timer = lifeTime;

        // Opcjonalnie: Zawsze zwr�cony do kamery
        transform.rotation = Camera.main.transform.rotation; 
        // (Ale w rzucie izometrycznym wystarczy ustawi� rotacj� w prefabie raz)
    }
//
    void Update()
    {
        // 1. Ruch w g�r�
        transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);

        // 2. Odliczanie czasu
        timer -= Time.deltaTime;

        // 3. Zanikanie (Fade out) pod koniec �ycia
        if (timer < 0.5f) // Ostatnie 0.5 sekundy
        {
            float alpha = textMesh.color.a - (fadeSpeed * Time.deltaTime);
            textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
        }

        // 4. �mier�
        if (timer <= 0)
        {
            Destroy(gameObject);
        }
    }
}