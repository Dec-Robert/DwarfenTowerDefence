using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Kontroler płynnego przesuwania (Pan) i skalowania (Zoom) grafu 2D meta-progresji.
/// Funkcjonalności:
/// - Płynne przesuwanie przeciąganiem myszy (LPM na tle lub ŚPM w dowolnym miejscu)
/// - Płynne skalowanie kółkiem myszy ku pozycji kursora (zakres zoomu: 0.25f - 2.5f)
/// - Klawisz 'F' centrujący widok na węźle Startowym (lub wybranym)
/// - Ograniczenie granic (Bounds Clamping) zabezpieczające przed odpłynięciem w próżnię
/// </summary>
public class MetaGraphPanZoomUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
{
    [Header("Referencje Transformów")]
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Vector2 fallbackViewportSize = new Vector2(1920f, 1080f);

    [Header("Parametry Skalowania (Zoom)")]
    [SerializeField] private float minZoom = 0.25f;
    [SerializeField] private float maxZoom = 2.5f;
    [SerializeField] private float zoomSensitivity = 0.15f;
    [SerializeField] private float zoomSmoothness = 14f;

    [Header("Parametry Przesuwania (Pan)")]
    [SerializeField] private float panSmoothness = 14f;
    [SerializeField] private Rect contentBounds = new Rect(400f, 200f, 5600f, 4800f);

    [Header("Pozycja Startowa / Domyślna")]
    [SerializeField] private Vector2 defaultFocusPosition = new Vector2(1460f, 2600f);

    private Vector2 targetPosition;
    private Vector2 currentPosition;
    private float targetScale = 1.0f;
    private float currentScale = 1.0f;

    private bool isDragging = false;
    private Vector2 lastMousePosition;
    private Camera eventCamera;

    public RectTransform ContentRect { get => contentRect; set => contentRect = value; }
    public RectTransform ViewportRect { get => viewportRect; set => viewportRect = value; }
    public Canvas Canvas { get => canvas; set => canvas = value; }
    public Vector2 FallbackViewportSize { get => fallbackViewportSize; set => fallbackViewportSize = value; }
    public float CurrentZoom => currentScale;
    public float TargetZoom => targetScale;
    public Vector2 CurrentPosition => currentPosition;
    public Vector2 TargetPosition => targetPosition;
    public Rect ContentBounds { get => contentBounds; set => contentBounds = value; }
    public Vector2 DefaultFocusPosition { get => defaultFocusPosition; set => defaultFocusPosition = value; }
    public float MinZoom => minZoom;
    public float MaxZoom => maxZoom;
    public float ZoomSensitivity => zoomSensitivity;

    /// <summary>
    /// Jawna inicjalizacja referencji do Contentu i Viewportu (Target Decoupling).
    /// </summary>
    public void Initialize(RectTransform content, RectTransform viewport)
    {
        contentRect = content;
        viewportRect = viewport;
        if (canvas == null)
        {
            try { canvas = GetComponentInParent<Canvas>(); } catch { }
        }
        if (canvas != null)
        {
            try
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    eventCamera = canvas.worldCamera;
                }
            }
            catch { }
        }
        ResetViewInstant();
    }

    private void Awake()
    {
        if (contentRect == null)
        {
            try { contentRect = GetComponent<RectTransform>(); } catch { }
        }
        if (viewportRect == null && transform.parent != null)
        {
            try { viewportRect = transform.parent.GetComponent<RectTransform>(); } catch { }
        }
        if (canvas == null)
        {
            try { canvas = GetComponentInParent<Canvas>(); } catch { }
        }

        currentPosition = contentRect != null ? contentRect.anchoredPosition : Vector2.zero;
        targetPosition = currentPosition;
        currentScale = contentRect != null ? contentRect.localScale.x : 1f;
        targetScale = currentScale;
    }

    private void Start()
    {
        if (canvas == null)
        {
            try { canvas = GetComponentInParent<Canvas>(); } catch { }
        }
        if (canvas != null)
        {
            try
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    eventCamera = canvas.worldCamera;
                }
            }
            catch { }
        }

        // Domyślne wycentrowanie na węźle Start
        ResetViewInstant();
    }

    private void Update()
    {
        HandleKeyboardInput();
        HandleMiddleMousePan();
        SmoothUpdate();
    }

    private void HandleKeyboardInput()
    {
        // Klawisz 'F' centruje widok na węźle startowym
        if (Input.GetKeyDown(KeyCode.F))
        {
            FocusOnPosition(defaultFocusPosition);
        }
    }

    private void HandleMiddleMousePan()
    {
        // Środkowy przycisk myszy pozwala na przesuwanie w dowolnym momencie
        if (Input.GetMouseButtonDown(2))
        {
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(2))
        {
            Vector2 currentMouse = Input.mousePosition;
            Vector2 delta = currentMouse - lastMousePosition;
            lastMousePosition = currentMouse;
            Pan(delta);
        }
    }

    private void SmoothUpdate()
    {
        if (contentRect == null) return;

        float dt = Time.unscaledDeltaTime;
        currentPosition = Vector2.Lerp(currentPosition, targetPosition, dt * panSmoothness);
        currentScale = Mathf.Lerp(currentScale, targetScale, dt * zoomSmoothness);

        contentRect.anchoredPosition = currentPosition;
        contentRect.localScale = new Vector3(currentScale, currentScale, 1f);
    }

    private Vector2 GetViewportSize()
    {
        if (viewportRect != null)
        {
            try
            {
                Vector2 size = viewportRect.rect.size;
                if (size.x > 0f && size.y > 0f) return size;
            }
            catch { }
        }
        return fallbackViewportSize;
    }

    /// <summary>
    /// Przesuwa widok o zadany wektor w przestrzeni ekranowej z uwzględnieniem Canvas Scalera.
    /// </summary>
    public void Pan(Vector2 screenDelta)
    {
        float scaleFactor = 1.0f;
        try
        {
            if (canvas != null && canvas.scaleFactor > 0.001f)
            {
                scaleFactor = canvas.scaleFactor;
            }
        }
        catch { }

        targetPosition += screenDelta / scaleFactor;
        ClampPositionToBounds();
    }

    private Rect GetContentBounds()
    {
        if (contentBounds.width > 0f && contentBounds.height > 0f) return contentBounds;
        return new Rect(400f, 200f, 5600f, 4800f);
    }

    /// <summary>
    /// Skaluje widok ze środkiem w punkcie wskazywanym przez kursor myszy (przestrzeń zakotwiczenia dolno-lewa).
    /// </summary>
    public void Zoom(float scrollDelta, Vector2 screenCursorPos)
    {
        if (targetScale <= 0f) targetScale = 1.0f;
        Vector2 viewportSize = GetViewportSize();
        Vector2 viewportLocalPoint = Vector2.zero;

        if (viewportRect != null)
        {
            try
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewportRect, screenCursorPos, eventCamera, out viewportLocalPoint);
            }
            catch
            {
                viewportLocalPoint = screenCursorPos - viewportSize * 0.5f;
            }
        }
        else
        {
            viewportLocalPoint = screenCursorPos - viewportSize * 0.5f;
        }

        // Przekształć punkt lokalny (wycentrowany w pivot) na przestrzeń zakotwiczenia dolno-lewej
        Vector2 cursorInAnchorSpace = viewportLocalPoint + viewportSize * 0.5f;
        Vector2 contentPoint = (cursorInAnchorSpace - targetPosition) / targetScale;

        // Oblicz nową skalę
        float effMin = minZoom > 0f ? minZoom : 0.25f;
        float effMax = maxZoom > 0f ? maxZoom : 2.5f;
        float effSens = zoomSensitivity > 0f ? zoomSensitivity : 0.15f;
        float newScale = Mathf.Clamp(targetScale + scrollDelta * (targetScale * effSens), effMin, effMax);

        // Dopasuj pozycję docelową, aby punkt pod kursorem pozostał w tym samym miejscu na ekranie
        targetPosition = cursorInAnchorSpace - contentPoint * newScale;
        targetScale = newScale;

        ClampPositionToBounds();
    }

    /// <summary>
    /// Wycentrowuje widok na zadaną pozycję w przestrzeni grafu.
    /// </summary>
    public void FocusOnPosition(Vector2 graphPosition)
    {
        if (targetScale <= 0f) targetScale = 1.0f;
        Vector2 viewportSize = GetViewportSize();
        Vector2 viewportCenter = viewportSize * 0.5f;
        targetPosition = viewportCenter - graphPosition * targetScale;
        ClampPositionToBounds();
    }

    /// <summary>
    /// Resetuje zoom do wartości 1.0 i centruje widok na węźle domyślnym.
    /// </summary>
    public void ResetView()
    {
        targetScale = 1.0f;
        Vector2 focusPos = defaultFocusPosition != Vector2.zero ? defaultFocusPosition : new Vector2(1460f, 2600f);
        FocusOnPosition(focusPos);
    }

    /// <summary>
    /// Natychmiastowe ustawienie widoku bez interpolacji (np. przy otwarciu okna).
    /// </summary>
    public void ResetViewInstant()
    {
        targetScale = 1.0f;
        currentScale = 1.0f;
        Vector2 viewportSize = GetViewportSize();
        Vector2 viewportCenter = viewportSize * 0.5f;
        Vector2 focusPos = defaultFocusPosition != Vector2.zero ? defaultFocusPosition : new Vector2(1460f, 2600f);
        targetPosition = viewportCenter - focusPos * targetScale;
        currentPosition = targetPosition;

        ClampPositionToBounds();

        if (contentRect != null)
        {
            contentRect.anchoredPosition = currentPosition;
            contentRect.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// Ogranicza pozycję kontenera w granicach contentBounds w przestrzeni zakotwiczenia dolno-lewej.
    /// </summary>
    public void ClampPositionToBounds()
    {
        if (targetScale <= 0f) targetScale = 1.0f;
        Vector2 viewportSize = GetViewportSize();
        if (viewportSize.x <= 0f || viewportSize.y <= 0f) return;

        Rect bounds = GetContentBounds();

        // Oblicz dopuszczalny zakres przesunięcia w oparciu o contentBounds i bieżącą skalę
        float minX = viewportSize.x - bounds.xMax * targetScale;
        float maxX = -bounds.xMin * targetScale;

        float minY = viewportSize.y - bounds.yMax * targetScale;
        float maxY = -bounds.yMin * targetScale;

        if (minX > maxX)
        {
            targetPosition.x = (viewportSize.x - (bounds.xMin + bounds.xMax) * targetScale) * 0.5f;
        }
        else
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        }

        if (minY > maxY)
        {
            targetPosition.y = (viewportSize.y - (bounds.yMin + bounds.yMax) * targetScale) * 0.5f;
        }
        else
        {
            targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);
        }
    }

    // =========================================================================
    // IMPLEMENTACJA EVENT SYSTEM (LPM PRZECIĄGANIE NA TLE VIEWPORTU)
    // =========================================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            isDragging = true;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            isDragging = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging && eventData.button == PointerEventData.InputButton.Left)
        {
            Pan(eventData.delta);
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData != null && Mathf.Abs(eventData.scrollDelta.y) > 0.001f)
        {
            Zoom(eventData.scrollDelta.y, eventData.position);
        }
    }
}
