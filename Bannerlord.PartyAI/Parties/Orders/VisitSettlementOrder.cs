using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>Travel to a settlement; the order completes on arrival.</summary>
public sealed class VisitSettlementOrder : OrderBehaviorBase
{
    protected override PartyOrderType OrderType => PartyOrderType.VisitSettlement;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order))
        {
            return;
        }

        if (party.CurrentSettlement is not null && party.CurrentSettlement == order.Target)
        {
            profile.ClearOrder();
            return;
        }

        Travel(party, profile, order, thinkParams);
    }

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order) && order.Target == settlement)
        {
            profile.ClearOrder();
        }
    }

    /// <summary>Shared with <see cref="StayInSettlementOrder"/>: head to the target and wait.</summary>
    internal static void Travel(MobileParty party, PartyProfile profile, PartyOrder order, PartyThinkParams thinkParams)
    {
        if (!RequireFriendlySettlement(party, profile, order, out Settlement? settlement))
        {
            return;
        }

        if (settlement.IsUnderSiege)
        {
            Stop(party, profile, order, StopReason.TargetSieged);
            return;
        }

        if (!TryNavigateToSettlement(party, settlement, AiBehavior.GoToSettlement, thinkParams))
        {
            Stop(party, profile, order, StopReason.TargetUnreachable);
            return;
        }

        HoldInitiative(party);
    }
}
