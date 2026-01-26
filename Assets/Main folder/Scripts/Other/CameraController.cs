using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Prêdkoœci")]
    public float moveSpeed = 20f;
    public float zoomSpeed = 10f;
    public float smoothing = 5f;

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

        // 1. Pobieramy kierunek, w którym patrzy kamera
        Vector3 cameraForward = transform.forward;
        Vector3 cameraRight = transform.right;

        // 2. "Sp³aszczamy" te wektory, ¿eby ignorowa³y pochylenie kamery w dó³ (oœ Y)
        // Chcemy poruszaæ siê tylko po p³aszczyŸnie mapy (X i Z)
        cameraForward.y = 0;
        cameraRight.y = 0;

        // 3. Normalizujemy, ¿eby mia³y d³ugoœæ 1 (po sp³aszczeniu mog³a siê zmieniæ)
        cameraForward.Normalize();
        cameraRight.Normalize();

        // 4. Tworzymy wektor ruchu relatywny do kamery
        // W * PrzódKamery + D * PrawoKamery
        Vector3 direction = (cameraForward * zInput + cameraRight * xInput).normalized;
        // ---------------------------

        if (direction.magnitude >= 0.1f)
        {
            // U¿ywamy unscaledDeltaTime dla p³ynnoœci przy zmianie prêdkoœci gry
            targetPosition += direction * moveSpeed * Time.unscaledDeltaTime;
        }

        // Obs³uga wysokoœci (Zoom)
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

        // Obliczamy offset Z, ¿eby zachowaæ kamerê w odpowiedniej odleg³oœci od punktu
        // bior¹c pod uwagê jej aktualny k¹t nachylenia (Pitch/X-Rotation)
        float currentAngleX = transform.eulerAngles.x;

        // Matematyka: tan(k¹t) = przeciwprostok¹tna (Y) / przyprostok¹tna (Z)
        // Zatem: Z = Y / tan(k¹t)
        // Uwaga: Jeœli k¹t jest bliski 90 (patrzy pionowo w dó³), tan d¹¿y do nieskoñczonoœci, co jest OK (offset Z bliski 0)

        if (currentAngleX > 5f && currentAngleX < 89f)
        {
            float zOffset = targetPosition.y / Mathf.Tan(currentAngleX * Mathf.Deg2Rad);
            targetPosition.z -= zOffset;
        }

        // Ponowne na³o¿enie limitów
        targetPosition.x = Mathf.Clamp(targetPosition.x, -mapLimit.x, mapLimit.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, -mapLimit.y, mapLimit.y);
    }
}