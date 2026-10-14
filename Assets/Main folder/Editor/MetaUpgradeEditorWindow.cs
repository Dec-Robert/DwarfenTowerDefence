#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Edytorskie okno do wizualizacji i edycji grafu zależności Meta Upgradów.
///
/// ── OTWIERANIE ───────────────────────────────────────────────────────────────
///   Menu: Tools → Meta Upgrade Graph
///
/// ── OBSŁUGA ──────────────────────────────────────────────────────────────────
///   • LPM na węźle = zaznacz i przeciągaj (Shift+LPM = multiselekcja)
///   • LPM na tle i przeciągnij = zaznaczanie ramką (Box Selection)
///   • Przeciągnij z portu (kółko po prawej/lewej stronie węzła) = połącz wymaganie
///   • PPM na porcie = rozłącz wymaganie
///   • Scroll = zoom do kursora myszy
///   • Środkowy przycisk myszy / przeciąganie PPM / Alt+LPM = przesuwanie widoku (Pan)
///   • Kliknięcie PPM (bez przeciągania) = menu kontekstowe (tła lub węzła)
///   • F = wycentruj widok na zaznaczonych (lub wszystkich) węzłach
///   • Ctrl+D = duplikuj węzeł jako kolejny Tier (automatycznie podpina wymaganie)
///   • Delete = usuń zaznaczony węzeł
/// </summary>
public class MetaUpgradeEditorWindow : EditorWindow
{
    // =========================================================================
    // STAŁE WIZUALNE I PREFS
    // =========================================================================

    private const float NODE_WIDTH       = 190f;
    private const float NODE_HEIGHT      = 76f;
    private const float NODE_SPACING_X   = 250f;
    private const float NODE_SPACING_Y   = 110f;
    private const float SIDE_PANEL_WIDTH = 310f;
    private const float TOOLBAR_HEIGHT   = 30f;
    private const float PORT_RADIUS      = 7f;
    private const float SNAP_GRID_SIZE   = 20f;

    private const string PREFS_PREFIX    = "MetaUpgradeGraph_";

    // Kolory węzłów
    private static readonly Color COLOR_UNLOCKED    = new Color(0.20f, 0.58f, 0.24f, 1f);
    private static readonly Color COLOR_LOCKED      = new Color(0.32f, 0.32f, 0.35f, 1f);
    private static readonly Color COLOR_AVAILABLE   = new Color(0.20f, 0.42f, 0.65f, 1f);
    private static readonly Color COLOR_SELECTED    = new Color(0.95f, 0.78f, 0.15f, 1f);
    private static readonly Color COLOR_SEARCH_HIT  = new Color(0.20f, 0.85f, 0.95f, 1f);

    // Kolory krawędzi
    private static readonly Color COLOR_EDGE_MET       = new Color(0.95f, 0.85f, 0.30f, 1f);
    private static readonly Color COLOR_EDGE_UNMET     = new Color(0.90f, 0.28f, 0.28f, 0.9f);
    private static readonly Color COLOR_EDGE_HIGHLIGHT = new Color(0.35f, 0.90f, 1.00f, 1f);
    private static readonly Color COLOR_PORT_NORMAL    = new Color(0.75f, 0.75f, 0.75f, 1f);
    private static readonly Color COLOR_PORT_HOVER     = new Color(1.00f, 0.90f, 0.30f, 1f);
    private static readonly Color COLOR_PORT_VALID     = new Color(0.30f, 0.95f, 0.40f, 1f);
    private static readonly Color COLOR_PORT_INVALID   = new Color(0.95f, 0.25f, 0.25f, 1f);

    // Grubości linii
    private const float EDGE_THICKNESS_MET       = 3.2f;
    private const float EDGE_THICKNESS_UNMET     = 2.2f;
    private const float EDGE_THICKNESS_HIGHLIGHT = 4.2f;

    // =========================================================================
    // STAN
    // =========================================================================

    private class UpgradeNode
    {
        public MetaUpgradeSO upgrade;
        public string        guid;
        public Rect          rect;
    }

    private readonly List<UpgradeNode>                      nodes         = new List<UpgradeNode>();
    private readonly Dictionary<MetaUpgradeSO, UpgradeNode> nodeByUpgrade = new Dictionary<MetaUpgradeSO, UpgradeNode>();

    // Powiązania z MetaMapConfig (ekspansje i modyfikatory terenu)
    private readonly Dictionary<MetaUpgradeSO, List<string>> mapConfigLinks = new Dictionary<MetaUpgradeSO, List<string>>();
    private MetaMapConfig primaryMapConfig;

    // Nawigacja
    private Vector2 panOffset = new Vector2(40f, 40f);
    private float   zoomScale = 1f;
    private bool    snapToGrid = false;
    private string  searchQuery = "";

    // Panning
    private bool    isPanning;
    private bool    isRightMouseDragging;
    private Vector2 rightMouseDownPos;
    private UpgradeNode rightMouseNode;

    // Selekcja i przeciąganie węzłów
    private UpgradeNode                  selectedNode;
    private readonly HashSet<UpgradeNode> selectedNodes = new HashSet<UpgradeNode>();
    private bool                         isDraggingNodes;
    private Vector2                      dragAccumulatedDelta;
    private readonly Dictionary<UpgradeNode, Vector2> dragStartPositions = new Dictionary<UpgradeNode, Vector2>();

    // Box selection (ramka zaznaczania LPM na tle)
    private bool    isBoxSelecting;
    private Vector2 boxSelectStartScreen;
    private Vector2 boxSelectCurrentScreen;

    // Interaktywne łączenie portów
    private bool        isConnecting;
    private UpgradeNode connectionSourceNode;
    private bool        isConnectingFromOutput; // true = z wyjścia (prawa strona), false = z wejścia (lewa strona)

    // Panel boczny
    private Vector2 sidePanelScroll;
    private bool    focusNameFieldNextFrame;

    // Źródło danych / Manager na scenie
    private MetaUpgradeManager targetManager;
    private int                unsyncedManagerCount;

    // Pozycja w przestrzeni grafu dla spawnu nowego węzła
    private Vector2 contextMenuGraphPos = new Vector2(100f, 100f);

    // ObjectPicker ("Dodaj istniejący")
    private bool waitingForObjectPicker;

    // Kolejka akcji wykonywanych bezpiecznie poza środkiem rysowania IMGUI
    private Action pendingSafeAction;

    // Cache stylów GUI (zapobiega alokacjom w każdej klatce OnGUI)
    private GUIStyle cachedNameStyle;
    private GUIStyle cachedEffectStyle;
    private GUIStyle cachedCostStyle;
    private GUIStyle cachedIdBadgeStyle;

    // =========================================================================
    // MENU
    // =========================================================================

    [MenuItem("Tools/Meta Upgrade Graph")]
    public static void OpenWindow()
    {
        var window = GetWindow<MetaUpgradeEditorWindow>("Meta Upgrade Graph");
        window.minSize = new Vector2(900, 540);
        window.Show();
    }

    // =========================================================================
    // UNITY EDITOR CALLBACKS
    // =========================================================================

    private void OnEnable()
    {
        LoadViewPrefs();

        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;

        if (targetManager == null)
            targetManager = FindFirstObjectByType<MetaUpgradeManager>();

        RefreshNodes(preservePositions: true);
    }

    private void OnDisable()
    {
        SaveViewPrefs();
        SaveAllNodePositionsToPrefs();

        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
    }

    private void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
    {
        if (targetManager == null)
            targetManager = FindFirstObjectByType<MetaUpgradeManager>();
        UpdateManagerSyncStatus();
        Repaint();
    }

    private void OnUndoRedoPerformed()
    {
        CleanupDestroyedNodes();
        RebuildLookupDictionaries();
        UpdateManagerSyncStatus();
        Repaint();
    }

    private void OnGUI()
    {
        // 1. Wykonaj odroczone akcje przed fazą Layout
        if (Event.current.type == EventType.Layout && pendingSafeAction != null)
        {
            var action = pendingSafeAction;
            pendingSafeAction = null;
            action.Invoke();
        }

        // 2. Oczyść ewentualne usunięte referencje SO
        CleanupDestroyedNodes();

        EnsureStylesInitialized();

        // Zamroź decyzję o widoczności panelu bocznego na cały przebieg bieżącego zdarzenia OnGUI
        bool showSidePanel = selectedNode != null && selectedNode.upgrade != null;

        Rect toolbarRect = new Rect(0, 0, position.width, TOOLBAR_HEIGHT);
        Rect graphRect = new Rect(
            0,
            TOOLBAR_HEIGHT,
            position.width - (showSidePanel ? SIDE_PANEL_WIDTH : 0f),
            Mathf.Max(1f, position.height - TOOLBAR_HEIGHT));

        Rect sideRect = new Rect(
            graphRect.xMax,
            TOOLBAR_HEIGHT,
            SIDE_PANEL_WIDTH,
            Mathf.Max(1f, position.height - TOOLBAR_HEIGHT));

        // 3. Rysuj Toolbar
        DrawToolbar(toolbarRect);

        // 4. Rysuj Graf (w bezpiecznym bloku BeginGroup/EndGroup)
        GUI.BeginGroup(graphRect);
        try
        {
            DrawGraph(new Rect(0, 0, graphRect.width, graphRect.height));
        }
        finally
        {
            GUI.EndGroup();
        }

        // 5. Rysuj Panel Boczny (tylko gdy zaznaczony węzeł)
        if (showSidePanel)
        {
            GUI.BeginGroup(sideRect);
            try
            {
                DrawSidePanel(new Rect(0, 0, SIDE_PANEL_WIDTH, sideRect.height));
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        // 6. Obsługa ObjectPickera
        HandleObjectPickerEvents();

        // 7. Obsługa Inputu (tylko dla odpowiednich stref!)
        HandleInput(graphRect, sideRect, toolbarRect);

        // Płynne odświeżanie podczas interakcji
        if (isDraggingNodes || isPanning || isBoxSelecting || isConnecting)
            Repaint();
    }

    private void QueueSafeAction(Action action)
    {
        if (action == null) return;
        pendingSafeAction += action;
        EditorApplication.delayCall += () =>
        {
            if (this != null) Repaint();
        };
        Repaint();
    }

    // =========================================================================
    // INICJALIZACJA STYLÓW I PREFS
    // =========================================================================

    private void EnsureStylesInitialized()
    {
        if (cachedNameStyle == null)
        {
            cachedNameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal    = { textColor = Color.white },
                wordWrap  = true,
                alignment = TextAnchor.UpperCenter,
                clipping  = TextClipping.Clip
            };
        }

        if (cachedEffectStyle == null)
        {
            cachedEffectStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { textColor = new Color(0.92f, 0.92f, 0.72f) },
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
                clipping  = TextClipping.Clip
            };
        }

        if (cachedCostStyle == null)
        {
            cachedCostStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { textColor = new Color(1f, 0.85f, 0.25f) },
                alignment = TextAnchor.LowerRight,
                fontStyle = FontStyle.Bold
            };
        }

        if (cachedIdBadgeStyle == null)
        {
            cachedIdBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { textColor = new Color(0.75f, 0.75f, 0.75f, 0.7f) },
                alignment = TextAnchor.LowerLeft
            };
        }
    }

    private void LoadViewPrefs()
    {
        if (EditorPrefs.HasKey(PREFS_PREFIX + "PanX"))
        {
            panOffset.x = EditorPrefs.GetFloat(PREFS_PREFIX + "PanX", 40f);
            panOffset.y = EditorPrefs.GetFloat(PREFS_PREFIX + "PanY", 40f);
            zoomScale   = Mathf.Clamp(EditorPrefs.GetFloat(PREFS_PREFIX + "Zoom", 1f), 0.2f, 2.5f);
            snapToGrid  = EditorPrefs.GetBool(PREFS_PREFIX + "Snap", false);
        }
    }

    private void SaveViewPrefs()
    {
        EditorPrefs.SetFloat(PREFS_PREFIX + "PanX", panOffset.x);
        EditorPrefs.SetFloat(PREFS_PREFIX + "PanY", panOffset.y);
        EditorPrefs.SetFloat(PREFS_PREFIX + "Zoom", zoomScale);
        EditorPrefs.SetBool(PREFS_PREFIX + "Snap",  snapToGrid);
    }

    private static string GetUpgradeGuid(MetaUpgradeSO upgrade)
    {
        if (upgrade == null) return string.Empty;
        string path = AssetDatabase.GetAssetPath(upgrade);
        return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
    }

    private bool TryLoadNodePositionFromPrefs(string guid, out Vector2 pos)
    {
        pos = Vector2.zero;
        if (string.IsNullOrEmpty(guid)) return false;

        string hasKey = $"{PREFS_PREFIX}Pos_{guid}_Has";
        if (!EditorPrefs.GetBool(hasKey, false)) return false;

        pos.x = EditorPrefs.GetFloat($"{PREFS_PREFIX}Pos_{guid}_X", 0f);
        pos.y = EditorPrefs.GetFloat($"{PREFS_PREFIX}Pos_{guid}_Y", 0f);
        return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y);
    }

    private void SaveNodePositionToPrefs(UpgradeNode node)
    {
        if (node == null || node.upgrade == null) return;
        if (string.IsNullOrEmpty(node.guid))
            node.guid = GetUpgradeGuid(node.upgrade);
        if (string.IsNullOrEmpty(node.guid)) return;

        EditorPrefs.SetBool($"{PREFS_PREFIX}Pos_{node.guid}_Has", true);
        EditorPrefs.SetFloat($"{PREFS_PREFIX}Pos_{node.guid}_X",  node.rect.x);
        EditorPrefs.SetFloat($"{PREFS_PREFIX}Pos_{node.guid}_Y",  node.rect.y);
    }

    private void SaveAllNodePositionsToPrefs()
    {
        foreach (var node in nodes)
            SaveNodePositionToPrefs(node);
    }

    // =========================================================================
    // TOOLBAR
    // =========================================================================

    private void DrawToolbar(Rect toolbarRect)
    {
        GUI.Box(toolbarRect, GUIContent.none, EditorStyles.toolbar);

        float x = 6f;

        // Manager picker (opcjonalny — graf działa też bez niego bezpośrednio na plikach z folderu)
        GUI.Label(new Rect(x, 4, 62, 22), "Manager:", EditorStyles.miniLabel);
        x += 62f;

        var newManager = (MetaUpgradeManager)EditorGUI.ObjectField(
            new Rect(x, 5, 150, 18),
            targetManager, typeof(MetaUpgradeManager), true);
        x += 156f;

        if (newManager != targetManager)
        {
            targetManager = newManager;
            UpdateManagerSyncStatus();
        }

        // Przycisk synchronizacji z Managerem jeśli wykryto różnice
        if (targetManager != null && unsyncedManagerCount > 0)
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.75f, 0.2f);
            if (GUI.Button(new Rect(x, 2, 135, 24), $"⚠ Sync Manager ({unsyncedManagerCount})", EditorStyles.toolbarButton))
            {
                QueueSafeAction(() =>
                {
                    MetaUpgradeTools.SyncManagerUpgrades(targetManager, nodes.Select(n => n.upgrade).Where(u => u != null).ToList());
                    UpdateManagerSyncStatus();
                });
            }
            GUI.backgroundColor = prevBg;
            x += 139f;
        }

        // Przycisk + Nowy Upgrade
        if (GUI.Button(new Rect(x, 2, 68, 24), "+ Nowy", EditorStyles.toolbarButton))
        {
            Vector2 centerGraph = ScreenToGraph(new Vector2(
                (position.width - (selectedNode != null ? SIDE_PANEL_WIDTH : 0f)) * 0.5f - NODE_WIDTH * 0.5f,
                (position.height - TOOLBAR_HEIGHT) * 0.5f - NODE_HEIGHT * 0.5f));
            contextMenuGraphPos = centerGraph;
            QueueSafeAction(() => CreateNewUpgradeImmediate(contextMenuGraphPos));
        }
        x += 72f;

        // Przycisk Odśwież (zachowuje pozycje z EditorPrefs)
        if (GUI.Button(new Rect(x, 2, 64, 24), "Odśwież", EditorStyles.toolbarButton))
        {
            QueueSafeAction(() => RefreshNodes(preservePositions: true));
        }
        x += 68f;

        // Przycisk Auto Layout (układa od nowa i zapisuje pozycje)
        if (GUI.Button(new Rect(x, 2, 84, 24), "Auto Layout", EditorStyles.toolbarButton))
        {
            QueueSafeAction(() =>
            {
                AutoLayout();
                SaveAllNodePositionsToPrefs();
                FrameNodes(nodes);
            });
        }
        x += 88f;

        // Przycisk Centruj (F)
        if (GUI.Button(new Rect(x, 2, 74, 24), "Centruj (F)", EditorStyles.toolbarButton))
        {
            FrameSelectionOrAll();
        }
        x += 78f;

        // Przełącznik Snap to Grid
        bool newSnap = GUI.Toggle(new Rect(x, 2, 52, 24), snapToGrid, "Snap", EditorStyles.toolbarButton);
        if (newSnap != snapToGrid)
        {
            snapToGrid = newSnap;
            SaveViewPrefs();
        }
        x += 58f;

        // Wyszukiwarka
        GUI.Label(new Rect(x, 4, 48, 22), "Szukaj:", EditorStyles.miniLabel);
        x += 48f;

        string newSearch = EditorGUI.TextField(new Rect(x, 5, 130, 18), searchQuery, EditorStyles.toolbarSearchField);
        if (newSearch != searchQuery)
        {
            searchQuery = newSearch;
            Repaint();
        }
        x += 134f;

        if (!string.IsNullOrEmpty(searchQuery))
        {
            if (GUI.Button(new Rect(x, 4, 20, 20), "✕", EditorStyles.toolbarButton))
            {
                searchQuery = "";
                GUI.FocusControl(null);
                Repaint();
            }
            x += 24f;
        }

        // Zoom info
        GUI.Label(new Rect(x, 4, 72, 22), $"Zoom: {zoomScale:F2}x", EditorStyles.miniLabel);

        // Legenda po prawej stronie (jeśli starczy miejsca)
        float legendStart = position.width - 285f;
        if (legendStart > x + 75f)
        {
            float lx = legendStart;
            DrawLegendItem(ref lx, COLOR_UNLOCKED,  "Kupiony");
            DrawLegendItem(ref lx, COLOR_AVAILABLE, "Dostępny");
            DrawLegendItem(ref lx, COLOR_LOCKED,    "Zablokowany");
        }
    }

    private void DrawLegendItem(ref float x, Color color, string label)
    {
        EditorGUI.DrawRect(new Rect(x, 8, 12, 12), color);
        x += 16f;
        GUI.Label(new Rect(x, 4, 72, 22), label, EditorStyles.miniLabel);
        x += 76f;
    }

    // =========================================================================
    // GRAF — RYSOWANIE
    // =========================================================================

    private void DrawGraph(Rect localGraphRect)
    {
        // Tło
        EditorGUI.DrawRect(localGraphRect, new Color(0.14f, 0.14f, 0.15f, 1f));

        // Siatka
        DrawGrid(localGraphRect);

        if (nodes.Count == 0)
        {
            GUI.Label(
                new Rect(localGraphRect.width * 0.5f - 180, localGraphRect.height * 0.5f - 25, 360, 50),
                $"Brak plików MetaUpgradeSO w:\n{MetaUpgradeTools.SCAN_PATH}\nKliknij PPM na tle → 'Utwórz nowy upgrade'.",
                EditorStyles.centeredGreyMiniLabel);
            return;
        }

        // Krawędzie zależności
        DrawEdges();

        // Podgląd przeciąganego połączenia
        if (isConnecting && connectionSourceNode != null && connectionSourceNode.upgrade != null)
        {
            DrawActiveConnectionWire();
        }

        // Węzły
        DrawNodes(localGraphRect);

        // Ramka zaznaczania (Box Selection)
        if (isBoxSelecting)
        {
            Rect boxRect = GetSelectionBoxScreenRect();
            EditorGUI.DrawRect(boxRect, new Color(0.25f, 0.6f, 1f, 0.15f));
            DrawBorder(boxRect, new Color(0.35f, 0.7f, 1f, 0.85f), 1.5f);
        }
    }

    private void DrawGrid(Rect graphRect)
    {
        float gridSize = Mathf.Max(8f, 40f * zoomScale);
        float offsetX  = ((panOffset.x % gridSize) + gridSize) % gridSize;
        float offsetY  = ((panOffset.y % gridSize) + gridSize) % gridSize;

        Handles.color = new Color(0.21f, 0.21f, 0.22f, 1f);

        for (float x = offsetX; x < graphRect.width; x += gridSize)
            Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, graphRect.height));
        for (float y = offsetY; y < graphRect.height; y += gridSize)
            Handles.DrawLine(new Vector3(0f, y), new Vector3(graphRect.width, y));
    }

    private void DrawEdges()
    {
        bool hasSearch = !string.IsNullOrWhiteSpace(searchQuery);

        foreach (var toNode in nodes)
        {
            if (toNode?.upgrade == null || toNode.upgrade.prerequisites == null) continue;

            foreach (var prereq in toNode.upgrade.prerequisites)
            {
                if (prereq == null || !nodeByUpgrade.TryGetValue(prereq, out var fromNode) || fromNode == null)
                    continue;

                Vector2 from = GetOutputPortCenterScreen(fromNode);
                Vector2 to   = GetInputPortCenterScreen(toNode);

                bool isRelatedToSelection =
                    selectedNodes.Contains(fromNode) || selectedNodes.Contains(toNode);

                bool met = prereq.isUnlocked;
                Color edgeColor = isRelatedToSelection
                    ? COLOR_EDGE_HIGHLIGHT
                    : (met ? COLOR_EDGE_MET : COLOR_EDGE_UNMET);

                if (hasSearch && !IsNodeMatchingSearch(fromNode) && !IsNodeMatchingSearch(toNode))
                    edgeColor.a *= 0.25f;

                float thickness = isRelatedToSelection
                    ? EDGE_THICKNESS_HIGHLIGHT
                    : (met ? EDGE_THICKNESS_MET : EDGE_THICKNESS_UNMET);

                DrawBezierConnection(from, to, edgeColor, thickness);
            }
        }
    }

    private void DrawActiveConnectionWire()
    {
        Vector2 mousePos = Event.current.mousePosition;
        Vector2 startPos = isConnectingFromOutput
            ? GetOutputPortCenterScreen(connectionSourceNode)
            : GetInputPortCenterScreen(connectionSourceNode);

        UpgradeNode targetNode = GetNodeAtScreen(mousePos);
        Color wireColor = COLOR_PORT_HOVER;

        if (targetNode != null && targetNode != connectionSourceNode)
        {
            MetaUpgradeSO fromSO = isConnectingFromOutput ? connectionSourceNode.upgrade : targetNode.upgrade;
            MetaUpgradeSO toSO   = isConnectingFromOutput ? targetNode.upgrade : connectionSourceNode.upgrade;
            bool valid = CanConnect(fromSO, toSO, out _);
            wireColor = valid ? COLOR_PORT_VALID : COLOR_PORT_INVALID;
        }

        if (isConnectingFromOutput)
            DrawBezierConnection(startPos, mousePos, wireColor, 3.5f);
        else
            DrawBezierConnection(mousePos, startPos, wireColor, 3.5f);
    }

    private void DrawBezierConnection(Vector2 from, Vector2 to, Color color, float thickness)
    {
        float distanceX = Mathf.Abs(to.x - from.x);
        float tangentStrength = Mathf.Max(45f * zoomScale, distanceX * 0.42f);

        Vector2 ctrl1 = from + new Vector2(tangentStrength, 0f);
        Vector2 ctrl2 = to   - new Vector2(tangentStrength, 0f);

        Handles.DrawBezier(
            new Vector3(from.x,  from.y,  0f),
            new Vector3(to.x,    to.y,    0f),
            new Vector3(ctrl1.x, ctrl1.y, 0f),
            new Vector3(ctrl2.x, ctrl2.y, 0f),
            color, null, thickness);

        Vector2 dir = (to - ctrl2).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
        DrawArrow(to, dir, color);
    }

    private void DrawArrow(Vector2 tip, Vector2 dir, Color color)
    {
        float size = Mathf.Clamp(8.5f * zoomScale, 5f, 14f);
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector3 a = new Vector3(tip.x, tip.y, 0f);
        Vector3 b = new Vector3(tip.x - dir.x * size + perp.x * size * 0.55f,
                                tip.y - dir.y * size + perp.y * size * 0.55f, 0f);
        Vector3 c = new Vector3(tip.x - dir.x * size - perp.x * size * 0.55f,
                                tip.y - dir.y * size - perp.y * size * 0.55f, 0f);

        Handles.color = color;
        Handles.DrawAAConvexPolygon(a, b, c);
    }

    private void DrawNodes(Rect localGraphRect)
    {
        bool hasSearch = !string.IsNullOrWhiteSpace(searchQuery);
        Vector2 mousePos = Event.current.mousePosition;

        float baseFontSize = Mathf.Clamp(zoomScale * 11.5f, 7f, 14f);
        cachedNameStyle.fontSize    = Mathf.RoundToInt(baseFontSize);
        cachedEffectStyle.fontSize  = Mathf.Max(7, Mathf.RoundToInt(baseFontSize * 0.85f));
        cachedCostStyle.fontSize    = Mathf.Max(7, Mathf.RoundToInt(baseFontSize * 0.78f));
        cachedIdBadgeStyle.fontSize = Mathf.Max(6, Mathf.RoundToInt(baseFontSize * 0.70f));

        foreach (var node in nodes)
        {
            if (node?.upgrade == null) continue;

            Rect screenRect = GraphRectToScreen(node.rect);

            // Culling poza ekranem (z marginesem na porty)
            if (screenRect.xMax + 20f < 0f || screenRect.x - 20f > localGraphRect.width ||
                screenRect.yMax + 20f < 0f || screenRect.y - 20f > localGraphRect.height)
                continue;

            bool isSelected = selectedNodes.Contains(node);
            bool matchesSearch = !hasSearch || IsNodeMatchingSearch(node);

            // Kolor tła węzła (zawsze wg stanu Kupiony / Dostępny / Zablokowany)
            Color bgColor = GetNodeColor(node);
            if (isSelected)
                bgColor = Color.Lerp(bgColor, COLOR_SELECTED, 0.35f);
            if (hasSearch && !matchesSearch)
                bgColor = Color.Lerp(bgColor, new Color(0.12f, 0.12f, 0.12f, 0.45f), 0.75f);

            EditorGUI.DrawRect(screenRect, bgColor);

            // Ramka węzła
            Color borderColor = new Color(0f, 0f, 0f, 0.6f);
            float borderThickness = 1.5f;

            if (isConnecting && connectionSourceNode != null && node != connectionSourceNode && screenRect.Contains(mousePos))
            {
                MetaUpgradeSO fromSO = isConnectingFromOutput ? connectionSourceNode.upgrade : node.upgrade;
                MetaUpgradeSO toSO   = isConnectingFromOutput ? node.upgrade : connectionSourceNode.upgrade;
                borderColor = CanConnect(fromSO, toSO, out _) ? COLOR_PORT_VALID : COLOR_PORT_INVALID;
                borderThickness = 3f;
            }
            else if (isSelected)
            {
                borderColor = node == selectedNode ? COLOR_SELECTED : new Color(0.95f, 0.82f, 0.35f, 0.85f);
                borderThickness = node == selectedNode ? 2.5f : 2f;
            }
            else if (hasSearch && matchesSearch)
            {
                borderColor = COLOR_SEARCH_HIT;
                borderThickness = 2.5f;
            }

            DrawBorder(screenRect, borderColor, borderThickness);

            // Nazwa upgradu
            string displayTitle = string.IsNullOrWhiteSpace(node.upgrade.upgradeName)
                ? $"({node.upgrade.name})"
                : node.upgrade.upgradeName;

            Rect nameRect = new Rect(
                screenRect.x + 6f, screenRect.y + 4f,
                screenRect.width - 12f, screenRect.height * 0.45f);
            GUI.Label(nameRect, displayTitle, cachedNameStyle);

            // Opis efektu (lub powiązania z MetaMapConfig)
            if (zoomScale > 0.45f)
            {
                Rect effectRect = new Rect(
                    screenRect.x + 6f, screenRect.y + screenRect.height * 0.44f,
                    screenRect.width - 12f, screenRect.height * 0.34f);

                string effectText = GetNodeEffectSummary(node.upgrade);
                GUI.Label(effectRect, effectText, cachedEffectStyle);
            }

            // Dolny pasek: ID / ostrzeżenia (po lewej) oraz Koszt (po prawej)
            if (zoomScale > 0.60f)
            {
                string leftBadge = string.IsNullOrWhiteSpace(node.upgrade.id)
                    ? "⚠ BRAK ID"
                    : node.upgrade.id;
                if (leftBadge.Length > 18) leftBadge = leftBadge.Substring(0, 17) + "…";

                GUI.Label(
                    new Rect(screenRect.x + 6f, screenRect.yMax - 17f, screenRect.width * 0.62f, 15f),
                    leftBadge, cachedIdBadgeStyle);

                GUI.Label(
                    new Rect(screenRect.x, screenRect.yMax - 17f, screenRect.width - 6f, 15f),
                    $"⬡ {node.upgrade.cost}", cachedCostStyle);
            }

            // Porty Wejścia (lewy) i Wyjścia (prawy)
            DrawNodePorts(node, mousePos);
        }
    }

    private void DrawNodePorts(UpgradeNode node, Vector2 mousePos)
    {
        float radius = Mathf.Clamp(PORT_RADIUS * zoomScale, 4.5f, 10f);

        Vector2 inCenter  = GetInputPortCenterScreen(node);
        Vector2 outCenter = GetOutputPortCenterScreen(node);

        bool hoverIn  = Vector2.Distance(mousePos, inCenter)  <= radius * 1.5f;
        bool hoverOut = Vector2.Distance(mousePos, outCenter) <= radius * 1.5f;

        bool hasPrereqs = node.upgrade.prerequisites != null && node.upgrade.prerequisites.Any(p => p != null);
        bool hasChildren = nodes.Any(n => n?.upgrade?.prerequisites != null && n.upgrade.prerequisites.Contains(node.upgrade));

        // Port wejściowy (lewa krawędź)
        Color inColor = hoverIn
            ? COLOR_PORT_HOVER
            : (hasPrereqs ? COLOR_EDGE_MET : COLOR_PORT_NORMAL);
        Handles.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        Handles.DrawSolidDisc(inCenter, Vector3.forward, radius + 1.5f);
        Handles.color = inColor;
        Handles.DrawSolidDisc(inCenter, Vector3.forward, radius);

        // Port wyjściowy (prawa krawędź)
        Color outColor = hoverOut
            ? COLOR_PORT_HOVER
            : (hasChildren ? COLOR_EDGE_MET : COLOR_PORT_NORMAL);
        Handles.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        Handles.DrawSolidDisc(outCenter, Vector3.forward, radius + 1.5f);
        Handles.color = outColor;
        Handles.DrawSolidDisc(outCenter, Vector3.forward, radius);
    }

    private Color GetNodeColor(UpgradeNode node)
    {
        if (node.upgrade.isUnlocked)            return COLOR_UNLOCKED;
        if (node.upgrade.ArePrerequisitesMet()) return COLOR_AVAILABLE;
        return COLOR_LOCKED;
    }

    private void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    // =========================================================================
    // PANEL BOCZNY
    // =========================================================================

    private void DrawSidePanel(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.19f, 1f));
        DrawBorder(rect, new Color(0.35f, 0.35f, 0.38f), 1f);

        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
        try
        {
            sidePanelScroll = GUILayout.BeginScrollView(sidePanelScroll);
            try
            {
                if (selectedNode == null || selectedNode.upgrade == null)
                    return;

                var upgrade = selectedNode.upgrade;

                // Nagłówek + informacja o multiselekcji
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(
                    selectedNodes.Count > 1
                        ? $"Edycja Upgradu (+{selectedNodes.Count - 1} zazn.)"
                        : "Edycja Upgradu",
                    EditorStyles.boldLabel);

                if (GUILayout.Button("✕", GUILayout.Width(22f), GUILayout.Height(18f)))
                {
                    QueueSafeAction(() =>
                    {
                        selectedNode = null;
                        selectedNodes.Clear();
                    });
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // Podstawowe pola z obsługą Undo
                EditorGUI.BeginChangeCheck();

                string newId   = EditorGUILayout.TextField("ID", upgrade.id);
                GUI.SetNextControlName("SidePanel_UpgradeNameField");
                string newName = EditorGUILayout.TextField("Nazwa", upgrade.upgradeName);

                if (focusNameFieldNextFrame)
                {
                    EditorGUI.FocusTextInControl("SidePanel_UpgradeNameField");
                    focusNameFieldNextFrame = false;
                }

                // Przycisk synchronizacji nazwy pliku .asset oraz ID wg wpisanej Nazwy
                if (GUILayout.Button("🔄 Zaktualizuj ID i plik (.asset) wg Nazwy", GUILayout.Height(22)))
                {
                    var nodeToRename = selectedNode;
                    QueueSafeAction(() => SyncAssetFileNameAndIdFromName(nodeToRename));
                }

                EditorGUILayout.Space(4);
                GUILayout.Label("Opis:", EditorStyles.miniLabel);
                string newDesc = EditorGUILayout.TextArea(upgrade.description, GUILayout.MinHeight(48));

                EditorGUILayout.Space(4);
                int newCost    = EditorGUILayout.IntField("Koszt (Artefakty)", upgrade.cost);
                Sprite newIcon = (Sprite)EditorGUILayout.ObjectField("Ikona", upgrade.icon, typeof(Sprite), false);

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("─── Efekt ───────────────────────", EditorStyles.miniLabel);

                MetaEffectType newEffectType = (MetaEffectType)EditorGUILayout.EnumPopup("Typ Efektu", upgrade.effectType);
                float newEffectValue  = upgrade.effectValue;
                float newEffectValue2 = upgrade.effectValue2;
                BuildingData newTargetBuilding = upgrade.targetBuilding;

                if (newEffectType != MetaEffectType.None)
                {
                    newEffectValue = EditorGUILayout.FloatField(
                        GetEffectValueLabel(newEffectType), upgrade.effectValue);

                    if (newEffectType == MetaEffectType.EliteChanceBoost)
                    {
                        newEffectValue2 = EditorGUILayout.FloatField(
                            "Od Fali (effectValue2)", upgrade.effectValue2);
                    }

                    bool needsBuilding =
                        newEffectType == MetaEffectType.SpecificBuildingUpgradeCostReduction ||
                        newEffectType == MetaEffectType.BuildingTerrainBonusMultiplier ||
                        newEffectType == MetaEffectType.SpecificBuildingProductionBonus;

                    if (needsBuilding)
                    {
                        newTargetBuilding = (BuildingData)EditorGUILayout.ObjectField(
                            "Budynek Docelowy", upgrade.targetBuilding, typeof(BuildingData), false);
                    }
                }

                // Sekcja powiązań z MetaMapConfig
                if (mapConfigLinks.TryGetValue(upgrade, out var links) && links.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox(
                        "Powiązania w MetaMapConfig:\n• " + string.Join("\n• ", links),
                        MessageType.Info);

                    if (primaryMapConfig != null && GUILayout.Button("Pokaż MetaMapConfig"))
                    {
                        EditorGUIUtility.PingObject(primaryMapConfig);
                    }
                }

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("─── Stan ────────────────────────", EditorStyles.miniLabel);
                bool newUnlocked = EditorGUILayout.Toggle("Odblokowany", upgrade.isUnlocked);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(upgrade, "Edycja MetaUpgradeSO");
                    upgrade.id             = newId;
                    upgrade.upgradeName    = newName;
                    upgrade.description    = newDesc;
                    upgrade.cost           = Mathf.Max(0, newCost);
                    upgrade.icon           = newIcon;
                    upgrade.effectType     = newEffectType;
                    upgrade.effectValue    = newEffectValue;
                    upgrade.effectValue2   = newEffectValue2;
                    upgrade.targetBuilding = newTargetBuilding;
                    upgrade.isUnlocked     = newUnlocked;
                    EditorUtility.SetDirty(upgrade);
                    Repaint();
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    upgrade.isUnlocked ? "✓ Kupiony (Odblokowany)" :
                    upgrade.ArePrerequisitesMet() ? "○ Dostępny do kupna" : "✗ Zablokowany (brak wymagań)",
                    upgrade.isUnlocked ? MessageType.Info : MessageType.None);

                // Sekcja Wymagań (Prerequisites)
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("─── Wymagania (Prerequisites) ───", EditorStyles.miniLabel);

                if (upgrade.prerequisites == null)
                    upgrade.prerequisites = new List<MetaUpgradeSO>();

                int removeIndex = -1;
                for (int i = 0; i < upgrade.prerequisites.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    var candidate = (MetaUpgradeSO)EditorGUILayout.ObjectField(
                        $"Wymaga [{i}]", upgrade.prerequisites[i], typeof(MetaUpgradeSO), false);

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (candidate == null || CanConnect(candidate, upgrade, out string reason, ignoreExistingAtIndex: i))
                        {
                            Undo.RecordObject(upgrade, "Zmień wymaganie MetaUpgrade");
                            upgrade.prerequisites[i] = candidate;
                            EditorUtility.SetDirty(upgrade);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Nieprawidłowe połączenie", reason, "OK");
                        }
                    }

                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                        removeIndex = i;

                    EditorGUILayout.EndHorizontal();
                }

                if (removeIndex >= 0 && removeIndex < upgrade.prerequisites.Count)
                {
                    Undo.RecordObject(upgrade, "Usuń wymaganie MetaUpgrade");
                    upgrade.prerequisites.RemoveAt(removeIndex);
                    EditorUtility.SetDirty(upgrade);
                    Repaint();
                }

                if (GUILayout.Button("+ Dodaj Wymaganie"))
                {
                    Undo.RecordObject(upgrade, "Dodaj slot wymagania MetaUpgrade");
                    upgrade.prerequisites.Add(null);
                    EditorUtility.SetDirty(upgrade);
                }

                // Podgląd efektu
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    $"Podgląd na karcie: {GetNodeEffectSummary(upgrade)}",
                    MessageType.None);

                // Akcje szybkie na dole panelu bocznego
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("─── Akcje ───────────────────────", EditorStyles.miniLabel);

                if (GUILayout.Button("📑 Duplikuj / Kolejny Tier (Ctrl+D)"))
                {
                    var nodeToDup = selectedNode;
                    QueueSafeAction(() => DuplicateNodeAsNextTier(nodeToDup));
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Pokaż w Project"))
                    EditorGUIUtility.PingObject(upgrade);

                if (GUILayout.Button("Inspector"))
                    Selection.activeObject = upgrade;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
                if (GUILayout.Button("🗑 Usuń węzeł i plik SO..."))
                {
                    var nodeToDelete = selectedNode;
                    QueueSafeAction(() => DeleteNodeAndAsset(nodeToDelete));
                }
                GUI.backgroundColor = prevColor;
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }
        finally
        {
            GUILayout.EndArea();
        }
    }

    // =========================================================================
    // INPUT
    // =========================================================================

    private void HandleInput(Rect graphRect, Rect sideRect, Rect toolbarRect)
    {
        Event e = Event.current;
        if (e == null) return;

        bool mouseInGraphArea = graphRect.Contains(e.mousePosition);
        Vector2 mouseInGraph  = e.mousePosition - new Vector2(graphRect.x, graphRect.y);

        // 1. Skróty klawiszowe (tylko gdy nie piszemy w polu tekstowym)
        if (e.type == EventType.KeyDown && !EditorGUIUtility.editingTextField)
        {
            if (e.keyCode == KeyCode.F)
            {
                FrameSelectionOrAll();
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.D && (e.control || e.command) && selectedNode != null)
            {
                var nodeToDup = selectedNode;
                QueueSafeAction(() => DuplicateNodeAsNextTier(nodeToDup));
                e.Use();
                return;
            }
            if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && selectedNode != null)
            {
                var nodeToDelete = selectedNode;
                QueueSafeAction(() => DeleteNodeAndAsset(nodeToDelete));
                e.Use();
                return;
            }
        }

        // 2. Zoom do kursora myszy (ScrollWheel w obszarze grafu)
        if (e.type == EventType.ScrollWheel && mouseInGraphArea)
        {
            Vector2 graphPosBefore = ScreenToGraph(mouseInGraph);
            float zoomFactor = e.delta.y < 0 ? 1.12f : (1f / 1.12f);
            zoomScale = Mathf.Clamp(zoomScale * zoomFactor, 0.2f, 2.5f);

            // Korekta panOffset tak, aby punkt pod kursorem pozostał w tym samym miejscu
            panOffset = mouseInGraph - graphPosBefore * zoomScale;
            SaveViewPrefs();

            e.Use();
            Repaint();
            return;
        }

        // 3. MouseDown — TYLKO gdy kliknięto wewnątrz obszaru grafu (nie w panelu bocznym ani toolbarze!)
        if (e.type == EventType.MouseDown && mouseInGraphArea)
        {
            // Alt + LPM lub Środkowy Przycisk Myszy = Pan
            if (e.button == 2 || (e.button == 0 && e.alt))
            {
                isPanning = true;
                e.Use();
                return;
            }

            if (e.button == 0) // LPM
            {
                // Sprawdź, czy kliknięto w port wyjściowy lub wejściowy któregoś z węzłów
                if (TryGetPortAtScreen(mouseInGraph, out UpgradeNode portNode, out bool isOutputPort))
                {
                    isConnecting           = true;
                    connectionSourceNode   = portNode;
                    isConnectingFromOutput = isOutputPort;
                    GUI.FocusControl(null);
                    e.Use();
                    return;
                }

                // Sprawdź, czy kliknięto w węzeł
                UpgradeNode clicked = GetNodeAtScreen(mouseInGraph);
                if (clicked != null)
                {
                    GUI.FocusControl(null);

                    bool shiftOrCtrl = e.shift || e.control || e.command;
                    if (shiftOrCtrl)
                    {
                        if (selectedNodes.Contains(clicked))
                        {
                            selectedNodes.Remove(clicked);
                            if (selectedNode == clicked)
                                selectedNode = selectedNodes.FirstOrDefault();
                        }
                        else
                        {
                            selectedNodes.Add(clicked);
                            selectedNode = clicked;
                        }
                    }
                    else
                    {
                        if (!selectedNodes.Contains(clicked))
                        {
                            selectedNodes.Clear();
                            selectedNodes.Add(clicked);
                        }
                        selectedNode = clicked;
                    }

                    // Rozpocznij przeciąganie wszystkich zaznaczonych węzłów
                    isDraggingNodes      = true;
                    dragAccumulatedDelta = Vector2.zero;
                    dragStartPositions.Clear();
                    foreach (var n in selectedNodes)
                        dragStartPositions[n] = n.rect.position;

                    e.Use();
                    return;
                }
                else
                {
                    // Kliknięto na pustym tle -> rozpocznij Box Selection
                    GUI.FocusControl(null);
                    if (!e.shift && !e.control && !e.command)
                    {
                        selectedNode = null;
                        selectedNodes.Clear();
                    }

                    isBoxSelecting         = true;
                    boxSelectStartScreen   = mouseInGraph;
                    boxSelectCurrentScreen = mouseInGraph;
                    e.Use();
                    return;
                }
            }
            else if (e.button == 1) // PPM
            {
                // Sprawdź czy kliknięto PPM bezpośrednio na porcie
                if (TryGetPortAtScreen(mouseInGraph, out UpgradeNode portNode, out bool isOutputPort))
                {
                    ShowPortContextMenu(portNode, isOutputPort);
                    e.Use();
                    return;
                }

                // Przygotuj pod Pan (przy przeciągnięciu) lub Menu Kontekstowe (przy puszczeniu bez przeciągnięcia)
                rightMouseDownPos    = e.mousePosition;
                isRightMouseDragging = false;
                rightMouseNode       = GetNodeAtScreen(mouseInGraph);
                contextMenuGraphPos  = ScreenToGraph(mouseInGraph);
                e.Use();
                return;
            }
        }

        // 4. MouseDrag
        if (e.type == EventType.MouseDrag)
        {
            if (isConnecting)
            {
                e.Use();
                Repaint();
                return;
            }

            if (isDraggingNodes && selectedNodes.Count > 0)
            {
                dragAccumulatedDelta += e.delta / zoomScale;
                bool snap = snapToGrid || e.control || e.command;

                foreach (var kvp in dragStartPositions)
                {
                    UpgradeNode n = kvp.Key;
                    Vector2 targetPos = kvp.Value + dragAccumulatedDelta;
                    if (snap)
                    {
                        targetPos.x = Mathf.Round(targetPos.x / SNAP_GRID_SIZE) * SNAP_GRID_SIZE;
                        targetPos.y = Mathf.Round(targetPos.y / SNAP_GRID_SIZE) * SNAP_GRID_SIZE;
                    }
                    n.rect.position = targetPos;
                }

                e.Use();
                Repaint();
                return;
            }

            if (isBoxSelecting)
            {
                boxSelectCurrentScreen = mouseInGraph;
                UpdateBoxSelection();
                e.Use();
                Repaint();
                return;
            }

            if (isPanning)
            {
                panOffset += e.delta;
                e.Use();
                Repaint();
                return;
            }

            if (e.button == 1)
            {
                if (!isRightMouseDragging && Vector2.Distance(e.mousePosition, rightMouseDownPos) > 5f)
                    isRightMouseDragging = true;

                if (isRightMouseDragging)
                {
                    panOffset += e.delta;
                    e.Use();
                    Repaint();
                    return;
                }
            }
        }

        // 5. MouseUp
        if (e.type == EventType.MouseUp)
        {
            if (isConnecting)
            {
                FinishConnectionDrag(mouseInGraph);
                isConnecting         = false;
                connectionSourceNode = null;
                e.Use();
                Repaint();
                return;
            }

            if (isDraggingNodes)
            {
                isDraggingNodes = false;
                dragStartPositions.Clear();
                SaveAllNodePositionsToPrefs();
                e.Use();
                Repaint();
                return;
            }

            if (isBoxSelecting)
            {
                isBoxSelecting = false;
                e.Use();
                Repaint();
                return;
            }

            if (isPanning)
            {
                isPanning = false;
                SaveViewPrefs();
                e.Use();
                Repaint();
                return;
            }

            if (e.button == 1)
            {
                if (!isRightMouseDragging && mouseInGraphArea)
                {
                    if (rightMouseNode != null && rightMouseNode.upgrade != null)
                        ShowNodeContextMenu(rightMouseNode);
                    else
                        ShowBackgroundContextMenu();
                }
                else if (isRightMouseDragging)
                {
                    SaveViewPrefs();
                }

                isRightMouseDragging = false;
                rightMouseNode       = null;
                e.Use();
                Repaint();
            }
        }
    }

    private Rect GetSelectionBoxScreenRect()
    {
        float xMin = Mathf.Min(boxSelectStartScreen.x, boxSelectCurrentScreen.x);
        float yMin = Mathf.Min(boxSelectStartScreen.y, boxSelectCurrentScreen.y);
        float xMax = Mathf.Max(boxSelectStartScreen.x, boxSelectCurrentScreen.x);
        float yMax = Mathf.Max(boxSelectStartScreen.y, boxSelectCurrentScreen.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void UpdateBoxSelection()
    {
        Rect boxScreen = GetSelectionBoxScreenRect();
        selectedNodes.Clear();

        foreach (var node in nodes)
        {
            if (node?.upgrade == null) continue;
            Rect nodeScreen = GraphRectToScreen(node.rect);
            if (boxScreen.Overlaps(nodeScreen, true))
                selectedNodes.Add(node);
        }

        selectedNode = selectedNodes.FirstOrDefault();
    }

    // =========================================================================
    // ŁĄCZENIE WĘZŁÓW (PORTY I WALIDACJA CYKLI)
    // =========================================================================

    private Vector2 GetInputPortCenterScreen(UpgradeNode node)
    {
        Rect sr = GraphRectToScreen(node.rect);
        return new Vector2(sr.x, sr.y + sr.height * 0.5f);
    }

    private Vector2 GetOutputPortCenterScreen(UpgradeNode node)
    {
        Rect sr = GraphRectToScreen(node.rect);
        return new Vector2(sr.xMax, sr.y + sr.height * 0.5f);
    }

    private bool TryGetPortAtScreen(Vector2 screenPos, out UpgradeNode hitNode, out bool isOutputPort)
    {
        float hitRadius = Mathf.Clamp(PORT_RADIUS * zoomScale * 1.6f, 8f, 16f);

        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            if (node?.upgrade == null) continue;

            if (Vector2.Distance(screenPos, GetOutputPortCenterScreen(node)) <= hitRadius)
            {
                hitNode      = node;
                isOutputPort = true;
                return true;
            }

            if (Vector2.Distance(screenPos, GetInputPortCenterScreen(node)) <= hitRadius)
            {
                hitNode      = node;
                isOutputPort = false;
                return true;
            }
        }

        hitNode      = null;
        isOutputPort = false;
        return false;
    }

    private void FinishConnectionDrag(Vector2 mouseInGraph)
    {
        if (connectionSourceNode?.upgrade == null) return;

        UpgradeNode targetNode = GetNodeAtScreen(mouseInGraph);
        if (targetNode == null && TryGetPortAtScreen(mouseInGraph, out var portTarget, out _))
            targetNode = portTarget;

        if (targetNode == null || targetNode.upgrade == null || targetNode == connectionSourceNode)
            return;

        MetaUpgradeSO prereqSO    = isConnectingFromOutput ? connectionSourceNode.upgrade : targetNode.upgrade;
        MetaUpgradeSO dependentSO = isConnectingFromOutput ? targetNode.upgrade : connectionSourceNode.upgrade;

        if (!CanConnect(prereqSO, dependentSO, out string reason))
        {
            ShowNotification(new GUIContent($"⚠ {reason}"));
            return;
        }

        Undo.RecordObject(dependentSO, "Połącz wymaganie MetaUpgrade");
        if (dependentSO.prerequisites == null)
            dependentSO.prerequisites = new List<MetaUpgradeSO>();
        dependentSO.prerequisites.Add(prereqSO);
        EditorUtility.SetDirty(dependentSO);
    }

    /// <summary>
    /// Sprawdza, czy dodanie <paramref name="prereq"/> jako wymagania dla <paramref name="dependent"/>
    /// jest poprawne i nie tworzy pętli (cyklu) w grafie.
    /// </summary>
    private bool CanConnect(MetaUpgradeSO prereq, MetaUpgradeSO dependent, out string reason, int ignoreExistingAtIndex = -1)
    {
        reason = string.Empty;
        if (prereq == null || dependent == null)
        {
            reason = "Brak obiektu docelowego.";
            return false;
        }

        if (prereq == dependent)
        {
            reason = "Upgrade nie może wymagać samego siebie.";
            return false;
        }

        if (dependent.prerequisites != null)
        {
            for (int i = 0; i < dependent.prerequisites.Count; i++)
            {
                if (i == ignoreExistingAtIndex) continue;
                if (dependent.prerequisites[i] == prereq)
                {
                    reason = $"'{dependent.upgradeName}' już wymaga '{prereq.upgradeName}'.";
                    return false;
                }
            }
        }

        // Sprawdź czy dependent nie jest już przodkiem prereq (co utworzyłoby cykl)
        if (IsAncestor(dependent, prereq))
        {
            reason = "Połączenie utworzyłoby pętlę (cykl zależności)!";
            return false;
        }

        return true;
    }

    private bool IsAncestor(MetaUpgradeSO potentialAncestor, MetaUpgradeSO startNode)
    {
        if (potentialAncestor == null || startNode == null) return false;

        var visited = new HashSet<MetaUpgradeSO>();
        var queue   = new Queue<MetaUpgradeSO>();
        queue.Enqueue(startNode);
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == potentialAncestor) return true;

            if (current.prerequisites == null) continue;
            foreach (var req in current.prerequisites)
            {
                if (req != null && visited.Add(req))
                    queue.Enqueue(req);
            }
        }

        return false;
    }

    // =========================================================================
    // MENU KONTEKSTOWE
    // =========================================================================

    private void ShowBackgroundContextMenu()
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("➕ Utwórz nowy upgrade tutaj"), false, () =>
        {
            Vector2 spawnPos = contextMenuGraphPos;
            QueueSafeAction(() => CreateNewUpgradeImmediate(spawnPos));
        });

        menu.AddItem(new GUIContent("📂 Dodaj istniejący plik SO..."), false, () =>
        {
            QueueSafeAction(AddExistingUpgrade);
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("📐 Auto Layout (ułóż wszystkie)"), false, () =>
        {
            QueueSafeAction(() =>
            {
                AutoLayout();
                SaveAllNodePositionsToPrefs();
                FrameNodes(nodes);
            });
        });

        menu.AddItem(new GUIContent("🎯 Centruj widok (F)"), false, FrameSelectionOrAll);

        if (targetManager != null)
        {
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("🔄 Synchronizuj wszystkie z MetaUpgradeManager"), false, () =>
            {
                QueueSafeAction(() =>
                {
                    MetaUpgradeTools.SyncManagerUpgrades(targetManager, nodes.Select(n => n.upgrade).Where(u => u != null).ToList());
                    UpdateManagerSyncStatus();
                });
            });
        }

        menu.ShowAsContext();
    }

    private void ShowNodeContextMenu(UpgradeNode node)
    {
        if (node?.upgrade == null) return;

        if (!selectedNodes.Contains(node))
        {
            selectedNodes.Clear();
            selectedNodes.Add(node);
        }
        selectedNode = node;

        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("📑 Duplikuj jako kolejny Tier (Ctrl+D)"), false, () =>
        {
            QueueSafeAction(() => DuplicateNodeAsNextTier(node));
        });

        menu.AddItem(new GUIContent("🔄 Zaktualizuj ID i nazwę pliku wg Nazwy"), false, () =>
        {
            QueueSafeAction(() => SyncAssetFileNameAndIdFromName(node));
        });

        menu.AddSeparator("");

        string lockLabel = node.upgrade.isUnlocked
            ? "🔒 Oznacz jako zablokowany"
            : "🔓 Oznacz jako odblokowany";
        menu.AddItem(new GUIContent(lockLabel), false, () =>
        {
            QueueSafeAction(() =>
            {
                if (node?.upgrade == null) return;
                Undo.RecordObject(node.upgrade, lockLabel);
                node.upgrade.isUnlocked = !node.upgrade.isUnlocked;
                EditorUtility.SetDirty(node.upgrade);
            });
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Pokaż w Project"), false,
            () => EditorGUIUtility.PingObject(node.upgrade));

        menu.AddItem(new GUIContent("Zaznacz w Inspector"), false,
            () => Selection.activeObject = node.upgrade);

        // Opcje szybkiego rozłączania
        bool hasPrereqs = node.upgrade.prerequisites != null && node.upgrade.prerequisites.Any(p => p != null);
        if (hasPrereqs)
        {
            menu.AddSeparator("");
            foreach (var prereq in node.upgrade.prerequisites.Where(p => p != null).ToList())
            {
                var capturedPrereq = prereq;
                menu.AddItem(new GUIContent($"Rozłącz wymaganie/Odłącz: {capturedPrereq.upgradeName}"), false, () =>
                {
                    QueueSafeAction(() =>
                    {
                        Undo.RecordObject(node.upgrade, "Odłącz wymaganie");
                        node.upgrade.prerequisites.Remove(capturedPrereq);
                        EditorUtility.SetDirty(node.upgrade);
                    });
                });
            }
        }

        menu.AddSeparator("");

        if (targetManager != null && targetManager.allUpgrades != null && targetManager.allUpgrades.Contains(node.upgrade))
        {
            menu.AddItem(new GUIContent("Usuń tylko z listy Managera"), false, () =>
            {
                QueueSafeAction(() => RemoveNodeFromManager(node));
            });
        }

        menu.AddItem(new GUIContent("🗑 Usuń węzeł i plik SO (Delete)..."), false, () =>
        {
            QueueSafeAction(() => DeleteNodeAndAsset(node));
        });

        menu.ShowAsContext();
    }

    private void ShowPortContextMenu(UpgradeNode node, bool isOutputPort)
    {
        if (node?.upgrade == null) return;
        var menu = new GenericMenu();

        if (!isOutputPort)
        {
            // Port wejściowy — lista wymagań tego węzła
            var prereqs = node.upgrade.prerequisites?.Where(p => p != null).ToList() ?? new List<MetaUpgradeSO>();
            if (prereqs.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Brak podpiętych wymagań"));
            }
            else
            {
                foreach (var req in prereqs)
                {
                    var capturedReq = req;
                    menu.AddItem(new GUIContent($"Rozłącz od: {capturedReq.upgradeName}"), false, () =>
                    {
                        QueueSafeAction(() =>
                        {
                            Undo.RecordObject(node.upgrade, "Rozłącz wymaganie");
                            node.upgrade.prerequisites.Remove(capturedReq);
                            EditorUtility.SetDirty(node.upgrade);
                        });
                    });
                }

                if (prereqs.Count > 1)
                {
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Rozłącz wszystkie wejścia"), false, () =>
                    {
                        QueueSafeAction(() =>
                        {
                            Undo.RecordObject(node.upgrade, "Rozłącz wszystkie wymagania");
                            node.upgrade.prerequisites.Clear();
                            EditorUtility.SetDirty(node.upgrade);
                        });
                    });
                }
            }
        }
        else
        {
            // Port wyjściowy — lista węzłów zależnych od tego węzła
            var dependents = nodes
                .Where(n => n?.upgrade?.prerequisites != null && n.upgrade.prerequisites.Contains(node.upgrade))
                .ToList();

            if (dependents.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Brak wychodzących połączeń"));
            }
            else
            {
                foreach (var dep in dependents)
                {
                    var capturedDep = dep;
                    menu.AddItem(new GUIContent($"Rozłącz do: {capturedDep.upgrade.upgradeName}"), false, () =>
                    {
                        QueueSafeAction(() =>
                        {
                            Undo.RecordObject(capturedDep.upgrade, "Rozłącz wymaganie");
                            capturedDep.upgrade.prerequisites.Remove(node.upgrade);
                            EditorUtility.SetDirty(capturedDep.upgrade);
                        });
                    });
                }

                if (dependents.Count > 1)
                {
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Rozłącz wszystkie wyjścia"), false, () =>
                    {
                        QueueSafeAction(() =>
                        {
                            foreach (var dep in dependents)
                            {
                                Undo.RecordObject(dep.upgrade, "Rozłącz wymagania");
                                dep.upgrade.prerequisites.Remove(node.upgrade);
                                EditorUtility.SetDirty(dep.upgrade);
                            }
                        });
                    });
                }
            }
        }

        menu.ShowAsContext();
    }

    // =========================================================================
    // OPERACJE TWORZENIA, DUPLIKOWANIA, ZMIANY NAZWY I USUWANIA
    // =========================================================================

    private bool EnsureMetaFolderExists()
    {
        if (AssetDatabase.IsValidFolder(MetaUpgradeTools.SCAN_PATH))
            return true;

        string parent = "Assets/Main folder/ScriptableObjects";
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EditorUtility.DisplayDialog("Brak Folderu",
                $"Folder nadrzędny nie istnieje:\n{parent}\n\nUtwórz go w oknie Project.", "OK");
            return false;
        }

        AssetDatabase.CreateFolder(parent, "Meta");
        return true;
    }

    private string GetUniqueAssetFileName(string baseName)
    {
        string sanitized = string.IsNullOrWhiteSpace(baseName) ? "NewMetaUpgrade" : baseName.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(c.ToString(), "");
        if (string.IsNullOrEmpty(sanitized)) sanitized = "NewMetaUpgrade";

        string candidate = sanitized;
        int suffix = 1;
        while (AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>($"{MetaUpgradeTools.SCAN_PATH}/{candidate}.asset") != null)
        {
            candidate = $"{sanitized}_{suffix}";
            suffix++;
        }
        return candidate;
    }

    private string GetUniqueUpgradeId(string desiredBaseId, MetaUpgradeSO ignoreUpgrade = null)
    {
        string baseId = MetaUpgradeTools.ToSnakeCasePublic(desiredBaseId);
        if (string.IsNullOrEmpty(baseId)) baseId = "upgrade";

        var usedIds = new HashSet<string>(
            nodes.Where(n => n?.upgrade != null && n.upgrade != ignoreUpgrade && !string.IsNullOrWhiteSpace(n.upgrade.id))
                 .Select(n => n.upgrade.id));

        string finalId = baseId;
        int suffix = 2;
        while (usedIds.Contains(finalId))
        {
            finalId = $"{baseId}_{suffix}";
            suffix++;
        }
        return finalId;
    }

    /// <summary>
    /// Tworzy nowy MetaUpgradeSO bezpośrednio w miejscu kliknięcia (bez modalnego okna ShowModal!),
    /// zaznacza go i ustawia fokus na polu "Nazwa" w panelu bocznym.
    /// </summary>
    private void CreateNewUpgradeImmediate(Vector2 graphPos)
    {
        if (!EnsureMetaFolderExists()) return;

        string fileName  = GetUniqueAssetFileName("Nowy Upgrade");
        string assetPath = $"{MetaUpgradeTools.SCAN_PATH}/{fileName}.asset";

        var newUpgrade = CreateInstance<MetaUpgradeSO>();
        newUpgrade.upgradeName   = fileName;
        newUpgrade.id            = GetUniqueUpgradeId(fileName);
        newUpgrade.cost          = 100;
        newUpgrade.effectType    = MetaEffectType.None;
        newUpgrade.prerequisites = new List<MetaUpgradeSO>();

        AssetDatabase.CreateAsset(newUpgrade, assetPath);
        AssetDatabase.SaveAssets();

        RegisterNewUpgradeInGraphAndManager(newUpgrade, graphPos, focusName: true);
    }

    /// <summary>
    /// Duplikuje wskazany węzeł jako kolejny Tier (np. "Optimized work I" -> "Optimized work II"),
    /// umieszczając go po prawej stronie i automatycznie podpinając oryginał jako wymaganie.
    /// </summary>
    private void DuplicateNodeAsNextTier(UpgradeNode sourceNode)
    {
        if (sourceNode?.upgrade == null) return;
        if (!EnsureMetaFolderExists()) return;

        var src = sourceNode.upgrade;
        string nextDisplayName = IncrementTierInName(
            string.IsNullOrWhiteSpace(src.upgradeName) ? src.name : src.upgradeName);

        string fileName  = GetUniqueAssetFileName(nextDisplayName);
        string assetPath = $"{MetaUpgradeTools.SCAN_PATH}/{fileName}.asset";

        var copy = CreateInstance<MetaUpgradeSO>();
        copy.upgradeName    = nextDisplayName;
        copy.id             = GetUniqueUpgradeId(fileName);
        copy.description    = src.description;
        copy.icon           = src.icon;
        copy.cost           = src.cost;
        copy.isUnlocked     = false;
        copy.effectType     = src.effectType;
        copy.effectValue    = src.effectValue;
        copy.effectValue2   = src.effectValue2;
        copy.targetBuilding = src.targetBuilding;
        copy.prerequisites  = new List<MetaUpgradeSO> { src };

        AssetDatabase.CreateAsset(copy, assetPath);
        AssetDatabase.SaveAssets();

        // Znajdź wolną pozycję po prawej stronie węzła źródłowego
        Vector2 targetPos = new Vector2(sourceNode.rect.x + NODE_SPACING_X, sourceNode.rect.y);
        while (nodes.Any(n => n?.upgrade != null && Vector2.Distance(n.rect.position, targetPos) < 30f))
        {
            targetPos.y += NODE_SPACING_Y;
        }

        RegisterNewUpgradeInGraphAndManager(copy, targetPos, focusName: true);
    }

    private void RegisterNewUpgradeInGraphAndManager(MetaUpgradeSO newUpgrade, Vector2 graphPos, bool focusName)
    {
        if (targetManager != null)
        {
            Undo.RecordObject(targetManager, "Dodaj nowy MetaUpgrade do Managera");
            if (targetManager.allUpgrades == null)
                targetManager.allUpgrades = new List<MetaUpgradeSO>();
            if (!targetManager.allUpgrades.Contains(newUpgrade))
                targetManager.allUpgrades.Add(newUpgrade);
            EditorUtility.SetDirty(targetManager);
        }

        var node = new UpgradeNode
        {
            upgrade = newUpgrade,
            guid    = GetUpgradeGuid(newUpgrade),
            rect    = new Rect(graphPos.x, graphPos.y, NODE_WIDTH, NODE_HEIGHT)
        };

        nodes.Add(node);
        nodeByUpgrade[newUpgrade] = node;
        SaveNodePositionToPrefs(node);
        UpdateManagerSyncStatus();

        selectedNodes.Clear();
        selectedNodes.Add(node);
        selectedNode = node;
        focusNameFieldNextFrame = focusName;

        EditorGUIUtility.PingObject(newUpgrade);
        Repaint();
    }

    /// <summary>
    /// Zmienia nazwę pliku .asset na dysku oraz generuje unikalne ID (snake_case) wg pola upgradeName.
    /// </summary>
    private void SyncAssetFileNameAndIdFromName(UpgradeNode node)
    {
        if (node?.upgrade == null) return;
        var upgrade = node.upgrade;

        string desiredName = string.IsNullOrWhiteSpace(upgrade.upgradeName)
            ? upgrade.name
            : upgrade.upgradeName.Trim();

        foreach (char c in Path.GetInvalidFileNameChars())
            desiredName = desiredName.Replace(c.ToString(), "");

        if (string.IsNullOrEmpty(desiredName))
        {
            ShowNotification(new GUIContent("⚠ Wpisz najpierw poprawną nazwę upgradu"));
            return;
        }

        string currentPath = AssetDatabase.GetAssetPath(upgrade);
        string currentFileName = Path.GetFileNameWithoutExtension(currentPath);

        Undo.RecordObject(upgrade, "Zaktualizuj ID i nazwę pliku MetaUpgrade");
        upgrade.id = GetUniqueUpgradeId(desiredName, ignoreUpgrade: upgrade);
        EditorUtility.SetDirty(upgrade);

        if (!string.IsNullOrEmpty(currentPath) && !string.Equals(currentFileName, desiredName, StringComparison.Ordinal))
        {
            string folder = Path.GetDirectoryName(currentPath)?.Replace('\\', '/') ?? MetaUpgradeTools.SCAN_PATH;
            string finalFileName = desiredName;
            int suffix = 1;
            while (true)
            {
                var existing = AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>($"{folder}/{finalFileName}.asset");
                if (existing == null || existing == upgrade) break;
                finalFileName = $"{desiredName}_{suffix}";
                suffix++;
            }

            string renameError = AssetDatabase.RenameAsset(currentPath, finalFileName);
            if (!string.IsNullOrEmpty(renameError))
                Debug.LogWarning($"[MetaUpgradeGraph] Nie udało się zmienić nazwy pliku: {renameError}");
        }

        AssetDatabase.SaveAssets();
        SaveNodePositionToPrefs(node);
        EditorGUIUtility.PingObject(upgrade);
        ShowNotification(new GUIContent($"✓ ID: {upgrade.id}"));
        Repaint();
    }

    private string IncrementTierInName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Nowy Upgrade II";

        // Dopasuj rzymskie numery na końcu lub przed myślnikiem (np. "Optimized work I" lub "Efficent Mining II - mountain")
        var romanMap = new (string from, string to)[]
        {
            ("VIII", "IX"), ("VII", "VIII"), ("VI", "VII"), ("V", "VI"),
            ("IIII", "V"),  ("IV", "V"),     ("III", "IV"), ("II", "III"), ("I", "II")
        };

        foreach (var pair in romanMap)
        {
            string pattern = $@"\b{pair.from}\b";
            var matches = Regex.Matches(name, pattern);
            if (matches.Count > 0)
            {
                Match lastMatch = matches[matches.Count - 1];
                return name.Substring(0, lastMatch.Index) + pair.to + name.Substring(lastMatch.Index + lastMatch.Length);
            }
        }

        // Dopasuj liczbę arabską na końcu
        var numMatch = Regex.Match(name, @"(\d+)\s*$");
        if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out int num))
        {
            return name.Substring(0, numMatch.Index) + (num + 1);
        }

        return name.TrimEnd() + " II";
    }

    private void AddExistingUpgrade()
    {
        EditorGUIUtility.ShowObjectPicker<MetaUpgradeSO>(null, false, "", controlID: 12345);
        waitingForObjectPicker = true;
    }

    private void HandleObjectPickerEvents()
    {
        if (!waitingForObjectPicker || Event.current == null) return;

        if (Event.current.commandName == "ObjectSelectorClosed")
        {
            waitingForObjectPicker = false;
            var picked = EditorGUIUtility.GetObjectPickerObject() as MetaUpgradeSO;
            if (picked != null)
            {
                Vector2 spawnPos = contextMenuGraphPos;
                QueueSafeAction(() =>
                {
                    if (nodeByUpgrade.TryGetValue(picked, out var existingNode))
                    {
                        selectedNodes.Clear();
                        selectedNodes.Add(existingNode);
                        selectedNode = existingNode;
                        FrameNodes(new List<UpgradeNode> { existingNode });
                    }
                    else
                    {
                        RegisterNewUpgradeInGraphAndManager(picked, spawnPos, focusName: false);
                    }
                });
            }
        }
    }

    private void RemoveNodeFromManager(UpgradeNode node)
    {
        if (targetManager == null || node?.upgrade == null) return;

        Undo.RecordObject(targetManager, "Usuń MetaUpgrade z Managera");
        targetManager.allUpgrades?.Remove(node.upgrade);
        EditorUtility.SetDirty(targetManager);

        UpdateManagerSyncStatus();
        Repaint();
    }

    private void DeleteNodeAndAsset(UpgradeNode node)
    {
        if (node?.upgrade == null) return;

        var upgradeToDelete = node.upgrade;
        string displayName = string.IsNullOrWhiteSpace(upgradeToDelete.upgradeName)
            ? upgradeToDelete.name
            : upgradeToDelete.upgradeName;

        bool confirm = EditorUtility.DisplayDialog(
            "Usuń plik SO",
            $"TRWALE usunąć '{displayName}'?\n\n" +
            "Plik zostanie usunięty z dysku, z listy Managera oraz z wymagań innych upgradów.\n" +
            "Tej operacji nie można cofnąć.",
            "Usuń trwale", "Anuluj");

        if (!confirm) return;

        // 1. Usuń referencje z prerequisites innych węzłów
        foreach (var other in nodes)
        {
            if (other?.upgrade == null || other == node || other.upgrade.prerequisites == null)
                continue;

            if (other.upgrade.prerequisites.Contains(upgradeToDelete))
            {
                Undo.RecordObject(other.upgrade, "Usuń referencję do kasowanego MetaUpgrade");
                other.upgrade.prerequisites.RemoveAll(p => p == upgradeToDelete);
                EditorUtility.SetDirty(other.upgrade);
            }
        }

        // 2. Usuń z Managera
        if (targetManager != null && targetManager.allUpgrades != null)
        {
            Undo.RecordObject(targetManager, "Usuń MetaUpgrade z Managera");
            targetManager.allUpgrades.Remove(upgradeToDelete);
            EditorUtility.SetDirty(targetManager);
        }

        // 3. Usuń z kolekcji grafu
        selectedNodes.Remove(node);
        if (selectedNode == node)
            selectedNode = selectedNodes.FirstOrDefault();

        nodes.Remove(node);
        nodeByUpgrade.Remove(upgradeToDelete);

        // 4. Usuń plik z dysku
        string path = AssetDatabase.GetAssetPath(upgradeToDelete);
        if (!string.IsNullOrEmpty(path))
            AssetDatabase.DeleteAsset(path);

        UpdateManagerSyncStatus();
        Repaint();
    }

    // =========================================================================
    // ŁADOWANIE DANYCH, SYNCHRONIZACJA I AUTO LAYOUT
    // =========================================================================

    private void CleanupDestroyedNodes()
    {
        int removed = nodes.RemoveAll(n => n == null || n.upgrade == null);
        if (removed > 0)
        {
            selectedNodes.RemoveWhere(n => n == null || n.upgrade == null);
            if (selectedNode != null && selectedNode.upgrade == null)
                selectedNode = selectedNodes.FirstOrDefault();
            RebuildLookupDictionaries();
        }
    }

    private void RebuildLookupDictionaries()
    {
        nodeByUpgrade.Clear();
        foreach (var n in nodes)
        {
            if (n?.upgrade != null)
                nodeByUpgrade[n.upgrade] = n;
        }
    }

    private void RefreshMetaMapConfigLinks()
    {
        mapConfigLinks.Clear();
        primaryMapConfig = null;

        string[] guids = AssetDatabase.FindAssets("t:MetaMapConfig");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<MetaMapConfig>(path);
            if (config == null) continue;

            if (primaryMapConfig == null)
                primaryMapConfig = config;

            if (config.expansions != null)
            {
                foreach (var exp in config.expansions)
                {
                    if (exp.requiredUpgrade == null) continue;
                    if (!mapConfigLinks.TryGetValue(exp.requiredUpgrade, out var list))
                        mapConfigLinks[exp.requiredUpgrade] = list = new List<string>();
                    list.Add($"🗺 Ekspansja ({exp.chunkCoordinate.x}, {exp.chunkCoordinate.y})");
                }
            }

            if (config.globalModifiers != null)
            {
                foreach (var mod in config.globalModifiers)
                {
                    if (mod.requiredUpgrade == null) continue;
                    if (!mapConfigLinks.TryGetValue(mod.requiredUpgrade, out var list))
                        mapConfigLinks[mod.requiredUpgrade] = list = new List<string>();
                    string modLabel = string.IsNullOrWhiteSpace(mod.name) ? "Modyfikator" : mod.name;
                    list.Add($"🗺 {modLabel} ({mod.targetChunk.x}, {mod.targetChunk.y})");
                }
            }
        }
    }

    private void UpdateManagerSyncStatus()
    {
        unsyncedManagerCount = 0;
        if (targetManager == null) return;

        var managerSet = new HashSet<MetaUpgradeSO>(
            (targetManager.allUpgrades ?? new List<MetaUpgradeSO>()).Where(u => u != null));

        foreach (var n in nodes)
        {
            if (n?.upgrade != null && !managerSet.Contains(n.upgrade))
                unsyncedManagerCount++;
        }

        if (targetManager.allUpgrades != null && targetManager.allUpgrades.Any(u => u == null))
            unsyncedManagerCount++;
    }

    private void RefreshNodes(bool preservePositions = true)
    {
        MetaUpgradeSO prevSelectedSO = selectedNode?.upgrade;
        var prevSelectedSOs = new HashSet<MetaUpgradeSO>(
            selectedNodes.Where(n => n?.upgrade != null).Select(n => n.upgrade));

        nodes.Clear();
        nodeByUpgrade.Clear();
        selectedNodes.Clear();
        selectedNode = null;

        RefreshMetaMapConfigLinks();

        // 1. Załaduj wszystkie MetaUpgradeSO z folderu projektu (działa na każdej scenie)
        var allUpgrades = MetaUpgradeTools.LoadAllUpgrades(quiet: true);

        // 2. Jeśli na scenie jest MetaUpgradeManager i ma dodatkowe SO spoza folderu — dołącz je
        if (targetManager == null)
            targetManager = FindFirstObjectByType<MetaUpgradeManager>();

        if (targetManager != null && targetManager.allUpgrades != null)
        {
            foreach (var u in targetManager.allUpgrades)
            {
                if (u != null && !allUpgrades.Contains(u))
                    allUpgrades.Add(u);
            }
        }

        bool anyLoadedFromPrefs = false;
        var unpositionedNodes = new List<UpgradeNode>();

        foreach (var upgrade in allUpgrades)
        {
            if (upgrade == null) continue;
            string guid = GetUpgradeGuid(upgrade);

            var node = new UpgradeNode
            {
                upgrade = upgrade,
                guid    = guid,
                rect    = new Rect(0, 0, NODE_WIDTH, NODE_HEIGHT)
            };

            if (preservePositions && TryLoadNodePositionFromPrefs(guid, out Vector2 savedPos))
            {
                node.rect.position = savedPos;
                anyLoadedFromPrefs = true;
            }
            else
            {
                unpositionedNodes.Add(node);
            }

            nodes.Add(node);
            nodeByUpgrade[upgrade] = node;

            if (prevSelectedSOs.Contains(upgrade))
                selectedNodes.Add(node);
            if (upgrade == prevSelectedSO)
                selectedNode = node;
        }

        if (!anyLoadedFromPrefs && nodes.Count > 0)
        {
            // Pierwsze uruchomienie — ułóż cały graf automatycznie i zapisz w EditorPrefs
            AutoLayout();
            SaveAllNodePositionsToPrefs();
        }
        else if (unpositionedNodes.Count > 0)
        {
            // Tylko nowo odkryte węzły (bez zapisanej pozycji) umieść w wolnym miejscu
            PlaceUnpositionedNodes(unpositionedNodes);
            foreach (var n in unpositionedNodes)
                SaveNodePositionToPrefs(n);
        }

        UpdateManagerSyncStatus();
        Repaint();
    }

    private void PlaceUnpositionedNodes(List<UpgradeNode> unpositioned)
    {
        float maxBottom = 40f;
        foreach (var n in nodes)
        {
            if (!unpositioned.Contains(n))
                maxBottom = Mathf.Max(maxBottom, n.rect.yMax + 40f);
        }

        foreach (var n in unpositioned)
        {
            var firstPrereq = n.upgrade.prerequisites?.FirstOrDefault(p => p != null && nodeByUpgrade.ContainsKey(p));
            if (firstPrereq != null && nodeByUpgrade.TryGetValue(firstPrereq, out var parentNode))
            {
                n.rect.position = new Vector2(parentNode.rect.x + NODE_SPACING_X, parentNode.rect.y);
            }
            else
            {
                n.rect.position = new Vector2(40f, maxBottom);
                maxBottom += NODE_SPACING_Y;
            }
        }
    }

    /// <summary>
    /// Automatyczny layout oparty na warstwach i spójnych składowych (drzewach).
    /// Gwarantuje brak nakładania się bloków oraz grupuje powiązane łańcuchy ulepszeń razem.
    /// </summary>
    private void AutoLayout()
    {
        CleanupDestroyedNodes();
        if (nodes.Count == 0) return;

        var parents  = new Dictionary<UpgradeNode, List<UpgradeNode>>();
        var children = new Dictionary<UpgradeNode, List<UpgradeNode>>();

        foreach (var n in nodes)
        {
            parents[n]  = new List<UpgradeNode>();
            children[n] = new List<UpgradeNode>();
        }

        foreach (var n in nodes)
        {
            if (n.upgrade.prerequisites == null) continue;
            foreach (var req in n.upgrade.prerequisites)
            {
                if (req != null && nodeByUpgrade.TryGetValue(req, out var pNode) && pNode != n)
                {
                    if (!parents[n].Contains(pNode))  parents[n].Add(pNode);
                    if (!children[pNode].Contains(n)) children[pNode].Add(n);
                }
            }
        }

        // Wyznacz warstwy (kolumny) najdłuższą ścieżką z ochroną przed cyklami
        var layers = new Dictionary<UpgradeNode, int>();
        foreach (var n in nodes) layers[n] = 0;

        bool changed = true;
        int safeguard = nodes.Count * 2 + 2;

        while (changed && safeguard > 0)
        {
            changed = false;
            safeguard--;
            foreach (var n in nodes)
            {
                int maxParentLayer = -1;
                foreach (var p in parents[n])
                {
                    if (layers[p] > maxParentLayer)
                        maxParentLayer = layers[p];
                }

                if (layers[n] <= maxParentLayer && maxParentLayer + 1 < nodes.Count)
                {
                    layers[n] = maxParentLayer + 1;
                    changed   = true;
                }
            }
        }

        // Podziel graf na spójne składowe (osobne gałęzie/drzewa), aby powiązane ulepszenia leżały obok siebie
        var visited = new HashSet<UpgradeNode>();
        var components = new List<List<UpgradeNode>>();

        foreach (var n in nodes.OrderBy(n => n.upgrade.upgradeName))
        {
            if (!visited.Add(n)) continue;
            var comp  = new List<UpgradeNode>();
            var queue = new Queue<UpgradeNode>();
            queue.Enqueue(n);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                comp.Add(curr);
                foreach (var p in parents[curr])
                    if (visited.Add(p)) queue.Enqueue(p);
                foreach (var c in children[curr])
                    if (visited.Add(c)) queue.Enqueue(c);
            }
            components.Add(comp);
        }

        // Sortuj składowe: najpierw większe drzewa, na końcu pojedyncze węzły
        components = components
            .OrderByDescending(c => c.Count > 1)
            .ThenBy(c => c.Min(n => n.upgrade.upgradeName))
            .ToList();

        float startX = 40f;
        float currentComponentTopY = 40f;

        foreach (var comp in components)
        {
            int maxLayer = comp.Max(n => layers[n]);
            var nodesByLayer = new List<List<UpgradeNode>>();
            for (int i = 0; i <= maxLayer; i++)
                nodesByLayer.Add(new List<UpgradeNode>());

            foreach (var n in comp)
                nodesByLayer[layers[n]].Add(n);

            for (int i = 0; i <= maxLayer; i++)
            {
                nodesByLayer[i] = nodesByLayer[i]
                    .OrderByDescending(n => children[n].Count)
                    .ThenBy(n => n.upgrade.upgradeName)
                    .ToList();

                float curY = currentComponentTopY;
                foreach (var n in nodesByLayer[i])
                {
                    n.rect.y = curY;
                    curY += NODE_SPACING_Y;
                }
            }

            // Relaksacja wewnątrz składowej (wyrównanie dzieci i rodziców)
            for (int iter = 0; iter < 4; iter++)
            {
                for (int i = 1; i <= maxLayer; i++)
                {
                    foreach (var n in nodesByLayer[i])
                    {
                        if (parents[n].Count > 0)
                            n.rect.y = parents[n].Average(p => p.rect.y);
                    }
                    nodesByLayer[i] = nodesByLayer[i].OrderBy(n => n.rect.y).ToList();
                    ResolveOverlaps(nodesByLayer[i], currentComponentTopY);
                }

                for (int i = maxLayer - 1; i >= 0; i--)
                {
                    foreach (var n in nodesByLayer[i])
                    {
                        if (children[n].Count > 0)
                            n.rect.y = children[n].Average(c => c.rect.y);
                    }
                    nodesByLayer[i] = nodesByLayer[i].OrderBy(n => n.rect.y).ToList();
                    ResolveOverlaps(nodesByLayer[i], currentComponentTopY);
                }
            }

            float maxYInComp = currentComponentTopY;
            foreach (var n in comp)
            {
                n.rect.x      = startX + layers[n] * NODE_SPACING_X;
                n.rect.width  = NODE_WIDTH;
                n.rect.height = NODE_HEIGHT;
                maxYInComp    = Mathf.Max(maxYInComp, n.rect.y + NODE_SPACING_Y);
            }

            currentComponentTopY = maxYInComp + 25f;
        }

        Repaint();
    }

    private void ResolveOverlaps(List<UpgradeNode> layerNodes, float startY)
    {
        float currentY = startY;
        foreach (var n in layerNodes)
        {
            if (n.rect.y < currentY)
                n.rect.y = currentY;
            currentY = n.rect.y + NODE_SPACING_Y;
        }
    }

    // =========================================================================
    // CENTROWANIE WIDOKU (FRAME) I WYSZUKIWANIE
    // =========================================================================

    private void FrameSelectionOrAll()
    {
        if (selectedNodes.Count > 0)
            FrameNodes(selectedNodes.ToList());
        else
            FrameNodes(nodes);
    }

    private void FrameNodes(List<UpgradeNode> targetNodes)
    {
        var valid = targetNodes?.Where(n => n?.upgrade != null).ToList();
        if (valid == null || valid.Count == 0)
        {
            panOffset = new Vector2(40f, 40f);
            zoomScale = 1f;
            SaveViewPrefs();
            Repaint();
            return;
        }

        float minX = valid.Min(n => n.rect.xMin);
        float minY = valid.Min(n => n.rect.yMin);
        float maxX = valid.Max(n => n.rect.xMax);
        float maxY = valid.Max(n => n.rect.yMax);

        Rect bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        float viewW = Mathf.Max(200f, position.width - (selectedNode != null ? SIDE_PANEL_WIDTH : 0f));
        float viewH = Mathf.Max(200f, position.height - TOOLBAR_HEIGHT);

        float padding = 80f;
        float zoomX = (viewW - padding * 2f) / Mathf.Max(1f, bounds.width);
        float zoomY = (viewH - padding * 2f) / Mathf.Max(1f, bounds.height);
        zoomScale = Mathf.Clamp(Mathf.Min(zoomX, zoomY), 0.35f, 1.35f);

        Vector2 boundsCenter = bounds.center;
        panOffset = new Vector2(viewW * 0.5f, viewH * 0.5f) - boundsCenter * zoomScale;

        SaveViewPrefs();
        Repaint();
    }

    private bool IsNodeMatchingSearch(UpgradeNode node)
    {
        if (node?.upgrade == null) return false;
        if (string.IsNullOrWhiteSpace(searchQuery)) return true;

        string q = searchQuery.Trim();
        var u = node.upgrade;

        if (!string.IsNullOrEmpty(u.upgradeName) && u.upgradeName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (!string.IsNullOrEmpty(u.id) && u.id.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (!string.IsNullOrEmpty(u.description) && u.description.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (GetNodeEffectSummary(u).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (u.targetBuilding != null && u.targetBuilding.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    // =========================================================================
    // HELPERS — TRANSFORMACJE
    // =========================================================================

    private Vector2 GraphToScreen(Vector2 graphPos)
    {
        return graphPos * zoomScale + panOffset;
    }

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
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var n = nodes[i];
            if (n?.upgrade == null) continue;
            Rect screenRect = GraphRectToScreen(n.rect);
            if (screenRect.Contains(screenPos))
                return n;
        }
        return null;
    }

    // =========================================================================
    // HELPERS — FORMATOWANIE EFEKTÓW I META MAP CONFIG
    // =========================================================================

    private string GetNodeEffectSummary(MetaUpgradeSO upgrade)
    {
        if (upgrade == null) return "—";

        bool hasMapLinks = mapConfigLinks.TryGetValue(upgrade, out var links) && links.Count > 0;
        if (upgrade.effectType == MetaEffectType.None)
        {
            if (hasMapLinks)
                return string.Join(", ", links);
            return "— (brak efektu)";
        }

        string formatted = FormatEffect(upgrade);
        if (hasMapLinks)
            return $"{formatted} + {links[0]}";
        return formatted;
    }

    private string FormatEffect(MetaUpgradeSO upgrade)
    {
        float v = upgrade.effectValue;
        string bld = upgrade.targetBuilding != null ? $" ({upgrade.targetBuilding.name})" : "";

        return upgrade.effectType switch
        {
            MetaEffectType.StartingEther                        => $"+{v} Kryształ Eteru",
            MetaEffectType.StartingWood                         => $"+{v} drewna",
            MetaEffectType.StartingStone                        => $"+{v} kamienia",
            MetaEffectType.StartingIron                         => $"+{v} żelaza",
            MetaEffectType.StartingCoal                         => $"+{v} węgla",
            MetaEffectType.StartingFood                         => $"+{v} jedzenia",
            MetaEffectType.BonusShifts                          => $"+{v} zmian",
            MetaEffectType.BonusWorkersPerShift                 => $"+{v} prac./zmianę",
            MetaEffectType.BuildingPassiveEfficiency            => $"+{v * 100:F0}% pasywnie",
            MetaEffectType.BuildingWorkerEfficiencyBonus        => $"+{v * 100:F0}% za prac.",
            MetaEffectType.BuildingTerrainBonusMultiplier       => $"x{v} bonus terenu{bld}",
            MetaEffectType.SpecificBuildingUpgradeCostReduction => $"-{v * 100:F0}% koszt ulepsz.{bld}",
            MetaEffectType.SpecificBuildingProductionBonus      => $"+{v * 100:F0}% produkcji{bld}",
            MetaEffectType.HousingStartPopulation               => $"+{v} mieszk. (start)",
            MetaEffectType.HousingMaxResidents                  => $"+{v} max pop (wszyscy)",
            MetaEffectType.HousingMaxResidents_Humans           => $"+{v} max pop (ludzie)",
            MetaEffectType.HousingMaxResidents_Elves            => $"+{v} max pop (elfy)",
            MetaEffectType.HousingMaxResidents_Dwarves          => $"+{v} max pop (krasnoludy)",
            MetaEffectType.EliteChanceBoost                     => $"+{v * 100:F0}% elit (od f.{upgrade.effectValue2})",
            MetaEffectType.EnemySurvivorPenaltyArmor            => $"-{v * 100:F0}% pancerz ocalałych",
            MetaEffectType.EnemySurvivorPenaltySpeed            => $"-{v * 100:F0}% prędkość ocalałych",
            MetaEffectType.EnemySurvivorPenaltyDodge            => $"-{v * 100:F0}% dodge ocalałych",
            MetaEffectType.TowerNoAmmoNightPenalty              => $"x{v} stats bez amunicji",
            MetaEffectType.ExpansionCostReduction               => $"-{v * 100:F0}% koszt ekspansji",
            MetaEffectType.CityBaseHealth                       => $"+{v} HP miasta",
            MetaEffectType.GateBaseHP                           => $"+{v} HP bram",
            MetaEffectType.WallSystem_Solution1                 => "Mury: Baza",
            MetaEffectType.WallSystem_Solution2                 => "Mury: Linia Frontu",
            MetaEffectType.UniqueChunkUnlock                    => "Unikatowy chunk",
            MetaEffectType.RuneDropChance_Uncommon              => $"+{v * 100:F0}% szansy run (Uncommon)",
            MetaEffectType.RuneDropChance_Rare                  => $"+{v * 100:F0}% szansy run (Rare)",
            MetaEffectType.RuneDropChance_Legendary             => $"+{v * 100:F0}% szansy run (Legendary)",
            MetaEffectType.RuneDropChance_Cursed                => $"+{v * 100:F0}% szansy run (Cursed)",
            MetaEffectType.RuneTowerLimit                       => $"+{v} limit wież runicznych",
            _                                                   => upgrade.effectType.ToString()
        };
    }

    private string GetEffectValueLabel(MetaEffectType type) => type switch
    {
        MetaEffectType.StartingEther
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
            or MetaEffectType.GateBaseHP
            or MetaEffectType.RuneTowerLimit   => "Wartość (całkowita)",
        MetaEffectType.BuildingPassiveEfficiency
            or MetaEffectType.BuildingWorkerEfficiencyBonus
            or MetaEffectType.EliteChanceBoost
            or MetaEffectType.EnemySurvivorPenaltyArmor
            or MetaEffectType.EnemySurvivorPenaltySpeed
            or MetaEffectType.EnemySurvivorPenaltyDodge
            or MetaEffectType.ExpansionCostReduction
            or MetaEffectType.SpecificBuildingUpgradeCostReduction
            or MetaEffectType.SpecificBuildingProductionBonus
            or MetaEffectType.RuneDropChance_Uncommon
            or MetaEffectType.RuneDropChance_Rare
            or MetaEffectType.RuneDropChance_Legendary
            or MetaEffectType.RuneDropChance_Cursed => "Wartość (0.0–1.0 = %)",
        MetaEffectType.TowerNoAmmoNightPenalty
            or MetaEffectType.BuildingTerrainBonusMultiplier => "Mnożnik",
        _ => "effectValue"
    };
}
#endif