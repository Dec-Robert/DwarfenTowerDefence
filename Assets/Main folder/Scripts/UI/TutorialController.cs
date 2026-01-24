using UnityEngine;
using UnityEngine.UIElements;

public class TutorialController : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;

    private VisualElement popup;
    private Button closeBtn;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        popup = root.Q<VisualElement>("TutorialPopup");
        closeBtn = root.Q<Button>("Btn_CloseTutorial");

        if (closeBtn != null)
        {
            closeBtn.clicked += ClosePopup;
        }
    }

    void ClosePopup()
    {
        if (popup != null)
        {
            popup.style.display = DisplayStyle.None;
        }
    }
}