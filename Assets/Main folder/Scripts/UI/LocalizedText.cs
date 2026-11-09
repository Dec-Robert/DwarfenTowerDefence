using TMPro;
using UnityEngine;

/// <summary>
/// Komponent automatycznie aktualizujący tekst TextMeshProUGUI w oparciu o LocalizationManager.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Klucz w pliku JSON lokalizacji (np. UI_BTN_RESUME)")]
    [SerializeField] private string localizationKey;

    [Tooltip("Czy automatycznie zmienić tekst na wielkie litery?")]
    [SerializeField] private bool toUpper = false;

    [Tooltip("Opcjonalny prefiks doklejany przed przetłumaczonym tekstem")]
    [SerializeField] private string prefix = "";

    [Tooltip("Opcjonalny sufiks doklejany po przetłumaczonym tekście")]
    [SerializeField] private string suffix = "";

    private TextMeshProUGUI tmpText;

    public string Key
    {
        get => localizationKey;
        set
        {
            localizationKey = value;
            RefreshText();
        }
    }

    private void Awake()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        RefreshText();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        RefreshText();
    }

    /// <summary>
    /// Odświeża zawartość TextMeshPro w oparciu o aktualny język.
    /// </summary>
    public void RefreshText()
    {
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
        if (tmpText == null || string.IsNullOrEmpty(localizationKey)) return;

        string translated = LocalizationManager.Get(localizationKey, tmpText.text);

        if (toUpper && !string.IsNullOrEmpty(translated))
        {
            translated = translated.ToUpper();
        }

        tmpText.text = $"{prefix}{translated}{suffix}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && !string.IsNullOrEmpty(localizationKey))
        {
            if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
            // Jeśli LocalizationManager jest zainicjalizowany w edytorze, odśwież podgląd
            if (LocalizationManager.IsInitialized && tmpText != null)
            {
                RefreshText();
            }
        }
    }
#endif
}
