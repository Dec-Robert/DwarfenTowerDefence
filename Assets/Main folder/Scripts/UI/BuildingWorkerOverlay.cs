using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Trójwymiarowa wisząca nakładka (World-Space Billboard) prezentująca liczbę pracowników
/// poszczególnych ras nad budynkiem produkcyjnym / wieżą.
/// Aktywowana przełącznikiem (klawisz X).
/// 
/// Zgodna z wytycznymi UI z GEMINI.md:
/// - Kapsułki o proporcjach pionowej pigułki
/// - Ramka w kolorze rasy (Ludzie: błękitny, Krasnoludy: brązowy, Elfy: fioletowy)
/// - Szara kapsułka ostrzegawcza [ ! ] dla budynków wstrzymanych lub bez załogi (0 pracowników)
/// - Ukrywanie ras z 0 pracownikami
/// - Dynamiczny billboard skierowany zawsze przodem do kamery
/// </summary>
public class BuildingWorkerOverlay : MonoBehaviour
{
    public class CapsuleView
    {
        public GameObject root;
        public Image bgImage;
        public Image borderImage;
        public TextMeshProUGUI countText;
    }

    [Header("Referencje")]
    [SerializeField] private BuildingEntity targetBuilding;
    [SerializeField] private RectTransform containerRect;
    [SerializeField] private Canvas worldCanvas;

    [Header("Pozycjonowanie")]
    [Tooltip("Dodatkowy offset w pionie ponad najwyższy punkt dachu budynku")]
    [SerializeField] private float verticalMargin = 0.6f;
    [SerializeField] private float minHeightOffset = 2.2f;
    [SerializeField] private float worldScale = 0.015f;

    // Kapsułki dla ras oraz ostrzegawcza
    private CapsuleView humansCapsule;
    private CapsuleView dwarvesCapsule;
    private CapsuleView elvesCapsule;
    private CapsuleView warningCapsule;

    private static Camera s_MainCamera;
    private float cachedHeightOffset = 2.2f;
    private bool isInitialized;
    private float pollTimer = 0f;
    private const float PollInterval = 0.35f;

    public BuildingEntity TargetBuilding => targetBuilding;

    public void Initialize(BuildingEntity building)
    {
        targetBuilding = building;
        CalculateHeightOffset();
        SetupWorldCanvas();
        CreateCapsules();
        isInitialized = true;
        RefreshView();
        UpdateVisibility(BuildingWorkerOverlayManager.Instance != null && BuildingWorkerOverlayManager.Instance.IsVisible);
    }

    private void Awake()
    {
        if (targetBuilding == null)
        {
            targetBuilding = GetComponentInParent<BuildingEntity>();
        }
    }

    private void Start()
    {
        if (!isInitialized && targetBuilding != null)
        {
            Initialize(targetBuilding);
        }
    }

    private void OnEnable()
    {
        BuildingWorkerOverlayManager.OnVisibilityChanged -= HandleVisibilityChanged;
        BuildingWorkerOverlayManager.OnVisibilityChanged += HandleVisibilityChanged;

        BuildingEntity.OnAnyBuildingEconomyChanged -= HandleEconomyChanged;
        BuildingEntity.OnAnyBuildingEconomyChanged += HandleEconomyChanged;

        if (BuildingWorkerOverlayManager.Instance != null)
        {
            UpdateVisibility(BuildingWorkerOverlayManager.Instance.IsVisible);
        }
        else
        {
            UpdateVisibility(false);
        }

        RefreshView();
    }

    private void OnDisable()
    {
        BuildingWorkerOverlayManager.OnVisibilityChanged -= HandleVisibilityChanged;
        BuildingEntity.OnAnyBuildingEconomyChanged -= HandleEconomyChanged;
    }

    private void HandleVisibilityChanged(bool visible)
    {
        UpdateVisibility(visible);
        if (visible)
        {
            RefreshView();
        }
    }

    private void HandleEconomyChanged()
    {
        if (gameObject.activeInHierarchy && (worldCanvas == null || worldCanvas.enabled))
        {
            RefreshView();
        }
    }

    private void LateUpdate()
    {
        if (worldCanvas == null || !worldCanvas.enabled) return;

        UpdateBillboard();

        // Lekki polling na wypadek bezpośrednich zmian stanu bez powiadomień
        pollTimer += Time.unscaledDeltaTime;
        if (pollTimer >= PollInterval)
        {
            pollTimer = 0f;
            RefreshView();
        }
    }

    private void UpdateBillboard()
    {
        if (s_MainCamera == null || !s_MainCamera.isActiveAndEnabled)
        {
            s_MainCamera = Camera.main;
        }

        if (s_MainCamera != null)
        {
            transform.rotation = s_MainCamera.transform.rotation;
        }

        // Kompensacja skali rodzica
        Vector3 parentLossy = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float localX = worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentLossy.x));
        float localY = worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentLossy.y));
        float localZ = worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentLossy.z));
        transform.localScale = new Vector3(localX, localY, localZ);

        // Pozycja lokalna z zachowaniem obliczonej wysokości
        transform.localPosition = new Vector3(0f, cachedHeightOffset, 0f);
    }

    public void UpdateVisibility(bool visible)
    {
        if (worldCanvas != null)
        {
            worldCanvas.enabled = visible;
        }
        if (containerRect != null)
        {
            containerRect.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Odświeża zawartość kapsułek na podstawie aktualnego stanu budynku i przypisanych pracowników.
    /// </summary>
    public void RefreshView()
    {
        if (targetBuilding == null || targetBuilding.Workers == null)
        {
            HideAllCapsules();
            return;
        }

        int humans = targetBuilding.Workers.GetWorkerCount(Race.Humans);
        int dwarves = targetBuilding.Workers.GetWorkerCount(Race.Dwarves);
        int elves = targetBuilding.Workers.GetWorkerCount(Race.Elves);
        int totalWorkers = humans + dwarves + elves;
        bool isActive = targetBuilding.isBuildingActive;

        // Stan ostrzegawczy [ ! ]:
        // Budynek wstrzymany LUB budynek aktywny, ale z 0 pracownikami
        if (!isActive || totalWorkers == 0)
        {
            SetCapsuleState(warningCapsule, true, "!", BuildingWorkerOverlayManager.WarningColor);

            // Jeśli budynek jest wstrzymany, ale posiada pracowników, pokazujemy ich w przyciemnionej formie obok '!'
            if (!isActive && totalWorkers > 0)
            {
                Color dimHuman = BuildingWorkerOverlayManager.HumansColor * 0.7f;
                dimHuman.a = 1f;
                Color dimDwarf = BuildingWorkerOverlayManager.DwarvesColor * 0.7f;
                dimDwarf.a = 1f;
                Color dimElf = BuildingWorkerOverlayManager.ElvesColor * 0.7f;
                dimElf.a = 1f;

                SetCapsuleState(humansCapsule, humans > 0, humans.ToString(), dimHuman);
                SetCapsuleState(dwarvesCapsule, dwarves > 0, dwarves.ToString(), dimDwarf);
                SetCapsuleState(elvesCapsule, elves > 0, elves.ToString(), dimElf);
            }
            else
            {
                SetCapsuleState(humansCapsule, false, "", Color.white);
                SetCapsuleState(dwarvesCapsule, false, "", Color.white);
                SetCapsuleState(elvesCapsule, false, "", Color.white);
            }
            return;
        }

        // Budynek aktywny z pracownikami: ukrywamy wykrzyknik, pokazujemy kapsułki obsadzonych ras
        SetCapsuleState(warningCapsule, false, "", Color.white);
        SetCapsuleState(humansCapsule, humans > 0, humans.ToString(), BuildingWorkerOverlayManager.HumansColor);
        SetCapsuleState(dwarvesCapsule, dwarves > 0, dwarves.ToString(), BuildingWorkerOverlayManager.DwarvesColor);
        SetCapsuleState(elvesCapsule, elves > 0, elves.ToString(), BuildingWorkerOverlayManager.ElvesColor);
    }

    private void SetCapsuleState(CapsuleView capsule, bool visible, string text, Color borderColor)
    {
        if (capsule == null || capsule.root == null) return;

        capsule.root.SetActive(visible);
        if (visible)
        {
            if (capsule.countText != null) capsule.countText.text = text;
            if (capsule.borderImage != null) capsule.borderImage.color = borderColor;
        }
    }

    private void HideAllCapsules()
    {
        if (humansCapsule?.root != null) humansCapsule.root.SetActive(false);
        if (dwarvesCapsule?.root != null) dwarvesCapsule.root.SetActive(false);
        if (elvesCapsule?.root != null) elvesCapsule.root.SetActive(false);
        if (warningCapsule?.root != null) warningCapsule.root.SetActive(false);
    }

    private void CalculateHeightOffset()
    {
        if (targetBuilding == null)
        {
            cachedHeightOffset = minHeightOffset;
            return;
        }

        float maxY = float.MinValue;
        var renderers = targetBuilding.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (r.bounds.max.y > maxY) maxY = r.bounds.max.y;
            }
        }

        if (maxY > float.MinValue)
        {
            float diff = maxY - targetBuilding.transform.position.y;
            cachedHeightOffset = Mathf.Max(minHeightOffset, diff + verticalMargin);
        }
        else
        {
            var col = targetBuilding.GetComponentInChildren<Collider>();
            if (col != null)
            {
                float diff = col.bounds.max.y - targetBuilding.transform.position.y;
                cachedHeightOffset = Mathf.Max(minHeightOffset, diff + verticalMargin);
            }
            else
            {
                cachedHeightOffset = minHeightOffset;
            }
        }
    }

    private void SetupWorldCanvas()
    {
        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null) worldCanvas = gameObject.AddComponent<Canvas>();

        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = Camera.main;
        worldCanvas.sortingOrder = 550;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster != null) Destroy(raycaster);

        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var rootRect = GetComponent<RectTransform>();
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(160f, 60f);

        // Kontener z układem poziomym
        var containerObj = new GameObject("Container_Capsules", typeof(RectTransform));
        containerObj.transform.SetParent(transform, false);
        containerRect = containerObj.GetComponent<RectTransform>();

        var hlg = containerObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var csf = containerObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void CreateCapsules()
    {
        Sprite bgSprite = BuildingWorkerOverlayManager.GetCapsuleBackgroundSprite();
        Sprite borderSprite = BuildingWorkerOverlayManager.GetCapsuleBorderSprite();

        // 1. Kapsułka ostrzegawcza [ ! ] (pojawia się z lewej strony przy problemie)
        warningCapsule = CreateSingleCapsule("Capsule_Warning", borderSprite, bgSprite);

        // 2. Kapsułka Ludzi (Niebieska)
        humansCapsule = CreateSingleCapsule("Capsule_Humans", borderSprite, bgSprite);

        // 3. Kapsułka Krasnoludów (Brązowa)
        dwarvesCapsule = CreateSingleCapsule("Capsule_Dwarves", borderSprite, bgSprite);

        // 4. Kapsułka Elfów (Fioletowa)
        elvesCapsule = CreateSingleCapsule("Capsule_Elves", borderSprite, bgSprite);
    }

    private CapsuleView CreateSingleCapsule(string name, Sprite borderSprite, Sprite bgSprite)
    {
        const float capsuleWidth = 28f;
        const float capsuleHeight = 44f;

        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(containerRect, false);

        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(capsuleWidth, capsuleHeight);

        var layout = root.AddComponent<LayoutElement>();
        layout.preferredWidth = capsuleWidth;
        layout.preferredHeight = capsuleHeight;
        layout.minWidth = capsuleWidth;
        layout.minHeight = capsuleHeight;

        // Tło pigułki
        var bgObj = new GameObject("Bg", typeof(RectTransform));
        bgObj.transform.SetParent(root.transform, false);
        var bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        var bgImage = bgObj.AddComponent<Image>();
        bgImage.sprite = bgSprite;
        bgImage.type = Image.Type.Sliced;
        bgImage.color = BuildingWorkerOverlayManager.CapsuleBgColor;
        bgImage.raycastTarget = false;

        // Kolorowa ramka pigułki
        var borderObj = new GameObject("Border", typeof(RectTransform));
        borderObj.transform.SetParent(root.transform, false);
        var borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;

        var borderImage = borderObj.AddComponent<Image>();
        borderImage.sprite = borderSprite;
        borderImage.type = Image.Type.Sliced;
        borderImage.color = Color.white;
        borderImage.raycastTarget = false;

        // Liczba TextMeshPro
        var textObj = new GameObject("Txt_Count", typeof(RectTransform));
        textObj.transform.SetParent(root.transform, false);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "0";
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        root.SetActive(false);

        return new CapsuleView
        {
            root = root,
            bgImage = bgImage,
            borderImage = borderImage,
            countText = tmp
        };
    }

    private void OnDestroy()
    {
        BuildingWorkerOverlayManager.OnVisibilityChanged -= HandleVisibilityChanged;
        BuildingEntity.OnAnyBuildingEconomyChanged -= HandleEconomyChanged;
    }
}
