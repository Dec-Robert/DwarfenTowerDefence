using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Panel inspektora dla Centrum Ekspedycyjnego.
/// Pokazuje: status elfa, aktywną misję i dostępne ulepszenia.
/// </summary>
public class ExpeditionCenterInspectorPanel
{
    // ── Sekcja główna ──────────────────────────────────────────────────────────
    private readonly VisualElement expeditionSection;

    // ── Status elfa ───────────────────────────────────────────────────────────
    private readonly VisualElement elfStatusDot;
    private readonly Label         lblElfStatus;

    // ── Status misji ──────────────────────────────────────────────────────────
    private readonly VisualElement missionStatusRow;
    private readonly Label         lblMissionStatus;
    private readonly VisualElement missionProgressBg;
    private readonly VisualElement missionProgressFill;
    private readonly Label         lblMissionProgress;

    // =========================================================================
    // Konstruktor – okablowanie elementów UI
    // =========================================================================

    public ExpeditionCenterInspectorPanel(VisualElement section)
    {
        expeditionSection = section;

        elfStatusDot    = section.Q<VisualElement>("Exp_ElfStatusDot");
        lblElfStatus    = section.Q<Label>("Exp_ElfStatusLabel");

        missionStatusRow    = section.Q<VisualElement>("Exp_MissionRow");
        lblMissionStatus    = section.Q<Label>("Exp_MissionStatusLabel");
        missionProgressBg   = section.Q<VisualElement>("Exp_ProgressBg");
        missionProgressFill = section.Q<VisualElement>("Exp_ProgressFill");
        lblMissionProgress  = section.Q<Label>("Exp_ProgressLabel");
    }

    // =========================================================================
    // API
    // =========================================================================

    public void Show() =>
        expeditionSection.style.display = DisplayStyle.Flex;

    public void Hide() =>
        expeditionSection.style.display = DisplayStyle.None;

    public void Refresh(ExpeditionCenterEntity center)
    {
        if (center == null) return;

        RefreshElfStatus(center);
        RefreshMissionStatus(center);
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void RefreshElfStatus(ExpeditionCenterEntity center)
    {
        if (lblElfStatus == null) return;

        if (center.HasElf)
        {
            lblElfStatus.text = "Elf przypisany — gotowy do ekspedycji";
            lblElfStatus.style.color = new Color(0.4f, 1f, 0.4f); // zielony
            SetDotColor(new Color(0.3f, 0.9f, 0.3f));
        }
        else
        {
            lblElfStatus.text = "Brak wolnego elfa — centrum nieaktywne!";
            lblElfStatus.style.color = new Color(1f, 0.35f, 0.35f); // czerwony
            SetDotColor(new Color(0.9f, 0.2f, 0.2f));
        }
    }

    private void RefreshMissionStatus(ExpeditionCenterEntity center)
    {
        if (lblMissionStatus == null) return;

        if (center.IsBusy && center.ActiveMission != null)
        {
            var mission = center.ActiveMission;

            lblMissionStatus.text = $"Ekspedycja: chunk {mission.targetChunk.x}, {mission.targetChunk.y}";
            lblMissionStatus.style.color = new Color(0.4f, 0.8f, 1f); // niebieski

            // Pasek postępu
            float progress = 1f - (float)mission.daysRemaining / Mathf.Max(1, mission.totalDays);
            float pct = Mathf.Clamp01(progress) * 100f;

            if (missionProgressFill != null)
                missionProgressFill.style.width = Length.Percent(pct);

            if (lblMissionProgress != null)
                lblMissionProgress.text = $"Postęp: {Mathf.RoundToInt(pct)}%  ({mission.daysRemaining} dni pozostało)";

            if (missionProgressBg != null)
                missionProgressBg.style.display = DisplayStyle.Flex;
        }
        else
        {
            lblMissionStatus.text = center.HasElf
                ? "Brak aktywnej misji — gotowy do wysłania"
                : "Brak aktywnej misji — przypisz elfa";
            lblMissionStatus.style.color = new Color(0.75f, 0.75f, 0.75f);

            if (missionProgressBg != null)
                missionProgressBg.style.display = DisplayStyle.None;
        }
    }

    private void SetDotColor(Color color)
    {
        if (elfStatusDot == null) return;
        elfStatusDot.style.backgroundColor = color;
    }
}