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

    [Header("Obrót kamery (PPM)")]
    [Tooltip("Włącza obracanie kamery prawym przyciskiem myszy (PPM)")]
    public bool enableRMBRotation = true;
    [Tooltip("Czułość obrotu kamery myszą")]
    public float rotationSpeed = 3f;
    [Tooltip("Minimalne nachylenie kamery (pitch w stopniach)")]
    public float minPitch = 20f;
    [Tooltip("Maksymalne nachylenie kamery (pitch w stopniach)")]
    public float maxPitch = 80f;
    [Tooltip("Odwrócenie osi pionowej przy obrocie nachylenia")]
    public bool invertPitch = false;
    [Tooltip("Szybkość płynnego powrotu rotacji po kliknięciu scrolla (0 = natychmiast)")]
    public float resetRotationSmoothing = 12f;

    [Header("Przesuwanie myszą")]
    [Tooltip("Włącza przesuwanie kamery po najechaniu kursorem na krawędzie ekranu")]
    public bool enableEdgePan = true;
    [Tooltip("Szerokość strefy aktywacji przy krawędzi ekranu w pikselach")]
    public float edgePanBorderThickness = 15f;
    [Tooltip("Opcjonalna prędkość przesuwania przy krawędzi (jeśli 0, używa moveSpeed)")]
    public float edgePanSpeed = 0f;

    [Header("Granice (Ograniczenia)")]
    [Tooltip("Domyślna wysokość początkowa kamery przy starcie gry")]
    public float defaultHeight = 25f;
    public float minHeight = 8f;
    public float maxHeight = 50f;
    public Vector2 mapLimit = new Vector2(50f, 50f);

    // Flaga blokady (np. gdy otwarte jest menu budowania)
    [HideInInspector] public bool isInputLocked = false;
    public bool IsRotatingRMB => isRotatingRMB;
    public bool IsRmbHeld => isRotatingRMB || Input.GetMouseButton(1);

    private Camera _cam;
    private float targetHeight;
    private Vector3 focusTarget;
    private bool isFocusing = false;

    // Zmienne obrotu kamery
    private Quaternion initialRotation;
    private float initialPitch;
    private float initialYaw;
    private float currentPitch;
    private float currentYaw;
    private Vector3 currentPivot;
    private Vector3 resetPivot;
    private bool isRotatingRMB = false;
    private bool isResettingRotation = false;
    private Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

    void Start()
    {
        if (_cam == null) _cam = Camera.main;

        initialRotation = transform.rotation;
        initialPitch = transform.eulerAngles.x;
        if (initialPitch > 180f) initialPitch -= 360f;
        initialYaw = transform.eulerAngles.y;
        currentPitch = initialPitch;
        currentYaw = initialYaw;

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
            HandleRotationInput();
            HandleMovementInput();
        }
        else
        {
            isRotatingRMB = false;
            isResettingRotation = false;
        }

        HandleHeightSmoothing();
        HandleFocusMovement();
    }

    void HandleRotationInput()
    {
        // 1. Reset rotacji kółkiem myszy (MMB / Scroll Click)
        if (Input.GetMouseButtonDown(2))
        {
            if (!(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                Ray ray = new Ray(transform.position, transform.forward);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    resetPivot = ray.GetPoint(enter);
                    resetPivot.y = 0f;
                }
                else
                {
                    resetPivot = new Vector3(transform.position.x, 0f, transform.position.z);
                }

                if (resetRotationSmoothing <= 0f)
                {
                    currentPitch = initialPitch;
                    currentYaw = initialYaw;
                    ApplyOrbit(resetPivot, currentPitch, currentYaw);
                    isResettingRotation = false;
                }
                else
                {
                    isResettingRotation = true;
                    isRotatingRMB = false;
                }
            }
        }

        // Płynny powrót rotacji do stanu początkowego wokół obserwowanego punktu
        if (isResettingRotation)
        {
            currentYaw = Mathf.LerpAngle(currentYaw, initialYaw, resetRotationSmoothing * Time.unscaledDeltaTime);
            currentPitch = Mathf.Lerp(currentPitch, initialPitch, resetRotationSmoothing * Time.unscaledDeltaTime);

            ApplyOrbit(resetPivot, currentPitch, currentYaw);

            if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, initialYaw)) < 0.2f && Mathf.Abs(currentPitch - initialPitch) < 0.2f)
            {
                currentYaw = initialYaw;
                currentPitch = initialPitch;
                ApplyOrbit(resetPivot, currentPitch, currentYaw);
                isResettingRotation = false;
            }
        }

        if (!enableRMBRotation || _cam == null) return;

        // 2. Rozpoczęcie obrotu PPM
        if (Input.GetMouseButtonDown(1))
        {
            if (!(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                isFocusing = false; // Przerwij ewentualny ruch kamery do punktu skupienia
                Ray ray = new Ray(transform.position, transform.forward);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    currentPivot = ray.GetPoint(enter);
                    currentPivot.y = 0f;
                    isRotatingRMB = true;
                    isResettingRotation = false;
                    currentPitch = transform.eulerAngles.x;
                    if (currentPitch > 180f) currentPitch -= 360f;
                    currentYaw = transform.eulerAngles.y;
                }
            }
        }

        // 3. Trzymanie i obracanie PPM (tylko gdy mysz faktycznie się poruszy)
        if (Input.GetMouseButton(1) && isRotatingRMB)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            if (Mathf.Abs(mouseX) > 0.0001f || Mathf.Abs(mouseY) > 0.0001f)
            {
                isFocusing = false;
                isResettingRotation = false;

                currentYaw += mouseX * rotationSpeed;
                float pitchDelta = invertPitch ? -mouseY : mouseY;
                currentPitch += pitchDelta * rotationSpeed;
                currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

                ApplyOrbit(currentPivot, currentPitch, currentYaw);
            }
        }

        // 4. Zakończenie obrotu PPM
        if (Input.GetMouseButtonUp(1))
        {
            isRotatingRMB = false;
        }
    }

    private void ApplyOrbit(Vector3 pivot, float pitch, float yaw)
    {
        Quaternion newRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 backDir = newRot * Vector3.back;
        if (backDir.y > 0.01f)
        {
            float currentY = (Mathf.Abs(transform.position.y - targetHeight) > 0.01f) ? transform.position.y : targetHeight;
            float dist = currentY / backDir.y;
            Vector3 newCamPos = pivot + backDir * dist;
            newCamPos.x = Mathf.Clamp(newCamPos.x, -mapLimit.x, mapLimit.x);
            newCamPos.z = Mathf.Clamp(newCamPos.z, -mapLimit.y, mapLimit.y);
            transform.position = newCamPos;
            transform.rotation = newRot;
        }
    }

    void HandleMovementInput()
    {
        bool isRmbHeld = isRotatingRMB || Input.GetMouseButton(1);

        float xInput = 0f;
        float zInput = 0f;
        float keyboardX = 0f;
        float keyboardZ = 0f;

        // Gdy PPM jest kliknięty/trzymany (zmiana kąta/obrót), całkowicie blokujemy klawiaturę oraz przesuwanie przy krawędziach
        if (!isRmbHeld)
        {
            keyboardX = Input.GetAxisRaw("Horizontal"); // A / D
            keyboardZ = Input.GetAxisRaw("Vertical");   // W / S
            xInput = keyboardX;
            zInput = keyboardZ;

            // Obsługa najechania na krawędzie ekranu (Edge Pan)
            if (enableEdgePan)
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
            isResettingRotation = false; // Przerwij ewentualny reset rotacji
            float currentMoveSpeed = (edgePanSpeed > 0f && Mathf.Approximately(keyboardX, 0f) && Mathf.Approximately(keyboardZ, 0f))
                ? edgePanSpeed
                : moveSpeed;

            Vector3 moveDelta = direction * (currentMoveSpeed * Time.unscaledDeltaTime);
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x + moveDelta.x, -mapLimit.x, mapLimit.x);
            pos.z = Mathf.Clamp(pos.z + moveDelta.z, -mapLimit.y, mapLimit.y);
            transform.position = pos;
        }

        // Obsługa wysokości (Zoom) klawiaturą (Q / E) - wyłączona gdy PPM jest wciśnięty
        float heightInput = 0f;
        if (!isRmbHeld)
        {
            if (Input.GetKey(KeyCode.Q)) heightInput = -1f;
            if (Input.GetKey(KeyCode.E)) heightInput = 1f;
        }

        if (Mathf.Abs(heightInput) > 0.01f)
        {
            isResettingRotation = false;
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
                isResettingRotation = false;
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

        Vector3 fwdFlat = transform.forward;
        fwdFlat.y = 0f;
        if (fwdFlat.sqrMagnitude > 0.001f)
        {
            fwdFlat.Normalize();
            float currentAngleX = transform.eulerAngles.x;
            if (currentAngleX > 5f && currentAngleX < 89f)
            {
                float dist = focusTarget.y / Mathf.Tan(currentAngleX * Mathf.Deg2Rad);
                focusTarget -= fwdFlat * dist;
            }
        }

        focusTarget.x = Mathf.Clamp(focusTarget.x, -mapLimit.x, mapLimit.x);
        focusTarget.z = Mathf.Clamp(focusTarget.z, -mapLimit.y, mapLimit.y);

        isFocusing = true;
        isResettingRotation = false;
    }
}
