using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SpellCastManager : MonoBehaviour
{
    public static SpellCastManager Instance { get; private set; }

    [Header("Klawisz Skrótu")]
    public KeyCode castHotkey = KeyCode.Q;

    public DruidTowerEntity ActiveTower { get; private set; }
    public bool IsTargeting { get; private set; }

    public int RemainingUses { get; private set; }
    public int CurrentCharges { get; private set; }
    public float CooldownTimer { get; private set; }

    public event Action OnSpellStateChanged;

    private GameObject targetingIndicator;
    private Renderer indicatorRenderer;
    private Color? lastIndicatorColor;
    private bool isSubscribedToTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        TrySubscribeTime();
    }

    private void OnEnable()
    {
        TrySubscribeTime();
    }

    private void OnDisable()
    {
        if (isSubscribedToTime && TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningStarted;
            TimePhaseManager.Instance.OnDuskStarted -= HandleNonNightStarted;
            TimePhaseManager.Instance.OnNightStarted -= HandleNightStarted;
            isSubscribedToTime = false;
        }
    }

    private void TrySubscribeTime()
    {
        if (!isSubscribedToTime && TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningStarted;
            TimePhaseManager.Instance.OnDuskStarted += HandleNonNightStarted;
            TimePhaseManager.Instance.OnNightStarted += HandleNightStarted;
            isSubscribedToTime = true;
        }
    }

    public void RegisterDruidTower(DruidTowerEntity tower)
    {
        ActiveTower = tower;
        ResetResourcesForNewDay();
        OnSpellStateChanged?.Invoke();
    }

    public void OnSpellStatsChanged()
    {
        if (ActiveTower == null || ActiveTower.ActiveSpell == null) return;
        int maxVal = ActiveTower.GetEffectiveMaxUsesOrCharges();
        if (ActiveTower.ActiveSpell.castLimitMode == SpellCastLimitMode.RechargeableCharges)
        {
            CurrentCharges = Mathf.Min(CurrentCharges + 1, maxVal);
        }
        OnSpellStateChanged?.Invoke();
    }

    private void HandleMorningStarted()
    {
        CancelTargetingMode();
        ResetResourcesForNewDay();
    }

    private void HandleNonNightStarted()
    {
        CancelTargetingMode();
        OnSpellStateChanged?.Invoke();
    }

    private void HandleNightStarted()
    {
        OnSpellStateChanged?.Invoke();
    }

    public void ResetResourcesForNewDay()
    {
        if (ActiveTower == null || ActiveTower.ActiveSpell == null) return;

        int maxVal = ActiveTower.GetEffectiveMaxUsesOrCharges();
        RemainingUses = maxVal;
        CurrentCharges = maxVal;
        CooldownTimer = 0f;
        OnSpellStateChanged?.Invoke();
    }

    public bool IsNightPhase()
    {
        return TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night;
    }

    public bool CanCastNow()
    {
        if (ActiveTower == null || ActiveTower.State != DruidTowerState.Active || ActiveTower.ActiveSpell == null)
            return false;

        if (!IsNightPhase()) return false;

        return ActiveTower.ActiveSpell.castLimitMode switch
        {
            SpellCastLimitMode.LimitedUsesPerNight => RemainingUses > 0,
            SpellCastLimitMode.RechargeableCharges => CurrentCharges > 0,
            SpellCastLimitMode.CooldownOnly => CooldownTimer <= 0f,
            _ => false
        };
    }

    public float GetCooldownProgress01()
    {
        if (ActiveTower == null || ActiveTower.ActiveSpell == null) return 0f;
        float totalCd = ActiveTower.GetEffectiveCooldown();
        if (totalCd <= 0f) return 0f;
        return Mathf.Clamp01(CooldownTimer / totalCd);
    }

    public void EnterTargetingMode()
    {
        if (!CanCastNow()) return;
        IsTargeting = true;
        EnsureIndicatorVisual();
        if (targetingIndicator != null) targetingIndicator.SetActive(true);
        OnSpellStateChanged?.Invoke();
    }

    public void CancelTargetingMode()
    {
        if (!IsTargeting) return;
        IsTargeting = false;
        if (targetingIndicator != null) targetingIndicator.SetActive(false);
        OnSpellStateChanged?.Invoke();
    }

    public void ToggleTargetingMode()
    {
        if (IsTargeting) CancelTargetingMode();
        else EnterTargetingMode();
    }

    private void Update()
    {
        if (ActiveTower == null || ActiveTower.State != DruidTowerState.Active || ActiveTower.ActiveSpell == null)
            return;

        UpdateCooldowns(Time.deltaTime);

        if (Input.GetKeyDown(castHotkey))
        {
            ToggleTargetingMode();
        }

        if (IsTargeting)
        {
            if (!IsNightPhase())
            {
                CancelTargetingMode();
                return;
            }

            UpdateTargetingIndicatorPosition();

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelTargetingMode();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                if (TryGetMouseWorldPoint(out Vector3 worldPoint))
                {
                    TryCastAt(worldPoint);
                }
            }
        }
    }

    public void UpdateCooldowns(float deltaTime)
    {
        if (ActiveTower == null || ActiveTower.ActiveSpell == null) return;
        if (!IsNightPhase()) return;

        var mode = ActiveTower.ActiveSpell.castLimitMode;
        if (mode == SpellCastLimitMode.CooldownOnly)
        {
            if (CooldownTimer > 0f)
            {
                CooldownTimer = Mathf.Max(0f, CooldownTimer - deltaTime);
                OnSpellStateChanged?.Invoke();
            }
        }
        else if (mode == SpellCastLimitMode.RechargeableCharges)
        {
            int maxCharges = ActiveTower.GetEffectiveMaxUsesOrCharges();
            if (CurrentCharges < maxCharges)
            {
                CooldownTimer -= deltaTime;
                if (CooldownTimer <= 0f)
                {
                    CurrentCharges++;
                    CooldownTimer = CurrentCharges < maxCharges ? ActiveTower.GetEffectiveCooldown() : 0f;
                }
                OnSpellStateChanged?.Invoke();
            }
        }
    }

    public bool TryCastAt(Vector3 worldPoint)
    {
        if (!CanCastNow()) return false;

        SpellEffectExecutor.Execute(ActiveTower, worldPoint);

        var mode = ActiveTower.ActiveSpell.castLimitMode;
        switch (mode)
        {
            case SpellCastLimitMode.LimitedUsesPerNight:
                RemainingUses = Mathf.Max(0, RemainingUses - 1);
                break;
            case SpellCastLimitMode.RechargeableCharges:
                int maxCharges = ActiveTower.GetEffectiveMaxUsesOrCharges();
                bool wasFull = CurrentCharges >= maxCharges;
                CurrentCharges = Mathf.Max(0, CurrentCharges - 1);
                if (wasFull || CooldownTimer <= 0f)
                {
                    CooldownTimer = ActiveTower.GetEffectiveCooldown();
                }
                break;
            case SpellCastLimitMode.CooldownOnly:
                CooldownTimer = ActiveTower.GetEffectiveCooldown();
                break;
        }

        CancelTargetingMode();
        OnSpellStateChanged?.Invoke();
        return true;
    }

    private void EnsureIndicatorVisual()
    {
        if (targetingIndicator != null) return;

        targetingIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        targetingIndicator.name = "SpellTargetingIndicator";
        var col = targetingIndicator.GetComponent<Collider>();
        if (col != null) Destroy(col);

        indicatorRenderer = targetingIndicator.GetComponent<Renderer>();
        if (indicatorRenderer != null)
        {
            indicatorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            indicatorRenderer.receiveShadows = false;
            ApplyTranslucentColor(indicatorRenderer, new Color(1f, 0.4f, 0.1f, 0.35f));
        }
        targetingIndicator.SetActive(false);
    }

    public static void ApplyTranslucentColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (shader != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != shader)
        {
            renderer.material = new Material(shader);
        }

        Material mat = renderer.material;
        if (mat == null) return;

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.color = color;
    }

    private void UpdateTargetingIndicatorPosition()
    {
        if (targetingIndicator == null || ActiveTower == null || ActiveTower.ActiveSpell == null) return;

        float radius = ActiveTower.GetEffectiveRadius();
        targetingIndicator.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);

        if (indicatorRenderer != null)
        {
            Color targetColor = ActiveTower.ActiveSpell.indicatorColor;
            targetColor.a = 0.35f;
            if (!lastIndicatorColor.HasValue || lastIndicatorColor.Value != targetColor)
            {
                lastIndicatorColor = targetColor;
                Material mat = indicatorRenderer.material;
                if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", targetColor);
                    mat.color = targetColor;
                }
            }
        }

        if (TryGetMouseWorldPoint(out Vector3 point))
        {
            targetingIndicator.transform.position = new Vector3(point.x, point.y + 0.1f, point.z);
        }
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            worldPoint = hit.point;
            return true;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        return false;
    }
}
