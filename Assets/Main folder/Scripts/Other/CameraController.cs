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
        float xInput = Input.GetAxisRaw("Horizontal");
        float zInput = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(xInput, 0f, zInput).normalized;

        if (direction.magnitude >= 0.1f)
        {
            // ZMIANA: unscaledDeltaTime (niezale¿ne od pauzy/przyspieszenia)
            targetPosition += direction * moveSpeed * Time.unscaledDeltaTime;
        }

        // Obs³uga wysokoœci
        float heightInput = 0f;
        if (Input.GetKey(KeyCode.Q)) heightInput = -1f;
        if (Input.GetKey(KeyCode.E)) heightInput = 1f;

        if (Mathf.Abs(heightInput) > 0.01f)
        {
            // ZMIANA: unscaledDeltaTime
            targetPosition.y += heightInput * zoomSpeed * Time.unscaledDeltaTime;
        }

        // Ograniczenia
        targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
        targetPosition.x = Mathf.Clamp(targetPosition.x, -mapLimit.x, mapLimit.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, -mapLimit.y, mapLimit.y);
    }

    void MoveCamera()
    {
        // ZMIANA: unscaledDeltaTime - dziêki temu kamera nie "szarpie" przy zmianie prêdkoœci gry
        // i dzia³a p³ynnie nawet na pauzie (TimeScale = 0)
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing * Time.unscaledDeltaTime);
    }

    public void FocusOnPoint(Vector3 worldPoint)
    {
        targetPosition = new Vector3(worldPoint.x, targetPosition.y, worldPoint.z);

        // Offset dla kamery pod k¹tem (np. 60 stopni)
        float zOffset = targetPosition.y / Mathf.Tan(transform.eulerAngles.x * Mathf.Deg2Rad);
        targetPosition.z -= zOffset;

        targetPosition.x = Mathf.Clamp(targetPosition.x, -mapLimit.x, mapLimit.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, -mapLimit.y, mapLimit.y);
    }
}