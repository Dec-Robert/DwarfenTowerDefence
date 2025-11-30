using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Prêdkoœci")]
    public float moveSpeed = 20f;
    public float zoomSpeed = 10f;
    public float smoothing = 5f; // Im wy¿sza wartoœæ, tym szybciej kamera hamuje

    [Header("Granice (Ograniczenia)")]
    public float minHeight = 5f;
    public float maxHeight = 25f;
    public Vector2 mapLimit = new Vector2(50f, 50f); // Ograniczenie ruchu X i Z

    private Vector3 targetPosition;

    void Start()
    {
        // Ustawiamy cel tam, gdzie kamera jest na starcie
        targetPosition = transform.position;
    }

    void Update()
    {
        HandleMovementInput();
        MoveCamera();
    }

    void HandleMovementInput()
    {
        float xInput = Input.GetAxisRaw("Horizontal"); // A / D
        float zInput = Input.GetAxisRaw("Vertical");   // W / S

        // 1. Obliczanie wektora ruchu w poziomie (X, Z)
        Vector3 direction = new Vector3(xInput, 0f, zInput).normalized;

        // Modyfikacja pozycji celu (nie ruszamy jeszcze samej kamery)
        if (direction.magnitude >= 0.1f)
        {
            targetPosition += direction * moveSpeed * Time.deltaTime;
        }

        // 2. Obs³uga wysokoœci (Q / E)
        float heightInput = 0f;
        if (Input.GetKey(KeyCode.Q)) heightInput = -1f; // Dó³
        if (Input.GetKey(KeyCode.E)) heightInput = 1f;  // Góra

        if (Mathf.Abs(heightInput) > 0.01f)
        {
            targetPosition.y += heightInput * zoomSpeed * Time.deltaTime;
        }

        // 3. Ograniczenia (Clamping)
        // Ogranicz wysokoœæ
        targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);

        // Ogranicz pozycjê na mapie (X i Z)
        targetPosition.x = Mathf.Clamp(targetPosition.x, -mapLimit.x, mapLimit.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, -mapLimit.y, mapLimit.y);
    }

    void MoveCamera()
    {
        // P³ynne przejœcie z obecnej pozycji do pozycji docelowej (Interpolacja)
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing * Time.deltaTime);
    }
}