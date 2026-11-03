using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class BuildingWorkerOverlayTests
{
    private List<GameObject> testGameObjects;

    [SetUp]
    public void SetUp()
    {
        testGameObjects = new List<GameObject>();
        BuildingWorkerOverlayManager.ResetStatics();
    }

    [TearDown]
    public void TearDown()
    {
        if (testGameObjects != null)
        {
            for (int i = 0; i < testGameObjects.Count; i++)
            {
                if (testGameObjects[i] != null)
                {
                    Object.DestroyImmediate(testGameObjects[i]);
                }
            }
            testGameObjects.Clear();
        }
        BuildingWorkerOverlayManager.ResetStatics();
    }

    // =========================================================================
    // 1. ELIGIBILITY & TOGGLE TESTS
    // =========================================================================

    [Test]
    public void Test01_IsEligibleBuilding_NullOrHousing_ReturnsFalse()
    {
        Assert.IsFalse(BuildingWorkerOverlayManager.IsEligibleBuilding(null));

        var housingGo = new GameObject("HousingTest");
        testGameObjects.Add(housingGo);
        var housing = housingGo.AddComponent<HousingEntity>();
        Assert.IsFalse(BuildingWorkerOverlayManager.IsEligibleBuilding(housing));

        var prodGo = new GameObject("BuildingWithHousingData");
        testGameObjects.Add(prodGo);
        var prod = prodGo.AddComponent<BuildingEntity>();
        var hData = ScriptableObject.CreateInstance<BuildingData>();
        hData.type = BuildingType.Housing;
        prod.data = hData;
        Assert.IsFalse(BuildingWorkerOverlayManager.IsEligibleBuilding(prod));
    }

    [Test]
    public void Test02_IsEligibleBuilding_ProductionWithWorkers_ReturnsTrue()
    {
        var bGo = new GameObject("ProdTest");
        testGameObjects.Add(bGo);
        var b = bGo.AddComponent<BuildingEntity>();
        var bData = ScriptableObject.CreateInstance<BuildingData>();
        bData.type = BuildingType.Economic;
        bData.baseMaxWorkers = 4;
        b.Initialize(bData);

        Assert.IsTrue(BuildingWorkerOverlayManager.IsEligibleBuilding(b));
    }

    [Test]
    public void Test03_ToggleVisibility_FiresEventAndUpdatesState()
    {
        var mgrGo = new GameObject("TestOverlayManager");
        testGameObjects.Add(mgrGo);
        var mgr = mgrGo.AddComponent<BuildingWorkerOverlayManager>();

        bool eventFired = false;
        bool lastState = false;
        BuildingWorkerOverlayManager.OnVisibilityChanged += (v) =>
        {
            eventFired = true;
            lastState = v;
        };

        Assert.IsFalse(mgr.IsVisible);

        mgr.ToggleVisibility();
        Assert.IsTrue(eventFired);
        Assert.IsTrue(lastState);
        Assert.IsTrue(mgr.IsVisible);

        eventFired = false;
        mgr.ToggleVisibility();
        Assert.IsTrue(eventFired);
        Assert.IsFalse(lastState);
        Assert.IsFalse(mgr.IsVisible);
    }

    // =========================================================================
    // 2. PALETTE & PROCEDURAL SPRITES TESTS
    // =========================================================================

    [Test]
    public void Test04_RaceColors_MatchDesignGuidelines()
    {
        // Ludzie (Niebieski): kanał niebieski dominuje
        Color human = BuildingWorkerOverlayManager.HumansColor;
        Assert.Greater(human.b, human.r);
        Assert.Greater(human.b, human.g);

        // Krasnoludy (Brązowy): kanał czerwony dominuje nad niebieskim i zielonym
        Color dwarf = BuildingWorkerOverlayManager.DwarvesColor;
        Assert.Greater(dwarf.r, dwarf.b);
        Assert.Greater(dwarf.r, dwarf.g);

        // Elfy (Fioletowy): kanały czerwony i niebieski wyższe niż zielony
        Color elf = BuildingWorkerOverlayManager.ElvesColor;
        Assert.Greater(elf.r, elf.g);
        Assert.Greater(elf.b, elf.g);

        // Szary ostrzegawczy [ ! ]: zbalansowane składowe RGB
        Color warn = BuildingWorkerOverlayManager.WarningColor;
        Assert.AreEqual(warn.r, warn.b, 0.1f);
        Assert.AreEqual(warn.r, warn.g, 0.1f);
    }

    [Test]
    public void Test05_ProceduralSprites_CreatedWith9SliceBorders()
    {
        Sprite border = BuildingWorkerOverlayManager.GetCapsuleBorderSprite();
        Assert.IsNotNull(border);
        Assert.AreEqual(24f, border.border.x);
        Assert.AreEqual(24f, border.border.y);
        Assert.AreEqual(24f, border.border.z);
        Assert.AreEqual(24f, border.border.w);

        Sprite bg = BuildingWorkerOverlayManager.GetCapsuleBackgroundSprite();
        Assert.IsNotNull(bg);
        Assert.AreEqual(24f, bg.border.x);
    }

    // =========================================================================
    // 3. OVERLAY VIEW & CAPSULE DISPLAY TESTS
    // =========================================================================

    [Test]
    public void Test06_ActiveBuilding_ZeroWorkers_ShowsWarningCapsuleOnly()
    {
        var bGo = new GameObject("ProdBuilding_ZeroWorkers");
        testGameObjects.Add(bGo);
        var b = bGo.AddComponent<BuildingEntity>();
        var bData = ScriptableObject.CreateInstance<BuildingData>();
        bData.type = BuildingType.Economic;
        bData.baseMaxWorkers = 3;
        b.Initialize(bData);

        var overlayGo = new GameObject("Overlay");
        overlayGo.transform.SetParent(bGo.transform);
        testGameObjects.Add(overlayGo);
        var overlay = overlayGo.AddComponent<BuildingWorkerOverlay>();
        overlay.Initialize(b);

        var warnField = typeof(BuildingWorkerOverlay).GetField("warningCapsule", BindingFlags.NonPublic | BindingFlags.Instance);
        var humansField = typeof(BuildingWorkerOverlay).GetField("humansCapsule", BindingFlags.NonPublic | BindingFlags.Instance);
        var dwarvesField = typeof(BuildingWorkerOverlay).GetField("dwarvesCapsule", BindingFlags.NonPublic | BindingFlags.Instance);
        var elvesField = typeof(BuildingWorkerOverlay).GetField("elvesCapsule", BindingFlags.NonPublic | BindingFlags.Instance);

        var warnCapsule = warnField.GetValue(overlay) as BuildingWorkerOverlay.CapsuleView;
        var humansCapsule = humansField.GetValue(overlay) as BuildingWorkerOverlay.CapsuleView;
        var dwarvesCapsule = dwarvesField.GetValue(overlay) as BuildingWorkerOverlay.CapsuleView;
        var elvesCapsule = elvesField.GetValue(overlay) as BuildingWorkerOverlay.CapsuleView;

        Assert.IsTrue(warnCapsule.root.activeSelf);
        Assert.AreEqual("!", warnCapsule.countText.text);

        Assert.IsFalse(humansCapsule.root.activeSelf);
        Assert.IsFalse(dwarvesCapsule.root.activeSelf);
        Assert.IsFalse(elvesCapsule.root.activeSelf);
    }

    [Test]
    public void Test07_ActiveBuilding_WithWorkers_ShowsOnlyAllocatedRaces()
    {
        var bGo = new GameObject("ProdBuilding_Staffed");
        testGameObjects.Add(bGo);
        var b = bGo.AddComponent<BuildingEntity>();
        var bData = ScriptableObject.CreateInstance<BuildingData>();
        bData.type = BuildingType.Economic;
        bData.baseMaxWorkers = 10;
        b.Initialize(bData);

        // Dodajemy ręcznie: 4 Ludzi, 2 Krasnoludy, 0 Elfów
        var citizensField = typeof(BuildingWorkerComponent).GetField("assignedCitizens", BindingFlags.NonPublic | BindingFlags.Instance);
        var citizens = citizensField.GetValue(b.Workers) as List<Citizen>;

        for (int i = 0; i < 4; i++)
            citizens.Add(new Citizen { race = Race.Humans, workState = WorkState.Assigned });
        for (int i = 0; i < 2; i++)
            citizens.Add(new Citizen { race = Race.Dwarves, workState = WorkState.Assigned });

        var overlayGo = new GameObject("Overlay");
        overlayGo.transform.SetParent(bGo.transform);
        testGameObjects.Add(overlayGo);
        var overlay = overlayGo.AddComponent<BuildingWorkerOverlay>();
        overlay.Initialize(b);

        var warnField = typeof(BuildingWorkerOverlay).GetField("warningCapsule", BindingFlags.NonPublic | BindingFlags.Instance);
        var humansField = typeof(BuildingWorkerOverlay).GetField("humansCapsule", BindingFlags.NonPublic | BindingFlags.Instance);
        var dwarvesField = typeof(BuildingWorkerOverlay).GetField("dwarvesCapsule", BindingFlags.NonPublic | BindingFlags.Instance);
        var elvesField = typeof(BuildingWorkerOverlay).GetField("elvesCapsule", BindingFlags.NonPublic | BindingFlags.Instance);

        var warnCapsule = warnField.GetValue(overlay) as BuildingWorkerOverlay.CapsuleView;
        var humansCapsule = humansField.GetValue(overlay) as BuildingWorkerOverlay.CapsuleView;
        var dwarvesCapsule = dwarvesField.GetValue(overlay) as BuildingWorkerOverlay.CapsuleView;
        var elvesCapsule = elvesField.GetValue(overlay) as BuildingWorkerOverlay.CapsuleView;

        // Ostrzegawcza kapsułka ukryta
        Assert.IsFalse(warnCapsule.root.activeSelf);

        // Ludzie widoczni z liczbą 4
        Assert.IsTrue(humansCapsule.root.activeSelf);
        Assert.AreEqual("4", humansCapsule.countText.text);

        // Krasnoludy widoczne z liczbą 2
        Assert.IsTrue(dwarvesCapsule.root.activeSelf);
        Assert.AreEqual("2", dwarvesCapsule.countText.text);

        // Elfy (0) całkowicie ukryte
        Assert.IsFalse(elvesCapsule.root.activeSelf);
    }

    [Test]
    public void Test08_PausedBuilding_ShowsWarningCapsule()
    {
        var bGo = new GameObject("ProdBuilding_Paused");
        testGameObjects.Add(bGo);
        var b = bGo.AddComponent<BuildingEntity>();
        var bData = ScriptableObject.CreateInstance<BuildingData>();
        bData.type = BuildingType.Economic;
        bData.baseMaxWorkers = 5;
        b.Initialize(bData);

        // Przypisani pracownicy, ale budynek wstrzymany
        var citizensField = typeof(BuildingWorkerComponent).GetField("assignedCitizens", BindingFlags.NonPublic | BindingFlags.Instance);
        var citizens = citizensField.GetValue(b.Workers) as List<Citizen>;
        citizens.Add(new Citizen { race = Race.Dwarves, workState = WorkState.Assigned });

        b.isBuildingActive = false; // Wstrzymany!

        var overlayGo = new GameObject("Overlay");
        overlayGo.transform.SetParent(bGo.transform);
        testGameObjects.Add(overlayGo);
        var overlay = overlayGo.AddComponent<BuildingWorkerOverlay>();
        overlay.Initialize(b);

        var warnField = typeof(BuildingWorkerOverlay).GetField("warningCapsule", BindingFlags.NonPublic | BindingFlags.Instance);
        var warnCapsule = warnField.GetValue(overlay) as BuildingWorkerOverlay.CapsuleView;

        // Pojawia się kapsułka '!'
        Assert.IsTrue(warnCapsule.root.activeSelf);
        Assert.AreEqual("!", warnCapsule.countText.text);
    }
}
