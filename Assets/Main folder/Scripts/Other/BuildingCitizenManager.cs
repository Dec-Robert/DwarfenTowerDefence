using TMPro;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UI;

public class BuildingCitizenManager : MonoBehaviour
{
    public static BuildingCitizenManager Instance;
    //
    [Header("Przyciski dodawania")]
    public Button addElf;
    public Button addDwarf;
    public Button addHuman;

    [Header("Przyciski usuwania")]
    public Button removeElf;
    public Button removeDwarf;
    public Button removeHuman;

    [Header("Wy�wietlanie ilo��")]
    public TextMeshProUGUI elfCountText;
    public TextMeshProUGUI dwarfCountText;
    public TextMeshProUGUI humanCountText;


    [SerializeField]private BuildingEntity targetBuilding;

    public void Awake()
    {
        Instance = this;
    }

    public void Setup(BuildingEntity entity)
    {
        targetBuilding = entity;

        RefreshUI();
        addElf.onClick.RemoveAllListeners();
        addDwarf.onClick.RemoveAllListeners();
        addHuman.onClick.RemoveAllListeners();
        removeElf.onClick.RemoveAllListeners();
        removeDwarf.onClick.RemoveAllListeners();
        removeHuman.onClick.RemoveAllListeners();

        addElf.onClick.AddListener(() => OnAddClick(Race.Elves));
        addDwarf.onClick.AddListener(() => OnAddClick(Race.Dwarves));
        addHuman.onClick.AddListener(() => OnAddClick(Race.Humans));

        removeElf.onClick.AddListener(() => OnRemoveClick(Race.Elves));
        removeDwarf.onClick.AddListener(() => OnRemoveClick(Race.Dwarves));
        removeHuman.onClick.AddListener(() => OnRemoveClick(Race.Humans));
    }

    void OnAddClick(Race race)
    {
        if (targetBuilding != null)
        {
            targetBuilding.TryAddWorker(race);
            RefreshUI();

            Debug.Log($"Pr�ba dodania rasy {race}  z budynku {targetBuilding.name}");
        }
    }

    void OnRemoveClick(Race race)
    {
        if (targetBuilding != null)
        {
            targetBuilding.RemoveWorker(race);
            RefreshUI();

            Debug.Log($"Pr�ba usuni�cia rasy {race} z budynku {targetBuilding.name}");
        }
    }

    void RefreshUI()
    {
        if (targetBuilding == null) return;

        // Aktualizacja licznik�w
        elfCountText.text = targetBuilding.GetWorkerCount(Race.Elves).ToString();
        dwarfCountText.text = targetBuilding.GetWorkerCount(Race.Dwarves).ToString();
        humanCountText.text = targetBuilding.GetWorkerCount(Race.Humans).ToString();
    }
}
