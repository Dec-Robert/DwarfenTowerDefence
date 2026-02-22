#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Edytorskie okno do wizualizacji i edycji grafu zależności Meta Upgradów.
///
/// ── OTWIERANIE ───────────────────────────────────────────────────────────────
///   Menu: Tools → Meta Upgrade Graph
///
/// ── OBSŁUGA ──────────────────────────────────────────────────────────────────
///   • Przeciągaj węzły (LPM na węźle)
///   • Scroll = zoom
///   • PPM na tle = pan
///   • Kliknij węzeł = zaznacz i edytuj w panelu bocznym
///   • Kliknij "Auto Layout" = automatyczne rozmieszczenie wg zależności
///   • Kliknij "Odśwież" = załaduj upgrady z wybranego MetaUpgradeManager
/// </summary>
public class MetaUpgradeEditorWindow : EditorWindow
{
    // =========================================================================
    // STAŁE WIZUALNE
    // =========================================================================

    private const float NODE_WIDTH    = 180f;
    private const float NODE_HEIGHT   = 70f;
    private const float NODE_SPACING_X = 220f;
    private const float NODE_SPACING_Y = 100f;
    private const float SIDE_PANEL_WIDTH = 280f;
    private const float TOOLBAR_HEIGHT   = 30f;

    // Kolory węzłów
    private static readonly Color COLOR_UNLOCKED    = new Color(0.2f, 0.6f, 0.2f, 1f);
    private static readonly Color COLOR_LOCKED      = new Color(0.35f, 0.35f, 0.35f, 1f);
    private static readonly Color COLOR_AVAILABLE   = new Color(0.2f, 0.4f, 0.6f, 1f);
    private static readonly Color COLOR_SELECTED    = new Color(0.8f, 0.7f, 0.1f, 1f);
    private static readonly Color COLOR_NONE_EFFECT = new Color(0.25f, 0.25f, 0.25f, 1f);

    // Kolory krawędzi — jasne żeby były widoczne na ciemnym tle
    // MET  = spełnione wymaganie → złota linia
    // UNMET = niespełnione → czerwona linia
    private static readonly Color COLOR_EDGE        = new Color(0.95f, 0.85f, 0.3f,  1f);
    private static readonly Color COLOR_EDGE_UNMET  = new Color(0.95f, 0.25f, 0.25f, 1f);

    // Grubości linii
    private const float EDGE_THICKNESS_MET   = 3.0f;
    private const float EDGE_THICKNESS_UNMET = 2.0f;

    // =========================================================================
    // STAN
    // =========================================================================

    // Węzły grafu
    private class UpgradeNode
    {
        public MetaUpgradeSO upgrade;
        public Rect           rect;
        public bool           isDragging;
    }

    private readonly List<UpgradeNode>           nodes    = new List<UpgradeNode>();
    private readonly Dictionary<string, UpgradeNode> nodeMap  = new Dictionary<string, UpgradeNode>();

    // Nawigacja
    private Vector2 panOffset     = Vector2.zero;
    private float   zoomScale     = 1f;
    private Vector2 lastMousePos;
    private bool    isPanning;

    // Selekcja i edycja
    private UpgradeNode selectedNode;
    private Vector2     sidePanelScroll;

    // Źródło danych
    private MetaUpgradeManager targetManager;
    private List<MetaUpgradeSO> upgrades => targetManager != null
        ? targetManager.allUpgrades
        : null;

    // Drag
    private UpgradeNode draggingNode;

    // Pozycja w przestrzeni grafu gdzie kliknięto PPM (do spawnu nowego węzła)
    private Vector2 contextMenuGraphPos;

    // =========================================================================
    // MENU
    // =========================================================================

    [MenuItem("Tools/Meta Upgrade Graph")]
    public static void OpenWindow()
    {
        var window = GetWindow<MetaUpgradeEditorWindow>("Meta Upgrade Graph");
        window.minSize = new Vector2(800, 500);
        window.Show();
    }

    // =========================================================================
    // UNITY EDITOR CALLBACKS
    // =========================================================================

    private void OnEnable()
    {
        // Spróbuj automatycznie znaleźć manager na scenie
        targetManager = FindObjectOfType<MetaUpgradeManager>();
        if (targetManager != null) RefreshNodes();
    }

    private void OnGUI()
    {
        // Układ: toolbar | [graf] | panel boczny
        DrawToolbar();

        Rect graphRect = new Rect(
            0,
            TOOLBAR_HEIGHT,
            position.width - (selectedNode != null ? SIDE_PANEL_WIDTH : 0),
            position.height - TOOLBAR_HEIGHT);

        Rect sideRect = new Rect(
            graphRect.xMax,
            TOOLBAR_HEIGHT,
            SIDE_PANEL_WIDTH,
            position.height - TOOLBAR_HEIGHT);

        // Graf
        GUI.BeginGroup(graphRect);
        DrawGraph(graphRect);
        GUI.EndGroup();

        // Panel boczny (tylko gdy zaznaczony węzeł)
        if (selectedNode != null)
        {
            GUI.BeginGroup(sideRect);
            DrawSidePanel(new Rect(0, 0, SIDE_PANEL_WIDTH, sideRect.height));
            GUI.EndGroup();
        }

        // Obsługa inputu
        HandleInput(graphRect);

        // ObjectPicker — obsługa "Dodaj istniejący"
        if (waitingForObjectPicker)
        {
            if (Event.current.commandName == "ObjectSelectorUpdated" ||
                Event.current.commandName == "ObjectSelectorClosed")
            {
                var picked = EditorGUIUtility.GetObjectPickerObject() as MetaUpgradeSO;
                if (picked != null &&
                    Event.current.commandName == "ObjectSelectorClosed")
                {
                    // Sprawdź czy już nie ma w liście
                    if (targetManager.allUpgrades == null)
                        targetManager.allUpgrades = new System.Collections.Generic.List<MetaUpgradeSO>();

                    if (!targetManager.allUpgrades.Contains(picked))
                    {
                        Undo.RecordObject(targetManager, "Dodaj istniejący MetaUpgrade");
                        targetManager.allUpgrades.Add(picked);
                        EditorUtility.SetDirty(targetManager);

                        var node = new UpgradeNode
                        {
                            upgrade = picked,
                            rect    = new Rect(contextMenuGraphPos.x, contextMenuGraphPos.y,
                                               NODE_WIDTH, NODE_HEIGHT)
                        };
                        nodes.Add(node);
                        if (!string.IsNullOrEmpty(picked.id))
                            nodeMap[picked.id] = node;

                        selectedNode = node;
                        EditorGUIUtility.PingObject(picked);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Już dodany",
                            $"'{picked.upgradeName}' jest już na liście Managera.", "OK");
                    }

                    waitingForObjectPicker = false;
                    Repaint();
                }
                else if (Event.current.commandName == "ObjectSelectorClosed")
                {
                    waitingForObjectPicker = false;
                }
            }
        }

        // Repaint podczas przeciągania
        if (draggingNode != null || isPanning)
            Repaint();
    }

    // =========================================================================
    // TOOLBAR
    // =========================================================================

    private void DrawToolbar()
    {
        Rect toolbarRect = new Rect(0, 0, position.width, TOOLBAR_HEIGHT);
        GUI.Box(toolbarRect, GUIContent.none, EditorStyles.toolbar);

        float x = 4f;

        // Manager picker
        GUI.Label(new Rect(x, 4, 130, 22), "MetaUpgradeManager:", EditorStyles.label);
        x += 134f;

        var newManager = (MetaUpgradeManager)EditorGUI.ObjectField(
            new Rect(x, 4, 200, 22),
            targetManager, typeof(MetaUpgradeManager), true);
        x += 206f;

        if (newManager != targetManager)
        {
            targetManager = newManager;
            RefreshNodes();
        }

        // Przycisk odśwież
        if (GUI.Button(new Rect(x, 2, 70, 26), "Odśwież", EditorStyles.toolbarButton))
        {
            RefreshNodes();
        }
        x += 74f;

        // Przycisk auto-layout
        if (GUI.Button(new Rect(x, 2, 90, 26), "Auto Layout", EditorStyles.toolbarButton))
        {
            AutoLayout();
        }
        x += 94f;

        // Zoom reset
        if (GUI.Button(new Rect(x, 2, 70, 26), "Reset View", EditorStyles.toolbarButton))
        {
            panOffset = Vector2.zero;
            zoomScale = 1f;
            Repaint();
        }
        x += 74f;

        // Zoom info
        GUI.Label(new Rect(x, 4, 80, 22),
            $"Zoom: {zoomScale:F2}x", EditorStyles.miniLabel);

        // Legenda
        float lx = position.width - 380f;
        DrawLegendItem(ref lx, COLOR_UNLOCKED,  "Odblokowany");
        DrawLegendItem(ref lx, COLOR_AVAILABLE, "Dostępny");
        DrawLegendItem(ref lx, COLOR_LOCKED,    "Zablokowany");
    }

    private void DrawLegendItem(ref float x, Color color, string label)
    {
        EditorGUI.DrawRect(new Rect(x, 8, 14, 14), color);
        x += 18f;
        GUI.Label(new Rect(x, 4, 80, 22), label, EditorStyles.miniLabel);
        x += 84f;
    }

    // =========================================================================
    // GRAF
    // =========================================================================

    private void DrawGraph(Rect graphRect)
    {
        // Tło
        EditorGUI.DrawRect(new Rect(0, 0, graphRect.width, graphRect.height),
            new Color(0.15f, 0.15f, 0.15f, 1f));

        // Siatka
        DrawGrid(graphRect);

        if (upgrades == null || upgrades.Count == 0)
        {
            GUI.Label(
                new Rect(graphRect.width * 0.5f - 150, graphRect.height * 0.5f - 20, 300, 40),
                "Brak upgradów. Wybierz MetaUpgradeManager i kliknij 'Odśwież'.",
                EditorStyles.centeredGreyMiniLabel);
            return;
        }

        // Rysuj krawędzie (zależności)
        DrawEdges();

        // Rysuj węzły
        DrawNodes();
    }

    private void DrawGrid(Rect graphRect)
    {
        float gridSize = 40f * zoomScale;
        float offsetX  = panOffset.x % gridSize;
        float offsetY  = panOffset.y % gridSize;

        Handles.color = new Color(0.22f, 0.22f, 0.22f, 1f);

        for (float x = offsetX; x < graphRect.width; x += gridSize)
            Handles.DrawLine(new Vector3(x, 0), new Vector3(x, graphRect.height));
        for (float y = offsetY; y < graphRect.height; y += gridSize)
            Handles.DrawLine(new Vector3(0, y), new Vector3(graphRect.width, y));
    }

    private void DrawEdges()
    {
        foreach (var node in nodes)
        {
            if (node.upgrade.prerequisites == null) continue;

            foreach (var prereq in node.upgrade.prerequisites)
            {
                if (prereq == null || !nodeMap.ContainsKey(prereq.id)) continue;

                var fromNode = nodeMap[prereq.id];
                var toNode   = node;

                Vector2 from = GraphToScreen(new Vector2(
                    fromNode.rect.x + fromNode.rect.width,
                    fromNode.rect.y + fromNode.rect.height * 0.5f));

                Vector2 to = GraphToScreen(new Vector2(
                    toNode.rect.x,
                    toNode.rect.y + toNode.rect.height * 0.5f));

                bool met = prereq.isUnlocked;
                Color edgeColor = met ? COLOR_EDGE : COLOR_EDGE_UNMET;

                // Bezierowa krzywa
                Vector2 ctrl1 = from + new Vector2(50f * zoomScale, 0);
                Vector2 ctrl2 = to   - new Vector2(50f * zoomScale, 0);

                Handles.DrawBezier(
                    new Vector3(from.x, from.y, 0),
                    new Vector3(to.x,   to.y,   0),
                    new Vector3(ctrl1.x, ctrl1.y, 0),
                    new Vector3(ctrl2.x, ctrl2.y, 0),
                    edgeColor, null, met ? EDGE_THICKNESS_MET : EDGE_THICKNESS_UNMET);

                // Strzałka
                DrawArrow(to, (to - ctrl2).normalized, edgeColor);
            }
        }
    }

    private void DrawArrow(Vector2 tip, Vector2 dir, Color color)
    {
        float size = 8f * zoomScale;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector3 a = new Vector3(tip.x, tip.y, 0);
        Vector3 b = new Vector3(tip.x - dir.x * size + perp.x * size * 0.5f,
                                tip.y - dir.y * size + perp.y * size * 0.5f, 0);
        Vector3 c = new Vector3(tip.x - dir.x * size - perp.x * size * 0.5f,
                                tip.y - dir.y * size - perp.y * size * 0.5f, 0);

        Handles.color = color;
        Handles.DrawAAConvexPolygon(a, b, c);
    }

    private void DrawNodes()
    {
        foreach (var node in nodes)
        {
            Rect screenRect = GraphRectToScreen(node.rect);

            // Kolor węzła
            Color bgColor = GetNodeColor(node);
            if (node == selectedNode) bgColor = Color.Lerp(bgColor, COLOR_SELECTED, 0.6f);

            // Tło węzła
            EditorGUI.DrawRect(screenRect, bgColor);

            // Ramka
            Color borderColor = node == selectedNode
                ? COLOR_SELECTED
                : new Color(0f, 0f, 0f, 0.5f);
            DrawBorder(screenRect, borderColor, 2f);

            // Treść
            float fontSize = Mathf.Clamp(zoomScale * 11f, 7f, 14f);
            var nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize    = (int)fontSize,
                normal      = { textColor = Color.white },
                wordWrap    = true,
                alignment   = TextAnchor.UpperCenter
            };
            var effectStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize    = (int)(fontSize * 0.85f),
                normal      = { textColor = new Color(0.9f, 0.9f, 0.7f) },
                alignment   = TextAnchor.LowerCenter,
                wordWrap    = true
            };

            // Nazwa
            Rect nameRect = new Rect(
                screenRect.x + 4, screenRect.y + 4,
                screenRect.width - 8, screenRect.height * 0.55f);
            GUI.Label(nameRect, node.upgrade.upgradeName, nameStyle);

            // Efekt
            if (zoomScale > 0.5f)
            {
                Rect effectRect = new Rect(
                    screenRect.x + 4, screenRect.y + screenRect.height * 0.55f,
                    screenRect.width - 8, screenRect.height * 0.4f);
                string effectText = node.upgrade.effectType == MetaEffectType.None
                    ? "—"
                    : FormatEffect(node.upgrade);
                GUI.Label(effectRect, effectText, effectStyle);
            }

            // Koszt
            if (zoomScale > 0.65f)
            {
                var costStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize  = (int)(fontSize * 0.75f),
                    normal    = { textColor = new Color(1f, 0.85f, 0.2f) },
                    alignment = TextAnchor.LowerRight
                };
                GUI.Label(
                    new Rect(screenRect.x, screenRect.yMax - 16, screenRect.width - 4, 16),
                    $"⬡ {node.upgrade.cost}", costStyle);
            }
        }
    }

    private Color GetNodeColor(UpgradeNode node)
    {
        if (node.upgrade.effectType == MetaEffectType.None) return COLOR_NONE_EFFECT;
        if (node.upgrade.isUnlocked)                        return COLOR_UNLOCKED;
        if (node.upgrade.ArePrerequisitesMet())             return COLOR_AVAILABLE;
        return COLOR_LOCKED;
    }

    private void DrawBorder(Rect rect, Color color, float thickness)
    {
        Handles.color = color;
        Handles.DrawSolidRectangleWithOutline(
            new Rect(rect.x, rect.y, rect.width, rect.height),
            Color.clear, color);
    }

    // =========================================================================
    // PANEL BOCZNY
    // =========================================================================

    private void DrawSidePanel(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f, 1f));
        DrawBorder(rect, new Color(0.4f, 0.4f, 0.4f), 1f);

        GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16));
        sidePanelScroll = GUILayout.BeginScrollView(sidePanelScroll);

        if (selectedNode == null) { GUILayout.EndScrollView(); GUILayout.EndArea(); return; }

        var upgrade = selectedNode.upgrade;

        // Nagłówek
        GUILayout.Label("Edycja Upgradu", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();

        // Podstawowe pola
        upgrade.id          = EditorGUILayout.TextField("ID", upgrade.id);
        upgrade.upgradeName = EditorGUILayout.TextField("Nazwa", upgrade.upgradeName);

        EditorGUILayout.Space(4);
        GUILayout.Label("Opis:", EditorStyles.miniLabel);
        upgrade.description = EditorGUILayout.TextArea(upgrade.description,
            GUILayout.MinHeight(50));

        EditorGUILayout.Space(4);
        upgrade.cost = EditorGUILayout.IntField("Koszt (Artefakty)", upgrade.cost);
        upgrade.icon = (Sprite)EditorGUILayout.ObjectField(
            "Ikona", upgrade.icon, typeof(Sprite), false);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("─── Efekt ───────────────────", EditorStyles.miniLabel);

        upgrade.effectType = (MetaEffectType)EditorGUILayout.EnumPopup(
            "Typ Efektu", upgrade.effectType);

        if (upgrade.effectType != MetaEffectType.None)
        {
            upgrade.effectValue = EditorGUILayout.FloatField(
                GetEffectValueLabel(upgrade.effectType), upgrade.effectValue);

            if (upgrade.effectType == MetaEffectType.EliteChanceBoost)
                upgrade.effectValue2 = EditorGUILayout.FloatField(
                    "Od Fali (effectValue2)", upgrade.effectValue2);

            if (upgrade.RequiresTargetBuilding)
                upgrade.targetBuilding = (BuildingData)EditorGUILayout.ObjectField(
                    "Budynek Docelowy", upgrade.targetBuilding, typeof(BuildingData), false);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("─── Stan ────────────────────", EditorStyles.miniLabel);
        upgrade.isUnlocked = EditorGUILayout.Toggle("Odblokowany", upgrade.isUnlocked);

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            upgrade.isUnlocked ? "✓ Kupiony" :
            upgrade.ArePrerequisitesMet() ? "○ Dostępny do kupna" : "✗ Zablokowany (brak wymagań)",
            upgrade.isUnlocked ? MessageType.Info : MessageType.None);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("─── Wymagania ───────────────", EditorStyles.miniLabel);

        // Lista prerequisites
        if (upgrade.prerequisites == null)
            upgrade.prerequisites = new System.Collections.Generic.List<MetaUpgradeSO>();

        for (int i = 0; i < upgrade.prerequisites.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            upgrade.prerequisites[i] = (MetaUpgradeSO)EditorGUILayout.ObjectField(
                $"Wymaga [{i}]", upgrade.prerequisites[i], typeof(MetaUpgradeSO), false);
            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                upgrade.prerequisites.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Dodaj Wymaganie"))
            upgrade.prerequisites.Add(null);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(upgrade);
            Repaint();
        }

        EditorGUILayout.Space(12);

        // Podgląd efektu
        if (upgrade.effectType != MetaEffectType.None)
        {
            EditorGUILayout.HelpBox(
                $"Efekt: {FormatEffect(upgrade)}", MessageType.None);
        }

        // Zaznacz w Project window
        EditorGUILayout.Space(8);
        if (GUILayout.Button("Pokaż w Project"))
            EditorGUIUtility.PingObject(upgrade);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // =========================================================================
    // INPUT
    // =========================================================================

    private void HandleInput(Rect graphRect)
    {
        Event e = Event.current;
        if (e == null) return;

        // Zoom (scroll)
        if (e.type == EventType.ScrollWheel && graphRect.Contains(e.mousePosition + new Vector2(0, TOOLBAR_HEIGHT)))
        {
            float delta = -e.delta.y * 0.05f;
            zoomScale = Mathf.Clamp(zoomScale + delta, 0.2f, 2.5f);
            e.Use();
            Repaint();
            return;
        }

        // Klik
        if (e.type == EventType.MouseDown)
        {
            Vector2 mouseInGraph = e.mousePosition - new Vector2(0, TOOLBAR_HEIGHT);

            if (e.button == 0) // LPM
            {
                // Szukaj węzła pod kursorem
                UpgradeNode clicked = GetNodeAtScreen(mouseInGraph);

                if (clicked != null)
                {
                    selectedNode  = clicked;
                    draggingNode  = clicked;
                    draggingNode.isDragging = true;
                    GUI.FocusControl(null);
                    e.Use();
                }
                else
                {
                    selectedNode = null;
                    e.Use();
                }
            }
            else if (e.button == 1) // PPM
            {
                mouseInGraph = e.mousePosition - new Vector2(0, TOOLBAR_HEIGHT);
                UpgradeNode nodeUnderMouse = GetNodeAtScreen(mouseInGraph);

                if (nodeUnderMouse != null)
                {
                    // PPM na węźle → menu kontekstowe węzła
                    ShowNodeContextMenu(nodeUnderMouse, e.mousePosition);
                }
                else
                {
                    // PPM na tle → menu tworzenia / dodawania
                    // Zapamiętaj pozycję w przestrzeni grafu (do spawnu węzła w tym miejscu)
                    contextMenuGraphPos = ScreenToGraph(mouseInGraph);
                    ShowBackgroundContextMenu();
                }
                e.Use();
            }
            else if (e.button == 2) // środkowy = pan
            {
                isPanning    = true;
                lastMousePos = e.mousePosition;
                e.Use();
            }
        }

        if (e.type == EventType.MouseDrag)
        {
            if (draggingNode != null && draggingNode.isDragging)
            {
                draggingNode.rect.x += e.delta.x / zoomScale;
                draggingNode.rect.y += e.delta.y / zoomScale;
                e.Use();
                Repaint();
            }
            else if (isPanning)
            {
                panOffset += e.mousePosition - lastMousePos;
                lastMousePos = e.mousePosition;
                e.Use();
                Repaint();
            }
        }

        if (e.type == EventType.MouseUp)
        {
            if (draggingNode != null)
            {
                draggingNode.isDragging = false;
                draggingNode = null;
            }
            isPanning = false;
        }
    }

    // =========================================================================
    // MENU KONTEKSTOWE
    // =========================================================================

    /// <summary>
    /// Menu PPM na pustym tle grafu — tworzenie lub dodawanie upgradu.
    /// </summary>
    private void ShowBackgroundContextMenu()
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("Utwórz nowy upgrade"), false, CreateNewUpgrade);
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Dodaj istniejący..."), false, AddExistingUpgrade);

        if (targetManager == null)
        {
            menu.AddSeparator("");
            menu.AddDisabledItem(new GUIContent("⚠ Brak MetaUpgradeManager na scenie"));
        }

        menu.ShowAsContext();
    }

    /// <summary>
    /// Menu PPM na węźle — operacje na konkretnym węźle.
    /// </summary>
    private void ShowNodeContextMenu(UpgradeNode node, Vector2 mousePos)
    {
        selectedNode = node;
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("Pokaż w Project"), false,
            () => EditorGUIUtility.PingObject(node.upgrade));

        menu.AddItem(new GUIContent("Zaznacz w Inspector"), false,
            () => Selection.activeObject = node.upgrade);

        menu.AddSeparator("");

        string lockLabel = node.upgrade.isUnlocked ? "Oznacz jako zablokowany" : "Oznacz jako odblokowany";
        menu.AddItem(new GUIContent(lockLabel), false, () =>
        {
            Undo.RecordObject(node.upgrade, lockLabel);
            node.upgrade.isUnlocked = !node.upgrade.isUnlocked;
            EditorUtility.SetDirty(node.upgrade);
            Repaint();
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Usuń z listy Managera"), false,
            () => RemoveNodeFromManager(node));

        menu.AddItem(new GUIContent("Usuń węzeł i plik SO"), false,
            () => DeleteNodeAndAsset(node));

        menu.ShowAsContext();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Akcje menu tła
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Otwiera okienko z polem nazwy, a po zatwierdzeniu tworzy MetaUpgradeSO
    /// z tą nazwą, ustawiając automatycznie id i upgradeName.
    /// </summary>
    private void CreateNewUpgrade()
    {
        if (targetManager == null)
        {
            EditorUtility.DisplayDialog("Brak Managera",
                "Dodaj MetaUpgradeManager do sceny przed tworzeniem upgradów.", "OK");
            return;
        }

        // Otwórz dialog z polem nazwy — przekaż callback który faktycznie tworzy asset
        NewUpgradeNameDialog.Show(enteredName =>
        {
            CreateNewUpgradeWithName(enteredName);
        });
    }

    /// <summary>
    /// Faktyczne tworzenie assetu po podaniu nazwy przez użytkownika.
    /// </summary>
    private void CreateNewUpgradeWithName(string displayName)
    {
        const string SCAN_PATH = "Assets/Main folder/ScriptableObjects/Meta";

        // Upewnij się że folder istnieje
        if (!AssetDatabase.IsValidFolder(SCAN_PATH))
        {
            string parent = "Assets/Main folder/ScriptableObjects";
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EditorUtility.DisplayDialog("Brak Folderu",
                    $"Folder nie istnieje:\n{SCAN_PATH}\n\nUtwórz go ręcznie w Project window.", "OK");
                return;
            }
            AssetDatabase.CreateFolder(parent, "Meta");
        }

        // Sanityzuj nazwę pliku (usuń niedozwolone znaki)
        string sanitized = string.IsNullOrWhiteSpace(displayName) ? "NewMetaUpgrade" : displayName.Trim();
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(c.ToString(), "");
        if (string.IsNullOrEmpty(sanitized)) sanitized = "NewMetaUpgrade";

        // Zadbaj o unikalność nazwy pliku
        string fileName = sanitized;
        int    suffix   = 1;
        while (AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>($"{SCAN_PATH}/{fileName}.asset") != null)
        {
            fileName = $"{sanitized}_{suffix}";
            suffix++;
        }

        // Utwórz asset — id i upgradeName wyprowadzone z wpisanej nazwy
        var newUpgrade = CreateInstance<MetaUpgradeSO>();
        newUpgrade.upgradeName = displayName.Trim();
        newUpgrade.id          = MetaUpgradeTools.ToSnakeCasePublic(fileName);
        newUpgrade.cost        = 100;
        newUpgrade.effectType  = MetaEffectType.None;

        string assetPath = $"{SCAN_PATH}/{fileName}.asset";
        AssetDatabase.CreateAsset(newUpgrade, assetPath);
        AssetDatabase.SaveAssets();

        // Dodaj do managera
        Undo.RecordObject(targetManager, "Dodaj nowy MetaUpgrade");
        if (targetManager.allUpgrades == null)
            targetManager.allUpgrades = new System.Collections.Generic.List<MetaUpgradeSO>();
        targetManager.allUpgrades.Add(newUpgrade);
        EditorUtility.SetDirty(targetManager);

        // Dodaj węzeł do grafu w miejscu kliknięcia
        var node = new UpgradeNode
        {
            upgrade = newUpgrade,
            rect    = new Rect(contextMenuGraphPos.x, contextMenuGraphPos.y, NODE_WIDTH, NODE_HEIGHT)
        };
        nodes.Add(node);
        if (!string.IsNullOrEmpty(newUpgrade.id))
            nodeMap[newUpgrade.id] = node;

        // Zaznacz nowy węzeł i otwórz panel edycji
        selectedNode = node;

        EditorGUIUtility.PingObject(newUpgrade);
        Selection.activeObject = newUpgrade;

        Debug.Log($"[MetaUpgradeGraph] Utworzono nowy upgrade: {assetPath}  (id: {newUpgrade.id})");
        Repaint();
    }

    /// <summary>
    /// Otwiera okno wyboru assetu — pozwala dodać istniejący MetaUpgradeSO
    /// który nie jest jeszcze w liście managera.
    /// </summary>
    private void AddExistingUpgrade()
    {
        if (targetManager == null)
        {
            EditorUtility.DisplayDialog("Brak Managera",
                "Dodaj MetaUpgradeManager do sceny przed dodawaniem upgradów.", "OK");
            return;
        }

        // Otwórz standardowe okno wyboru assetu Unity
        // Callback: AddExistingUpgradeCallback
        EditorGUIUtility.ShowObjectPicker<MetaUpgradeSO>(
            null, false, "", controlID: 12345);

        // Obsługa callbacku w OnGUI przez nasłuchiwanie na ObjectSelectorClosed
        waitingForObjectPicker = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Akcje menu węzła
    // ─────────────────────────────────────────────────────────────────────────

    private void RemoveNodeFromManager(UpgradeNode node)
    {
        if (targetManager == null) return;

        bool confirm = EditorUtility.DisplayDialog(
            "Usuń z listy",
            $"Usunąć '{node.upgrade.upgradeName}' z listy MetaUpgradeManager?\n\nPlik SO pozostanie na dysku.",
            "Usuń", "Anuluj");

        if (!confirm) return;

        Undo.RecordObject(targetManager, "Usuń MetaUpgrade z Managera");
        targetManager.allUpgrades?.Remove(node.upgrade);
        EditorUtility.SetDirty(targetManager);

        nodes.Remove(node);
        if (!string.IsNullOrEmpty(node.upgrade.id))
            nodeMap.Remove(node.upgrade.id);

        if (selectedNode == node) selectedNode = null;
        Repaint();
    }

    private void DeleteNodeAndAsset(UpgradeNode node)
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Usuń plik SO",
            $"TRWALE usunąć '{node.upgrade.upgradeName}'?\n\n" +
            $"Plik zostanie usunięty z dysku i z listy Managera.\n" +
            "Tej operacji nie można cofnąć.",
            "Usuń trwale", "Anuluj");

        if (!confirm) return;

        // Usuń z managera
        if (targetManager != null)
        {
            Undo.RecordObject(targetManager, "Usuń MetaUpgrade");
            targetManager.allUpgrades?.Remove(node.upgrade);
            EditorUtility.SetDirty(targetManager);
        }

        // Usuń z grafu
        nodes.Remove(node);
        if (!string.IsNullOrEmpty(node.upgrade.id))
            nodeMap.Remove(node.upgrade.id);
        if (selectedNode == node) selectedNode = null;

        // Usuń asset z dysku
        string path = AssetDatabase.GetAssetPath(node.upgrade);
        if (!string.IsNullOrEmpty(path))
            AssetDatabase.DeleteAsset(path);

        Repaint();
    }

    // =========================================================================
    // OBJECT PICKER — obsługa "Dodaj istniejący"
    // =========================================================================

    private bool waitingForObjectPicker = false;

    // =========================================================================
    // DANE I LAYOUT
    // =========================================================================

    private void RefreshNodes()
    {
        nodes.Clear();
        nodeMap.Clear();
        selectedNode = null;

        if (upgrades == null) return;

        foreach (var upgrade in upgrades)
        {
            if (upgrade == null) continue;
            var node = new UpgradeNode
            {
                upgrade = upgrade,
                rect    = new Rect(0, 0, NODE_WIDTH, NODE_HEIGHT)
            };
            nodes.Add(node);
            if (!string.IsNullOrEmpty(upgrade.id))
                nodeMap[upgrade.id] = node;
        }

        AutoLayout();
    }

    /// <summary>
    /// Automatyczny layout: BFS od korzeni (brak prerequisites).
    /// Każdy poziom zależności → osobna kolumna.
    /// </summary>
    private void AutoLayout()
    {
        if (nodes.Count == 0) return;

        // Przypisz poziomy (columns) przez BFS
        var levels = new Dictionary<UpgradeNode, int>();
        var queue  = new Queue<UpgradeNode>();

        // Korzenie = brak prerequisites lub wszystkie null
        foreach (var node in nodes)
        {
            bool isRoot = node.upgrade.prerequisites == null
                || node.upgrade.prerequisites.All(p => p == null);
            if (isRoot)
            {
                levels[node] = 0;
                queue.Enqueue(node);
            }
        }

        // Jeśli brak korzeni — wszytkie na poziomie 0
        if (queue.Count == 0)
        {
            foreach (var node in nodes) { levels[node] = 0; queue.Enqueue(node); }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int nextLevel = levels[current] + 1;

            foreach (var other in nodes)
            {
                if (other.upgrade.prerequisites == null) continue;
                if (!other.upgrade.prerequisites.Contains(current.upgrade)) continue;
                if (levels.TryGetValue(other, out int existing) && existing >= nextLevel) continue;

                levels[other] = nextLevel;
                queue.Enqueue(other);
            }
        }

        // Pozostałe węzły (bez poziomu)
        foreach (var node in nodes)
            if (!levels.ContainsKey(node)) levels[node] = 0;

        // Grupuj po poziomach i rozmieść
        var byLevel = nodes.GroupBy(n => levels.TryGetValue(n, out int l) ? l : 0)
                           .OrderBy(g => g.Key)
                           .ToList();

        float startX = 20f;
        float startY = 20f;

        foreach (var group in byLevel)
        {
            float x = startX + group.Key * NODE_SPACING_X;
            float y = startY;
            int   row = 0;

            foreach (var node in group.OrderBy(n => n.upgrade.upgradeName))
            {
                node.rect = new Rect(x, y + row * NODE_SPACING_Y, NODE_WIDTH, NODE_HEIGHT);
                row++;
            }
        }

        panOffset = Vector2.zero;
        Repaint();
    }

    // =========================================================================
    // HELPERS — TRANSFORMACJE
    // =========================================================================

    private Vector2 GraphToScreen(Vector2 graphPos)
    {
        return graphPos * zoomScale + panOffset;
    }

    /// <summary>Odwrotność GraphToScreen — pozycja ekranowa → przestrzeń grafu.</summary>
    private Vector2 ScreenToGraph(Vector2 screenPos)
    {
        return (screenPos - panOffset) / zoomScale;
    }

    private Rect GraphRectToScreen(Rect graphRect)
    {
        return new Rect(
            graphRect.x * zoomScale + panOffset.x,
            graphRect.y * zoomScale + panOffset.y,
            graphRect.width  * zoomScale,
            graphRect.height * zoomScale);
    }

    private UpgradeNode GetNodeAtScreen(Vector2 screenPos)
    {
        // Odwrócona iteracja = wierzchni węzeł priorytet
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            Rect screenRect = GraphRectToScreen(nodes[i].rect);
            if (screenRect.Contains(screenPos))
                return nodes[i];
        }
        return null;
    }

    // =========================================================================
    // HELPERS — FORMATOWANIE
    // =========================================================================

    private string FormatEffect(MetaUpgradeSO upgrade)
    {
        float v = upgrade.effectValue;
        return upgrade.effectType switch
        {
            MetaEffectType.StartingGold         => $"+{v} złota",
            MetaEffectType.StartingWood         => $"+{v} drewna",
            MetaEffectType.StartingStone        => $"+{v} kamienia",
            MetaEffectType.StartingIron         => $"+{v} żelaza",
            MetaEffectType.StartingCoal         => $"+{v} węgla",
            MetaEffectType.StartingFood         => $"+{v} jedzenia",
            MetaEffectType.BonusShifts          => $"+{v} zmian",
            MetaEffectType.BonusWorkersPerShift => $"+{v} prac./zmianę",
            MetaEffectType.BuildingPassiveEfficiency       => $"+{v*100:F0}% pasywnie",
            MetaEffectType.BuildingWorkerEfficiencyBonus   => $"+{v*100:F0}% za prac.",
            MetaEffectType.BuildingTerrainBonusMultiplier  => $"x{v} bonus terenu",
            MetaEffectType.SpecificBuildingUpgradeCostReduction => $"-{v*100:F0}% koszt ulepszenia",
            MetaEffectType.HousingStartPopulation          => $"+{v} mieszkańców (start)",
            MetaEffectType.HousingMaxResidents              => $"+{v} max pop (wszyscy)",
            MetaEffectType.HousingMaxResidents_Humans       => $"+{v} max pop (ludzie)",
            MetaEffectType.HousingMaxResidents_Elves        => $"+{v} max pop (elfy)",
            MetaEffectType.HousingMaxResidents_Dwarves      => $"+{v} max pop (krasnoludy)",
            MetaEffectType.EliteChanceBoost                => $"+{v*100:F0}% elit (od f.{upgrade.effectValue2})",
            MetaEffectType.EnemySurvivorPenaltyArmor       => $"-{v*100:F0}% pancerz ocalałych",
            MetaEffectType.EnemySurvivorPenaltySpeed       => $"-{v*100:F0}% prędkość ocalałych",
            MetaEffectType.EnemySurvivorPenaltyDodge       => $"-{v*100:F0}% dodge ocalałych",
            MetaEffectType.TowerNoAmmoNightPenalty         => $"x{v} stats bez amunicji",
            MetaEffectType.ExpansionCostReduction          => $"-{v*100:F0}% koszt ekspansji",
            MetaEffectType.CityBaseHealth                  => $"+{v} HP miasta",
            MetaEffectType.GateBaseHP                      => $"+{v} HP bram",
            MetaEffectType.WallSystem_Solution1            => "Mury: Baza",
            MetaEffectType.WallSystem_Solution2            => "Mury: Linia Frontu",
            MetaEffectType.UniqueChunkUnlock               => "Unikatowy chunk",
            _                                              => upgrade.effectType.ToString()
        };
    }

    private string GetEffectValueLabel(MetaEffectType type) => type switch
    {
        MetaEffectType.StartingGold
            or MetaEffectType.StartingWood
            or MetaEffectType.StartingStone
            or MetaEffectType.StartingIron
            or MetaEffectType.StartingCoal
            or MetaEffectType.StartingFood     => "Ilość",
        MetaEffectType.BonusShifts
            or MetaEffectType.BonusWorkersPerShift
            or MetaEffectType.HousingStartPopulation
            or MetaEffectType.HousingMaxResidents
            or MetaEffectType.HousingMaxResidents_Humans
            or MetaEffectType.HousingMaxResidents_Elves
            or MetaEffectType.HousingMaxResidents_Dwarves
            or MetaEffectType.CityBaseHealth
            or MetaEffectType.GateBaseHP       => "Wartość (całkowita)",
        MetaEffectType.BuildingPassiveEfficiency
            or MetaEffectType.BuildingWorkerEfficiencyBonus
            or MetaEffectType.EliteChanceBoost
            or MetaEffectType.EnemySurvivorPenaltyArmor
            or MetaEffectType.EnemySurvivorPenaltySpeed
            or MetaEffectType.EnemySurvivorPenaltyDodge
            or MetaEffectType.ExpansionCostReduction
            or MetaEffectType.SpecificBuildingUpgradeCostReduction => "Wartość (0.0–1.0 = %)",
        MetaEffectType.TowerNoAmmoNightPenalty
            or MetaEffectType.BuildingTerrainBonusMultiplier => "Mnożnik",
        _                                       => "effectValue"
    };
}

// =============================================================================
// DIALOG — wpisywanie nazwy nowego upgradu
// =============================================================================

/// <summary>
/// Małe modalne okienko EditorWindow z jednym polem tekstowym.
/// Po kliknięciu "Utwórz" wywołuje callback z wpisaną nazwą.
/// Zamknięcie okienka (X) lub "Anuluj" nie wywołuje callbacku.
/// </summary>
public class NewUpgradeNameDialog : EditorWindow
{
    private string enteredName = "";
    private System.Action<string> onConfirm;
    private bool focusSet = false;

    /// <summary>Otwiera dialog. <paramref name="onConfirm"/> wywoływany z wpisaną nazwą po zatwierdzeniu.</summary>
    public static void Show(System.Action<string> onConfirm)
    {
        var dialog = CreateInstance<NewUpgradeNameDialog>();
        dialog.titleContent = new GUIContent("Nowy Meta Upgrade");
        dialog.onConfirm    = onConfirm;
        dialog.minSize      = new Vector2(320, 110);
        dialog.maxSize      = new Vector2(320, 110);
        dialog.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Nazwa nowego upgradu:", EditorStyles.boldLabel);

        // Ustaw focus na polu tekstowym przy pierwszym rysowaniu
        GUI.SetNextControlName("NameField");
        enteredName = EditorGUILayout.TextField(enteredName);

        if (!focusSet)
        {
            EditorGUI.FocusTextInControl("NameField");
            focusSet = true;
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();

        bool confirm = GUILayout.Button("Utwórz", GUILayout.Height(28));
        bool cancel  = GUILayout.Button("Anuluj",  GUILayout.Height(28));

        EditorGUILayout.EndHorizontal();

        // Enter = potwierdź, Escape = anuluj
        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                confirm = true;
            else if (Event.current.keyCode == KeyCode.Escape)
                cancel = true;
        }

        if (confirm && !string.IsNullOrWhiteSpace(enteredName))
        {
            onConfirm?.Invoke(enteredName.Trim());
            Close();
        }
        else if (cancel)
        {
            Close();
        }
    }
}
#endif