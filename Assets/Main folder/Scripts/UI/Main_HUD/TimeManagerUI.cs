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

    void Start()
    {
        pauseBtn.onClick.AddListener(() =>TimeCycleManager.Instance.SetTimeSpeed(0));
        speedOneBtn.onClick.AddListener(() =>TimeCycleManager.Instance.SetTimeSpeed(1));
        speedTwoBtn.onClick.AddListener(() =>TimeCycleManager.Instance.SetTimeSpeed(2));
        speedThreeBtn.onClick.AddListener(() =>TimeCycleManager.Instance.SetTimeSpeed(3));
        speedFiveBtn.onClick.AddListener(() =>TimeCycleManager.Instance.SetTimeSpeed(5));
    }

    private void OnDestroy()
    {
        if (pauseBtn != null) pauseBtn.onClick.RemoveAllListeners();
        if (speedOneBtn != null) speedOneBtn.onClick.RemoveAllListeners();
        if (speedTwoBtn != null) speedTwoBtn.onClick.RemoveAllListeners();
        if (speedThreeBtn != null) speedThreeBtn.onClick.RemoveAllListeners();
        if (speedFiveBtn != null) speedFiveBtn.onClick.RemoveAllListeners();
    }

    // Update is called once per frame
    void Update()
    {
        if (TimeCycleManager.Instance != null)
        {
            SetTimeTxt();
        }
    }

    private void SetTimeTxt()
    {
        string dayText = TimeCycleManager.Instance.dayCount.ToString();
        string timeText = TimeCycleManager.Instance.GetFormattedHour();
        dateTimeTxt.text = $"Day: {dayText} \n {timeText}";
    }
}
