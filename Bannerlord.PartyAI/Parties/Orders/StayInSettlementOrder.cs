using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>Travel to a settlement and remain inside indefinitely.</summary>
public sealed class StayInSettlementOrder : OrderBehaviorBase
{
    protected override PartyOrderType OrderType => PartyOrderType.StayInSettlement;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order))
        {
            VisitSettlementOrder.Travel(party, profile, order, thinkParams);
        }
    }
}
