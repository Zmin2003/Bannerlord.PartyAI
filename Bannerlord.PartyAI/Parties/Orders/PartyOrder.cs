using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>
/// One instruction for a party. Orders issued by the mod itself (automatic defense) carry a
/// non-zero <see cref="AutomationToken"/> so they can be told apart from the player's orders.
/// </summary>
public class PartyOrder
{
    [SaveableProperty(1)] public IMapPoint? Target { get; set; }
    [SaveableProperty(2)] public PartyOrderType Behavior { get; set; }
    [SaveableProperty(3)] public int AutomationToken { get; set; }

    public PartyOrder(PartyOrderType behavior, IMapPoint? target = null, int automationToken = 0)
    {
        Behavior = behavior;
        Target = target;
        AutomationToken = automationToken;
    }

    public PartyOrder(PartyOrder original)
        : this(original.Behavior, original.Target, original.AutomationToken)
    {
    }

    public bool IsPlayerOrder => AutomationToken == 0;

    public bool IsAutomatic => AutomationToken > 0;
}
