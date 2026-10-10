using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler modalu wyboru wieży dla Kuźni Runicznej (Pnl_RuneTowerSelectionModal).
/// Zgodnie z zasadami gry:
/// - Wyświetla wyłącznie wieże obronne, które nie są aktualnie przypisane do żadnej innej Kuźni w twierdzy,
/// - Po zatwierdzeniu wyboru nasyca wieżę w RuneForgeEntity i otwiera główny panel Kuźni.
/// </summary>
public class RuneTowerSelectionModalUI : MonoBehaviour
{
    [Header("Główne Referencje")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private Button btnClose;
    [SerializeField] private Transform towersContainer;

    [Header("Baza Dostępnych Wież")]
    [SerializeField] private List<TowerData> allTowersDatabase = new List<TowerData>();

    private RuneForgeEntity currentForge;

    public List<TowerData> AllTowersDatabase => allTowersDatabase;

    private void Awake()
    {
        AutoBindReferences();
        InitializeTowersDatabase();
    }

    private void Start()
    {
        InitializeTowers();
    }

    private void OnValidate()
    {
        AutoBindReferences();
        InitializeTowersDatabase();
    }

    public void AutoBindReferences()
    {
        if (txtTitle == null)
            txtTitle = transform.Find("Pnl_ModalWindow/Pnl_ModalTopBar/Txt_Title")?.GetComponent<TextMeshProUGUI>();

        if (btnClose == null)
            btnClose = transform.Find("Pnl_ModalWindow/Pnl_ModalTopBar/Btn_Close")?.GetComponent<Button>();

        if (towersContainer == null)
            towersContainer = transform.Find("Pnl_ModalWindow/Scroll_Towers/Viewport/Content");

        InitializeTowersDatabase();
    }

    /// <summary>
    /// Inicjalizacja bazy wież w edytorze lub runtime.
    /// </summary>
    public void InitializeTowersDatabase()
    {
        if (allTowersDatabase == null)
            allTowersDatabase = new List<TowerData>();

#if UNITY_EDITOR
        if (allTowersDatabase.Count == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TowerData");
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var tower = UnityEditor.AssetDatabase.LoadAssetAtPath<TowerData>(path);
                if (tower != null && !allTowersDatabase.Contains(tower))
                {
                    allTowersDatabase.Add(tower);
                }
            }
            allTowersDatabase.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        }
#endif

        if (allTowersDatabase.Count == 0)
        {
            var loaded = Resources.FindObjectsOfTypeAll<TowerData>();
            if (loaded != null && loaded.Length > 0)
            {
                foreach (var t in loaded)
                {
                    if (t != null && !allTowersDatabase.Contains(t))
                        allTowersDatabase.Add(t);
                }
                allTowersDatabase.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            }
        }
    }

    /// <summary>
    /// Pełna inicjalizacja wież na samym początku gry (tworzenie i wstępne wypełnienie kart wież).
    /// </summary>
    public void InitializeTowers()
    {
        AutoBindReferences();
        InitializeTowersDatabase();
        RefreshTowersList();
    }

    public void Setup(RuneForgeEntity forge)
    {
        Open(forge);
    }

    public void Open(RuneForgeEntity forge)
    {
        AutoBindReferences();
        currentForge = forge;
        gameObject.SetActive(true);

        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(Close);
        }

        RefreshTowersList();
    }

    public void Close()
    {
        gameObject.SetActive(false);

        if (currentForge != null && currentForge.infusedTowerData == null)
        {
            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.DeselectAll();
            }
        }
    }

    public void RefreshTowersList()
    {
        if (towersContainer == null) return;

        // 1. Sprawdź, które wieże są już przypisane do istniejących kuźni na mapie
        HashSet<TowerData> occupiedTowers = new HashSet<TowerData>();
        var allForges = Object.FindObjectsByType<RuneForgeEntity>(FindObjectsSortMode.None);
        foreach (var forge in allForges)
        {
            if (forge != null && forge != currentForge && forge.infusedTowerData != null)
            {
                occupiedTowers.Add(forge.infusedTowerData);
            }
        }

        // 2. Filtrujemy tylko wolne wieże
        List<TowerData> freeTowers = new List<TowerData>();
        foreach (var tower in allTowersDatabase)
        {
            if (tower != null && !occupiedTowers.Contains(tower))
            {
                freeTowers.Add(tower);
            }
        }

        // 3. Renderuj karty wież
        int activeCards = 0;
        for (int i = 0; i < freeTowers.Count; i++)
        {
            var tower = freeTowers[i];
            Transform cardTr;

            if (i < towersContainer.childCount)
            {
                cardTr = towersContainer.GetChild(i);
                cardTr.gameObject.SetActive(true);
            }
            else
            {
                if (towersContainer.childCount > 0)
                {
                    cardTr = Instantiate(towersContainer.GetChild(0), towersContainer, false);
                    cardTr.gameObject.SetActive(true);
                }
                else break;
            }

            // Skonfiguruj zawartość karty
            SetupCard(cardTr, tower);
            activeCards++;
        }

        // Ukryj nadmiarowe karty
        for (int i = activeCards; i < towersContainer.childCount; i++)
        {
            towersContainer.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void SetupCard(Transform cardTr, TowerData tower)
    {
        if (cardTr == null || tower == null) return;

        // Ikona
        Image iconImg = cardTr.Find("Pnl_IconFrame/Img_TowerIcon")?.GetComponent<Image>();
        if (iconImg != null)
        {
            iconImg.sprite = tower.icon;
            iconImg.color = tower.icon != null ? Color.white : new Color(0.96f, 0.78f, 0.28f, 0.8f);
        }

        // Nazwa
        TextMeshProUGUI nameTmp = cardTr.Find("Txt_TowerName")?.GetComponent<TextMeshProUGUI>();
        if (nameTmp != null)
        {
            nameTmp.text = tower.buildingName.ToUpper();
        }

        // Kategoria / Podtytuł
        TextMeshProUGUI catTmp = cardTr.Find("Txt_Category")?.GetComponent<TextMeshProUGUI>();
        if (catTmp != null)
        {
            catTmp.text = "WIEŻA OBRONNA";
        }

        // Opis
        TextMeshProUGUI descTmp = cardTr.Find("Txt_Description")?.GetComponent<TextMeshProUGUI>();
        if (descTmp != null)
        {
            descTmp.text = string.IsNullOrEmpty(tower.description) ? "Ciężka konstrukcja obronna twierdzy." : tower.description;
        }

        // Przycisk wyboru
        Button selectBtn = cardTr.Find("Btn_SelectTower")?.GetComponent<Button>();
        if (selectBtn != null)
        {
            selectBtn.onClick.RemoveAllListeners();
            selectBtn.onClick.AddListener(() => OnTowerSelected(tower));

            TextMeshProUGUI btnTxt = selectBtn.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
            if (btnTxt != null)
            {
                btnTxt.text = "WYBIERZ WIEŻĘ";
            }
        }
    }

    private void OnTowerSelected(TowerData selectedTower)
    {
        if (currentForge == null || selectedTower == null) return;

        currentForge.SetInfusedTower(selectedTower);
        gameObject.SetActive(false);

        if (CenterInfoPanelControllerUI.Instance != null)
        {
            CenterInfoPanelControllerUI.Instance.OpenRuneForge(currentForge);
        }
    }
}
