using UnityEngine;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }
    //
    [Header("Referencje")]
    public GameObject floatingTextPrefab; // Prefab z komponentem FloatingText

    [Header("Kolory")]
    public Color productionColor = Color.green;
    public Color upkeepColor = Color.red;
    public Color warningColor = Color.yellow;

    private void Awake()
    {
        Instance = this;
    }

    public void ShowGain(Vector3 position, string resourceName, int amount)
    {
        ShowText(position, $"+{amount} {resourceName}", productionColor);
    }

    public void ShowLoss(Vector3 position, string resourceName, int amount)
    {
        ShowText(position, $"-{amount} {resourceName}", upkeepColor);
    }

    public void ShowText(Vector3 position, string text, Color color)
    {

    }
}