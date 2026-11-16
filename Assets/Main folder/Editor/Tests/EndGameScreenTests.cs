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
    public void EndGameScreenUI_ShowAndScroll_PausesSpawnsCardsAndHandlesOverflow()
    {
        testGo = new GameObject("Test_EndGameScreenUI");
        var root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(testGo.transform);

        var scrollGo = new GameObject("Scroll View", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(root.transform);
        var viewportRt = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        viewportRt.SetParent(scrollGo.transform);
        viewportRt.sizeDelta = new Vector2(200f, 100f);

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(viewportRt);
        contentRt.anchorMin = new Vector2(0f, 0f);
        contentRt.anchorMax = new Vector2(0f, 1f);
        contentRt.pivot = new Vector2(0f, 0.5f);
        var hlg = contentGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childControlWidth = false;
        hlg.childForceExpandWidth = false;

        var scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;

        var cardTemplateGo = new GameObject("Card_Hope", typeof(RectTransform), typeof(LayoutElement));
        cardTemplateGo.transform.SetParent(contentRt);
        cardTemplateGo.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 92f);
        cardTemplateGo.GetComponent<LayoutElement>().preferredWidth = 120f;
        var txtTitle = new GameObject("Title").AddComponent<TextMeshProUGUI>();
        txtTitle.transform.SetParent(cardTemplateGo.transform);
        var txtAmount = new GameObject("Amount").AddComponent<TextMeshProUGUI>();
        txtAmount.transform.SetParent(cardTemplateGo.transform);

        var cardTemplate = cardTemplateGo.AddComponent<EndGameHopeCardUI>();
        SetPrivateField(cardTemplate, "txtTitle", txtTitle);
        SetPrivateField(cardTemplate, "txtAmount", txtAmount);

        var btnPrev = new GameObject("Prev").AddComponent<Button>();
        btnPrev.transform.SetParent(root.transform);
        var btnNext = new GameObject("Next").AddComponent<Button>();
        btnNext.transform.SetParent(root.transform);
        var txtPage = new GameObject("Page").AddComponent<TextMeshProUGUI>();
        txtPage.transform.SetParent(root.transform);

        var ui = testGo.AddComponent<EndGameScreenUI>();
        SetPrivateField(ui, "panelRoot", root);
        SetPrivateField(ui, "scrollRect", scrollRect);
        SetPrivateField(ui, "cardsContent", contentRt);
        SetPrivateField(ui, "cardTemplate", cardTemplate);
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
        Assert.IsFalse(txtPage.gameObject.activeSelf, "Txt_PageIndicator powinien być ukryty.");
        Assert.IsFalse(cardTemplate.gameObject.activeSelf, "Szablon karty powinien pozostać ukryty.");

        var spawnedCards = (List<EndGameHopeCardUI>)typeof(EndGameScreenUI)
            .GetField("spawnedCards", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(ui);
        Assert.AreEqual(3, spawnedCards.Count, "Powinny zostać wygenerowane 3 karty Nadziei.");
        Assert.AreEqual(ContentSizeFitter.FitMode.PreferredSize, contentGo.GetComponent<ContentSizeFitter>().horizontalFit);
        Assert.IsTrue(btnPrev.gameObject.activeSelf && btnNext.gameObject.activeSelf, "Przy overflow przyciski przewijania powinny być widoczne.");
        Assert.IsFalse(btnPrev.interactable, "Na początku scrolla przycisk w lewo powinien być nieaktywny.");
        Assert.IsTrue(btnNext.interactable, "Na początku scrolla przy overflow przycisk w prawo powinien być aktywny.");
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
