using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Główny menedżer nakładki pracowników nad budynkami (QoL).
/// Odpowiada za:
/// - Obsługę skrótu klawiszowego (domyślnie klawisz X) jako przełącznik (Toggle)
/// - Rozgłaszanie zdarzeń OnVisibilityChanged
/// - Automatyczne tworzenie i rejestrowanie komponentów BuildingWorkerOverlay dla uprawnionych budynków
/// - Udostępnianie proceduralnych, 9-slice'owanych sprite'ów kapsułek oraz palety barw ras
/// </summary>
public class BuildingWorkerOverlayManager : MonoBehaviour
{
    private static BuildingWorkerOverlayManager _instance;
    public static BuildingWorkerOverlayManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<BuildingWorkerOverlayManager>();
                if (_instance == null && Application.isPlaying)
                {
                    var go = new GameObject("BuildingWorkerOverlayManager");
                    _instance = go.AddComponent<BuildingWorkerOverlayManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("Sterowanie")]
    [Tooltip("Klawisz przełączający widoczność nakładki")]
    public KeyCode toggleKey = KeyCode.X;

    [Header("Stan")]
    [SerializeField] private bool isVisible = false;

    public static event Action<bool> OnVisibilityChanged;

    // --- Paleta barw zgodna z ustaleniami z wywiadu architektonicznego ---
    public static readonly Color HumansColor = new Color(0.231f, 0.510f, 0.965f, 1f);   // Błękitny (#3B82F6)
    public static readonly Color DwarvesColor = new Color(0.706f, 0.380f, 0.157f, 1f);  // Ciepły brąz / brązowy (#B46128)
    public static readonly Color ElvesColor = new Color(0.659f, 0.333f, 0.969f, 1f);    // Fioletowy (#A855F7)
    public static readonly Color WarningColor = new Color(0.612f, 0.639f, 0.686f, 1f);  // Szary dla stanu ostrzegawczego [ ! ] (#9CA3AF)
    public static readonly Color CapsuleBgColor = new Color(0.094f, 0.094f, 0.110f, 0.88f); // Ciemne półprzezroczyste wypełnienie (#18181C)

    // Bufor procedularnych sprite'ów
    private static Sprite s_BorderSprite;
    private static Sprite s_BgSprite;

    private readonly List<BuildingWorkerOverlay> registeredOverlays = new();

    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible != value)
            {
                isVisible = value;
                OnVisibilityChanged?.Invoke(isVisible);
                if (isVisible)
                {
                    EnsureOverlaysForAllBuildings();
                }
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Application.isPlaying)
        {
            var mgr = Instance;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_instance != null)
        {
            _instance.EnsureOverlaysForAllBuildings();
        }
    }

    public static void ResetStatics()
    {
        _instance = null;
        s_BorderSprite = null;
        s_BgSprite = null;
        OnVisibilityChanged = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        SubscribeRegistry();
        EnsureOverlaysForAllBuildings();
    }

    private void OnDestroy()
    {
        UnsubscribeRegistry();
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        // Obsługa przełącznika X
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleVisibility();
        }
    }

    public void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
    }

    private void SubscribeRegistry()
    {
        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.OnBuildingRegistered -= HandleBuildingRegistered;
            BuildingRegistry.Instance.OnBuildingRegistered += HandleBuildingRegistered;
            BuildingRegistry.Instance.OnBuildingUnregistered -= HandleBuildingUnregistered;
            BuildingRegistry.Instance.OnBuildingUnregistered += HandleBuildingUnregistered;
        }
    }

    private void UnsubscribeRegistry()
    {
        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.OnBuildingRegistered -= HandleBuildingRegistered;
            BuildingRegistry.Instance.OnBuildingUnregistered -= HandleBuildingUnregistered;
        }
    }

    private void HandleBuildingRegistered(BuildingEntity building)
    {
        if (IsEligibleBuilding(building))
        {
            EnsureOverlayForBuilding(building);
        }
    }

    private void HandleBuildingUnregistered(BuildingEntity building)
    {
        registeredOverlays.RemoveAll(o => o == null || o.TargetBuilding == building);
    }

    /// <summary>
    /// Sprawdza, czy budynek kwalifikuje się do wyświetlania nakładki pracowników.
    /// Zgodnie z ustaleniami: tylko miejsca pracy (produkcja, wieże), z wyłączeniem domów (HousingEntity).
    /// </summary>
    public static bool IsEligibleBuilding(BuildingEntity building)
    {
        if (building == null) return false;
        if (building is HousingEntity) return false;
        if (building.data != null && building.data.type == BuildingType.Housing) return false;
        return building.Workers != null;
    }

    /// <summary>
    /// Gwarantuje, że wszystkie uprawnione budynki w rejestrze posiadają nakładkę.
    /// </summary>
    public void EnsureOverlaysForAllBuildings()
    {
        if (BuildingRegistry.Instance == null) return;

        SubscribeRegistry();

        var buildings = BuildingRegistry.Instance.AllBuildings;
        if (buildings == null) return;

        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (IsEligibleBuilding(b))
            {
                EnsureOverlayForBuilding(b);
            }
        }

        // Czyszczenie martwych referencji
        registeredOverlays.RemoveAll(o => o == null);
    }

    /// <summary>
    /// Tworzy lub zwraca istniejącą nakładkę dla wskazanego budynku.
    /// </summary>
    public BuildingWorkerOverlay EnsureOverlayForBuilding(BuildingEntity building)
    {
        if (!IsEligibleBuilding(building)) return null;

        var existing = building.GetComponentInChildren<BuildingWorkerOverlay>(true);
        if (existing == null)
        {
            var overlayObj = new GameObject("WorkerOverlay_Root");
            overlayObj.transform.SetParent(building.transform, false);
            existing = overlayObj.AddComponent<BuildingWorkerOverlay>();
            existing.Initialize(building);
        }

        if (!registeredOverlays.Contains(existing))
        {
            registeredOverlays.Add(existing);
        }

        existing.UpdateVisibility(IsVisible);
        existing.RefreshView();
        return existing;
    }

    // =========================================================================
    // PROCEDURALNA GENERACJA SPRITE'ÓW KAPSUŁKI (9-SLICE SDF)
    // =========================================================================

    public static Sprite GetCapsuleBorderSprite()
    {
        if (s_BorderSprite == null)
        {
            s_BorderSprite = GenerateRoundedRectSprite(borderOnly: true);
        }
        return s_BorderSprite;
    }

    public static Sprite GetCapsuleBackgroundSprite()
    {
        if (s_BgSprite == null)
        {
            s_BgSprite = GenerateRoundedRectSprite(borderOnly: false);
        }
        return s_BgSprite;
    }

    private static Sprite GenerateRoundedRectSprite(bool borderOnly)
    {
        const int size = 64;
        const float radius = 20f;
        const float borderThickness = 4.0f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = borderOnly ? "Capsule_Border_Procedural" : "Capsule_Bg_Procedural";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Vector2 halfExtents = new Vector2(size * 0.5f - radius, size * 0.5f - radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Obliczanie SDF dla zaokrąglonego prostokąta
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfExtents;
                float dist = Mathf.Max(d.x, d.y);
                if (d.x > 0f && d.y > 0f) dist = d.magnitude;

                float sdf = dist - radius;

                float alpha;
                if (borderOnly)
                {
                    // Wygładzony pierścień ramki o szerokości borderThickness
                    float borderDist = Mathf.Abs(sdf + borderThickness * 0.5f) - (borderThickness * 0.5f);
                    alpha = Mathf.Clamp01(0.5f - borderDist);
                }
                else
                {
                    // Wypełnienie wnętrza
                    alpha = Mathf.Clamp01(0.5f - sdf);
                }

                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        // 9-slice borders: Left = 24, Bottom = 24, Right = 24, Top = 24
        Vector4 border = new Vector4(24f, 24f, 24f, 24f);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}
