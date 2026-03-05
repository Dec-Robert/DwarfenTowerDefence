using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Analytics;
using System.Threading.Tasks;

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    private async void Awake()
    {
        // Standardowy Singleton – chcemy, żeby ten skrypt żył od odpalenia menu aż do wyłączenia gry
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Uruchamiamy proces łączenia z chmurą
        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            // 1. Inicjalizacja rdzenia usług Unity (wymagane dla każdej usługi UGS)
            await UnityServices.InitializeAsync();

            // Opcjonalnie: Obsługa zgody na RODO (GDPR/COPPA)
            // W pełnej wersji gry powinieneś wyświetlić popup z prośbą o zgodę.
            // Dla naszych celów testowych wymuszamy start zbierania danych:
            AnalyticsService.Instance.StartDataCollection();

            Debug.Log("<color=green>[TelemetryManager] Unity Analytics pomyślnie zainicjowane i połączone z chmurą!</color>");
        }
        catch (System.Exception e)
        {
            // Łapiemy błąd, żeby gra nie "wybuchła", jeśli np. gracz nie ma internetu
            Debug.LogWarning($"<color=orange>[TelemetryManager] Błąd inicjalizacji Unity Services (brak internetu?): {e.Message}</color>");
        }
    }
}