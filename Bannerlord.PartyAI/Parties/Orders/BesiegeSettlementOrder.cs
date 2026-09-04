using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>Lay siege to a hostile fortification until it falls or the war ends.</summary>
public sealed class BesiegeSettlementOrder : OrderBehaviorBase
{
    protected override PartyOrderType OrderType => PartyOrderType.BesiegeSettlement;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order)
            || !RequireHostileSettlement(party, profile, order, out Settlement? settlement))
        {
            return;
        }

        if (!TryNavigateToSettlement(party, settlement, AiBehavior.BesiegeSettlement, thinkParams))
        {
            Stop(party, profile, order, StopReason.TargetUnreachable);
            return;
        }

        HoldInitiative(party);
    }

    private void OnSettlementOwnerChanged(
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturer,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        foreach (PartyProfile profile in PartyAi.Parties.ProfilesWithOrders)
        {
            PartyOrder? order = profile.Order;
            if (order is null
                || order.Behavior != OrderType
                || order.Target != settlement
                || profile.Hero?.PartyBelongedTo is not MobileParty party)
            {
                continue;
            }

            if (!FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
            {
                Stop(party, profile, order, StopReason.TargetFriendly);
            }
        }
    }
}
