using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>
/// Roam between the clan's own settlements. The patrol target is re-rolled daily and whenever
/// the party strays too far from clan lands.
/// </summary>
public sealed class PatrolClanLandsOrder : OrderBehaviorBase
{
    protected override PartyOrderType OrderType => PartyOrderType.PatrolClanLands;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, OnDailyTickParty);
    }

    private void OnDailyTickParty(MobileParty party)
    {
        if (TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order))
        {
            PickTarget(party, profile, order);
        }
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order))
        {
            return;
        }

        bool targetValid = order.Target is Settlement current
            && !FactionManager.IsAtWarAgainstFaction(party.MapFaction, current.MapFaction);
        if (!targetValid && !PickTarget(party, profile, order))
        {
            return;
        }

        if (order.Target is not Settlement settlement)
        {
            order.Target = null;
            return;
        }

        if (!TryNavigateToSettlement(party, settlement, AiBehavior.PatrolAroundPoint, thinkParams))
        {
            order.Target = null; // Fall back to vanilla AI until the target is re-rolled.
        }
    }

    /// <summary>Re-rolls the patrol target; shared with the main-party autopilot.</summary>
    internal static bool PickTarget(MobileParty party, PartyProfile profile, PartyOrder order)
    {
        var settlements = party.LeaderHero.Clan.Settlements;
        if (settlements.Count == 0)
        {
            profile.ClearOrder();
            return false;
        }

        float range = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default)
            * profile.PatrolRadius;
        float maxDistanceSq = range * 2f * (range * 2f);

        var byDistance = settlements
            .Select(settlement => (Settlement: settlement, DistanceSq: settlement.Position.DistanceSquared(party.Position)))
            .OrderBy(pair => pair.DistanceSq)
            .ToArray();

        // Too far from home: head straight for the closest fief.
        if (byDistance[0].DistanceSq > maxDistanceSq)
        {
            order.Target = byDistance[0].Settlement;
            return true;
        }

        float roll = MBRandom.RandomFloat;
        if (roll < 0.01f)
        {
            order.Target = settlements.GetRandomElement();
        }
        else if (roll < 0.25f)
        {
            order.Target = byDistance.Where(pair => pair.DistanceSq < maxDistanceSq).ToArray().GetRandomElement().Settlement;
        }
        else if (order.Target is null)
        {
            order.Target = byDistance[0].Settlement;
        }

        return true;
    }
}
