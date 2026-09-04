using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>
/// Garrison a settlement: wait inside while it is safe, sortie to break a siege or blockade.
/// </summary>
public sealed class DefendSettlementOrder : OrderBehaviorBase
{
    protected override PartyOrderType OrderType => PartyOrderType.DefendSettlement;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order))
        {
            return;
        }

        if (!RequireFriendlySettlement(party, profile, order, out Settlement? settlement))
        {
            return;
        }

        if (!settlement.IsUnderSiege && !IsBlockaded(party, settlement))
        {
            // Travel() may stop the order (e.g. target sieged while we are away); the base already
            // notifies automation, so nothing more to do here.
            VisitSettlementOrder.Travel(party, profile, order, thinkParams);
            return;
        }

        if (!TryNavigateToSettlement(party, settlement, AiBehavior.DefendSettlement, thinkParams))
        {
            Stop(party, profile, order, StopReason.TargetUnreachable);
        }
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

            if (FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
            {
                Stop(party, profile, order, StopReason.TargetEnemy);
            }
        }
    }

    /// <summary>A naval-capable party should sortie against an active port blockade.</summary>
    private static bool IsBlockaded(MobileParty party, Settlement settlement)
    {
        if (!settlement.HasPort || !party.HasNavalNavigationCapability)
        {
            return false;
        }

        SiegeEvent? siege = settlement.SiegeEvent;
        if (siege is null || !siege.IsBlockadeActive)
        {
            return false;
        }

        var mapEvent = siege.BesiegerCamp.LeaderParty.MapEvent;
        return mapEvent is not null && (mapEvent.IsBlockade || mapEvent.IsBlockadeSallyOut);
    }
}
