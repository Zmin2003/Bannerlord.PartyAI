using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>Patrol around a friendly settlement indefinitely.</summary>
public sealed class PatrolAroundSettlementOrder : OrderBehaviorBase
{
    protected override PartyOrderType OrderType => PartyOrderType.PatrolAroundPoint;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order)
            || !RequireFriendlySettlement(party, profile, order, out Settlement? settlement))
        {
            return;
        }

        if (!TryNavigateToSettlement(party, settlement, AiBehavior.PatrolAroundPoint, thinkParams))
        {
            Stop(party, profile, order, StopReason.TargetUnreachable);
        }
    }
}
