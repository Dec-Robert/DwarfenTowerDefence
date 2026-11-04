using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Proceduralny mesh pojedynczego heksa terenu: górna powierzchnia + boczne ściany dopasowane do sąsiadów.
///
/// KONWENCJE (muszą zgadzać się z HexGridMath.AxialToWorld - heksy "pointy-top"):
///   - Pivot meshu = środek górnej powierzchni (y = 0). Ściany schodzą w dół (y &lt; 0).
///   - Kierunek k (0..5) odpowiada sąsiadowi pod kątem 60*k stopni (od +X w stronę +Z),
///     patrz <see cref="Directions"/>. Krawędź k leży pomiędzy narożnikami k oraz k+1.
///   - Submesh 0 = góra, Submesh 1 = boki (patrz <see cref="TopSubmesh"/>, <see cref="SideSubmesh"/>).
///
/// ZASADA WŁASNOŚCI ŚCIAN (brak podwójnych ścian):
///   Ścianę na wspólnej krawędzi generuje WYŁĄCZNIE wyższy heks. Schodzi ona do górnej powierzchni
///   niższego sąsiada (+ opcjonalny, ukryty "skirt" pod spodem). Sąsiedzi o równej wysokości nie
///   generują widocznych ścian. Brak sąsiada = krawędź mapy -> ściana do stałego poziomu dna.
/// </summary>
public static class HexTerrainMeshBuilder
{
    public const int TopSubmesh = 0;
    public const int SideSubmesh = 1;

    /// <summary>Typ krawędzi zapisywany w UV2.y bocznych ścian (kontrakt dla shadera HexSideTerrain).</summary>
    public enum EdgeKind : byte
    {
        None = 255,  // brak ściany (sąsiad wyższy)
        Cliff = 0,   // uskok do niższego sąsiada (widoczny)
        Skirt = 1,   // sąsiad tej samej wysokości - ukryty pas pod powierzchnią (widoczny tylko przy podniesieniu heksa)
        MapEdge = 2  // brak sąsiada - zewnętrzna krawędź mapy
    }

    /// <summary>Opis jednej z 6 krawędzi heksa.</summary>
    public struct EdgeSpec
    {
        public EdgeKind kind;
        /// <summary>Lokalne Y (względem góry heksa, &lt;= 0) poziomu, do którego ściana jest WIDOCZNA (powierzchnia sąsiada / dno krawędzi mapy).</summary>
        public float visibleBottom;
        /// <summary>Lokalne Y faktycznego dołu ściany (visibleBottom minus ukryty skirt).</summary>
        public float meshBottom;
    }

    /// <summary>Przesunięcia axial sąsiadów w kolejności kątów 0,60,120,180,240,300 stopni.</summary>
    public static readonly Vector2Int[] Directions =
    {
        new Vector2Int( 1,  0),
        new Vector2Int( 0,  1),
        new Vector2Int(-1,  1),
        new Vector2Int(-1,  0),
        new Vector2Int( 0, -1),
        new Vector2Int( 1, -1),
    };

    private const float Epsilon = 0.0005f;

    /// <summary>Narożnik i (0..5) heksa o promieniu radius; kąt -30 + 60*i stopni (od +X w stronę +Z).</summary>
    public static Vector3 Corner(int i, float radius)
    {
        float a = Mathf.Deg2Rad * (-30f + 60f * (i % 6));
        return new Vector3(radius * Mathf.Cos(a), 0f, radius * Mathf.Sin(a));
    }

    /// <summary>Zewnętrzna (pozioma) normalna krawędzi k.</summary>
    public static Vector3 EdgeNormal(int k)
    {
        float a = Mathf.Deg2Rad * (60f * (k % 6));
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    }

    /// <summary>
    /// Wylicza specyfikację krawędzi na podstawie wysokości własnej i sąsiada.
    /// </summary>
    /// <param name="ownHeight">Wysokość góry tego heksa (lokalne Y w chunku).</param>
    /// <param name="hasNeighbor">Czy po tej stronie istnieje heks.</param>
    /// <param name="neighborHeight">Wysokość góry sąsiada.</param>
    /// <param name="mapEdgeBottomY">Poziom dna ścian na krawędzi mapy (lokalne Y w chunku).</param>
    /// <param name="skirtDepth">Głębokość ukrytego pasa pod powierzchnią (0 = brak).</param>
    public static EdgeSpec ComputeEdge(float ownHeight, bool hasNeighbor, float neighborHeight, float mapEdgeBottomY, float skirtDepth)
    {
        EdgeSpec e;
        skirtDepth = Mathf.Max(0f, skirtDepth);

        if (!hasNeighbor)
        {
            e.kind = EdgeKind.MapEdge;
            e.visibleBottom = Mathf.Min(mapEdgeBottomY - ownHeight, -skirtDepth);
            e.meshBottom = e.visibleBottom;
            return e;
        }

        float delta = neighborHeight - ownHeight;
        if (delta < -Epsilon)
        {
            e.kind = EdgeKind.Cliff;
            e.visibleBottom = delta;
            e.meshBottom = delta - skirtDepth;
            return e;
        }

        if (delta <= Epsilon && skirtDepth > Epsilon)
        {
            e.kind = EdgeKind.Skirt;
            e.visibleBottom = 0f;
            e.meshBottom = -skirtDepth;
            return e;
        }

        e.kind = EdgeKind.None;
        e.visibleBottom = 0f;
        e.meshBottom = 0f;
        return e;
    }

    /// <summary>Klucz cache - heksy o identycznym układzie krawędzi, masce drogi i znormalizowanej wysokości wzgórza współdzielą jeden mesh.</summary>
    public static string BuildCacheKey(float radius, float ownHeight, float levelStep, EdgeSpec[] edges, int roadEdgeMask = 0, float hillHeight01 = 0f)
    {
        var sb = new StringBuilder(120);
        sb.Append(Mathf.RoundToInt(radius * 1000f)).Append('|');
        // Fazę poziomów (miejsca podziału ścian) wyznacza ownHeight modulo levelStep.
        float phase = levelStep > Epsilon ? Mathf.Repeat(ownHeight, levelStep) : 0f;
        sb.Append(Mathf.RoundToInt(phase * 1000f)).Append('|').Append(Mathf.RoundToInt(levelStep * 1000f));
        if (roadEdgeMask != 0)
        {
            sb.Append("|r:").Append(roadEdgeMask);
        }
        if (hillHeight01 > 0.001f)
        {
            sb.Append("|h:").Append(Mathf.RoundToInt(hillHeight01 * 1000f));
        }
        for (int k = 0; k < 6; k++)
        {
            sb.Append('|').Append((int)edges[k].kind)
              .Append(':').Append(Mathf.RoundToInt(edges[k].visibleBottom * 1000f))
              .Append(':').Append(Mathf.RoundToInt(edges[k].meshBottom * 1000f));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Buduje mesh heksa.
    /// </summary>
    /// <param name="radius">Promień zewnętrzny (środek -> narożnik).</param>
    /// <param name="ownHeight">Wysokość góry heksa w chunku (potrzebna do podziału ścian na poziomy).</param>
    /// <param name="levelStep">Wysokość jednego poziomu elewacji; ściany są dzielone na pasy co ten krok,
    /// dzięki czemu wierzchołki sąsiednich ścian pokrywają się (brak T-junction / migotania na narożnikach).</param>
    /// <param name="edges">6 specyfikacji krawędzi.</param>
    /// <param name="roadEdgeMask">Maska 6-bitowa krawędzi wymagających pobocza (zapisywana w UV1.x dla górnej powierzchni).</param>
    /// <param name="hillHeight01">Znormalizowana wysokość wzgórza 0..1 dla Hill.shader (zapisywana w UV1.y dla górnej powierzchni).</param>
    public static Mesh Build(float radius, float ownHeight, float levelStep, EdgeSpec[] edges, int roadEdgeMask = 0, float hillHeight01 = 0f)
    {
        var verts = new List<Vector3>(64);
        var normals = new List<Vector3>(64);
        var uv0 = new List<Vector2>(64);
        var uv1 = new List<Vector2>(64);
        var uv2 = new List<Vector2>(64);
        var topTris = new List<int>(12);
        var sideTris = new List<int>(96);

        // ---------- GÓRA (submesh 0) ----------
        // UV0: identyczne jak w oryginalnym Untitled.obj: u = 0.25 - 0.24*x/R, v = 0.25 - 0.24*z/R
        // UV1.x: 6-bitowa maska krawędzi drogi dla Road.shader (kontrakt V1.2)
        // UV1.y: Znormalizowana wysokość wzgórza (0..1) dla Hill.shader (kontrakt V2)
        Vector2 topData = new Vector2(roadEdgeMask, hillHeight01);
        for (int i = 0; i < 6; i++)
        {
            Vector3 c = Corner(i, radius);
            verts.Add(c);
            normals.Add(Vector3.up);
            uv0.Add(new Vector2(0.25f - 0.24f * c.x / radius, 0.25f - 0.24f * c.z / radius));
            uv1.Add(topData);
            uv2.Add(Vector2.zero);
        }
        // Fan z narożnika 0, kolejność zgodna z ruchem wskazówek zegara patrząc z góry (front face w Unity).
        for (int i = 1; i < 5; i++)
        {
            topTris.Add(0);
            topTris.Add(i + 1);
            topTris.Add(i);
        }

        // ---------- BOKI (submesh 1) ----------
        var heights = new List<float>(16);
        for (int k = 0; k < 6; k++)
        {
            EdgeSpec e = edges[k];
            if (e.kind == EdgeKind.None || e.meshBottom > -Epsilon) continue;

            Vector3 a = Corner(k, radius);
            Vector3 b = Corner(k + 1, radius);
            Vector3 n = EdgeNormal(k);
            float u0 = k * radius;
            float u1 = (k + 1) * radius;
            float visibleDrop = -e.visibleBottom;

            CollectBandHeights(ownHeight, levelStep, e.visibleBottom, e.meshBottom, heights);

            int baseIndex = verts.Count;
            for (int h = 0; h < heights.Count; h++)
            {
                float y = heights[h];
                verts.Add(new Vector3(a.x, y, a.z));
                verts.Add(new Vector3(b.x, y, b.z));
                normals.Add(n);
                normals.Add(n);
                uv0.Add(new Vector2(u0, y));
                uv0.Add(new Vector2(u1, y));
                Vector2 sideData = new Vector2(visibleDrop, (float)e.kind);
                uv1.Add(sideData);
                uv1.Add(sideData);
                uv2.Add(sideData);
                uv2.Add(sideData);
            }

            // Patrząc z zewnątrz: 'a' po lewej, 'b' po prawej. Front face = clockwise.
            for (int h = 0; h < heights.Count - 1; h++)
            {
                int aTop = baseIndex + h * 2;
                int bTop = aTop + 1;
                int aBot = aTop + 2;
                int bBot = aTop + 3;
                sideTris.Add(aTop); sideTris.Add(bTop); sideTris.Add(bBot);
                sideTris.Add(aTop); sideTris.Add(bBot); sideTris.Add(aBot);
            }
        }

        var mesh = new Mesh { name = roadEdgeMask != 0 ? $"HexTerrain_R{roadEdgeMask}" : (hillHeight01 > 0.001f ? $"HexTerrain_H{Mathf.RoundToInt(hillHeight01 * 100f)}" : "HexTerrain") };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetUVs(2, uv2);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(topTris, TopSubmesh, false);
        mesh.SetTriangles(sideTris, SideSubmesh, false);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    /// <summary>
    /// Lista lokalnych Y od 0 (góra) w dół do bottom, z podziałem na każdym pełnym poziomie elewacji.
    /// Dodatkowo zawsze zawiera visibleBottom (linia styku z powierzchnią sąsiada).
    /// </summary>
    private static void CollectBandHeights(float ownHeight, float levelStep, float visibleBottom, float bottom, List<float> result)
    {
        result.Clear();
        result.Add(0f);
        if (levelStep > Epsilon)
        {
            // Poziomy absolutne L*step leżące ściśle pomiędzy (ownHeight + bottom) a ownHeight.
            int lHigh = Mathf.FloorToInt((ownHeight - Epsilon) / levelStep);
            int lLow = Mathf.CeilToInt((ownHeight + bottom + Epsilon) / levelStep);
            for (int l = lHigh; l >= lLow; l--)
            {
                float y = l * levelStep - ownHeight;
                if (y < -Epsilon && y > bottom + Epsilon) result.Add(y);
            }
        }
        if (visibleBottom < -Epsilon && visibleBottom > bottom + Epsilon)
        {
            bool present = false;
            for (int i = 0; i < result.Count; i++)
                if (Mathf.Abs(result[i] - visibleBottom) <= Epsilon) { present = true; break; }
            if (!present) result.Add(visibleBottom);
        }
        result.Add(bottom);
        result.Sort((x, y) => y.CompareTo(x)); // malejąco: od góry w dół
    }
}
