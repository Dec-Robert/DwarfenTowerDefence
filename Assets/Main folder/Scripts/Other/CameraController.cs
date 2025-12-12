using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Prêdkoœci")]
    public float moveSpeed = 20f;
    public float zoomSpeed = 10f;
    public float smoothing = 5f;

    [Header("Granice")]
    public float minHeight = 5f;
    public float maxHeight = 25f;
    public Vector2 mapLimit = new Vector2(50f, 50f);

    // NOWE POLE: Flaga blokady
    [HideInInspector] public bool isInputLocked = false;

    private Vector3 targetPosition;

    void Start()
    {
        targetPosition = transform.position;
    }

    void Update()
    {
        // Jeœli zablokowane, nie czytamy inputu, ale kamera nadal mo¿e dokoñczyæ p³ynny ruch
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
            targetPosition += direction * moveSpeed * Time.deltaTime;
        }

        float heightInput = 0f;
        if (Input.GetKey(KeyCode.Q)) heightInput = -1f;
        if (Input.GetKey(KeyCode.E)) heightInput = 1f;

        if (Mathf.Abs(heightInput) > 0.01f)
        {
            targetPosition.y += heightInput * zoomSpeed * Time.deltaTime;
        }

        // Clamping
        targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
        targetPosition.x = Mathf.Clamp(targetPosition.x, -mapLimit.x, mapLimit.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, -mapLimit.y, mapLimit.y);
    }

    void MoveCamera()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing * Time.deltaTime);
    }

    public void FocusOnPoint(Vector3 worldPoint)
    {
        targetPosition = new Vector3(worldPoint.x, targetPosition.y, worldPoint.z);

        // Opcjonalny offset dla kamery pod k¹tem (np. 60 stopni)
        float zOffset = targetPosition.y / Mathf.Tan(transform.eulerAngles.x * Mathf.Deg2Rad);
        targetPosition.z -= zOffset;

        targetPosition.x = Mathf.Clamp(targetPosition.x, -mapLimit.x, mapLimit.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, -mapLimit.y, mapLimit.y);
    }
}