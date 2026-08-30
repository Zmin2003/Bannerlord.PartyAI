using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Models;

public class TownGovernorAssignmentState
{
    [SaveableProperty(1)] public Hero? RecommendedGovernor { get; set; }
    [SaveableProperty(2)] public CampaignTime LastRecommendationTime { get; set; } = CampaignTime.Never;
    [SaveableProperty(3)] public Hero? LastAssignedGovernor { get; set; }
    [SaveableProperty(4)] public CampaignTime LastAssignmentTime { get; set; } = CampaignTime.Never;

    public TownGovernorAssignmentState()
    {
    }

    public TownGovernorAssignmentState(TownGovernorAssignmentState source)
    {
        RecommendedGovernor = source.RecommendedGovernor;
        LastRecommendationTime = source.LastRecommendationTime;
        LastAssignedGovernor = source.LastAssignedGovernor;
        LastAssignmentTime = source.LastAssignmentTime;
        Normalize();
    }

    public TownGovernorAssignmentState DeepCopy() => new(this);

    public void Normalize()
    {
        if (RecommendedGovernor?.IsAlive == false)
        {
            RecommendedGovernor = null;
        }

        if (LastAssignedGovernor?.IsAlive == false)
        {
            LastAssignedGovernor = null;
        }
    }
}
