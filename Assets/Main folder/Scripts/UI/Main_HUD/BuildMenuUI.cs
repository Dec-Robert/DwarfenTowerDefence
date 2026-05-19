using UnityEngine;
using UnityEngine.UI;

public class BuildMenuUI : MonoBehaviour
{
    [Header("Referencje do przycisków")]
    [SerializeField] private Button btnBuildTower;
    [SerializeField] private Button btnBuildProduction;
    [SerializeField] private Button btnBuildHouses;
    [SerializeField] private Button btnBuildUniq;

    private void Start()
    {
        // Rejestrowanie akcji przycisków
        if(btnBuildTower != null) btnBuildTower.onClick.AddListener(OnTowerButtonClicked);
        if(btnBuildProduction != null) btnBuildProduction.onClick.AddListener(OnProductionButtonClicked);
        if(btnBuildHouses != null) btnBuildHouses.onClick.AddListener(OnHousesButtonClicked);
        if(btnBuildUniq != null) btnBuildUniq.onClick.AddListener(OnUniqButtonClicked);
    }

    private void OnDestroy()
    {
        // Wyrejestrowanie akcji
        if(btnBuildTower != null) btnBuildTower.onClick.RemoveAllListeners();
        if(btnBuildProduction != null) btnBuildProduction.onClick.RemoveAllListeners();
        if(btnBuildHouses != null) btnBuildHouses.onClick.RemoveAllListeners();
        if(btnBuildUniq != null) btnBuildUniq.onClick.RemoveAllListeners();
    }

    private void OnTowerButtonClicked()
    {
        Debug.Log("[BuildMenuUI] Inicjalizacja budowy wieży.");
    }

    private void OnProductionButtonClicked()
    {
        Debug.Log("[BuildMenuUI] Inicjalizacja budowy budynku produkcyjnego.");
    }

    private void OnHousesButtonClicked()
    {
        Debug.Log("[BuildMenuUI] Inicjalizacja budowy domów.");
    }

    private void OnUniqButtonClicked()
    {
        Debug.Log("[BuildMenuUI] Inicjalizacja budowy struktur unikalnych.");
    }
}