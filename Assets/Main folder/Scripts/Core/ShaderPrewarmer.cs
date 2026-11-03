using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Zarządza prekompilacją i rozgrzewaniem (warmup) shaderów w tle w Menu Głównym,
/// aby zapobiec zacięciom (hitching) podczas ładowania i pierwszej renderacji rozgrywki.
/// </summary>
public class ShaderPrewarmer : MonoBehaviour
{
    private static ShaderPrewarmer instance;
    public static ShaderPrewarmer Instance
    {
        get
        {
            if (instance == null)
            {
                EnsureInitialized();
            }
            return instance;
        }
    }

    [Header("Ustawienia")]
    [SerializeField] private bool autoWarmupOnStart = true;
    [SerializeField] private string shaderVariantCollectionResourcePath = "WarmupShaderVariants";

    [Header("Stan")]
    [SerializeField] private bool isWarmedUp = false;
    [Range(0f, 1f)]
    [SerializeField] private float progress = 0f;

    public bool IsWarmedUp => isWarmedUp;
    public float Progress => progress;

    public event Action OnWarmupComplete;

    private Coroutine warmupCoroutine;

    // Kluczowe shadery używane w projekcie Dwarfen Tower Defence
    private static readonly string[] ProjectShaderNames = new string[]
    {
        "Custom/CastleStone",
        "Custom/FertileSoil",
        "Custom/Hill",
        "Custom/Mountain",
        "Custom/Road",
        "Custom/Sinkhole",
        "Custom/EnemyHealthBar",
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Particles/Lit",
        "Universal Render Pipeline/Particles/Unlit",
        "Universal Render Pipeline/Terrain/Lit",
        "TextMeshPro/Distance Field",
        "TextMeshPro/Mobile/Distance Field"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void EnsureInitialized()
    {
        if (instance != null) return;

        GameObject go = new GameObject("[ShaderPrewarmer]");
        instance = go.AddComponent<ShaderPrewarmer>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (autoWarmupOnStart && !isWarmedUp && warmupCoroutine == null)
        {
            StartWarmup();
        }
    }

    /// <summary>
    /// Rozpoczyna proces prekompilacji shaderów w tle.
    /// </summary>
    public void StartWarmup()
    {
        if (isWarmedUp || warmupCoroutine != null) return;
        warmupCoroutine = StartCoroutine(WarmupRoutine());
    }

    private IEnumerator WarmupRoutine()
    {
        float startTime = Time.realtimeSinceStartup;
        progress = 0.05f;
        yield return null;

        // KROK 1: Podgrzanie ShaderVariantCollection z Resources jeśli istnieje (bezpieczne dla URP)
        if (!string.IsNullOrEmpty(shaderVariantCollectionResourcePath))
        {
            ShaderVariantCollection svc = Resources.Load<ShaderVariantCollection>(shaderVariantCollectionResourcePath);
            if (svc != null)
            {
                if (!svc.isWarmedUp)
                {
                    svc.WarmUp();
                }
            }
        }

        progress = 0.45f;
        yield return null;

        // KROK 2: Bezpośrednie wyszukanie i bezpieczne przygotowanie kluczowych shaderów projektu w ich własnej przestrzeni słów kluczowych
        int totalShaders = ProjectShaderNames.Length;
        for (int i = 0; i < totalShaders; i++)
        {
            string sName = ProjectShaderNames[i];
            Shader sh = Shader.Find(sName);
            if (sh != null)
            {
                // Tworzymy na chwilę tymczasowy materiał w pamięci, aby wymusić inicjalizację GPU
                try
                {
                    Material tempMat = new Material(sh);
                    Destroy(tempMat);
                }
                catch
                {
                    // Ignoruj błędy pojedynczych shaderów
                }
            }

            progress = Mathf.Lerp(0.45f, 0.95f, (float)(i + 1) / totalShaders);
            if (i % 3 == 0)
            {
                yield return null;
            }
        }

        progress = 1f;
        isWarmedUp = true;
        warmupCoroutine = null;

        float duration = Time.realtimeSinceStartup - startTime;
        Debug.Log($"[ShaderPrewarmer] Pomyślnie podgrzano shadery w {duration:F2}s.");

        OnWarmupComplete?.Invoke();
    }
}
