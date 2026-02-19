using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HitscanProjectile : ProjectileBase
{
    [Header("Efekt Wizualny")]
    public float visualDuration = 0.2f; // Piorun jest szybki, 0.5s to mo�e by� za d�ugo
    public float textureScrollSpeed = 20f; // Szybko�� przesuwania tekstury pioruna

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;
    }

    public override void Launch(Transform _target, Vector3 _targetPos, Transform _firingPoint = null)
    {
        // Jeśli nie podano firingPoint, używamy własnej pozycji (instancja jest w lufie)
        Vector3 startPos = _firingPoint != null ? _firingPoint.position : transform.position;

        if (_target != null)
        {
            EnemyStats enemy = _target.GetComponent<EnemyStats>();
            
            // Przenosimy obiekt do wroga (logic damage point)
            transform.position = enemy.transform.position;

            // Rysujemy linię
            lineRenderer.enabled = true;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(1, enemy.transform.position);

            lineRenderer.material.mainTextureOffset = new Vector2(Random.Range(0f, 1f), 0f);

            ApplyDamageAndEffects(enemy);
            StartCoroutine(FadeAndDestroy());
        }
        else
        {
            // Hitscan musi mieć cel, żeby trafić. Jak nie ma, znika.
            Destroy(gameObject);
        }
    }

    private IEnumerator FadeAndDestroy()
    {
        float timer = 0f;
        float initialWidth = lineRenderer.widthMultiplier;

        while (timer < visualDuration)
        {
            timer += Time.deltaTime;
            float offset = Time.time * textureScrollSpeed;
            lineRenderer.material.SetTextureOffset("_MainTex", new Vector2(offset, 0));
            float progress = timer / visualDuration;
            lineRenderer.widthMultiplier = Mathf.Lerp(initialWidth, 0f, progress);
            yield return null;
        }
        Destroy(gameObject);
    }
}