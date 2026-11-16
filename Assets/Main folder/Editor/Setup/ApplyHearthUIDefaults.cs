using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Narzędzie edytorowe do jednorazowego utrwalenia zmian UI okna Płomień Nadziei (HearthWindowUI) bezpośrednio w scenie.
/// </summary>
public static class ApplyHearthUIDefaults
{
    private static readonly string[] DefaultTitles =
    {
        "Tlejący żar",
        "Przygaszony płomień",
        "Równy ogień",
        "Huczący płomień",
        "Rozszalały ogień"
    };

    [MenuItem("Tools/Setup & Maintenance/Apply Hearth UI Defaults to Scene", priority = 302)]
    public static void Execute()
    {
        var hearthUI = Object.FindFirstObjectByType<HearthWindowUI>(FindObjectsInactive.Include);
        if (hearthUI == null)
        {
            Debug.LogError("[ApplyHearthUIDefaults] Nie znaleziono HearthWindowUI w aktywnej scenie!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(hearthUI.gameObject, "Apply Hearth UI Defaults");
        hearthUI.AutoBindReferences();

        if (hearthUI.backgroundBlockButton != null)
        {
            var bgImg = hearthUI.backgroundBlockButton.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = new Color(0.03f, 0.04f, 0.06f, 0.94f);
            }
        }

        if (hearthUI.txtIgniteLabel != null)
        {
            hearthUI.txtIgniteLabel.text = "Rozpocznij noc";
        }

        var pnlHearth = hearthUI.transform.Find("Pnl_Hearth");
        if (pnlHearth != null)
        {
            HideEmptyChildIcons(pnlHearth.Find("Pnl_EffectsColumnsRow/Sec_NightEffects/Pnl_Header"));
            HideEmptyChildIcons(pnlHearth.Find("Pnl_EffectsColumnsRow/Sec_DayEffects/Pnl_Header"));
        }

        if (hearthUI.btnIgniteAndStart != null)
        {
            HideEmptyChildIcons(hearthUI.btnIgniteAndStart.transform);
        }

        if (hearthUI.levelCards != null)
        {
            for (int i = 0; i < hearthUI.levelCards.Length; i++)
            {
                var card = hearthUI.levelCards[i];
                if (card == null || card.rootObject == null) continue;

                if (card.coalCostObject != null) card.coalCostObject.SetActive(true);
                if (card.badgeFreeObject != null) card.badgeFreeObject.SetActive(false);

                bool hasIcon = card.imgLevelIcon != null && card.imgLevelIcon.sprite != null;
                if (card.imgIconBg != null) card.imgIconBg.gameObject.SetActive(hasIcon);
                if (card.imgLevelIcon != null) card.imgLevelIcon.gameObject.SetActive(hasIcon);

                var iconContainer = card.rootObject.transform.Find("Pnl_IconContainer");
                if (iconContainer != null) iconContainer.gameObject.SetActive(hasIcon);

                if (card.badgeTagImage != null) card.badgeTagImage.gameObject.SetActive(false);
                if (card.txtTag != null) card.txtTag.gameObject.SetActive(false);

                var tagRow = card.rootObject.transform.Find("Pnl_TagRow");
                if (tagRow != null) tagRow.gameObject.SetActive(false);

                if (card.txtLevelTitle != null && i < DefaultTitles.Length)
                {
                    card.txtLevelTitle.text = DefaultTitles[i];
                }

                if (card.outline != null)
                {
                    Color c = GetLevelOutlineColor(i + 1);
                    c.a = (i + 1 == 3) ? 1f : 0.55f;
                    card.outline.effectColor = c;
                    card.outline.effectDistance = (i + 1 == 3) ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
                }
            }
        }

        HideEmptyEffects(hearthUI.nightEffectItems);
        HideEmptyEffects(hearthUI.dayEffectItems);

        EditorUtility.SetDirty(hearthUI.gameObject);
        EditorSceneManager.MarkSceneDirty(hearthUI.gameObject.scene);
        EditorSceneManager.SaveScene(hearthUI.gameObject.scene);

        Debug.Log("[ApplyHearthUIDefaults] Zapisano zmiany UI Płomienia Nadziei bezpośrednio w scenie.");
    }

    private static void HideEmptyEffects(HearthWindowUI.HearthEffectItemRefs[] items)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item == null) continue;
            if (item.imgEffectIcon != null)
            {
                item.imgEffectIcon.gameObject.SetActive(item.imgEffectIcon.sprite != null);
            }
            if (item.txtEffectDesc != null)
            {
                item.txtEffectDesc.gameObject.SetActive(false);
            }
        }
    }

    private static void HideEmptyChildIcons(Transform parent)
    {
        if (parent == null) return;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.GetComponent<Button>() != null) continue;
            var img = child.GetComponent<Image>();
            if (img != null && img.sprite == null && img != parent.GetComponent<Image>())
            {
                img.gameObject.SetActive(false);
            }
        }
    }

    private static Color GetLevelOutlineColor(int levelIndex)
    {
        return levelIndex switch
        {
            1 => new Color(0.141f, 0.063f, 0.200f, 1f), // #241033
            2 => new Color(0.431f, 0.118f, 0.349f, 1f), // #6E1E59
            3 => new Color(0.710f, 0.169f, 0.263f, 1f), // #B52B43
            4 => new Color(0.890f, 0.278f, 0.141f, 1f), // #E34724
            5 => new Color(1.000f, 0.478f, 0.000f, 1f), // #FF7A00
            _ => new Color(0.710f, 0.169f, 0.263f, 1f)
        };
    }
}
