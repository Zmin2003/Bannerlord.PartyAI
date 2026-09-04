using Bannerlord.PartyAI.Core;
using Helpers;
using System.Diagnostics.CodeAnalysis;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>
/// Base for the campaign behaviors that execute one <see cref="PartyOrderType"/> each by
/// feeding the vanilla AI a strongly weighted behavior score every hour.
/// </summary>
public abstract class OrderBehaviorBase : CampaignBehaviorBase
{
    /// <summary>High enough to beat any vanilla behavior score.</summary>
    protected const float OrderScore = 25f;

    protected abstract PartyOrderType OrderType { get; }

    protected OrderBehaviorBase()
    {
    }

    /// <summary>For behaviors that persist data and must keep their historical save key.</summary>
    protected OrderBehaviorBase(string legacyStringId) : base(legacyStringId)
    {
    }

    protected enum StopReason
    {
        Silent,
        TargetInvalid,
        TargetUnreachable,
        TargetEnemy,
        TargetFriendly,
        TargetSieged,
        NoValidTargets
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    // ---- Relevance -----------------------------------------------------------------------

    /// <summary>True when the party is managed and its active order is of this behavior's type.</summary>
    protected bool TryGetRelevantOrder(
        MobileParty? party,
        [NotNullWhen(true)] out PartyProfile? profile,
        [NotNullWhen(true)] out PartyOrder? order)
        => TryGetRelevantOrder(party?.LeaderHero, out profile, out order);

    protected bool TryGetRelevantOrder(
        Hero? hero,
        [NotNullWhen(true)] out PartyProfile? profile,
        [NotNullWhen(true)] out PartyOrder? order)
    {
        profile = null;
        order = null;

        if (!PartyAi.Parties.IsHeroManageable(hero))
        {
            return false;
        }

        profile = PartyAi.Parties.Profile(hero);
        if (!profile.HasActiveOrder || profile.Order.Behavior != OrderType)
        {
            return false;
        }

        order = profile.Order;
        return true;
    }

    // ---- Stopping ------------------------------------------------------------------------

    /// <summary>Ends the order, tells the player why, and lets automation clean up after itself.</summary>
    protected static void Stop(MobileParty party, PartyProfile profile, PartyOrder order, StopReason reason)
    {
        switch (reason)
        {
            case StopReason.TargetInvalid: Notify.OrderStoppedTargetInvalid(party, order); break;
            case StopReason.TargetUnreachable: Notify.OrderStoppedTargetUnreachable(party, order); break;
            case StopReason.TargetEnemy: Notify.OrderStoppedTargetEnemy(party, order); break;
            case StopReason.TargetFriendly: Notify.OrderStoppedTargetFriendly(party, order); break;
            case StopReason.TargetSieged: Notify.OrderStoppedTargetSieged(party, order); break;
            case StopReason.NoValidTargets: Notify.OrderStoppedNoValidTargets(party, order); break;
        }

        profile.ClearOrder();

        if (order.IsAutomatic
            && !PartyAi.Defense.HandleAutomaticOrderFailure(party.LeaderHero, order.AutomationToken))
        {
            PartyAi.Offense.HandleAutomaticOrderFailure(party.LeaderHero, order.AutomationToken);
        }
    }

    // ---- Target validation ---------------------------------------------------------------

    /// <summary>Requires a settlement target the party's faction is not at war with.</summary>
    protected static bool RequireFriendlySettlement(
        MobileParty party,
        PartyProfile profile,
        PartyOrder order,
        [NotNullWhen(true)] out Settlement? settlement)
    {
        settlement = order.Target as Settlement;
        if (settlement is null)
        {
            Stop(party, profile, order, StopReason.TargetInvalid);
            return false;
        }

        if (FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
        {
            Stop(party, profile, order, StopReason.TargetEnemy);
            settlement = null;
            return false;
        }

        return true;
    }

    /// <summary>Requires a settlement target the party's faction is at war with.</summary>
    protected static bool RequireHostileSettlement(
        MobileParty party,
        PartyProfile profile,
        PartyOrder order,
        [NotNullWhen(true)] out Settlement? settlement)
    {
        settlement = order.Target as Settlement;
        if (settlement is null)
        {
            Stop(party, profile, order, StopReason.TargetInvalid);
            return false;
        }

        if (!FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
        {
            Stop(party, profile, order, StopReason.TargetFriendly);
            settlement = null;
            return false;
        }

        return true;
    }

    /// <summary>Requires a party target; <paramref name="hostile"/> selects which faction relation is required.</summary>
    protected static bool RequirePartyTarget(
        MobileParty party,
        PartyProfile profile,
        PartyOrder order,
        bool hostile,
        [NotNullWhen(true)] out MobileParty? target)
    {
        target = order.Target as MobileParty;
        if (target is null || !target.IsActive)
        {
            Stop(party, profile, order, StopReason.TargetInvalid);
            target = null;
            return false;
        }

        bool atWar = FactionManager.IsAtWarAgainstFaction(party.MapFaction, target.MapFaction);
        if (atWar != hostile)
        {
            Stop(party, profile, order, hostile ? StopReason.TargetFriendly : StopReason.TargetEnemy);
            target = null;
            return false;
        }

        return true;
    }

    // ---- Scoring -------------------------------------------------------------------------

    protected static void AddBehaviorScore(AIBehaviorData behaviorData, float score, PartyThinkParams thinkParams)
    {
        if (thinkParams.TryGetBehaviorScore(in behaviorData, out float previous))
        {
            thinkParams.SetBehaviorScore(in behaviorData, score + previous);
        }
        else
        {
            thinkParams.AddBehaviorScore((behaviorData, score));
        }
    }

    /// <summary>Scores travelling to a settlement. Returns false when it is unreachable.</summary>
    protected static bool TryNavigateToSettlement(
        MobileParty party,
        Settlement settlement,
        AiBehavior behavior,
        PartyThinkParams thinkParams)
    {
        AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
            party,
            settlement,
            isTargetingPort: false,
            out MobileParty.NavigationType navigation,
            out _,
            out bool isFromPort);

        if (navigation == MobileParty.NavigationType.None)
        {
            return false;
        }

        // A party acting on the mod's behalf must not stall to gather a kingdom army first (and pay influence for it).
        bool isAutomatic = PartyAi.Parties.Profile(party.LeaderHero).Order is { IsAutomatic: true };
        bool willGatherArmy = !isAutomatic && thinkParams.PossibleArmyMembersUponArmyCreation?.Count > 5;

        AddBehaviorScore(
            new AIBehaviorData(settlement, behavior, navigation, willGatherArmy, isFromPort, false),
            OrderScore,
            thinkParams);
        return true;
    }

    /// <summary>Scores travelling to another party. Returns false when it is unreachable.</summary>
    protected static bool TryNavigateToParty(
        MobileParty party,
        MobileParty target,
        AiBehavior behavior,
        PartyThinkParams thinkParams)
    {
        bool isFromPort = false;
        bool isTargetingPort = false;
        MobileParty.NavigationType navigation;

        if (target.CurrentSettlement is Settlement settlement)
        {
            isTargetingPort = settlement.HasPort && party.IsCurrentlyAtSea;
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                party, settlement, isTargetingPort, out navigation, out _, out isFromPort);
        }
        else
        {
            AiHelper.GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(
                party, target, out navigation, out _);
        }

        if (navigation == MobileParty.NavigationType.None)
        {
            return false;
        }

        bool willGatherArmy = thinkParams.PossibleArmyMembersUponArmyCreation?.Count > 5;
        AddBehaviorScore(
            new AIBehaviorData(target, behavior, navigation, willGatherArmy, isFromPort, isTargetingPort),
            OrderScore,
            thinkParams);
        return true;
    }

    /// <summary>Stops the vanilla AI from wandering off while executing the order.</summary>
    protected static void HoldInitiative(MobileParty party, float attack = 0f, float avoid = 1f, float hours = 2f)
        => party.Ai.SetInitiative(attack, avoid, hours);
}
