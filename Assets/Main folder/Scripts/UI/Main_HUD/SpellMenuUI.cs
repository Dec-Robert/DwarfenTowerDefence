using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Zarządza widocznością i pozycją panelu Pnl_SpellMenu na HUD-zie:
/// - całkowicie ukryty i wyłączony (CanvasGroup + X = -220), gdy gracz nie ma aktywnego zaklęcia,
/// - po zdobyciu pierwszego zaklęcia płynnie wjeżdża z -220 na pozycję docelową,
/// - w dzień (Morning / Dusk) schowany na X = -190, a po najechaniu kursorem wyjeżdża na X = -96,
/// - w nocy (Night) na stałe wysunięty na X = -96.
/// </summary>
public class SpellMenuUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Pozycje X panelu")]
    [SerializeField] private float noSpellHiddenX = -220f;
    [SerializeField] private float dayHiddenX = -190f;
    [SerializeField] private float visibleX = -96f;
    [SerializeField] private float slideSpeed = 12f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private bool isHovered;
    private bool hasSpell;
    private bool subscribedToSpellManager;
    private bool subscribedToTimeManager;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        RefreshState(snapPosition: true);
    }

    private void Start()
    {
        TrySubscribe();
        RefreshState(snapPosition: true);
    }

    private void OnDisable()
    {
        Unsubscribe();
        isHovered = false;
    }

    private void Update()
    {
        if (!subscribedToSpellManager || !subscribedToTimeManager)
        {
            if (TrySubscribe())
            {
                RefreshState(snapPosition: true);
            }
        }

        if (!hasSpell || rectTransform == null) return;

        float targetX = GetTargetX();
        Vector2 pos = rectTransform.anchoredPosition;
        if (!Mathf.Approximately(pos.x, targetX))
        {
            pos.x = Mathf.MoveTowards(pos.x, targetX, Mathf.Abs(visibleX - dayHiddenX) * slideSpeed * Time.unscaledDeltaTime);
            rectTransform.anchoredPosition = pos;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hasSpell) return;
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    public void RefreshState(bool snapPosition = false)
    {
        var mgr = SpellCastManager.Instance;
        hasSpell = mgr != null
            && mgr.ActiveTower != null
            && mgr.ActiveTower.State == DruidTowerState.Active
            && mgr.ActiveTower.ActiveSpell != null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = hasSpell ? 1f : 0f;
            canvasGroup.interactable = hasSpell;
            canvasGroup.blocksRaycasts = hasSpell;
        }

        if (!hasSpell)
        {
            isHovered = false;
            SetAnchoredX(noSpellHiddenX);
            return;
        }

        if (snapPosition)
        {
            SetAnchoredX(GetTargetX());
        }
    }

    private float GetTargetX()
    {
        bool isNight = TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night;
        return (isNight || isHovered) ? visibleX : dayHiddenX;
    }

    private void SetAnchoredX(float x)
    {
        if (rectTransform == null) return;
        Vector2 pos = rectTransform.anchoredPosition;
        pos.x = x;
        rectTransform.anchoredPosition = pos;
    }

    private bool TrySubscribe()
    {
        bool newlySubscribed = false;

        if (!subscribedToSpellManager && SpellCastManager.Instance != null)
        {
            SpellCastManager.Instance.OnSpellStateChanged += HandleSpellStateChanged;
            subscribedToSpellManager = true;
            newlySubscribed = true;
        }

        if (!subscribedToTimeManager && TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandlePhaseChanged;
            TimePhaseManager.Instance.OnDuskStarted += HandlePhaseChanged;
            TimePhaseManager.Instance.OnNightStarted += HandlePhaseChanged;
            subscribedToTimeManager = true;
            newlySubscribed = true;
        }

        return newlySubscribed;
    }

    private void Unsubscribe()
    {
        if (subscribedToSpellManager && SpellCastManager.Instance != null)
        {
            SpellCastManager.Instance.OnSpellStateChanged -= HandleSpellStateChanged;
        }
        subscribedToSpellManager = false;

        if (subscribedToTimeManager && TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandlePhaseChanged;
            TimePhaseManager.Instance.OnDuskStarted -= HandlePhaseChanged;
            TimePhaseManager.Instance.OnNightStarted -= HandlePhaseChanged;
        }
        subscribedToTimeManager = false;
    }

    private void HandleSpellStateChanged()
    {
        RefreshState(snapPosition: false);
    }

    private void HandlePhaseChanged()
    {
        RefreshState(snapPosition: false);
    }
}
