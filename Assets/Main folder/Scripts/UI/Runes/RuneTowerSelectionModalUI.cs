using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler modalu wyboru wieży dla Kuźni Runicznej (Pnl_RuneTowerSelectionModal).
/// - Wyświetla wieże obronne nieprzypisane jeszcze do żadnej innej Kuźni na mapie.
/// - Korzysta ze zaktualizowanego layoutu kart (Card_BuildingChoice.prefab).
/// - Obsługuje okno dialogowe z potwierdzeniem wyboru (Pnl_ConfirmWarningDialog).
/// </summary>
public class RuneTowerSelectionModalUI : MonoBehaviour
{
    [Header("Główne Referencje")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private Button btnClose;
    [SerializeField] private Transform towersContainer;

    [Header("Okno Potwierdzenia")]
    [SerializeField] private GameObject pnlConfirmWarningDialog;
    [SerializeField] private TextMeshProUGUI txtWarningTitle;
    [SerializeField] private TextMeshProUGUI txtWarningMessage;
    [SerializeField] private Button btnConfirmChoice;
    [SerializeField] private Button btnCancelChoice;

    [Header("Baza Dostępnych Wież")]
    [SerializeField] private List<TowerData> allTowersDatabase = new List<TowerData>();

    private RuneForgeEntity currentForge;
    private TowerData pendingSelectedTower;

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
            txtTitle = transform.Find("Pnl_ModalWindow/Pnl_ModalTopBar/Txt_Title")?.GetComponent<TextMeshProUGUI>()
                    ?? transform.Find("Pnl_ModalTopBar/Txt_Title")?.GetComponent<TextMeshProUGUI>();

        if (btnClose == null)
            btnClose = transform.Find("Pnl_ModalWindow/Pnl_ModalTopBar/Btn_Close")?.GetComponent<Button>()
                    ?? transform.Find("Pnl_ModalTopBar/Btn_Close")?.GetComponent<Button>();

        if (towersContainer == null)
            towersContainer = transform.Find("Pnl_ModalWindow/Scroll_Towers/Viewport/Content")
                           ?? transform.Find("Scroll_Towers/Viewport/Content");

        // Okno potwierdzenia
        if (pnlConfirmWarningDialog == null)
        {
            var dialogTr = transform.Find("Pnl_ConfirmWarningDialog");
            if (dialogTr != null) pnlConfirmWarningDialog = dialogTr.gameObject;
        }

        if (pnlConfirmWarningDialog != null)
        {
            if (txtWarningTitle == null)
                txtWarningTitle = pnlConfirmWarningDialog.transform.Find("Pnl_DialogBox/Txt_WarningTitle")?.GetComponent<TextMeshProUGUI>();

            if (txtWarningMessage == null)
                txtWarningMessage = pnlConfirmWarningDialog.transform.Find("Pnl_DialogBox/Txt_WarningMessage")?.GetComponent<TextMeshProUGUI>();

            if (btnConfirmChoice == null)
                btnConfirmChoice = pnlConfirmWarningDialog.transform.Find("Pnl_DialogBox/Pnl_ButtonRow/Btn_ConfirmChoice")?.GetComponent<Button>();

            if (btnCancelChoice == null)
                btnCancelChoice = pnlConfirmWarningDialog.transform.Find("Pnl_DialogBox/Pnl_ButtonRow/Btn_CancelChoice")?.GetComponent<Button>();
        }

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
        pendingSelectedTower = null;

        if (pnlConfirmWarningDialog != null)
        {
            pnlConfirmWarningDialog.SetActive(false);
        }

        gameObject.SetActive(true);

        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(Close);
        }

        if (btnConfirmChoice != null)
        {
            btnConfirmChoice.onClick.RemoveAllListeners();
            btnConfirmChoice.onClick.AddListener(OnConfirmSelection);
        }

        if (btnCancelChoice != null)
        {
            btnCancelChoice.onClick.RemoveAllListeners();
            btnCancelChoice.onClick.AddListener(OnCancelSelection);
        }

        RefreshTowersList();
    }

    public void Close()
    {
        if (pnlConfirmWarningDialog != null)
        {
            pnlConfirmWarningDialog.SetActive(false);
        }

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

        // Ikona wieży (obsługuje Card_BuildingChoice: Pnl_IconFrame/Img_BuildingIcon lub fallback Pnl_IconFrame/Img_TowerIcon)
        Image iconImg = cardTr.Find("Pnl_IconFrame/Img_BuildingIcon")?.GetComponent<Image>()
                     ?? cardTr.Find("Pnl_IconFrame/Img_TowerIcon")?.GetComponent<Image>()
                     ?? cardTr.Find("Img_TowerIcon")?.GetComponent<Image>();

        if (iconImg != null)
        {
            iconImg.sprite = tower.icon;
            iconImg.color = tower.icon != null ? Color.white : new Color(0.96f, 0.78f, 0.28f, 0.8f);
        }

        // Nazwa wieży
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

        // Przycisk wyboru (obsługuje Btn_SelectBuilding lub Btn_SelectTower)
        Button selectBtn = cardTr.Find("Btn_SelectBuilding")?.GetComponent<Button>()
                        ?? cardTr.Find("Btn_SelectTower")?.GetComponent<Button>();

        if (selectBtn != null)
        {
            selectBtn.onClick.RemoveAllListeners();
            selectBtn.onClick.AddListener(() => OnTowerCardClicked(tower));

            TextMeshProUGUI btnTxt = selectBtn.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
            if (btnTxt != null)
            {
                btnTxt.text = "WYBIERZ";
            }
        }
    }

    private void OnTowerCardClicked(TowerData tower)
    {
        if (tower == null) return;
        pendingSelectedTower = tower;

        if (pnlConfirmWarningDialog != null)
        {
            if (txtWarningMessage != null)
            {
                txtWarningMessage.text = $"Czy na pewno chcesz poświęcić tę Kuźnię dla: <b><color=#FFD700>{tower.buildingName.ToUpper()}</color></b>?\n\n" +
                                         $"Wszystkie osadzone tu runy będą nasycać wyłącznie wybraną konstrukcję w całej twierdzy. " +
                                         $"<b>Tej decyzji nie można cofnąć!</b>";
            }
            pnlConfirmWarningDialog.SetActive(true);
        }
        else
        {
            // Jeśli okno dialogowe nie jest obecne na scenie, zatwierdź natychmiast
            OnConfirmSelection();
        }
    }

    private void OnConfirmSelection()
    {
        if (currentForge == null || pendingSelectedTower == null) return;

        currentForge.SetInfusedTower(pendingSelectedTower);

        if (pnlConfirmWarningDialog != null)
        {
            pnlConfirmWarningDialog.SetActive(false);
        }

        gameObject.SetActive(false);

        if (CenterInfoPanelControllerUI.Instance != null)
        {
            CenterInfoPanelControllerUI.Instance.OpenRuneForge(currentForge);
        }
    }

    private void OnCancelSelection()
    {
        pendingSelectedTower = null;
        if (pnlConfirmWarningDialog != null)
        {
            pnlConfirmWarningDialog.SetActive(false);
        }
    }
}
