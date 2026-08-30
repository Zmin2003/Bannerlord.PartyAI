using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.CampaignBehaviors.AiBehaviors;

public class DefendSettlementBehavior : PartyOrderBehaviorBase
{
    private readonly StayInSettlementBehavior _stayInSettlementBehavior;

    public DefendSettlementBehavior(StayInSettlementBehavior stayInSettlementBehavior)
    {
        _stayInSettlementBehavior = stayInSettlementBehavior;
    }

    protected override PartyAiOrderType OrderType => PartyAiOrderType.DefendSettlement;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
    }

    private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        foreach (PartyAiEntitySettings settings in SubModule.PartySettingsManager.HeroesWithOrders)
        {
            var order = settings.Order;

            if (order is null || order.Behavior != OrderType || order.Target != settlement)
            {
                continue;
            }

            if (settings.Hero is not Hero hero)
            {
                continue;
            }

            if (FactionManager.IsAtWarAgainstFaction(hero.MapFaction, settlement.MapFaction))
            {
                if (hero.PartyBelongedTo is MobileParty party)
                {
                    Message.OrderStoppedTargetEnemy(party, order);
                }
                settings.ClearOrder();
            }
        }
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!IsPartyOrderRelevant(party, out var settings, out var order))
        {
            return;
        }

        var targetSettlement = order.Target as Settlement;
        if (targetSettlement is null)
        {
            Message.OrderStoppedTargetInvalid(party, order);
            settings.ClearOrder();
            return;
        }

        var shouldDefendPort = ShouldDefendPort(party, targetSettlement);

        if (!targetSettlement.IsUnderSiege && !shouldDefendPort)
        {
            _stayInSettlementBehavior.HandleStayInSettlement(party, settings, order, thinkParams);
            return;
        }

        if (!TryNavigateToSettlement(party, targetSettlement, AiBehavior.DefendSettlement, thinkParams))
        {
            Message.OrderStoppedTargetUnreachable(party, order);
            settings.ClearOrder();
        }
    }

    private bool ShouldDefendPort(MobileParty party, Settlement targetSettlement)
    {
        // I don't really get this, I've copied and refactored it from
        // AiMilitaryBehavior.GetDistanceScoreForDefending
        // I really hope TW knows what they're doing (won't be surprised if they don't) :)
        var canUsePort = targetSettlement.HasPort && party.HasNavalNavigationCapability;

        if (!canUsePort)
        {
            return false;
        }

        var siegeEvent = targetSettlement.SiegeEvent;
        if (siegeEvent == null)
        {
            return false;
        }

        if (!siegeEvent.IsBlockadeActive)
        {
            return false;
        }

        var mapEvent = siegeEvent.BesiegerCamp.LeaderParty.MapEvent;
        return mapEvent != null
            && (mapEvent.IsBlockade || mapEvent.IsBlockadeSallyOut);
    }
}
