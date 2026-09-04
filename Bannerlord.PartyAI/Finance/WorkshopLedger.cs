using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Finance;

/// <summary>Daily capital readings of one player workshop. Save id 10.</summary>
public sealed class WorkshopLedger
{
    public const int HistoryLength = 30;

    [SaveableProperty(1)] public List<int> CapitalHistory { get; private set; } = new();
    [SaveableProperty(2)] public CampaignTime LastProductionChange { get; private set; } = CampaignTime.Never;

    public void Record(int capital)
    {
        CapitalHistory ??= new();
        CapitalHistory.Add(capital);
        while (CapitalHistory.Count > HistoryLength)
        {
            CapitalHistory.RemoveAt(0);
        }
    }

    public void MarkProductionChanged() => LastProductionChange = CampaignTime.Now;

    /// <summary>Capital change over the last <paramref name="days"/> readings; null while there is too little history.</summary>
    public int? Trend(int days)
    {
        if (CapitalHistory is null || CapitalHistory.Count < 2)
        {
            return null;
        }

        int span = System.Math.Min(days, CapitalHistory.Count - 1);
        return CapitalHistory[CapitalHistory.Count - 1] - CapitalHistory[CapitalHistory.Count - 1 - span];
    }

    public int DaysTracked => CapitalHistory?.Count ?? 0;

    public bool ChangedRecently(int cooldownDays)
        => LastProductionChange != CampaignTime.Never
            && (LastProductionChange.IsNow || LastProductionChange.IsPast)
            && LastProductionChange.ElapsedDaysUntilNow < cooldownDays;

    /// <summary>Discard readings taken before a production change; they describe a different business.</summary>
    public void Reset()
    {
        CapitalHistory = CapitalHistory?.Count > 0 ? new List<int> { CapitalHistory.Last() } : new();
    }
}
