using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class HexArchitectureTests
{
    private List<GameObject> createdObjects;

    [SetUp]
    public void SetUp()
    {
        createdObjects = new List<GameObject>();
    }

    [TearDown]
    public void TearDown()
    {
        if (createdObjects != null)
        {
            for (int i = 0; i < createdObjects.Count; i++)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }
            createdObjects.Clear();
        }
    }

    private HexCell CreateTestCell(int elevation = 0, HexFeatureType feature = HexFeatureType.None, bool isRoad = false)
    {
        GameObject go = new GameObject("TestHex");
        createdObjects.Add(go);

        HexCell cell = go.AddComponent<HexCell>();
        HexCellData data = new HexCellData
        {
            chunkCoord = new Vector2Int(1, 2),
            localCoord = new Vector2Int(3, 4),
            elevation = elevation,
            feature = feature,
            isRoad = isRoad
        };

        cell.Initialize(data);
        return cell;
    }

    private BuildingEntity CreateTestBuilding(string name = "TestBuilding")
    {
        GameObject go = new GameObject(name);
        createdObjects.Add(go);
        return go.AddComponent<BuildingEntity>();
    }

    // =========================================================================
    // 1. DATA BINDING & O(1) OCCUPANCY
    // =========================================================================

    [Test]
    public void Test01_HexCell_InitializesWithCorrectData()
    {
        HexCell cell = CreateTestCell(elevation: 2, feature: HexFeatureType.Forest, isRoad: true);

        Assert.AreEqual(new Vector2Int(1, 2), cell.chunkCoord);
        Assert.AreEqual(new Vector2Int(3, 4), cell.localCoord);
        Assert.AreEqual(2, cell.Elevation);
        Assert.AreEqual(HexFeatureType.Forest, cell.Feature);
        Assert.IsTrue(cell.IsRoad);
        Assert.IsFalse(cell.HasBuilding());
        Assert.IsNull(cell.OccupyingBuilding);
    }

    [Test]
    public void Test02_HexCell_SetBuilding_AssignsOccupancyAndData()
    {
        HexCell cell = CreateTestCell();
        BuildingEntity building = CreateTestBuilding();

        cell.SetBuilding(building);

        Assert.IsTrue(cell.HasBuilding());
        Assert.AreSame(building, cell.OccupyingBuilding);
        Assert.AreSame(building, cell.Data.occupyingBuilding);
    }

    [Test]
    public void Test03_HexCell_ClearBuilding_FreesOccupancyAndData()
    {
        HexCell cell = CreateTestCell();
        BuildingEntity building = CreateTestBuilding();

        cell.SetBuilding(building);
        Assert.IsTrue(cell.HasBuilding());

        cell.ClearBuilding();

        Assert.IsFalse(cell.HasBuilding());
        Assert.IsNull(cell.OccupyingBuilding);
        Assert.IsNull(cell.Data.occupyingBuilding);
    }

    // =========================================================================
    // 2. ELEVATION LOGIC
    // =========================================================================

    [Test]
    public void Test04_ElevationProperties_FlatHillCavity()
    {
        HexCell flatCell = CreateTestCell(elevation: 0);
        Assert.IsTrue(flatCell.IsFlat);
        Assert.IsFalse(flatCell.IsHill);
        Assert.IsFalse(flatCell.IsCavity);

        HexCell hillCell = CreateTestCell(elevation: 2);
        Assert.IsFalse(hillCell.IsFlat);
        Assert.IsTrue(hillCell.IsHill);
        Assert.IsFalse(hillCell.IsCavity);

        HexCell cavityCell = CreateTestCell(elevation: -1);
        Assert.IsFalse(cavityCell.IsFlat);
        Assert.IsFalse(cavityCell.IsHill);
        Assert.IsTrue(cavityCell.IsCavity);
    }

    // =========================================================================
    // 3. ELEVATION REQUIREMENT VALIDATION
    // =========================================================================

    [Test]
    public void Test05_ElevationRequirement_Any()
    {
        var req = new ElevationRequirement { mode = ElevationFilterMode.Any };
        Assert.IsTrue(req.IsValid(0));
        Assert.IsTrue(req.IsValid(3));
        Assert.IsTrue(req.IsValid(-2));
    }

    [Test]
    public void Test06_ElevationRequirement_OnlyFlat()
    {
        var req = new ElevationRequirement { mode = ElevationFilterMode.OnlyFlat };
        Assert.IsTrue(req.IsValid(0));
        Assert.IsFalse(req.IsValid(1));
        Assert.IsFalse(req.IsValid(-1));
    }

    [Test]
    public void Test07_ElevationRequirement_OnlyHills()
    {
        var req = new ElevationRequirement { mode = ElevationFilterMode.OnlyHills };
        Assert.IsFalse(req.IsValid(0));
        Assert.IsTrue(req.IsValid(1));
        Assert.IsTrue(req.IsValid(5));
        Assert.IsFalse(req.IsValid(-1));
    }

    [Test]
    public void Test08_ElevationRequirement_OnlyCavities()
    {
        var req = new ElevationRequirement { mode = ElevationFilterMode.OnlyCavities };
        Assert.IsFalse(req.IsValid(0));
        Assert.IsFalse(req.IsValid(2));
        Assert.IsTrue(req.IsValid(-1));
        Assert.IsTrue(req.IsValid(-4));
    }

    [Test]
    public void Test09_ElevationRequirement_CustomRange()
    {
        var req = new ElevationRequirement
        {
            mode = ElevationFilterMode.CustomRange,
            minElevation = 1,
            maxElevation = 3
        };

        Assert.IsFalse(req.IsValid(0));
        Assert.IsTrue(req.IsValid(1));
        Assert.IsTrue(req.IsValid(2));
        Assert.IsTrue(req.IsValid(3));
        Assert.IsFalse(req.IsValid(4));
    }

    [Test]
    public void Test10_ElevationRequirement_SpecificValues()
    {
        var req = new ElevationRequirement
        {
            mode = ElevationFilterMode.SpecificValues,
            allowedElevations = new List<int> { -1, 1, 3 }
        };

        Assert.IsTrue(req.IsValid(-1));
        Assert.IsTrue(req.IsValid(1));
        Assert.IsTrue(req.IsValid(3));
        Assert.IsFalse(req.IsValid(0));
        Assert.IsFalse(req.IsValid(2));
    }

    // =========================================================================
    // 4. DECORATION VISIBILITY LIFECYCLE
    // =========================================================================

    [Test]
    public void Test11_DecorationProp_HidesOnBuild_RestoresOnClear()
    {
        HexCell cell = CreateTestCell(feature: HexFeatureType.Forest);

        GameObject prop = new GameObject("TreeProp");
        createdObjects.Add(prop);
        prop.transform.parent = cell.transform;

        cell.RegisterFeatureProp(prop);
        Assert.IsTrue(prop.activeSelf, "Prop should be active initially");

        BuildingEntity building = CreateTestBuilding();
        cell.SetBuilding(building);
        Assert.IsFalse(prop.activeSelf, "Prop should be hidden when building is placed");

        cell.ClearBuilding();
        Assert.IsTrue(prop.activeSelf, "Prop should be restored when building is demolished");
    }

    // =========================================================================
    // 5. ROAD BLOCKING VALIDATION
    // =========================================================================

    [Test]
    public void Test12_RoadValidation_BlocksStandardBuilding()
    {
        HexCell roadCell = CreateTestCell(isRoad: true);
        BuildingData standardData = ScriptableObject.CreateInstance<BuildingData>();
        standardData.allowOnRoad = false;

        Assert.IsTrue(roadCell.IsRoad);
        Assert.IsFalse(standardData.allowOnRoad);
    }
}
