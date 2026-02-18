using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HitscanProjectile : ProjectileBase
{
    [Header("Efekt Wizualny")]
    public float visualDuration = 0.2f; // Piorun jest szybki, 0.5s to mo¿e byæ za d³ugo
    public float textureScrollSpeed = 20f; // Szybkoœæ przesuwania tekstury pioruna

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        // Upewniamy siê, ¿e linia korzysta z wspó³rzêdnych œwiata, ¿eby ³atwo ³¹czyæ dwa punkty
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false; // Domyœlnie wy³¹czony, w³¹czymy przy strzale
    }

    /// <summary>
    /// Uruchamia piorun z punktu startu do celu.
    /// </summary>
    /// <param name="startPoint">Pozycja lufy wie¿y</param>
    /// <param name="_target">Cel</param>
    public void LaunchAt(Vector3 startPoint, Transform _target)
    {
        if (_target != null)
        {
            EnemyStats enemy = _target.GetComponent<EnemyStats>();

            // 1. Przenosimy sam obiekt pocisku do wroga (dla porz¹dku logicznego i np. efektu uderzenia w miejscu wroga)
            transform.position = enemy.transform.position;

            // 2. Ustawiamy Line Renderera (Piorun)
            lineRenderer.enabled = true;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startPoint);       // Pocz¹tek (wie¿a)
            lineRenderer.SetPosition(1, enemy.transform.position); // Koniec (wróg)

            // Opcjonalnie: Losowanie offsetu tekstury, ¿eby ka¿dy strza³ wygl¹da³ inaczej
            lineRenderer.material.mainTextureOffset = new Vector2(Random.Range(0f, 1f), 0f);

            // 3. Zadaj obra¿enia
            ApplyDamageAndEffects(enemy);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 4. Uruchamiamy korutynê zanikania i niszczenia
        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        float timer = 0f;
        float initialWidth = lineRenderer.widthMultiplier;

        while (timer < visualDuration)
        {
            timer += Time.deltaTime;

            // Opcjonalnie: Przesuwanie tekstury w czasie (animacja p³yniêcia pr¹du)
            float offset = Time.time * textureScrollSpeed;
            lineRenderer.material.SetTextureOffset("_MainTex", new Vector2(offset, 0));

            // Opcjonalnie: Zwê¿anie pioruna pod koniec ¿ycia
            float progress = timer / visualDuration;
            lineRenderer.widthMultiplier = Mathf.Lerp(initialWidth, 0f, progress);

            yield return null;
        }

        Destroy(gameObject);
    }
}