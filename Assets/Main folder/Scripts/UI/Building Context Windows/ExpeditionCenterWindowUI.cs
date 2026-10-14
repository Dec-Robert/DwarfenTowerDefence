using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler bocznego okna kontekstowego Centrum Ekspedycji (Obozu Zwiadowców).
/// Odpowiada za:
/// - Wyświetlanie globalnych statystyk wyposażenia (centra, sloty, wolny sprzęt),
/// - Wizualną listę kapsułek slotów ekwipunku (Pnl_ExpeditionEq_Slot),
/// - Listę trwających wypraw (Pnl_Expedition_Mission) z przyciskiem NAMIERZ (centrowanie kamery na chunk),
/// - Podświetlenie misji, gdy gracz kliknie chunk z trwającą ekspedycją.
/// </summary>
public class ExpeditionCenterWindowUI : MonoBehaviour
{
    [Header("Nagłówek i Ogólne")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private Button btnClose;

    [Header("Górny Baner Statystyk")]
    [SerializeField] private TextMeshProUGUI txtBuildingsValue;
    [SerializeField] private TextMeshProUGUI txtEquipmentValue;
    [SerializeField] private TextMeshProUGUI txtAvailableValue;

    [Header("Stan Ekwipunków")]
    [SerializeField] private TextMeshProUGUI txtUsageSummary;
    [SerializeField] private Transform pnlSlotsRow;
    [SerializeField] private GameObject slotBadgePrefab;

    [Header("Lista Aktywnych Wypraw")]
    [SerializeField] private Transform pnlMissionsList;
    [SerializeField] private GameObject missionCardPrefab;

    // Paleta kolorów
    private static readonly Color ColorGreenAvailable = new Color(0.180f, 0.800f, 0.443f, 1.0f); // #2ECC71
    private static readonly Color ColorGrayInUse     = new Color(0.240f, 0.240f, 0.260f, 1.0f); // #3D3D42
    private static readonly Color ColorHighlightGold = new Color(1.000f, 0.750f, 0.000f, 1.0f); // #FFBF00

    private ExpeditionCenterEntity currentCenter;
    private Coroutine highlightCoroutine;

    private void Awake()
    {
        AutoBindReferences();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            highlightCoroutine = null;
        }
    }

    private void SubscribeEvents()
    {
        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnEquipmentChanged += HandleEquipmentChanged;
            MapExpansionManager.Instance.OnMissionStarted += HandleMissionUpdated;
            MapExpansionManager.Instance.OnMissionCompleted += HandleMissionUpdated;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningTick;
        }
    }

    private void UnsubscribeEvents()
    {
        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnEquipmentChanged -= HandleEquipmentChanged;
            MapExpansionManager.Instance.OnMissionStarted -= HandleMissionUpdated;
            MapExpansionManager.Instance.OnMissionCompleted -= HandleMissionUpdated;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningTick;
        }
    }

    private void HandleEquipmentChanged(int available, int total)
    {
        RefreshUI();
    }

    private void HandleMissionUpdated(ScoutingMission mission)
    {
        RefreshUI();
    }

    private void HandleMorningTick()
    {
        RefreshUI();
    }

    // =========================================================================
    // AUTO-BINDING REFERENCJI
    // =========================================================================

    public void AutoBindReferences()
    {
        // 1. Nagłówek
        Transform topBar = transform.Find("Pnl_TopBar") ?? transform.Find("Pnl_Header");
        if (topBar != null)
        {
            if (txtTitle == null) txtTitle = topBar.Find("Txt_Title")?.GetComponent<TextMeshProUGUI>();
            if (btnClose == null) btnClose = topBar.Find("Btn_Close")?.GetComponent<Button>();
        }

        // 2. Baner statystyk
        Transform statsBanner = transform.Find("Pnl_TopStatsBanner") ?? transform.Find("Pnl_StatsBanner");
        if (statsBanner != null)
        {
            if (txtBuildingsValue == null) txtBuildingsValue = statsBanner.Find("Col_Buildings/Txt_Value")?.GetComponent<TextMeshProUGUI>();
            if (txtEquipmentValue == null) txtEquipmentValue = statsBanner.Find("Col_Equipment/Txt_Value")?.GetComponent<TextMeshProUGUI>();
            if (txtAvailableValue == null) txtAvailableValue = statsBanner.Find("Col_Available/Txt_Value")?.GetComponent<TextMeshProUGUI>();
        }

        // 3. Stan ekwipunków
        Transform secGear = transform.Find("Sec_EquipmentStatus");
        if (secGear != null)
        {
            Transform gearCard = secGear.Find("Pnl_GearCard");
            if (gearCard != null)
            {
                if (txtUsageSummary == null)
                    txtUsageSummary = gearCard.Find("Pnl_GearSubheader/Txt_UsageSummary")?.GetComponent<TextMeshProUGUI>();

                if (pnlSlotsRow == null)
                    pnlSlotsRow = gearCard.Find("Pnl_SlotsRow");
            }
        }

        // 4. Lista aktywnych wypraw
        Transform secMissions = transform.Find("Sec_ActiveMissions");
        if (secMissions != null)
        {
            if (pnlMissionsList == null)
                pnlMissionsList = secMissions.Find("Pnl_MissionsList");
        }

        // Prefabs fallback z bazy zasobów
#if UNITY_EDITOR
        if (slotBadgePrefab == null)
        {
            slotBadgePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/UI/Pnl_ExpeditionEq_Slot.prefab");
            if (slotBadgePrefab == null)
                slotBadgePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/UI/Pnl_ExpeditionSlotBadge.prefab");
        }

        if (missionCardPrefab == null)
        {
            missionCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/UI/Pnl_Expedition_Mission.prefab");
            if (missionCardPrefab == null)
                missionCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/UI/Card_ExpeditionMission.prefab");
        }
#endif
    }

    // =========================================================================
    // KONFIGURACJA I OTWARCIE
    // =========================================================================

    public void Setup(ExpeditionCenterEntity center = null, Vector2Int? highlightChunk = null)
    {
        AutoBindReferences();
        currentCenter = center;

        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(CloseWindow);
        }

        RefreshUI();

        if (highlightChunk.HasValue)
        {
            HighlightMission(highlightChunk.Value);
        }
    }

    public void CloseWindow()
    {
        gameObject.SetActive(false);
    }

    // =========================================================================
    // ODŚWIEŻANIE WIDOKU
    // =========================================================================

    public void RefreshUI()
    {
        if (MapExpansionManager.Instance == null) return;

        int totalEquipment = MapExpansionManager.Instance.TotalEquipment;
        int usedEquipment = MapExpansionManager.Instance.UsedEquipment;
        int availableEquipment = MapExpansionManager.Instance.AvailableEquipment;

        // 1. Górny baner statystyk
        // Liczba centrów ekspedycyjnych
        int centersCount = FindObjectsByType<ExpeditionCenterEntity>(FindObjectsSortMode.None).Length;
        if (txtBuildingsValue != null)
            txtBuildingsValue.text = centersCount > 0 ? $"{centersCount}" : "0";

        if (txtEquipmentValue != null)
            txtEquipmentValue.text = $"{totalEquipment} slotów";

        if (txtAvailableValue != null)
            txtAvailableValue.text = $"{availableEquipment} wolne";

        // 2. Karta zużycia ekwipunków
        if (txtUsageSummary != null)
        {
            txtUsageSummary.text = $"{usedEquipment} / {totalEquipment} W użyciu";
        }

        // Rząd slotów (kapsułki 1..N)
        RebuildSlotBadges(totalEquipment, usedEquipment);

        // 3. Lista misji w toku
        RebuildMissionsList();
    }

    private void RebuildSlotBadges(int total, int used)
    {
        if (pnlSlotsRow == null) return;

        // Usuń istniejące sloty
        for (int i = pnlSlotsRow.childCount - 1; i >= 0; i--)
        {
            Destroy(pnlSlotsRow.GetChild(i).gameObject);
        }

        if (slotBadgePrefab == null || total <= 0) return;

        // Tworzymy kapsułki dla każdego slotu
        for (int i = 1; i <= total; i++)
        {
            GameObject slotObj = Instantiate(slotBadgePrefab, pnlSlotsRow);
            slotObj.name = $"Slot_{i}";

            // Indeks slotu
            TextMeshProUGUI txtIndex = slotObj.transform.Find("Txt_Index")?.GetComponent<TextMeshProUGUI>()
                ?? slotObj.GetComponentInChildren<TextMeshProUGUI>();
            if (txtIndex != null)
            {
                txtIndex.text = i.ToString();
            }

            // Pasek statusu
            Image bar = slotObj.transform.Find("Img_SlotBar")?.GetComponent<Image>();
            if (bar != null)
            {
                bool isBusy = i <= used;
                bar.color = isBusy ? ColorGrayInUse : ColorGreenAvailable;
            }
        }
    }

    private void RebuildMissionsList()
    {
        if (pnlMissionsList == null) return;

        // Wyczyść stare karty
        for (int i = pnlMissionsList.childCount - 1; i >= 0; i--)
        {
            Destroy(pnlMissionsList.GetChild(i).gameObject);
        }

        if (missionCardPrefab == null || MapExpansionManager.Instance == null) return;

        var missions = MapExpansionManager.Instance.ActiveMissions;
        if (missions == null || missions.Count == 0) return;

        foreach (var mission in missions)
        {
            GameObject cardObj = Instantiate(missionCardPrefab, pnlMissionsList);
            cardObj.name = $"Mission_{mission.targetChunk.x}_{mission.targetChunk.y}";

            // Tytuł
            TextMeshProUGUI txtTitle = cardObj.transform.Find("Pnl_TextInfo/Txt_Title")?.GetComponent<TextMeshProUGUI>()
                ?? cardObj.transform.Find("Txt_Title")?.GetComponent<TextMeshProUGUI>();
            if (txtTitle != null)
            {
                string typePrefix = mission.missionType == ScoutingMissionType.Recon ? "Zwiad taktyczny" : "Ekspedycja";
                txtTitle.text = $"{typePrefix}: Chunk [{mission.targetChunk.x}, {mission.targetChunk.y}]";
            }

            // Podtytuł (Pozostało X cykli, Y elfów)
            TextMeshProUGUI txtSub = cardObj.transform.Find("Pnl_TextInfo/Txt_Subtitle")?.GetComponent<TextMeshProUGUI>()
                ?? cardObj.transform.Find("Txt_Subtitle")?.GetComponent<TextMeshProUGUI>();
            if (txtSub != null)
            {
                string cycleWord = mission.daysRemaining == 1 ? "cykl" : "cykle";
                txtSub.text = $"Pozostało: {mission.daysRemaining} {cycleWord} ({mission.assignedElves.Count} elfów)";
            }

            // Przycisk NAMIERZ (Btn_Focus)
            Button btnFocus = cardObj.transform.Find("Btn_Focus")?.GetComponent<Button>()
                ?? cardObj.transform.Find("Btn_Track")?.GetComponent<Button>();
            if (btnFocus != null)
            {
                Vector2Int target = mission.targetChunk;
                btnFocus.onClick.RemoveAllListeners();
                btnFocus.onClick.AddListener(() => FocusCameraOnChunk(target));
            }
        }
    }

    private void FocusCameraOnChunk(Vector2Int targetChunk)
    {
        Vector3 worldPos = GetChunkWorldPosition(targetChunk);
        if (CameraController.Instance != null)
        {
            CameraController.Instance.FocusOnPoint(worldPos);
        }
    }

    public static Vector3 GetChunkWorldPosition(Vector2Int coord)
    {
        if (HexMapVisualizer.Instance != null)
        {
            GameObject chunkObj = HexMapVisualizer.Instance.GetChunkGameObject(coord);
            if (chunkObj != null) return chunkObj.transform.position;
        }

        // Fallback obliczeniowy
        int radius = HexMapGenerator.Instance != null ? HexMapGenerator.Instance.chunkRadius : 3;
        float size = HexMapGenerator.Instance != null ? HexMapGenerator.Instance.hexSize : 1f;
        float pad = HexMapGenerator.Instance != null ? HexMapGenerator.Instance.padding : 0f;
        return HexGridMath.GetChunkCenterWorld(coord, radius, size, pad);
    }

    // =========================================================================
    // PODŚWIETLENIE MISJI (Gdy kliknięto chunk z aktywną misją)
    // =========================================================================

    public void HighlightMission(Vector2Int targetChunk)
    {
        if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
        highlightCoroutine = StartCoroutine(AnimateHighlightMission(targetChunk));
    }

    private IEnumerator AnimateHighlightMission(Vector2Int targetChunk)
    {
        yield return null; // Odczekaj klatkę na wygenerowanie listy
        if (pnlMissionsList == null) yield break;

        string targetName = $"Mission_{targetChunk.x}_{targetChunk.y}";
        Transform targetCard = pnlMissionsList.Find(targetName);
        if (targetCard == null) yield break;

        Outline outline = targetCard.GetComponent<Outline>();
        if (outline == null) outline = targetCard.gameObject.AddComponent<Outline>();

        Color initialColor = outline.effectColor;

        for (int pulse = 0; pulse < 3; pulse++)
        {
            float elapsed = 0f;
            float dur = 0.25f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                outline.effectColor = Color.Lerp(initialColor, ColorHighlightGold, elapsed / dur);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                outline.effectColor = Color.Lerp(ColorHighlightGold, initialColor, elapsed / dur);
                yield return null;
            }
        }

        outline.effectColor = initialColor;
        highlightCoroutine = null;
    }
}
