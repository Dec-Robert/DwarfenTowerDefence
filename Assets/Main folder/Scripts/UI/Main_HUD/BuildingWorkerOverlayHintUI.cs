using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Kontroler małego komunikatu pomocniczego w lewym dolnym rogu ekranu (HUD):
/// "X - aby włączyć/wyłączyć nakładkę produkcji"
/// 
/// Zgodny z GEMINI.md:
/// - Rozmiar fontu 12 pt (pomocniczy tekst informacyjny)
/// - Ciemne, półprzezroczyste tło
/// - Dynamiczna zmiana stanu (WŁĄCZONA / WYŁĄCZONA)
/// - Obsługuje również kliknięcie myszką jako alternatywę dla klawisza X
/// </summary>
public class BuildingWorkerOverlayHintUI : MonoBehaviour
{
    public static BuildingWorkerOverlayHintUI Instance { get; private set; }

    [Header("Komponenty UI")]
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button clickButton;

    private const string DefaultOffText = "<b>[X]</b> - aby włączyć/wyłączyć nakładkę produkcji";
    private const string DefaultOnText = "<b><color=#60A5FA>[X]</color></b> Nakładka produkcji: <color=#4ADE80><b>WŁĄCZONA</b></color>";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Application.isPlaying)
        {
            TryCreateOrAttach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateOrAttach();
    }

    public static void TryCreateOrAttach()
    {
        if (Instance != null)
        {
            Instance.RefreshState(BuildingWorkerOverlayManager.Instance != null && BuildingWorkerOverlayManager.Instance.IsVisible);
            return;
        }

        // 1. Szukamy Canvas_HUD
        var canvasHud = GameObject.Find("Canvas_HUD");
        Transform parentTransform = null;

        if (canvasHud != null)
        {
            parentTransform = canvasHud.transform;
        }
        else
        {
            var anyCanvas = FindFirstObjectByType<Canvas>();
            if (anyCanvas != null) parentTransform = anyCanvas.transform;
        }

        if (parentTransform == null) return;

        // Sprawdzamy czy już istnieje
        var existing = parentTransform.Find("Pnl_WorkerOverlayHint");
        if (existing != null)
        {
            var comp = existing.GetComponent<BuildingWorkerOverlayHintUI>();
            if (comp == null) comp = existing.gameObject.AddComponent<BuildingWorkerOverlayHintUI>();
            Instance = comp;
            return;
        }

        // Tworzymy element w lewym dolnym rogu
        CreateHintObject(parentTransform);
    }

    private static void CreateHintObject(Transform parent)
    {
        var hintObj = new GameObject("Pnl_WorkerOverlayHint", typeof(RectTransform));
        hintObj.transform.SetParent(parent, false);

        var rect = hintObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(24f, 20f);

        // Tło
        var bgImage = hintObj.AddComponent<Image>();
        bgImage.sprite = BuildingWorkerOverlayManager.GetCapsuleBackgroundSprite();
        bgImage.type = Image.Type.Sliced;
        bgImage.color = new Color32(20, 20, 24, 215);

        // Layout
        var hlg = hintObj.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 6, 6);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var csf = hintObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Przycisk klikalny myszą
        var btn = hintObj.AddComponent<Button>();
        btn.targetGraphic = bgImage;

        // Tekst
        var textObj = new GameObject("Txt_Hint", typeof(RectTransform));
        textObj.transform.SetParent(hintObj.transform, false);

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = DefaultOffText;
        tmp.fontSize = 12f; // Zgodnie z GEMINI.md (tekst pomocniczy)
        tmp.color = new Color32(220, 225, 235, 255);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;

        var comp = hintObj.AddComponent<BuildingWorkerOverlayHintUI>();
        comp.hintText = tmp;
        comp.backgroundImage = bgImage;
        comp.clickButton = btn;

        btn.onClick.AddListener(() =>
        {
            if (BuildingWorkerOverlayManager.Instance != null)
            {
                BuildingWorkerOverlayManager.Instance.ToggleVisibility();
            }
        });

        Instance = comp;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (hintText == null) hintText = GetComponentInChildren<TextMeshProUGUI>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (clickButton == null) clickButton = GetComponent<Button>();

        if (clickButton != null)
        {
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(() =>
            {
                if (BuildingWorkerOverlayManager.Instance != null)
                {
                    BuildingWorkerOverlayManager.Instance.ToggleVisibility();
                }
            });
        }
    }

    private void Start()
    {
        BuildingWorkerOverlayManager.OnVisibilityChanged -= RefreshState;
        BuildingWorkerOverlayManager.OnVisibilityChanged += RefreshState;

        RefreshState(BuildingWorkerOverlayManager.Instance != null && BuildingWorkerOverlayManager.Instance.IsVisible);
    }

    private void OnDestroy()
    {
        BuildingWorkerOverlayManager.OnVisibilityChanged -= RefreshState;
        if (Instance == this) Instance = null;
    }

    public void RefreshState(bool isOverlayVisible)
    {
        if (hintText != null)
        {
            hintText.text = isOverlayVisible ? DefaultOnText : DefaultOffText;
        }

        if (backgroundImage != null)
        {
            // Subtelna zmiana tła przy stanie aktywnym
            backgroundImage.color = isOverlayVisible
                ? new Color32(24, 32, 48, 235)
                : new Color32(20, 20, 24, 215);
        }
    }
}
