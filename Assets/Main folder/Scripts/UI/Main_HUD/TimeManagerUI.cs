using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimeManagerUI : MonoBehaviour
{
    public TextMeshProUGUI dateTimeTxt;

    [Header("Time speed Buttons")]
    public Button pauseBtn;
    public Button speedOneBtn;
    public Button speedTwoBtn;
    public Button speedThreeBtn;
    public Button speedFiveBtn;

    private void OnDestroy()
    {
        if (pauseBtn != null) pauseBtn.onClick.RemoveAllListeners();
        if (speedOneBtn != null) speedOneBtn.onClick.RemoveAllListeners();
        if (speedTwoBtn != null) speedTwoBtn.onClick.RemoveAllListeners();
        if (speedThreeBtn != null) speedThreeBtn.onClick.RemoveAllListeners();
        if (speedFiveBtn != null) speedFiveBtn.onClick.RemoveAllListeners();
    }

    private void Update()
    {
        if (TimePhaseManager.Instance != null && dateTimeTxt != null)
        {
            SetTimeTxt();
        }
    }

    private void SetTimeTxt()
    {
        int day = TimePhaseManager.Instance.CurrentDay;
        string phase = TimePhaseManager.Instance.CurrentTimePhase.ToString().ToUpper();
        dateTimeTxt.text = $"Day: {day}\n{phase}";
    }
}