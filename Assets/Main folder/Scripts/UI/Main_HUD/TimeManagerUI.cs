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
        /*
        pauseBtn.onClick.AddListener(() =>TimePhaseManager.Instance.SetTimeSpeed(0));
        speedOneBtn.onClick.AddListener(() =>TimePhaseManager.Instance.SetTimeSpeed(1));
        speedTwoBtn.onClick.AddListener(() =>TimePhaseManager.Instance.SetTimeSpeed(2));
        speedThreeBtn.onClick.AddListener(() =>TimePhaseManager.Instance.SetTimeSpeed(3));
        speedFiveBtn.onClick.AddListener(() =>TimePhaseManager.Instance.SetTimeSpeed(5));
        */
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
        if (TimePhaseManager.Instance != null)
        {
            SetTimeTxt();
        }
    }

    private void SetTimeTxt()
    {

        dateTimeTxt.text = $"Day: X - MORNING";
    }
}
