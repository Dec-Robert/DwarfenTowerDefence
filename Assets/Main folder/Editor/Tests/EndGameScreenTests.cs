using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndGameScreenTests
{
    private GameObject testGo;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (testGo != null)
        {
            Object.DestroyImmediate(testGo);
        }
        SetStaticProperty(typeof(HopeSessionManager), "Instance", null);
        SetStaticProperty(typeof(SaveManager), "Instance", null);
    }

    [Test]
    public void BuildGroupedHopeCards_SumsRepeatedRecordsAndScalesRarityTier()
    {
        var records = new List<HopeSessionManager.HopeRecord>
        {
            new HopeSessionManager.HopeRecord { reason = "Survivor 1", amount = 100, milestoneType = MilestoneType.NightsSurvived, groupKey = "NightsSurvived", groupTitle = "Survivor" },
            new HopeSessionManager.HopeRecord { reason = "Survivor 2", amount = 200, milestoneType = MilestoneType.NightsSurvived, groupKey = "NightsSurvived", groupTitle = "Survivor" },
            new HopeSessionManager.HopeRecord { reason = "K1", amount = 10, milestoneType = MilestoneType.EnemiesKilled, groupKey = "EnemiesKilled", groupTitle = "Łowca" },
            new HopeSessionManager.HopeRecord { reason = "K2", amount = 20, milestoneType = MilestoneType.EnemiesKilled, groupKey = "EnemiesKilled", groupTitle = "Łowca" },
            new HopeSessionManager.HopeRecord { reason = "K3", amount = 30, milestoneType = MilestoneType.EnemiesKilled, groupKey = "EnemiesKilled", groupTitle = "Łowca" },
            new HopeSessionManager.HopeRecord { reason = "K4", amount = 40, milestoneType = MilestoneType.EnemiesKilled, groupKey = "EnemiesKilled", groupTitle = "Łowca" },
            new HopeSessionManager.HopeRecord { reason = "K5", amount = 50, milestoneType = MilestoneType.EnemiesKilled, groupKey = "EnemiesKilled", groupTitle = "Łowca" },
            new HopeSessionManager.HopeRecord { reason = "Zero", amount = 0, milestoneType = MilestoneType.TotalTowers, groupKey = "TotalTowers", groupTitle = "Wieże" }
        };

        List<HopeSessionManager.GroupedHopeCardData> cards = HopeSessionManager.BuildGroupedHopeCards(records);

        Assert.AreEqual(2, cards.Count, "Rekord o wartości 0 powinien zostać pominięty, a pozostałe zgrupowane w 2 karty.");

        Assert.AreEqual("Survivor", cards[0].title);
        Assert.AreEqual(300, cards[0].totalAmount);
        Assert.AreEqual(2, cards[0].count);
        Assert.AreEqual(1, cards[0].rarityTier, "2 rekordy w grupie -> rarityTier 1.");
        Assert.IsNotNull(cards[0].breakdownLines);
        Assert.AreEqual(2, cards[0].breakdownLines.Count);
        Assert.AreEqual("Survivor 1 (+100)", cards[0].breakdownLines[0]);
        Assert.AreEqual("Survivor 2 (+200)", cards[0].breakdownLines[1]);

        Assert.AreEqual("Łowca", cards[1].title);
        Assert.AreEqual(150, cards[1].totalAmount);
        Assert.AreEqual(5, cards[1].count);
        Assert.AreEqual(3, cards[1].rarityTier, "5 rekordów w grupie -> rarityTier ograniczony do max 3.");
    }

    [Test]
    public void EndGameScreenUI_ShowAndPagination_PausesAndDisplaysGroupedCards()
    {
        testGo = new GameObject("Test_EndGameScreenUI");
        var root = new GameObject("Root");
        root.transform.SetParent(testGo.transform);

        var slots = new EndGameHopeCardUI[2];
        for (int i = 0; i < 2; i++)
        {
            var cardGo = new GameObject($"Card_{i}");
            cardGo.transform.SetParent(root.transform);
            var txtTitle = new GameObject("Title").AddComponent<TextMeshProUGUI>();
            txtTitle.transform.SetParent(cardGo.transform);
            var txtAmount = new GameObject("Amount").AddComponent<TextMeshProUGUI>();
            txtAmount.transform.SetParent(cardGo.transform);

            var card = cardGo.AddComponent<EndGameHopeCardUI>();
            SetPrivateField(card, "txtTitle", txtTitle);
            SetPrivateField(card, "txtAmount", txtAmount);
            slots[i] = card;
        }

        var btnPrev = new GameObject("Prev").AddComponent<Button>();
        btnPrev.transform.SetParent(root.transform);
        var btnNext = new GameObject("Next").AddComponent<Button>();
        btnNext.transform.SetParent(root.transform);
        var txtPage = new GameObject("Page").AddComponent<TextMeshProUGUI>();
        txtPage.transform.SetParent(root.transform);

        var ui = testGo.AddComponent<EndGameScreenUI>();
        SetPrivateField(ui, "panelRoot", root);
        SetPrivateField(ui, "cardSlots", slots);
        SetPrivateField(ui, "btnPrevPage", btnPrev);
        SetPrivateField(ui, "btnNextPage", btnNext);
        SetPrivateField(ui, "txtPageIndicator", txtPage);

        var summary = new HopeSessionManager.HopeSessionSummary
        {
            isVictory = true,
            nightsSurvived = 12,
            finalTotalHope = 500,
            totalAccountHope = 1500,
            records = new List<HopeSessionManager.HopeRecord>
            {
                new HopeSessionManager.HopeRecord { reason = "A", amount = 100, groupKey = "G1", groupTitle = "Grupa 1" },
                new HopeSessionManager.HopeRecord { reason = "B", amount = 200, groupKey = "G2", groupTitle = "Grupa 2" },
                new HopeSessionManager.HopeRecord { reason = "C", amount = 300, groupKey = "G3", groupTitle = "Grupa 3" }
            }
        };

        ui.Show(summary);

        Assert.AreEqual(0f, Time.timeScale, 0.001f, "Ekran końcowy powinien zapauzować grę.");
        Assert.IsTrue(root.activeSelf);
        Assert.AreEqual("1 / 2", txtPage.text);
        Assert.IsFalse(btnPrev.interactable);
        Assert.IsTrue(btnNext.interactable);

        ui.NextPage();

        Assert.AreEqual("2 / 2", txtPage.text);
        Assert.IsTrue(btnPrev.interactable);
        Assert.IsFalse(btnNext.interactable);
        Assert.IsTrue(slots[0].gameObject.activeSelf);
        Assert.IsFalse(slots[1].gameObject.activeSelf);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Nie znaleziono pola {fieldName}");
        field.SetValue(target, value);
    }

    private static void SetStaticProperty(System.Type type, string propertyName, object value)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(null, value);
        }
    }
}
