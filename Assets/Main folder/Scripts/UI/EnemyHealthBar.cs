using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("Referencje")]
    public EnemyStats stats;
    public StyleSheet styleSheet; // Przypisz HealthBar.uss

    [Header("Ustawienia")]
    public Vector3 offset = new Vector3(0, 2.5f, 0); // Wysokoœæ nad g³ow¹ wroga

    // Elementy UI
    private VisualElement healthBarContainer;
    private List<VisualElement> fillElements = new List<VisualElement>();
    private VisualElement parentLayer; // Warstwa w g³ównym HUD

    // Cache
    private Camera mainCam;
    private float hpPerSegment;
    private float cachedMaxHp = -1;
    private string colorClass;

    private void Start()
    {
        mainCam = Camera.main;

        // 1. ZnajdŸ g³ówny HUD
        if (UIManager.Instance == null || UIManager.Instance.uiDocument == null)
        {
            // Debug.LogWarning("Brak UIManagera lub UIDocument! Pasek zdrowia nie powstanie.");
            return;
        }

        var root = UIManager.Instance.uiDocument.rootVisualElement;
        parentLayer = root.Q("HealthBarLayer");

        if (parentLayer == null)
        {
            Debug.LogError("Brak kontenera 'HealthBarLayer' w GameHUD.uxml!");
            return;
        }

        // 2. Podepnij eventy
        if (stats != null)
        {
            stats.OnHealthChanged += UpdateHealthBar;
            // Wymuœ pierwsze odœwie¿enie (jeœli wróg ju¿ ma HP)
            UpdateHealthBar(stats.GetMaxHealth(), stats.GetMaxHealth());
            // ^ U¿yj gettera lub publicznego pola, tu zak³adam ¿e startuje z max
        }
    }

    private void OnDestroy()
    {
        if (stats != null) stats.OnHealthChanged -= UpdateHealthBar;

        // WA¯NE: Usuñ pasek z ekranu gdy wróg ginie!
        if (healthBarContainer != null && healthBarContainer.parent != null)
        {
            healthBarContainer.parent.Remove(healthBarContainer);
        }
    }

    private void LateUpdate()
    {
        if (healthBarContainer == null) return;

        // 3. Pozycjonowanie (Follow)
        Vector3 worldPos = transform.position + offset;
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

        // Jeœli wróg jest za kamer¹ -> ukryj
        if (screenPos.z < 0)
        {
            healthBarContainer.style.display = DisplayStyle.None;
            return;
        }

        healthBarContainer.style.display = DisplayStyle.Flex;
        healthBarContainer.style.visibility = Visibility.Visible;

        // Przeliczenie wspó³rzêdnych (UI Toolkit ma (0,0) w lewym górnym rogu)
        // Musimy te¿ odj¹æ po³owê szerokoœci paska, ¿eby by³ wyœrodkowany
        float panelHeight = parentLayer.layout.height; // Wysokoœæ ekranu w jednostkach UI
        if (float.IsNaN(panelHeight)) panelHeight = Screen.height; // Fallback

        // Centrowanie: (Szerokoœæ paska z USS to 60px)
        float centeredX = screenPos.x - 30;
        float invertedY = Screen.height - screenPos.y;

        // Dostosowanie do skali Panel Settings (RuntimePanelUtils to pomocne narzêdzie)
        // Ale przy prostym setupie Overlay to wystarczy:
        healthBarContainer.style.left = centeredX;
        healthBarContainer.style.top = invertedY;
    }

    void UpdateHealthBar(float currentHp, float maxHp)
    {
        if (parentLayer == null) return;

        // Budowa paska (tylko raz lub przy zmianie MaxHP)
        if (healthBarContainer == null || IsMaxHpChanged(maxHp))
        {
            RebuildBar(maxHp);
        }

        // Aktualizacja wype³nienia
        float remainingHp = currentHp;
        for (int i = 0; i < fillElements.Count; i++)
        {
            VisualElement fill = fillElements[i];
            if (remainingHp >= hpPerSegment)
            {
                fill.style.width = Length.Percent(100);
                remainingHp -= hpPerSegment;
            }
            else if (remainingHp > 0)
            {
                float percent = (remainingHp / hpPerSegment) * 100f;
                fill.style.width = Length.Percent(percent);
                remainingHp = 0;
            }
            else
            {
                fill.style.width = Length.Percent(0);
            }
        }
    }

    void RebuildBar(float maxHp)
    {
        // Usuñ stary jeœli istnieje (reset)
        if (healthBarContainer != null) healthBarContainer.parent.Remove(healthBarContainer);

        cachedMaxHp = maxHp;

        // Logika kolorów
        if (maxHp < 500) { hpPerSegment = 100; colorClass = "color-red"; }
        else if (maxHp < 1250) { hpPerSegment = 250; colorClass = "color-blue"; }
        else if (maxHp < 5000) { hpPerSegment = 1000; colorClass = "color-green"; }
        else if (maxHp < 25000) { hpPerSegment = 5000; colorClass = "color-yellow"; }
        else { hpPerSegment = maxHp / 5f; colorClass = "color-purple"; }

        int segments = Mathf.CeilToInt(maxHp / hpPerSegment);
        if (segments > 5) segments = 5;
        if (segments < 1) segments = 1;

        // Tworzenie kontenera
        healthBarContainer = new VisualElement();
        healthBarContainer.AddToClassList("bar-container");

        // Dodanie stylów (jeœli asset jest przypisany)
        if (styleSheet != null) healthBarContainer.styleSheets.Add(styleSheet);

        // Dodanie do g³ównego HUD
        parentLayer.Add(healthBarContainer);
        fillElements.Clear();

        // Tworzenie segmentów
        for (int i = 0; i < segments; i++)
        {
            VisualElement seg = new VisualElement();
            seg.AddToClassList("segment");

            VisualElement fill = new VisualElement();
            fill.AddToClassList("segment-fill");
            fill.AddToClassList(colorClass);

            seg.Add(fill);
            healthBarContainer.Add(seg);
            fillElements.Add(fill);
        }
    }

    bool IsMaxHpChanged(float max) => Mathf.Abs(cachedMaxHp - max) > 1f;
}