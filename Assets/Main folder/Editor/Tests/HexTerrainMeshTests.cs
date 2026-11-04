using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Testy EditMode proceduralnej geometrii heksów (HexTerrainMeshBuilder):
/// brak szczelin, brak podwójnych ścian, poprawne normalne / winding, krawędź mapy, zgodność z siatką axial.
/// </summary>
[TestFixture]
public class HexTerrainMeshTests
{
    private const float Step = 0.35f;
    private const float Radius = 1f;
    private const float EdgeBottom = -1.4f;
    private const float Skirt = 0.25f;
    private const float Eps = 0.0005f;

    private readonly List<Mesh> created = new List<Mesh>();

    [TearDown]
    public void TearDown()
    {
        foreach (var m in created) if (m != null) Object.DestroyImmediate(m);
        created.Clear();
    }

    // ---------------------------------------------------------------- helpers

    private HexTerrainMeshBuilder.EdgeSpec[] Edges(float own, float?[] neighbors, float skirt = Skirt)
    {
        var e = new HexTerrainMeshBuilder.EdgeSpec[6];
        for (int k = 0; k < 6; k++)
        {
            bool has = neighbors[k].HasValue;
            e[k] = HexTerrainMeshBuilder.ComputeEdge(own, has, has ? neighbors[k].Value : 0f, EdgeBottom, skirt);
        }
        return e;
    }

    private Mesh Build(float own, HexTerrainMeshBuilder.EdgeSpec[] edges)
    {
        var m = HexTerrainMeshBuilder.Build(Radius, own, Step, edges);
        created.Add(m);
        return m;
    }

    private static float?[] All(float? h) => new[] { h, h, h, h, h, h };

    /// <summary>Wierzchołki ścian należące do krawędzi k (rozpoznawane po normalnej).</summary>
    private static List<Vector3> SideVertsOfEdge(Mesh m, int k)
    {
        var verts = m.vertices;
        var normals = m.normals;
        var result = new List<Vector3>();
        Vector3 n = HexTerrainMeshBuilder.EdgeNormal(k);
        foreach (int idx in m.GetTriangles(HexTerrainMeshBuilder.SideSubmesh))
        {
            if (Vector3.Dot(normals[idx], n) > 0.999f) result.Add(verts[idx]);
        }
        return result;
    }

    private static void AssertWindingAndNormals(Mesh m)
    {
        var v = m.vertices;
        var n = m.normals;
        for (int s = 0; s < m.subMeshCount; s++)
        {
            int[] t = m.GetTriangles(s);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Assert.Greater(face.magnitude, 1e-6f, "Zdegenerowany trojkat");
                Vector3 fn = face.normalized;
                for (int j = 0; j < 3; j++)
                {
                    Assert.Greater(Vector3.Dot(fn, n[t[i + j]]), 0.999f,
                        $"Submesh {s}: winding/normalna niezgodne (front face Unity = clockwise)");
                }
            }
        }
    }

    // ---------------------------------------------------------------- 1. ta sama wysokość

    [Test]
    public void EqualHeights_NoVisibleWalls()
    {
        var edges = Edges(0f, All(0f));
        foreach (var e in edges)
        {
            Assert.AreEqual(HexTerrainMeshBuilder.EdgeKind.Skirt, e.kind);
            Assert.AreEqual(0f, e.visibleBottom, Eps, "Sasiedzi o rownej wysokosci nie moga miec widocznej sciany");
        }

        var noSkirt = Build(0f, Edges(0f, All(0f), skirt: 0f));
        Assert.AreEqual(0, noSkirt.GetTriangles(HexTerrainMeshBuilder.SideSubmesh).Length,
            "Bez skirtu plaski teren nie generuje zadnych scian wewnetrznych");
        Assert.AreEqual(2, noSkirt.subMeshCount);
        Assert.AreEqual(12, noSkirt.GetTriangles(HexTerrainMeshBuilder.TopSubmesh).Length);
    }

    // ---------------------------------------------------------------- 2. wzgórze obok płaskiego

    [Test]
    public void HillNextToFlat_HillOwnsWallDownToNeighborSurface()
    {
        float hill = Step;
        var hillEdges = Edges(hill, new float?[] { 0f, hill, hill, hill, hill, hill });
        Assert.AreEqual(HexTerrainMeshBuilder.EdgeKind.Cliff, hillEdges[0].kind);
        Assert.AreEqual(-Step, hillEdges[0].visibleBottom, Eps);

        // Płaski sąsiad patrzący na wzgórze (kierunek przeciwny = 3) nie generuje ściany.
        var flatEdge = HexTerrainMeshBuilder.ComputeEdge(0f, true, hill, EdgeBottom, Skirt);
        Assert.AreEqual(HexTerrainMeshBuilder.EdgeKind.None, flatEdge.kind, "Nizszy heks nie moze dublowac sciany");

        var mesh = Build(hill, hillEdges);
        AssertWindingAndNormals(mesh);
        var wall = SideVertsOfEdge(mesh, 0);
        Assert.IsTrue(wall.Exists(p => Mathf.Abs(p.y) < Eps), "Sciana zaczyna sie na krawedzi gory");
        Assert.IsTrue(wall.Exists(p => Mathf.Abs(p.y + Step) < Eps), "Sciana ma wierzcholki na poziomie powierzchni sasiada");
        Assert.LessOrEqual(MinY(wall), -Step + Eps);
    }

    // ---------------------------------------------------------------- 3. zapadlisko

    [Test]
    public void Sinkhole_SurroundingHexesCloseTheCavity()
    {
        float sink = -2f * Step;
        var sinkEdges = Edges(sink, All(0f));
        foreach (var e in sinkEdges) Assert.AreEqual(HexTerrainMeshBuilder.EdgeKind.None, e.kind);

        // Każdy płaski sąsiad ma ścianę do dna zapadliska, z podziałem na poziomie -1.
        var flatEdges = Edges(0f, new float?[] { sink, 0f, 0f, 0f, 0f, 0f });
        var mesh = Build(0f, flatEdges);
        AssertWindingAndNormals(mesh);
        var wall = SideVertsOfEdge(mesh, 0);
        Assert.IsTrue(wall.Exists(p => Mathf.Abs(p.y + Step) < Eps), "Brak wierzcholka na posrednim poziomie (T-junction)");
        Assert.IsTrue(wall.Exists(p => Mathf.Abs(p.y - sink) < Eps), "Sciana nie siega dna zapadliska");
    }

    // ---------------------------------------------------------------- 4. kilka wysokości

    [Test]
    public void MultipleHeights_WallsAreSplitAtEveryLevel()
    {
        float mountain = 3f * Step;
        var edges = Edges(mountain, new float?[] { Step, 0f, -Step, mountain, 2f * Step, -3f * Step });
        var mesh = Build(mountain, edges);
        AssertWindingAndNormals(mesh);

        for (int k = 0; k < 6; k++)
        {
            if (edges[k].kind != HexTerrainMeshBuilder.EdgeKind.Cliff) continue;
            var wall = SideVertsOfEdge(mesh, k);
            for (float y = 0f; y >= edges[k].visibleBottom - Eps; y -= Step)
            {
                float yy = y;
                Assert.IsTrue(wall.Exists(p => Mathf.Abs(p.y - yy) < Eps), $"Krawedz {k}: brak podzialu na y={yy}");
            }
        }
        Assert.AreEqual(HexTerrainMeshBuilder.EdgeKind.Skirt, edges[3].kind);
    }

    // ---------------------------------------------------------------- 5. krawędź mapy

    [Test]
    public void MapEdge_WallReachesMapBottom()
    {
        var edges = Edges(0f, new float?[] { null, 0f, 0f, 0f, 0f, 0f });
        Assert.AreEqual(HexTerrainMeshBuilder.EdgeKind.MapEdge, edges[0].kind);
        var mesh = Build(0f, edges);
        AssertWindingAndNormals(mesh);
        Assert.AreEqual(EdgeBottom, MinY(SideVertsOfEdge(mesh, 0)), Eps);

        // UV2.y = typ krawędzi (kontrakt shadera)
        var uv2 = new List<Vector2>();
        mesh.GetUVs(2, uv2);
        var n = mesh.normals;
        for (int i = 0; i < n.Length; i++)
        {
            if (Vector3.Dot(n[i], HexTerrainMeshBuilder.EdgeNormal(0)) > 0.999f)
                Assert.AreEqual((float)HexTerrainMeshBuilder.EdgeKind.MapEdge, uv2[i].y, Eps);
        }
    }

    // ---------------------------------------------------------------- szczelność całej siatki

    [Test]
    public void RandomGrid_EveryHeightStepIsClosedExactlyOnce()
    {
        var rng = new System.Random(1234);
        var heights = new Dictionary<Vector2Int, float>();
        const int R = 5;
        for (int q = -R; q <= R; q++)
            for (int r = Mathf.Max(-R, -q - R); r <= Mathf.Min(R, -q + R); r++)
                heights[new Vector2Int(q, r)] = rng.Next(-3, 4) * Step;

        foreach (var kv in heights)
        {
            float own = kv.Value;
            var edges = new HexTerrainMeshBuilder.EdgeSpec[6];
            for (int k = 0; k < 6; k++)
            {
                bool has = heights.TryGetValue(kv.Key + HexTerrainMeshBuilder.Directions[k], out float nh);
                edges[k] = HexTerrainMeshBuilder.ComputeEdge(own, has, nh, EdgeBottom, Skirt);
            }
            var mesh = Build(own, edges);
            AssertWindingAndNormals(mesh);

            for (int k = 0; k < 6; k++)
            {
                Vector2Int nCoord = kv.Key + HexTerrainMeshBuilder.Directions[k];
                if (!heights.TryGetValue(nCoord, out float nh))
                {
                    Assert.AreEqual(HexTerrainMeshBuilder.EdgeKind.MapEdge, edges[k].kind);
                    continue;
                }

                var back = HexTerrainMeshBuilder.ComputeEdge(nh, true, own, EdgeBottom, Skirt);
                bool ownVisible = edges[k].kind == HexTerrainMeshBuilder.EdgeKind.Cliff;
                bool backVisible = back.kind == HexTerrainMeshBuilder.EdgeKind.Cliff;

                if (Mathf.Abs(own - nh) < Eps)
                {
                    Assert.IsFalse(ownVisible || backVisible, "Rowne wysokosci: brak widocznych scian");
                }
                else
                {
                    Assert.IsTrue(ownVisible ^ backVisible, "Uskok musi byc zamkniety dokladnie jedna sciana");
                    if (ownVisible)
                    {
                        var wall = SideVertsOfEdge(mesh, k);
                        Assert.AreEqual(0f, MaxY(wall), Eps);
                        Assert.LessOrEqual(MinY(wall), nh - own + Eps, "Szczelina pod sciana");
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------- zgodność z siatką axial

    [Test]
    public void Corners_AreSharedExactlyWithAxialNeighbors()
    {
        Vector3 a = HexGridMath.AxialToWorld(0, 0, Radius, 0f);
        for (int k = 0; k < 6; k++)
        {
            Vector2Int d = HexTerrainMeshBuilder.Directions[k];
            Vector3 b = HexGridMath.AxialToWorld(d.x, d.y, Radius, 0f);

            Vector3 dir = (b - a).normalized;
            Assert.Greater(Vector3.Dot(dir, HexTerrainMeshBuilder.EdgeNormal(k)), 0.9999f, $"Kierunek {k} niezgodny z AxialToWorld");

            Vector3 a0 = a + HexTerrainMeshBuilder.Corner(k, Radius);
            Vector3 a1 = a + HexTerrainMeshBuilder.Corner(k + 1, Radius);
            Vector3 b0 = b + HexTerrainMeshBuilder.Corner(k + 4, Radius);
            Vector3 b1 = b + HexTerrainMeshBuilder.Corner(k + 3, Radius);
            Assert.Less(Vector3.Distance(a0, b0), 1e-4f);
            Assert.Less(Vector3.Distance(a1, b1), 1e-4f);
        }
    }

    [Test]
    public void TopSurface_MatchesOriginalHexMesh()
    {
        var original = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Main folder/Models&Textures/Untitled.obj");
        if (original == null) Assert.Inconclusive("Brak Untitled.obj");
        if (!original.isReadable) Assert.Inconclusive("Untitled.obj nie jest Read/Write - pomijam porownanie UV");

        var mesh = Build(0f, Edges(0f, All(0f), skirt: 0f));
        var ourV = mesh.vertices;
        var ourUV = mesh.uv;
        var ov = original.vertices;
        var on = original.normals;
        var ouv = original.uv;
        int matched = 0;
        for (int i = 0; i < ov.Length; i++)
        {
            if (on[i].y < 0.99f || Mathf.Abs(ov[i].y) > Eps) continue;
            int j = System.Array.FindIndex(ourV, p => Vector3.Distance(p, ov[i]) < 1e-4f);
            Assert.GreaterOrEqual(j, 0, $"Narozik {ov[i]} oryginalnego meshu nie istnieje w nowym");
            Assert.Less(Vector2.Distance(ourUV[j], ouv[i]), 1e-3f, "UV gornej powierzchni zmienione");
            matched++;
        }
        Assert.AreEqual(6, matched);
    }

    [Test]
    public void RoadEdgeMask_StoredInUV1_AndDistinguishedInCache()
    {
        int mask = 54; // 110110
        var edges = Edges(0f, All(0f), skirt: 0f);
        var meshWithMask = HexTerrainMeshBuilder.Build(Radius, 0f, Step, edges, mask);
        created.Add(meshWithMask);

        var uv1 = new List<Vector2>();
        meshWithMask.GetUVs(1, uv1);

        Assert.AreEqual(6, uv1.Count, "Top surface powinna miec 6 wierzcholkow w UV1");
        for (int i = 0; i < 6; i++)
        {
            Assert.AreEqual(54f, uv1[i].x, Eps, "Wszystkie wierzcholki gory musza miec maske 54 w UV1.x");
            Assert.AreEqual(0f, uv1[i].y, Eps);
        }

        string key0 = HexTerrainMeshBuilder.BuildCacheKey(Radius, 0f, Step, edges, 0);
        string key54 = HexTerrainMeshBuilder.BuildCacheKey(Radius, 0f, Step, edges, 54);
        Assert.AreNotEqual(key0, key54, "Klucze cache dla roznych masek drog musza sie roznic");
        StringAssert.Contains("|r:54", key54);

        // Test z widocznymi ścianami bocznymi (np. cliff / skirt) - UV1 musi mieć dokładnie tyle samo elementów co vertexCount
        var cliffEdges = Edges(Step, All(0f)); // Uskok do niższego sąsiada
        var meshWithSides = HexTerrainMeshBuilder.Build(Radius, Step, Step, cliffEdges, mask);
        created.Add(meshWithSides);

        var uv1WithSides = new List<Vector2>();
        meshWithSides.GetUVs(1, uv1WithSides);
        Assert.AreEqual(meshWithSides.vertexCount, uv1WithSides.Count, "Liczba UV1 musi byc identyczna z vertexCount przy obecnosci scian bocznych");
    }

    [Test]
    public void HillHeight_EncodedInUV1_GeneratesDistinctCacheKeys()
    {
        var edges = Edges(0f, All(0f), skirt: 0f);

        // Low hill (h = 0.0), Mid hill (h = 0.5), High hill (h = 1.0)
        var meshLow = HexTerrainMeshBuilder.Build(Radius, 0f, Step, edges, 0, 0f);
        var meshMid = HexTerrainMeshBuilder.Build(Radius, 0f, Step, edges, 0, 0.5f);
        var meshHigh = HexTerrainMeshBuilder.Build(Radius, 0f, Step, edges, 0, 1.0f);
        created.Add(meshLow);
        created.Add(meshMid);
        created.Add(meshHigh);

        var uv1Low = new List<Vector2>();
        var uv1Mid = new List<Vector2>();
        var uv1High = new List<Vector2>();
        meshLow.GetUVs(1, uv1Low);
        meshMid.GetUVs(1, uv1Mid);
        meshHigh.GetUVs(1, uv1High);

        Assert.AreEqual(0f, uv1Low[0].y, Eps);
        Assert.AreEqual(0.5f, uv1Mid[0].y, Eps);
        Assert.AreEqual(1.0f, uv1High[0].y, Eps);

        string keyLow = HexTerrainMeshBuilder.BuildCacheKey(Radius, 0f, Step, edges, 0, 0f);
        string keyMid = HexTerrainMeshBuilder.BuildCacheKey(Radius, 0f, Step, edges, 0, 0.5f);
        string keyHigh = HexTerrainMeshBuilder.BuildCacheKey(Radius, 0f, Step, edges, 0, 1.0f);

        Assert.AreNotEqual(keyLow, keyMid);
        Assert.AreNotEqual(keyMid, keyHigh);
        StringAssert.Contains("|h:500", keyMid);
        StringAssert.Contains("|h:1000", keyHigh);
    }

    private static float MinY(List<Vector3> pts) { float m = float.MaxValue; foreach (var p in pts) m = Mathf.Min(m, p.y); return m; }
    private static float MaxY(List<Vector3> pts) { float m = float.MinValue; foreach (var p in pts) m = Mathf.Max(m, p.y); return m; }
}
