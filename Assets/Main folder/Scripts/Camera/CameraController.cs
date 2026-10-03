using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Pr�dko�ci")]
    public float moveSpeed = 20f;
    public float zoomSpeed = 10f;
    public float smoothing = 5f;
    //
    [Header("Granice (Ograniczenia)")]
    public float minHeight = 5f;
    public float maxHeight = 25f;
    public Vector2 mapLimit = new Vector2(50f, 50f);

    // Flaga blokady (np. gdy otwarte jest menu budowania)
    [HideInInspector] public bool isInputLocked = false;

    private Vector3 targetPosition;

    void Start()
    {
        targetPosition = transform.position;
    }

    void Update()
    {
        if (!isInputLocked)
        {
            HandleMovementInput();
        }
        MoveCamera();
    }

    void HandleMovementInput()
    {
        float xInput = Input.GetAxisRaw("Horizontal"); // A / D
        float zInput = Input.GetAxisRaw("Vertical");   // W / S

        // --- ZMIANA LOGIKI RUCHU ---

        // 1. Pobieramy kierunek, w kt�rym patrzy kamera
        Vector3 cameraForward = transform.forward;
        Vector3 cameraRight = transform.right;

        // 2. "Sp�aszczamy" te wektory, �eby ignorowa�y pochylenie kamery w d� (o� Y)
        // Chcemy porusza� si� tylko po p�aszczy�nie mapy (X i Z)
        cameraForward.y = 0;
        cameraRight.y = 0;

        // 3. Normalizujemy, �eby mia�y d�ugo�� 1 (po sp�aszczeniu mog�a si� zmieni�)
        cameraForward.Normalize();
        cameraRight.Normalize();

        // 4. Tworzymy wektor ruchu relatywny do kamery
        // W * Prz�dKamery + D * PrawoKamery
        Vector3 direction = (cameraForward * zInput + cameraRight * xInput).normalized;
        // ---------------------------

        if (direction.magnitude >= 0.1f)
        {
            // U�ywamy unscaledDeltaTime dla p�ynno�ci przy zmianie pr�dko�ci gry
            targetPosition += direction * moveSpeed * Time.unscaledDeltaTime;
        }

        // Obs�uga wysoko�ci (Zoom)
        float heightInput = 0f;
        if (Input.GetKey(KeyCode.Q)) heightInput = -1f;
        if (Input.GetKey(KeyCode.E)) heightInput = 1f;

        if (Mathf.Abs(heightInput) > 0.01f)
        {
            targetPosition.y += heightInput * zoomSpeed * Time.unscaledDeltaTime;
        }

        // Ograniczenia (Clamping)
        targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
        targetPosition.x = Mathf.Clamp(targetPosition.x, -mapLimit.x, mapLimit.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, -mapLimit.y, mapLimit.y);
    }

    void MoveCamera()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing * Time.unscaledDeltaTime);
    }

    public void FocusOnPoint(Vector3 worldPoint)
    {
        targetPosition = new Vector3(worldPoint.x, targetPosition.y, worldPoint.z);

        // Obliczamy offset Z, �eby zachowa� kamer� w odpowiedniej odleg�o�ci od punktu
        // bior�c pod uwag� jej aktualny k�t nachylenia (Pitch/X-Rotation)
        float currentAngleX = transform.eulerAngles.x;

        // Matematyka: tan(k�t) = przeciwprostok�tna (Y) / przyprostok�tna (Z)
        // Zatem: Z = Y / tan(k�t)
        // Uwaga: Je�li k�t jest bliski 90 (patrzy pionowo w d�), tan d��y do niesko�czono�ci, co jest OK (offset Z bliski 0)

        if (currentAngleX > 5f && currentAngleX < 89f)
        {
            float zOffset = targetPosition.y / Mathf.Tan(currentAngleX * Mathf.Deg2Rad);
            targetPosition.z -= zOffset;
        }

        // Ponowne na�o�enie limit�w
        targetPosition.x = Mathf.Clamp(targetPosition.x, -mapLimit.x, mapLimit.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, -mapLimit.y, mapLimit.y);
    }
}