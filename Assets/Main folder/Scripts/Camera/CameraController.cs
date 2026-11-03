using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    private static CameraController _instance;
    public static CameraController Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<CameraController>();
            return _instance;
        }
        private set => _instance = value;
    }

    void Awake()
    {
        _instance = this;
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
    }

    [Header("Prędkości")]
    public float moveSpeed = 20f;
    public float zoomSpeed = 10f;
    public float smoothing = 5f;

    [Header("Zoom pod scrollem")]
    [Tooltip("Czułość przybliżania/oddalania kółkiem myszy")]
    public float scrollSpeed = 30f;
    [Tooltip("Szybkość wygładzania zoomu scrollem (wyższa wartość = szybsze, bardziej responsywne dojście do celu)")]
    public float scrollSmoothing = 15f;
    [Tooltip("Odwrócenie kierunku scrolla (domyślnie: w przód = przybliżenie/w dół, w tył = oddalenie/w górę)")]
    public bool invertScroll = false;

    [Header("Przesuwanie myszą")]
    [Tooltip("Włącza przeciąganie mapy prawym przyciskiem myszy (PPM)")]
    public bool enableRMBDrag = true;
    [Tooltip("Włącza przesuwanie kamery po najechaniu kursorem na krawędzie ekranu")]
    public bool enableEdgePan = true;
    [Tooltip("Szerokość strefy aktywacji przy krawędzi ekranu w pikselach")]
    public float edgePanBorderThickness = 15f;
    [Tooltip("Opcjonalna prędkość przesuwania przy krawędzi (jeśli 0, używa moveSpeed)")]
    public float edgePanSpeed = 0f;

    [Header("Granice (Ograniczenia)")]
    [Tooltip("Domyślna wysokość początkowa kamery przy starcie gry")]
    public float defaultHeight = 25f;
    public float minHeight = 15f;
    public float maxHeight = 50f;
    public Vector2 mapLimit = new Vector2(50f, 50f);

    // Flaga blokady (np. gdy otwarte jest menu budowania)
    [HideInInspector] public bool isInputLocked = false;

    private Camera _cam;
    private float targetHeight;
    private Vector3 focusTarget;
    private bool isFocusing = false;

    // Zmienne do przeciągania PPM
    private bool isDraggingRMB = false;
    private Vector3 lastGroundPos;
    private Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

    void Start()
    {
        if (_cam == null) _cam = Camera.main;

        // Ustalenie prawidłowej pozycji startowej (zapobiega startowi na ziemi / poniżej minHeight)
        float startY = (defaultHeight > 0f) ? defaultHeight : transform.position.y;
        startY = Mathf.Clamp(startY, minHeight, maxHeight);

        Vector3 pos = transform.position;
        pos.y = startY;
        pos.x = Mathf.Clamp(pos.x, -mapLimit.x, mapLimit.x);
        pos.z = Mathf.Clamp(pos.z, -mapLimit.y, mapLimit.y);
        transform.position = pos;

        targetHeight = startY;
    }

    void Update()
    {
        if (!isInputLocked)
        {
            HandleMouseDrag();
            HandleMovementInput();
        }
        else
        {
            isDraggingRMB = false;
        }

        HandleHeightSmoothing();
        HandleFocusMovement();
    }

    void HandleMouseDrag()
    {
        if (!enableRMBDrag || _cam == null) return;

        // Rozpoczęcie przeciągania
        if (Input.GetMouseButtonDown(1))
        {
            if (!(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    isDraggingRMB = true;
                    lastGroundPos = ray.GetPoint(enter);
                }
            }
        }

        // Trzymanie i przeciąganie PPM
        if (Input.GetMouseButton(1) && isDraggingRMB)
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 currentGroundPos = ray.GetPoint(enter);
                Vector3 delta = lastGroundPos - currentGroundPos;
                delta.y = 0;

                if (delta.sqrMagnitude > 0.00001f)
                {
                    isFocusing = false;
                    Vector3 newPos = transform.position + delta;
                    newPos.x = Mathf.Clamp(newPos.x, -mapLimit.x, mapLimit.x);
                    newPos.z = Mathf.Clamp(newPos.z, -mapLimit.y, mapLimit.y);
                    transform.position = newPos;

                    // Po przesunięciu kamery aktualizujemy punkt referencyjny na bieżącej pozycji kursora
                    Ray newRay = _cam.ScreenPointToRay(Input.mousePosition);
                    if (groundPlane.Raycast(newRay, out float newEnter))
                    {
                        lastGroundPos = newRay.GetPoint(newEnter);
                    }
                }
            }
        }

        // Zakończenie przeciągania - natychmiastowe zatrzymanie
        if (Input.GetMouseButtonUp(1))
        {
            isDraggingRMB = false;
        }
    }

    void HandleMovementInput()
    {
        float xInput = Input.GetAxisRaw("Horizontal"); // A / D
        float zInput = Input.GetAxisRaw("Vertical");   // W / S

        // Obsługa najechania na krawędzie ekranu (Edge Pan) - wyłączone podczas aktywnego dragowania PPM
        if (enableEdgePan && !isDraggingRMB)
        {
            Vector3 mousePos = Input.mousePosition;
            // Sprawdzamy czy kursor znajduje się wewnątrz okna gry
            if (mousePos.x >= 0 && mousePos.x <= Screen.width &&
                mousePos.y >= 0 && mousePos.y <= Screen.height)
            {
                if (mousePos.x <= edgePanBorderThickness) xInput -= 1f;
                if (mousePos.x >= Screen.width - edgePanBorderThickness) xInput += 1f;
                if (mousePos.y <= edgePanBorderThickness) zInput -= 1f;
                if (mousePos.y >= Screen.height - edgePanBorderThickness) zInput += 1f;
            }
        }

        // 1. Kierunki kamery na płaszczyźnie poziomej
        Vector3 cameraForward = transform.forward;
        Vector3 cameraRight = transform.right;

        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 direction = (cameraForward * zInput + cameraRight * xInput).normalized;

        // Natychmiastowy, płynny ruch klawiaturą / krawędziami (zatrzymanie natychmiast po puszczeniu)
        if (direction.sqrMagnitude > 0.001f)
        {
            isFocusing = false; // Przerwij ewentualny ruch kamery do punktu skupienia
            float currentMoveSpeed = (edgePanSpeed > 0f && Mathf.Approximately(Input.GetAxisRaw("Horizontal"), 0f) && Mathf.Approximately(Input.GetAxisRaw("Vertical"), 0f))
                ? edgePanSpeed
                : moveSpeed;

            Vector3 moveDelta = direction * (currentMoveSpeed * Time.unscaledDeltaTime);
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x + moveDelta.x, -mapLimit.x, mapLimit.x);
            pos.z = Mathf.Clamp(pos.z + moveDelta.z, -mapLimit.y, mapLimit.y);
            transform.position = pos;
        }

        // Obsługa wysokości (Zoom) klawiaturą (Q / E) - natychmiastowe zatrzymanie bez pływania
        float heightInput = 0f;
        if (Input.GetKey(KeyCode.Q)) heightInput = -1f;
        if (Input.GetKey(KeyCode.E)) heightInput = 1f;

        if (Mathf.Abs(heightInput) > 0.01f)
        {
            float currentY = transform.position.y + heightInput * zoomSpeed * Time.unscaledDeltaTime;
            currentY = Mathf.Clamp(currentY, minHeight, maxHeight);
            transform.position = new Vector3(transform.position.x, currentY, transform.position.z);
            targetHeight = currentY; // Synchronizacja z wysokością docelową dla scrolla
        }

        // Obsługa scrolla myszy (płynny zoom góra/dół)
        bool isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!isPointerOverUI)
        {
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollInput) > 0.0001f)
            {
                float dir = invertScroll ? 1f : -1f;
                targetHeight += scrollInput * scrollSpeed * dir;
                targetHeight = Mathf.Clamp(targetHeight, minHeight, maxHeight);
            }
        }
    }

    void HandleHeightSmoothing()
    {
        // Jeśli nie trzymamy aktywnie Q/E, a cel wysokości różni się od bieżącego (np. po scrollu)
        float currentY = transform.position.y;
        if (Mathf.Abs(currentY - targetHeight) > 0.001f)
        {
            float smoothY = Mathf.Lerp(currentY, targetHeight, scrollSmoothing * Time.unscaledDeltaTime);
            if (Mathf.Abs(smoothY - targetHeight) < 0.01f)
            {
                smoothY = targetHeight;
            }
            transform.position = new Vector3(transform.position.x, smoothY, transform.position.z);
        }
    }

    void HandleFocusMovement()
    {
        if (!isFocusing) return;

        Vector3 currentPos = transform.position;
        Vector3 targetXZ = new Vector3(focusTarget.x, currentPos.y, focusTarget.z);
        transform.position = Vector3.Lerp(currentPos, targetXZ, smoothing * Time.unscaledDeltaTime);

        if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(focusTarget.x, focusTarget.z)) < 0.05f)
        {
            transform.position = targetXZ;
            isFocusing = false;
        }
    }

    public void FocusOnPoint(Vector3 worldPoint)
    {
        focusTarget = new Vector3(worldPoint.x, transform.position.y, worldPoint.z);

        float currentAngleX = transform.eulerAngles.x;
        if (currentAngleX > 5f && currentAngleX < 89f)
        {
            float zOffset = focusTarget.y / Mathf.Tan(currentAngleX * Mathf.Deg2Rad);
            focusTarget.z -= zOffset;
        }

        focusTarget.x = Mathf.Clamp(focusTarget.x, -mapLimit.x, mapLimit.x);
        focusTarget.z = Mathf.Clamp(focusTarget.z, -mapLimit.y, mapLimit.y);

        isFocusing = true;
    }
}
