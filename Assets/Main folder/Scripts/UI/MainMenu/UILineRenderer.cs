using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Styl linii połączenia w grafie meta-ulepszeń.
/// </summary>
public enum ConnectionStyle
{
    Active,     // Wykupione połączenie: jasne złoto / zieleń, pełna linia, grubość 4px
    Available,  // Dostępne połączenie: błękit / cyan, pełna linia, grubość 3.2px
    Mystery,    // Tajemnicze połączenie: półprzezroczysta linia przerywana (dashed), grubość 2.2px
    Hidden      // Ukryte we mgle: niewidoczne (pomijane w siatce)
}

/// <summary>
/// Dane pojedynczej krawędzi łączącej węzeł nadrzędny z podrzędnym.
/// </summary>
[Serializable]
public struct GraphConnection
{
    public Vector2 startPoint;
    public Vector2 endPoint;
    public ConnectionStyle style;

    public GraphConnection(Vector2 start, Vector2 end, ConnectionStyle style)
    {
        this.startPoint = start;
        this.endPoint = end;
        this.style = style;
    }
}

/// <summary>
/// Proceduralny renderer linii połączeń dla grafu 2D (UGUI).
/// Dziedziczy po MaskableGraphic, generując gładkie krzywe Beziera w pojedynczym wywołaniu rysowania (single draw-call mesh).
/// Automatycznie wspiera RectMask2D Viewportu.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UILineRenderer : MaskableGraphic
{
    protected override void Awake()
    {
        EnsureCanvasRenderer();
        base.Awake();
    }

    protected override void OnEnable()
    {
        EnsureCanvasRenderer();
        base.OnEnable();
    }

#if UNITY_EDITOR
    protected override void Reset()
    {
        EnsureCanvasRenderer();
        base.Reset();
    }

    protected override void OnValidate()
    {
        EnsureCanvasRenderer();
        base.OnValidate();
    }
#endif

    private void EnsureCanvasRenderer()
    {
        if (GetComponent<CanvasRenderer>() == null)
        {
            gameObject.AddComponent<CanvasRenderer>();
        }
    }

    [Header("Konfiguracja Stylów")]
    public static readonly Color ColorActive = new Color(0.95f, 0.78f, 0.20f, 1.0f);   // Jasne złoto
    public static readonly Color ColorAvailable = new Color(0.20f, 0.80f, 1.00f, 1.0f); // Jasny błękit / cyan
    public static readonly Color ColorMystery = new Color(0.55f, 0.60f, 0.70f, 0.40f);   // Półprzezroczysty grafit

    public const float ThicknessActive = 4.0f;
    public const float ThicknessAvailable = 3.2f;
    public const float ThicknessMystery = 2.2f;

    [Range(6, 32)]
    [SerializeField] private int bezierSegments = 16;

    private readonly List<GraphConnection> connections = new List<GraphConnection>();

    public IReadOnlyList<GraphConnection> Connections => connections;
    public int BezierSegments => bezierSegments;

    /// <summary>
    /// Ustawia zestaw połączeń do wyrenderowania i wymusza przebudowę siatki w następnej klatce.
    /// </summary>
    public void SetConnections(IEnumerable<GraphConnection> newConnections)
    {
        connections.Clear();
        if (newConnections != null)
        {
            connections.AddRange(newConnections);
        }
        SetVerticesDirty();
    }

    /// <summary>
    /// Dodaje pojedyncze połączenie.
    /// </summary>
    public void AddConnection(GraphConnection connection)
    {
        connections.Add(connection);
        SetVerticesDirty();
    }

    /// <summary>
    /// Czyści wszystkie połączenia.
    /// </summary>
    public void ClearConnections()
    {
        connections.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (connections == null || connections.Count == 0) return;

        foreach (var conn in connections)
        {
            if (conn.style == ConnectionStyle.Hidden) continue;

            DrawBezierCurve(vh, conn);
        }
    }

    /// <summary>
    /// Generuje geometrię krzywej Beziera w buforze VertexHelper.
    /// </summary>
    private void DrawBezierCurve(VertexHelper vh, GraphConnection conn)
    {
        Vector2 p0 = conn.startPoint;
        Vector2 p3 = conn.endPoint;

        // Punkty kontrolne krzywej sześciennej Beziera (Cubic Bezier)
        float dx = Mathf.Max(Mathf.Abs(p3.x - p0.x) * 0.5f, 40f);
        Vector2 p1 = p0 + new Vector2(dx, 0f);
        Vector2 p2 = p3 - new Vector2(dx, 0f);

        Color lineColor;
        float thickness;
        bool isDashed = false;

        switch (conn.style)
        {
            case ConnectionStyle.Active:
                lineColor = ColorActive;
                thickness = ThicknessActive;
                break;
            case ConnectionStyle.Available:
                lineColor = ColorAvailable;
                thickness = ThicknessAvailable;
                break;
            case ConnectionStyle.Mystery:
                lineColor = ColorMystery;
                thickness = ThicknessMystery;
                isDashed = true; // Linia przerywana dla tajemniczego połączenia
                break;
            default:
                return;
        }

        int segCount = Mathf.Max(4, bezierSegments);
        Vector2 prevPoint = p0;

        for (int i = 1; i <= segCount; i++)
        {
            float t = i / (float)segCount;
            Vector2 currentPoint = EvaluateCubicBezier(p0, p1, p2, p3, t);

            // Dla linii przerywanej (dashed) co drugi segment jest pomijany
            if (isDashed && (i % 2 == 0))
            {
                prevPoint = currentPoint;
                continue;
            }

            DrawSegment(vh, prevPoint, currentPoint, thickness, lineColor);
            prevPoint = currentPoint;
        }
    }

    /// <summary>
    /// Rysuje pojedynczy prostokątny odcinek linii z prostopadłą grubością.
    /// </summary>
    private void DrawSegment(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color color)
    {
        Vector2 dir = end - start;
        float len = dir.magnitude;
        if (len < 0.001f) return;

        Vector2 normDir = dir / len;
        Vector2 normal = new Vector2(-normDir.y, normDir.x) * (thickness * 0.5f);

        Vector2 v0 = start + normal;
        Vector2 v1 = start - normal;
        Vector2 v2 = end - normal;
        Vector2 v3 = end + normal;

        int startIndex = vh.currentVertCount;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        vert.position = v0;
        vh.AddVert(vert);

        vert.position = v1;
        vh.AddVert(vert);

        vert.position = v2;
        vh.AddVert(vert);

        vert.position = v3;
        vh.AddVert(vert);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }

    /// <summary>
    /// Matematyczne wyliczenie punktu na sześciennej krzywej Beziera.
    /// B(t) = (1-t)^3 * P0 + 3(1-t)^2 * t * P1 + 3(1-t) * t^2 * P2 + t^3 * P3
    /// </summary>
    public static Vector2 EvaluateCubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        float uu = u * u;
        float uuu = uu * u;
        float tt = t * t;
        float ttt = tt * t;

        Vector2 point = uuu * p0;
        point += 3f * uu * t * p1;
        point += 3f * u * tt * p2;
        point += ttt * p3;

        return point;
    }
}
