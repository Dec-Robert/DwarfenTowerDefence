using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Text;

public class ResourceLogger : MonoBehaviour
{
    public static ResourceLogger Instance { get; private set; }

    [Header("Konfiguracja")]
    [Tooltip("Nazwa pliku w folderze Assets (w edytorze) lub AppData (w buildzie)")]
    public string fileName = "EconomyLog.txt";
    public bool enableLogging = true;

    private string filePath;
    private int lastLoggedDay = -1;



    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this) Destroy(gameObject);
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Opcjonalne, jeœli chcesz logowaæ miêdzy scenami
        }

        // Ustalenie œcie¿ki (W edytorze: obok folderu Assets, w Buildzie: w danych aplikacji)
#if UNITY_EDITOR
        filePath = Path.Combine(Application.dataPath, "../", fileName); // Obok folderu Assets
#else
        filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

        // Reset pliku na starcie gry
        if (enableLogging)
        {
            try
            {
                File.WriteAllText(filePath, $"=== ROZPOCZÊCIE GRY: {System.DateTime.Now} ===\n\n");
                Debug.Log($"[Logger] Plik logów utworzony: {filePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Logger] B³¹d tworzenia pliku: {e.Message}");
                enableLogging = false;
            }

            Debug.Log($"<color=yellow>[Logger] PLIK ZAPISANY TUTAJ: {Path.GetFullPath(filePath)}</color>");

        }
    }

    private void Start()
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnDayChanged += LogNewDay;
        }
    }

    private void OnDestroy()
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnDayChanged -= LogNewDay;
        }
    }


    // --- METODY LOGOWANIA ---

    // Automatycznie wywo³ywane przy zmianie dnia
    private void LogNewDay(int day)
    {

        Debug.LogWarning($"[Logger] Nowy dzieñ: {day}");
        if (!enableLogging) return;
        AppendToFile($"\n[Dzieñ {day}]\n==========================================\n");
        lastLoggedDay = day;
    }

    /// <summary>
    /// Loguje zestaw zmian surowców pod jednym zdarzeniem.
    /// </summary>
    public void LogTransaction(string eventName, Dictionary<ResourceType, float> changes)
    {
        if (!enableLogging || changes == null || changes.Count == 0) return;

        // Sprawdzenie czy dzieñ siê zmieni³ (zabezpieczenie)
        if (TimeCycleManager.Instance != null && TimeCycleManager.Instance.dayCount != lastLoggedDay)
        {
            LogNewDay(TimeCycleManager.Instance.dayCount);
        }

        string time = "00:00";
        if (TimeCycleManager.Instance != null)
        {
            // Pobieramy sformatowan¹ godzinê
            // Musimy lekko przerobiæ getter w TimeCycleManager lub sformatowaæ tu rêcznie
            float t = TimeCycleManager.Instance.currentTime;
            float m = (t - Mathf.Floor(t)) * 60;
            time = $"{Mathf.FloorToInt(t):00}:{Mathf.FloorToInt(m):00}";
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[{eventName}] [{time}] -----------");

        foreach (var kvp in changes)
        {
            // Formatowanie: +5.0 Wood lub -2.5 Stone
            string sign = kvp.Value > 0 ? "+" : "";
            sb.AppendLine($"{kvp.Key} \t{sign}{kvp.Value:F1}");
        }

        sb.AppendLine("[koniec zdarzenia] -----------");

        AppendToFile(sb.ToString());
    }

    // Przeci¹¿enie dla pojedynczego surowca (np. drop z wroga)
    public void LogSingleEvent(string eventName, ResourceType type, float amount)
    {
        var dict = new Dictionary<ResourceType, float> { { type, amount } };
        LogTransaction(eventName, dict);
    }

    private void AppendToFile(string content)
    {
        try
        {
            File.AppendAllText(filePath, content);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Logger] B³¹d zapisu: {e.Message}");
        }
    }

    [ContextMenu("OTWÓRZ PLIK LOGÓW")]
    public void OpenLogFile()
    {
        if (File.Exists(filePath))
        {
            Application.OpenURL(filePath); // Otwiera w domyœlnym edytorze tekstu
            Debug.Log($"Otwieranie pliku: {filePath}");
        }
        else
        {
            Debug.LogError($"Nie znaleziono pliku w: {filePath}");
        }
    }
}