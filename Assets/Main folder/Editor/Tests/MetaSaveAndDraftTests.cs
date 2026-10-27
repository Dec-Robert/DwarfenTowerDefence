using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MetaSaveAndDraftTests
{
    private List<GameObject> spawnedGameObjects;
    private List<ScriptableObject> createdScriptableObjects;
    private string tempSavePath;
    private GameObject saveManagerGO;
    private SaveManager saveManager;
    private List<TowerData> testTowers;

    [SetUp]
    public void SetUp()
    {
        spawnedGameObjects = new List<GameObject>();
        createdScriptableObjects = new List<ScriptableObject>();

        tempSavePath = Path.Combine(Application.temporaryCachePath, $"test_save_{Guid.NewGuid():N}.json");

        saveManagerGO = new GameObject("TestSaveManager");
        saveManager = saveManagerGO.AddComponent<SaveManager>();
        saveManager.SavePath = tempSavePath;
        spawnedGameObjects.Add(saveManagerGO);

        // Tworzymy zestaw 7 wież testowych odpowiadających katalogowi wież w projekcie
        testTowers = new List<TowerData>
        {
            CreateTestTower("1. Sentinel Arbalest", "Arbalet", 25f, 15f, 1.2f, 20f, 0f),
            CreateTestTower("2. Thunderbuss", "Garłacz Gromowy", 12f, 40f, 0.8f, 35f, 0f),
            CreateTestTower("3. Steam Repeater", "Repetier Parowy", 18f, 10f, 3.5f, 10f, 0f),
            CreateTestTower("4. Crucible of Cinders", "Tygiel Pożogi", 14f, 22f, 1.5f, 15f, 25f),
            CreateTestTower("5. Shadow Spire", "Iglica Cieni", 22f, 60f, 0.5f, 0f, 50f),
            CreateTestTower("6. Royal Bombard", "Bombarda", 30f, 85f, 0.4f, 50f, 0f),
            CreateTestTower("7. Soul Monolith", "Monolit dusz", 20f, 45f, 1.0f, 20f, 40f)
        };
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(tempSavePath))
        {
            try { File.Delete(tempSavePath); } catch { }
        }

        foreach (var so in createdScriptableObjects)
        {
            if (so != null) UnityEngine.Object.DestroyImmediate(so);
        }
        createdScriptableObjects.Clear();

        foreach (var go in spawnedGameObjects)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        spawnedGameObjects.Clear();
    }

    private TowerData CreateTestTower(string assetName, string buildingName, float range, float damage, float fireRate, float armorPen, float magicPen)
    {
        TowerData tower = ScriptableObject.CreateInstance<TowerData>();
        tower.name = assetName;
        tower.buildingName = buildingName;
        tower.description = $"Testowy opis dla wieży {buildingName}";
        tower.baseRange = range;
        tower.baseDamage = damage;
        tower.fireRate = fireRate;
        tower.armorPenetration = armorPen;
        tower.magicPenetration = magicPen;
        tower.isUnlocked = true;

        createdScriptableObjects.Add(tower);
        return tower;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: New SaveData has initialized unlockedTowerIDs
    // ─────────────────────────────────────────────────────────────────────────
    [Test]
    public void Test1_SaveData_NewInstance_HasInitializedUnlockedTowerIDs()
    {
        SaveData data = new SaveData();

        Assert.IsNotNull(data.unlockedTowerIDs, "SaveData.unlockedTowerIDs nie może być nullem po utworzeniu instancji.");
        Assert.AreEqual(0, data.unlockedTowerIDs.Count, "Nowa instancja SaveData powinna mieć pustą listę odblokowanych wież przed inicjalizacją.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: SaveManager.LoadGame() defaults to 2 starter towers
    // ─────────────────────────────────────────────────────────────────────────
    [Test]
    public void Test2_SaveManager_LoadGame_DefaultsTo2StarterTowers()
    {
        // Upewniamy się, że plik nie istnieje
        if (File.Exists(tempSavePath)) File.Delete(tempSavePath);

        saveManager.LoadGame();

        Assert.IsNotNull(saveManager.currentSaveData.unlockedTowerIDs, "unlockedTowerIDs nie może być nullem po LoadGame()");
        Assert.AreEqual(2, saveManager.currentSaveData.unlockedTowerIDs.Count, "Nowy profil powinien domyślnie posiadać dokładnie 2 wieże startowe.");
        Assert.IsTrue(saveManager.currentSaveData.unlockedTowerIDs.Contains("1. Sentinel Arbalest"), "Lista wież startowych musi zawierać '1. Sentinel Arbalest'.");
        Assert.IsTrue(saveManager.currentSaveData.unlockedTowerIDs.Contains("6. Royal Bombard"), "Lista wież startowych musi zawierać '6. Royal Bombard'.");

        // Weryfikacja zapisu do pliku na dysku
        Assert.IsTrue(File.Exists(tempSavePath), "Plik zapisu powinien zostać automatycznie utworzony na dysku.");
        string json = File.ReadAllText(tempSavePath);
        StringAssert.Contains("1. Sentinel Arbalest", json, "Zapisany JSON musi zawierać nazwę pierwszej wieży startowej.");
        StringAssert.Contains("6. Royal Bombard", json, "Zapisany JSON musi zawierać nazwę drugiej wieży startowej.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: SyncTowersWithSave sets isUnlocked = true for starter towers and false for 5 draft towers
    // ─────────────────────────────────────────────────────────────────────────
    [Test]
    public void Test3_SyncTowersWithSave_SetsStarterTowersTrue_AndDraftTowersFalse()
    {
        saveManager.currentSaveData = new SaveData
        {
            unlockedTowerIDs = new List<string> { "1. Sentinel Arbalest", "6. Royal Bombard" }
        };

        // Wszystkie wieże na start mają isUnlocked = true
        foreach (var t in testTowers) t.isUnlocked = true;

        saveManager.SyncTowersWithSave(testTowers);

        // Starter 1: Sentinel Arbalest
        Assert.IsTrue(testTowers[0].isUnlocked, "Sentinel Arbalest (startowa wieża) musi mieć isUnlocked == true.");

        // Starter 2: Royal Bombard
        Assert.IsTrue(testTowers[5].isUnlocked, "Royal Bombard (startowa wieża) musi mieć isUnlocked == true.");

        // Pozostałe 5 wież w puli draftu
        Assert.IsFalse(testTowers[1].isUnlocked, "Thunderbuss (wieża zablokowana) musi mieć isUnlocked == false.");
        Assert.IsFalse(testTowers[2].isUnlocked, "Steam Repeater (wieża zablokowana) musi mieć isUnlocked == false.");
        Assert.IsFalse(testTowers[3].isUnlocked, "Crucible of Cinders (wieża zablokowana) musi mieć isUnlocked == false.");
        Assert.IsFalse(testTowers[4].isUnlocked, "Shadow Spire (wieża zablokowana) musi mieć isUnlocked == false.");
        Assert.IsFalse(testTowers[6].isUnlocked, "Soul Monolith (wieża zablokowana) musi mieć isUnlocked == false.");

        int unlockedCount = 0;
        foreach (var t in testTowers) if (t.isUnlocked) unlockedCount++;
        Assert.AreEqual(2, unlockedCount, "Dokładnie 2 z 7 wież powinny być odblokowane po synchronizacji profilu startowego.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: UnlockTower adds ID to list and serializes to JSON
    // ─────────────────────────────────────────────────────────────────────────
    [Test]
    public void Test4_UnlockTower_AddsIDToList_AndSerializesToJSON()
    {
        saveManager.LoadGame(); // 2 wieże startowe
        saveManager.allTowers = new List<TowerData>(testTowers);
        saveManager.SyncTowersWithSave(testTowers);

        Assert.IsFalse(testTowers[1].isUnlocked, "Przed odblokowaniem Thunderbuss powinien być zablokowany.");

        // Odblokuj Thunderbuss
        saveManager.UnlockTower("2. Thunderbuss");

        // Weryfikacja w pamięci
        Assert.AreEqual(3, saveManager.currentSaveData.unlockedTowerIDs.Count, "Po odblokowaniu lista powinna mieć 3 wieże.");
        Assert.IsTrue(saveManager.currentSaveData.unlockedTowerIDs.Contains("2. Thunderbuss"), "Lista musi zawierać '2. Thunderbuss'.");
        Assert.IsTrue(testTowers[1].isUnlocked, "Flaga isUnlocked w instancji TowerData musi zostać zaktualizowana na true.");

        // Weryfikacja zapisu w pliku JSON
        Assert.IsTrue(File.Exists(tempSavePath), "Plik JSON musi istnieć.");
        string json = File.ReadAllText(tempSavePath);
        StringAssert.Contains("2. Thunderbuss", json, "Plik JSON musi zawierać nowo odblokowaną wieżę.");

        // Weryfikacja wczytania na czystym obiekcie SaveData
        SaveData reloaded = JsonUtility.FromJson<SaveData>(json);
        Assert.AreEqual(3, reloaded.unlockedTowerIDs.Count, "Wczytany z dysku SaveData musi zawierać 3 wieże.");
        Assert.IsTrue(reloaded.unlockedTowerIDs.Contains("2. Thunderbuss"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5: Draft candidate filter leaves exactly 5, 4, 3, 2, or 1 towers as drafted
    // ─────────────────────────────────────────────────────────────────────────
    [Test]
    public void Test5_DraftCandidateFilter_LeavesRemainingLockedTowersSequentially()
    {
        List<string> unlockedIDs = new List<string> { "1. Sentinel Arbalest", "6. Royal Bombard" };

        // Krok 0: 2 wieże startowe odblokowane -> w puli draftu pozostaje 5 wież (Nowa wieża 1)
        List<TowerData> pool0 = TowerDraftModalUI.FilterCandidates(testTowers, unlockedIDs);
        Assert.AreEqual(5, pool0.Count, "Dla Nowa wieża 1 w puli do wyboru powinno być dokładnie 5 wież.");

        // Krok 1: Gracz wybiera Thunderbuss
        unlockedIDs.Add("2. Thunderbuss");
        List<TowerData> pool1 = TowerDraftModalUI.FilterCandidates(testTowers, unlockedIDs);
        Assert.AreEqual(4, pool1.Count, "Dla Nowa wieża 2 w puli do wyboru powinny być dokładnie 4 wieże.");
        Assert.IsFalse(pool1.Exists(t => t.name == "2. Thunderbuss"), "Odblokowana wieża nie może pojawić się w kandydatach draftu.");

        // Krok 2: Gracz wybiera Steam Repeater
        unlockedIDs.Add("3. Steam Repeater");
        List<TowerData> pool2 = TowerDraftModalUI.FilterCandidates(testTowers, unlockedIDs);
        Assert.AreEqual(3, pool2.Count, "Dla Nowa wieża 3 w puli do wyboru powinny być dokładnie 3 wieże.");

        // Krok 3: Gracz wybiera Crucible of Cinders
        unlockedIDs.Add("4. Crucible of Cinders");
        List<TowerData> pool3 = TowerDraftModalUI.FilterCandidates(testTowers, unlockedIDs);
        Assert.AreEqual(2, pool3.Count, "Dla Nowa wieża 4 w puli do wyboru powinny być dokładnie 2 wieże.");

        // Krok 4: Gracz wybiera Shadow Spire
        unlockedIDs.Add("5. Shadow Spire");
        List<TowerData> pool4 = TowerDraftModalUI.FilterCandidates(testTowers, unlockedIDs);
        Assert.AreEqual(1, pool4.Count, "Dla Nowa wieża 5 w puli do wyboru powinna być dokładnie 1 wieża.");
        Assert.AreEqual("7. Soul Monolith", pool4[0].name, "Ostatnią pozostałą wieżą w tym scenariuszu powinien być Soul Monolith.");

        // Krok 5: Ostatnia wieża odblokowana
        unlockedIDs.Add("7. Soul Monolith");
        List<TowerData> pool5 = TowerDraftModalUI.FilterCandidates(testTowers, unlockedIDs);
        Assert.AreEqual(0, pool5.Count, "Gdy wszystkie wieże są odblokowane, pula kandydatów draftu powinna być pusta.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6: GEMINI.md UI Typography & Icon Size Compliance
    // ─────────────────────────────────────────────────────────────────────────
    [Test]
    public void Test6_GEMINI_Typography_And_IconSizes_Compliance()
    {
        Assert.AreEqual(20f, TowerDraftModalUI.FontSizeHeader, "GEMINI.md wymaga 20 pt dla nagłówków.");
        Assert.AreEqual(16f, TowerDraftModalUI.FontSizeButton, "GEMINI.md wymaga 16 pt dla przycisków funkcyjnych.");
        Assert.IsTrue(TowerDraftModalUI.FontSizeContent >= 12f && TowerDraftModalUI.FontSizeContent <= 14f,
            "GEMINI.md wymaga 12-14 pt dla opisów i statystyk.");
        Assert.AreEqual(10f, TowerDraftModalUI.FontSizeHelper, "GEMINI.md wymaga 10 pt dla tekstów pomocniczych.");

        Assert.AreEqual(32f, TowerDraftModalUI.IconSizeStandard, "GEMINI.md wymaga 32 px dla standardowych ikon.");
        Assert.AreEqual(24f, TowerDraftModalUI.IconSizeSmall, "GEMINI.md wymaga 24 px dla małych ikon.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 7: MetaUpgradeManager Node Purchased Hook and Tower Draft Detection
    // ─────────────────────────────────────────────────────────────────────────
    [Test]
    public void Test7_MetaUpgradeManager_OnNodePurchased_TriggersHook_AndDetectsDraft()
    {
        GameObject metaMgrGO = new GameObject("TestMetaMgr");
        MetaUpgradeManager metaMgr = metaMgrGO.AddComponent<MetaUpgradeManager>();
        spawnedGameObjects.Add(metaMgrGO);

        MetaUpgradeSO draftNode = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        draftNode.id = "nowa_wieza_1";
        draftNode.upgradeName = "Nowa wieża 1";
        draftNode.effectType = MetaEffectType.TowerDraftUnlock;
        createdScriptableObjects.Add(draftNode);

        MetaUpgradeSO normalNode = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        normalNode.id = "krasnoludzka_sila_1";
        normalNode.upgradeName = "Krasnoludzka siła I";
        normalNode.effectType = MetaEffectType.BonusWorkersPerShift;
        createdScriptableObjects.Add(normalNode);

        Assert.IsTrue(metaMgr.IsTowerDraftUpgrade(draftNode), "nowa_wieza_1 musi być rozpoznany jako węzeł draftu wieży.");
        Assert.IsFalse(metaMgr.IsTowerDraftUpgrade(normalNode), "Zwykły węzeł nie może być rozpoznany jako draft wieży.");

        MetaUpgradeSO receivedNode = null;
        metaMgr.OnUpgradePurchased += (node) => receivedNode = node;

        metaMgr.OnNodePurchased(normalNode);

        Assert.AreSame(normalNode, receivedNode, "Event OnUpgradePurchased musi zostać wywołany z zakupionym węzłem.");
        Assert.IsTrue(normalNode.isUnlocked, "Zakupiony węzeł musi mieć isUnlocked == true.");
        Assert.IsTrue(saveManager.currentSaveData.unlockedUpgradeIDs.Contains("krasnoludzka_sila_1"),
            "SaveData musi zawierać ID nowo zakupionego węzła.");
    }
}
