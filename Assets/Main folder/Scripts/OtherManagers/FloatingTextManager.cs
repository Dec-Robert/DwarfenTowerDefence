using UnityEngine;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

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
        if (floatingTextPrefab == null) return;

        // Losowy offset, ¿eby teksty nie nak³ada³y siê idealnie na siebie
        Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 2.5f, Random.Range(-0.2f, 0.2f));
        Vector3 spawnPos = position + offset;

        GameObject obj = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);

        // Jeœli masz kamerê pod k¹tem, ustaw rotacjê tekstu tutaj, np. 45 stopni w X
        // obj.transform.rotation = Quaternion.Euler(60, 0, 0); 

        FloatingText ft = obj.GetComponent<FloatingText>();
        if (ft != null)
        {
            ft.Setup(text, color);
        }
    }
}