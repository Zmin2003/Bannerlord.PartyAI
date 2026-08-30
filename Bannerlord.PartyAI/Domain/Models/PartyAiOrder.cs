using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Domain.Models;

public class PartyAiOrder
{
    [SaveableProperty(1)] public IMapPoint? Target { get; set; }
    [SaveableProperty(2)] public PartyAiOrderType Behavior { get; set; }
    [SaveableProperty(3)] public int AutomationToken { get; set; }

    public PartyAiOrder(
        PartyAiOrderType behavior,
        IMapPoint? target = null,
        int automationToken = 0)
    {
        Target = target;
        Behavior = behavior;
        AutomationToken = automationToken;
    }

    public PartyAiOrder(PartyAiOrder original)
    {
        Target = original.Target;
        Behavior = original.Behavior;
        AutomationToken = original.AutomationToken;
    }
}
