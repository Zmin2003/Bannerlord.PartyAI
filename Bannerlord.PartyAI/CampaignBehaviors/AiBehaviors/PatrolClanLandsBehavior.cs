using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.CampaignBehaviors.AiBehaviors;

internal class PatrolClanLandsBehavior : PartyOrderBehaviorBase
{
    protected override PartyAiOrderType OrderType => PartyAiOrderType.PatrolClanLands;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    private void OnDailyTick(MobileParty party)
    {
        var hero = party.LeaderHero;
        if (!IsPartyOrderRelevant(party, out var settings, out var order))
        {
            return;
        }

        TryPickNewTarget(hero, settings, order);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        var hero = party.LeaderHero;
        if (!IsPartyOrderRelevant(hero, out var settings, out var order))
        {
            return;
        }

        if (!ShouldContinueExecutingOrder(hero, settings, order))
        {
            return;
        }

        if (order.Target is not Settlement targetSettlement)
        {
            order.Target = null; // Not sure how it can happen yet, but let's be safe and reroll
            return;
        }

        if (!TryNavigateToSettlement(party, targetSettlement, AiBehavior.PatrolAroundPoint, thinkParams))
        {
            order.Target = null;
            return; // Fallback to default AI until settlement is rerolled
        }

#if LOWER_THAN_1_5
        if (party.Objective != MobileParty.PartyObjective.Defensive)
        {
            settings.CachedPartyObjective = party.Objective;
            party.SetPartyObjective(MobileParty.PartyObjective.Defensive);
        }
#endif
    }

    private bool ShouldContinueExecutingOrder(Hero hero, PartyAiEntitySettings settings, PartyAiOrder order)
    {
        var target = order.Target as Settlement;
        return (target is not null && !FactionManager.IsAtWarAgainstFaction(hero.MapFaction, target.MapFaction))
            || TryPickNewTarget(hero, settings, order);
    }

    private bool TryPickNewTarget(Hero hero, PartyAiEntitySettings settings, PartyAiOrder order)
    {
        var averageDistance = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default);
        var range = averageDistance * settings.PatrolRadius;
        var maxDistanceSq = range * 2 * (range * 2);

        var clanSettlements = hero.Clan.Settlements;
        if (clanSettlements.Count == 0)
        {
            settings.ClearOrder(); // fallback to kingdom settlements?
            return false;
        }

        var settlementsByDistance = clanSettlements
            .Select(settlement => (Settlement: settlement, DistanceSquared: settlement.Position.DistanceSquared(hero.PartyBelongedTo.Position)))
            .OrderBy(tuple => tuple.DistanceSquared)
            .ToArray();
        var closest = settlementsByDistance[0];

        // Away from clan lands
        if (closest.DistanceSquared > maxDistanceSq)
        {
            order.Target = closest.Settlement;
            return true;
        }

        // Variety in patrols
        var random = MBRandom.RandomFloat;
        if (random < 0.01)
        {
            // 1% to switch to a completely random clan settlement
            order.Target = clanSettlements.GetRandomElement();
        }
        else if (random < 0.25f)
        {
            // 25% to switch to a random clan settlement within range
            var settlementsInRange = settlementsByDistance
                .Where(tuple => tuple.DistanceSquared < maxDistanceSq)
                .ToArray();

            order.Target = settlementsInRange.GetRandomElement().Settlement;
        }

        return true;
    }
}
