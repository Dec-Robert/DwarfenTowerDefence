using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("Referencje")]
    public EnemyStats stats;
    public StyleSheet styleSheet; // Przypisz HealthBar.uss

    [Header("Ustawienia")]
    public Vector3 offset = new Vector3(0, 2.5f, 0); // Wysoko�� nad g�ow� wroga

    // Elementy UI
    private VisualElement healthBarContainer;
    private List<VisualElement> fillElements = new List<VisualElement>();
    private VisualElement parentLayer; // Warstwa w g��wnym HUD

    // Cache
    private Camera mainCam;
    private float hpPerSegment;
    private float cachedMaxHp = -1;
    private string colorClass;

    private void Start()
    {
        mainCam = Camera.main;
    //
        // 1. Znajd� g��wny HUD
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
            // Wymu� pierwsze od�wie�enie (je�li wr�g ju� ma HP)
            UpdateHealthBar(stats.GetMaxHealth(), stats.GetMaxHealth());
            // ^ U�yj gettera lub publicznego pola, tu zak�adam �e startuje z max
        }
    }

    private void OnDestroy()
    {
        if (stats != null) stats.OnHealthChanged -= UpdateHealthBar;

        // WA�NE: Usu� pasek z ekranu gdy wr�g ginie!
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

        // Je�li wr�g jest za kamer� -> ukryj
        if (screenPos.z < 0)
        {
            healthBarContainer.style.display = DisplayStyle.None;
            return;
        }

        healthBarContainer.style.display = DisplayStyle.Flex;
        healthBarContainer.style.visibility = Visibility.Visible;

        // Przeliczenie wsp�rz�dnych (UI Toolkit ma (0,0) w lewym g�rnym rogu)
        // Musimy te� odj�� po�ow� szeroko�ci paska, �eby by� wy�rodkowany
        float panelHeight = parentLayer.layout.height; // Wysoko�� ekranu w jednostkach UI
        if (float.IsNaN(panelHeight)) panelHeight = Screen.height; // Fallback

        // Centrowanie: (Szeroko�� paska z USS to 60px)
        float centeredX = screenPos.x - 30;
        float invertedY = Screen.height - screenPos.y;

        // Dostosowanie do skali Panel Settings (RuntimePanelUtils to pomocne narz�dzie)
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

        // Aktualizacja wype�nienia
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
        // Usu� stary je�li istnieje (reset)
        if (healthBarContainer != null) healthBarContainer.parent.Remove(healthBarContainer);

        cachedMaxHp = maxHp;

        // Logika kolor�w
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

        // Dodanie styl�w (je�li asset jest przypisany)
        if (styleSheet != null) healthBarContainer.styleSheets.Add(styleSheet);

        // Dodanie do g��wnego HUD
        parentLayer.Add(healthBarContainer);
        fillElements.Clear();

        // Tworzenie segment�w
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