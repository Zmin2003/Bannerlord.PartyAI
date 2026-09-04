using Bannerlord.PartyAI.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>
/// Housekeeping that keeps order state consistent with what happens on the map: clearing orders
/// when a party is captured, destroyed or drafted into an army, applying fallback orders, and
/// making sure the vanilla AI re-thinks while an order is active.
/// </summary>
public sealed class OrderLifecycleBehavior : CampaignBehaviorBase
{
    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
        CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, OnPartyCreated);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
        CampaignEvents.OnPartyJoinedArmyEvent.AddNonSerializedListener(this, OnPartyJoinedArmy);
        CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnHourlyTickParty(MobileParty party)
    {
        Hero? hero = party?.LeaderHero;
        if (!PartyAi.Parties.IsHeroManageable(hero))
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(hero);

        if (party!.Army is not null && ArmyRules.MustLeaveArmy(party.Army, profile))
        {
            ArmyRules.LeaveArmyWithRefund(party);
        }

        if (profile.HasActiveOrder)
        {
            party.Ai.RethinkAtNextHourlyTick = true;
            party.Ai.SetDoNotMakeNewDecisions(false);
        }
        else if (party.Army is null)
        {
            ApplyFallback(profile);
        }
    }

    private void OnPartyCreated(MobileParty party)
    {
        Hero? hero = party?.LeaderHero;
        if (!PartyAi.Parties.IsHeroManageable(hero))
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(hero);
        profile.ClearAllOrders();
        profile.ResetBudgets(PartyAi.Settings.TroopsConvertedPerDay);
        ApplyFallback(profile);
    }

    private void OnPartyDestroyed(MobileParty destroyed, PartyBase destroyer)
    {
        Hero? hero = destroyed?.LeaderHero;
        if (PartyAi.Parties.IsHeroManageable(hero))
        {
            PartyAi.Parties.Profile(hero).ClearAllOrders();
        }

        foreach (PartyProfile profile in PartyAi.Parties.ProfilesWithOrders)
        {
            if (profile.Order?.Target == destroyed)
            {
                profile.ClearOrder();
            }
        }
    }

    private void OnPartyJoinedArmy(MobileParty party)
    {
        Hero? hero = party?.LeaderHero;
        if (!PartyAi.Parties.IsHeroManageable(hero))
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(hero);
        if (!profile.HasActiveOrder)
        {
            return;
        }

        // Members of an army the offensive formed itself were not drafted away; their orders are
        // parked in the operation record and come back when it ends.
        if (!PartyAi.Offense.OwnsArmy(party!.Army))
        {
            TextObject armyName = party.Army?.Name ?? L.T("{=PAI_AN_ARMY}an army");
            Notify.OrderStoppedCalledToArmy(party, profile.Order, armyName);
        }

        profile.ClearAllOrders();
    }

    private void OnHeroPrisonerTaken(PartyBase captor, Hero prisoner)
    {
        if (PartyAi.Parties.IsHeroManageable(prisoner))
        {
            PartyAi.Parties.Profile(prisoner).ClearAllOrders();
        }
    }

    private static void ApplyFallback(PartyProfile profile)
    {
        PartyOrder? fallback = profile.FallbackOrder;
        if (fallback is not null && fallback.Behavior != PartyOrderType.None)
        {
            profile.SetOrder(fallback.Behavior, fallback.Target);
        }
    }
}
