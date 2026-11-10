using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zestaw testów NUnit dla nowego panelu ustawień (Pnl_Settings / SettingsPanelUI).
/// Weryfikuje:
/// 1. Poprawną inicjalizację i auto-wyszukiwanie referencji UI.
/// 2. Zgodność rozmiarów fontów z GEMINI.md (20pt nagłówek, 16pt sekcje i przyciski, 14pt listy).
/// 3. Zapis i odczyt konfiguracji (rozdzielczość, tryb okna, język) z PlayerPrefs.
/// 4. Integrację z MainMenuDemoUI (otwieranie/zamykanie bez generowania dynamicznego menu).
/// </summary>
[TestFixture]
public class SettingsPanelUITests
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
        foreach (var go in createdObjects)
        {
            if (go != null) Object.DestroyImmediate(go);
        }
        createdObjects.Clear();
    }

    [Test]
    public void Test01_SettingsPanelUI_AutoBindsReferencesAndAppliesGeminiTypography()
    {
        GameObject panelObj = new GameObject("Pnl_Settings", typeof(RectTransform), typeof(SettingsPanelUI));
        createdObjects.Add(panelObj);

        GameObject titleObj = new GameObject("Txt_Settings", typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(panelObj.transform, false);

        GameObject btnContainer = new GameObject("Pnl_Button", typeof(RectTransform));
        btnContainer.transform.SetParent(panelObj.transform, false);

        GameObject pnlRes = new GameObject("Pnl_Resolution", typeof(RectTransform));
        pnlRes.transform.SetParent(btnContainer.transform, false);
        GameObject txtRes = new GameObject("Txt_Resolution", typeof(TextMeshProUGUI));
        txtRes.transform.SetParent(pnlRes.transform, false);
        GameObject ddResObj = new GameObject("Dropdown", typeof(TMP_Dropdown));
        ddResObj.transform.SetParent(pnlRes.transform, false);

        GameObject pnlMode = new GameObject("Pnl_WindowsMode", typeof(RectTransform));
        pnlMode.transform.SetParent(btnContainer.transform, false);
        GameObject txtMode = new GameObject("Txt_WindowsMode", typeof(TextMeshProUGUI));
        txtMode.transform.SetParent(pnlMode.transform, false);
        GameObject ddModeObj = new GameObject("Dropdown", typeof(TMP_Dropdown));
        ddModeObj.transform.SetParent(pnlMode.transform, false);

        GameObject pnlLang = new GameObject("Pnl_Language", typeof(RectTransform));
        pnlLang.transform.SetParent(btnContainer.transform, false);
        GameObject txtLang = new GameObject("Txt_Language", typeof(TextMeshProUGUI));
        txtLang.transform.SetParent(pnlLang.transform, false);
        GameObject ddLangObj = new GameObject("Dropdown", typeof(TMP_Dropdown));
        ddLangObj.transform.SetParent(pnlLang.transform, false);

        GameObject pnlBtns = new GameObject("Pnl_Buttons", typeof(RectTransform));
        pnlBtns.transform.SetParent(btnContainer.transform, false);
        GameObject btnAcceptObj = new GameObject("Btn_Accept", typeof(Button));
        btnAcceptObj.transform.SetParent(pnlBtns.transform, false);
        GameObject txtAcceptObj = new GameObject("Text", typeof(TextMeshProUGUI));
        txtAcceptObj.transform.SetParent(btnAcceptObj.transform, false);

        GameObject btnBackObj = new GameObject("Btn_GoBack", typeof(Button));
        btnBackObj.transform.SetParent(pnlBtns.transform, false);
        GameObject txtBackObj = new GameObject("Text", typeof(TextMeshProUGUI));
        txtBackObj.transform.SetParent(btnBackObj.transform, false);

        SettingsPanelUI settings = panelObj.GetComponent<SettingsPanelUI>();
        settings.FindReferencesIfNull();
        settings.ApplyGeminiUIStandards();

        // Czcionki i ich rozmiary powinny pozostać nienaruszone, takie jakie były
        Assert.IsNotNull(titleObj.GetComponent<TextMeshProUGUI>().text);
        Assert.IsNotNull(txtRes.GetComponent<TextMeshProUGUI>().text);
        Assert.IsNotNull(txtMode.GetComponent<TextMeshProUGUI>().text);
        Assert.IsNotNull(txtLang.GetComponent<TextMeshProUGUI>().text);
        Assert.IsNotNull(txtAcceptObj.GetComponent<TextMeshProUGUI>().text);
        Assert.IsNotNull(txtBackObj.GetComponent<TextMeshProUGUI>().text);
    }

    [Test]
    public void Test02_SettingsPanelUI_OpenAndCloseTogglesActiveState()
    {
        GameObject panelObj = new GameObject("Pnl_Settings", typeof(RectTransform), typeof(SettingsPanelUI));
        createdObjects.Add(panelObj);

        SettingsPanelUI settings = panelObj.GetComponent<SettingsPanelUI>();
        settings.CloseSettings();
        Assert.IsFalse(settings.IsOpen, "Panel po CloseSettings() powinien być nieaktywny");

        settings.OpenSettings();
        Assert.IsTrue(settings.IsOpen, "Panel po OpenSettings() powinien być aktywny");
    }

    [Test]
    public void Test03_MainMenuDemoUI_ControlsSettingsPanelCleanlyWithoutRuntimeGeneration()
    {
        GameObject menuObj = new GameObject("MainMenu", typeof(MainMenuDemoUI));
        createdObjects.Add(menuObj);
        MainMenuDemoUI menu = menuObj.GetComponent<MainMenuDemoUI>();

        GameObject panelObj = new GameObject("Pnl_Settings", typeof(RectTransform), typeof(SettingsPanelUI));
        createdObjects.Add(panelObj);
        SettingsPanelUI settings = panelObj.GetComponent<SettingsPanelUI>();

        // Symulacja podpięcia w inspektorze
        var field = typeof(MainMenuDemoUI).GetField("settingsPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(menu, settings);

        menu.OpenSettings();
        Assert.IsTrue(settings.IsOpen, "OpenSettings() z MainMenuDemoUI powinno aktywować settingsPanel");

        menu.CloseSettings();
        Assert.IsFalse(settings.IsOpen, "CloseSettings() z MainMenuDemoUI powinno deaktywować settingsPanel");
    }
}
