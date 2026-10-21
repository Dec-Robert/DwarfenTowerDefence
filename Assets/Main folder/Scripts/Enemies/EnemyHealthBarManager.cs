using System;
using UnityEngine;

/// <summary>
/// Centralny orkiestrator i konfigurator pasków zdrowia wrogów.
/// Przechowuje flagę isVisible (pod przyszłe menu ustawień), konfigurację kolorów progowych
/// oraz stałe wymiary paska (96x24 w przestrzeni wirtualnej).
/// </summary>
public class EnemyHealthBarManager : MonoBehaviour
{
    private static EnemyHealthBarManager _instance;
    public static EnemyHealthBarManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<EnemyHealthBarManager>();
                if (_instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("EnemyHealthBarManager");
                    _instance = go.AddComponent<EnemyHealthBarManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("Widoczność (Flaga dla menu ustawień)")]
    [SerializeField] private bool isVisible = true;
    public static event Action<bool> OnVisibilityChanged;

    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible != value)
            {
                isVisible = value;
                OnVisibilityChanged?.Invoke(isVisible);
            }
        }
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
    }

    [Header("Wymiary paska w świecie")]
    [Tooltip("Wymiary paska w wirtualnych pikselach")]
    public Vector2 barPixelSize = new Vector2(96f, 24f);

    [Tooltip("Skala przelicznika pikseli na jednostki świata (np. 0.01 daje 0.96m x 0.24m)")]
    public float worldPixelScale = 0.01f;

    [Tooltip("Grubość obramowania w pikselach")]
    public float borderThickness = 2.0f;

    [Tooltip("Grubość linii podziału segmentu w pikselach")]
    public float dividerThickness = 1.5f;

    [Header("Kolory progów HP (wg Max HP)")]
    [Tooltip("Czerwony: Max HP <= 1 000 (1 segment = 100 HP, max 10 segmentów)")]
    public Color redColor = new Color(0.85f, 0.15f, 0.15f, 1f);

    [Tooltip("Niebieski: 1 000 < Max HP <= 10 000 (1 segment = 1 000 HP, max 10 segmentów)")]
    public Color blueColor = new Color(0.2f, 0.5f, 0.95f, 1f);

    [Tooltip("Zielony: 10 000 < Max HP <= 100 000 (1 segment = 10 000 HP, max 10 segmentów)")]
    public Color greenColor = new Color(0.15f, 0.8f, 0.25f, 1f);

    [Tooltip("Fioletowy: Max HP > 100 000 (1 segment = 100 000 HP)")]
    public Color purpleColor = new Color(0.7f, 0.2f, 0.85f, 1f);

    [Header("Kolory tła, ramki i podziałek")]
    public Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
    public Color borderColor = new Color(0.02f, 0.02f, 0.02f, 1f);
    public Color dividerColor = new Color(0.0f, 0.0f, 0.0f, 1f);

    [Header("Limity czytelności")]
    [Tooltip("Maksymalna liczba widocznych podziałek dla fioletowego paska (>100k), by linie nie zlały się w czarną plamę")]
    public float maxVisiblePurpleSegments = 20f;

    // Domyślne kolory fallback (jeśli manager nie istnieje w scenie/testach)
    private static readonly Color FallbackRed = new Color(0.85f, 0.15f, 0.15f, 1f);
    private static readonly Color FallbackBlue = new Color(0.2f, 0.5f, 0.95f, 1f);
    private static readonly Color FallbackGreen = new Color(0.15f, 0.8f, 0.25f, 1f);
    private static readonly Color FallbackPurple = new Color(0.7f, 0.2f, 0.85f, 1f);
    private static readonly Color FallbackBg = new Color(0.12f, 0.12f, 0.12f, 0.95f);
    private static readonly Color FallbackBorder = new Color(0.02f, 0.02f, 0.02f, 1f);
    private static readonly Color FallbackDivider = new Color(0.0f, 0.0f, 0.0f, 1f);

    private static Material s_SharedBarMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        OnVisibilityChanged = null;
        s_SharedBarMaterial = null;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static bool GetGlobalVisibility()
    {
        return Instance == null || Instance.isVisible;
    }

    public static Material GetSharedBarMaterial()
    {
        if (s_SharedBarMaterial == null)
        {
            Shader shader = Shader.Find("Custom/EnemyHealthBar")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");

            if (shader != null)
            {
                s_SharedBarMaterial = new Material(shader)
                {
                    name = "EnemyHealthBar_SharedMat"
                };
            }
        }
        return s_SharedBarMaterial;
    }

    public static void EvaluateTier(float maxHp, out Color barColor, out float segmentHpValue, out float visualSegmentCount)
    {
        float hp = Mathf.Max(1f, maxHp);

        if (hp <= 1000f)
        {
            barColor = Instance != null ? Instance.redColor : FallbackRed;
            segmentHpValue = 100f;
            visualSegmentCount = hp / segmentHpValue;
        }
        else if (hp <= 10000f)
        {
            barColor = Instance != null ? Instance.blueColor : FallbackBlue;
            segmentHpValue = 1000f;
            visualSegmentCount = hp / segmentHpValue;
        }
        else if (hp <= 100000f)
        {
            barColor = Instance != null ? Instance.greenColor : FallbackGreen;
            segmentHpValue = 10000f;
            visualSegmentCount = hp / segmentHpValue;
        }
        else
        {
            barColor = Instance != null ? Instance.purpleColor : FallbackPurple;
            segmentHpValue = 100000f;
            float rawSegments = hp / segmentHpValue;
            float cap = Instance != null ? Instance.maxVisiblePurpleSegments : 20f;
            visualSegmentCount = Mathf.Min(rawSegments, cap);
        }
    }

    public static void GetStylingColors(out Color bg, out Color border, out Color divider)
    {
        if (Instance != null)
        {
            bg = Instance.backgroundColor;
            border = Instance.borderColor;
            divider = Instance.dividerColor;
        }
        else
        {
            bg = FallbackBg;
            border = FallbackBorder;
            divider = FallbackDivider;
        }
    }

    public static Vector2 GetBarPixelSize()
    {
        return Instance != null ? Instance.barPixelSize : new Vector2(96f, 24f);
    }

    public static float GetWorldPixelScale()
    {
        return Instance != null ? Instance.worldPixelScale : 0.01f;
    }

    public static float GetBorderThickness()
    {
        return Instance != null ? Instance.borderThickness : 2.0f;
    }

    public static float GetDividerThickness()
    {
        return Instance != null ? Instance.dividerThickness : 1.5f;
    }
}
