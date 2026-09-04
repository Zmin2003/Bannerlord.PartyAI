using Bannerlord.PartyAI.Parties.Recruitment;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>
/// Tour nearby settlements recruiting troops that fit the party's template and composition.
/// Also issues itself automatically when a party drops below its auto-recruit threshold.
/// </summary>
public sealed class RecruitOrder : OrderBehaviorBase
{
    private const string LegacyStringId = "RecruitmentBehavior";
    private const int SettlementCooldownDays = 10;

    private List<SettlementVisitLog> _recentlyRecruitedFrom = new();

    public RecruitOrder() : base(LegacyStringId)
    {
    }

    protected override PartyOrderType OrderType => PartyOrderType.RecruitFromTemplate;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, OnDailyTickParty);
        CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnTroopRecruited);
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_recentlyRecruitedFromSettlements", ref _recentlyRecruitedFrom);
        _recentlyRecruitedFrom ??= new();
    }

    private void OnDailyTick()
        => _recentlyRecruitedFrom.RemoveAll(log => log.Visited.ElapsedDaysUntilNow > SettlementCooldownDays);

    private void OnDailyTickParty(MobileParty party)
    {
        Hero? hero = party?.LeaderHero;
        if (!PartyAi.Parties.IsHeroManageable(hero))
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(hero);
        bool wantsRecruits = profile.AutoRecruitment
            && party!.PartySizeRatio < profile.AutoRecruitmentPercentage
            && party.Army is null;
        bool alreadyRecruiting = profile.Order?.Behavior == OrderType
            || profile.OrderQueue.Any(order => order.Behavior == OrderType);

        if (wantsRecruits && !alreadyRecruiting)
        {
            profile.SetOrder(OrderType);
        }
    }

    private void OnTroopRecruited(Hero recruiter, Settlement settlement, Hero source, CharacterObject troop, int amount)
    {
        if (settlement is not null
            && TryGetRelevantOrder(recruiter, out _, out _)
            && recruiter.PartyBelongedTo is MobileParty party)
        {
            _recentlyRecruitedFrom.Add(new SettlementVisitLog(settlement, CampaignTime.Now, party));
        }
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order))
        {
            return;
        }

        int freeSlots = party.Party.PartySizeLimit - party.Party.NumberOfAllMembers;
        if (freeSlots <= 0 || party.PartySizeRatio > profile.Composition.Total)
        {
            profile.ClearOrder();
            return;
        }

        PartyComposition current = RecruitmentRules.GetPartyComposition(party.Party, profile);
        Settlement? target = order.Target as Settlement;

        if (target is null || !IsStillWorthVisiting(party, profile, target, current))
        {
            target = Navigation.FindNearestSettlement(party, settlement => IsGoodRecruitingTarget(party, profile, settlement, current));
            order.Target = target;
        }

        if (target is null)
        {
            Stop(party, profile, order, StopReason.NoValidTargets);
            return;
        }

        if (!TryNavigateToSettlement(party, target, AiBehavior.GoToSettlement, thinkParams))
        {
            Stop(party, profile, order, StopReason.TargetUnreachable);
            return;
        }

        HoldInitiative(party);
    }

    /// <summary>
    /// Target selection for a party that is not driven by the AI think loop (the player's party
    /// under autopilot). Returns null when nothing nearby is worth a visit.
    /// </summary>
    internal Settlement? FindTarget(MobileParty party, PartyProfile profile, Settlement? current)
    {
        PartyComposition composition = RecruitmentRules.GetPartyComposition(party.Party, profile);
        if (current is not null && IsStillWorthVisiting(party, profile, current, composition))
        {
            return current;
        }

        return Navigation.FindNearestSettlement(party, settlement => IsGoodRecruitingTarget(party, profile, settlement, composition));
    }

    /// <summary>True once the party is as full as its composition asks for.</summary>
    internal static bool IsSatisfied(MobileParty party, PartyProfile profile)
        => party.Party.PartySizeLimit - party.Party.NumberOfAllMembers <= 0
            || party.PartySizeRatio > profile.Composition.Total;

    /// <summary>Records a visit made outside the AI think loop so the settlement is not revisited at once.</summary>
    internal void MarkVisited(MobileParty party, Settlement settlement)
        => _recentlyRecruitedFrom.Add(new SettlementVisitLog(settlement, CampaignTime.Now, party));

    private bool IsStillWorthVisiting(MobileParty party, PartyProfile profile, Settlement settlement, PartyComposition current)
        => !WasRecentlyVisited(party, settlement)
            && RecruitmentRules.CollectEligibleVolunteers(party, settlement, profile, current).Count > 0
            && IsSuitableForVisiting(party, settlement);

    private bool IsGoodRecruitingTarget(MobileParty party, PartyProfile profile, Settlement settlement, PartyComposition current)
    {
        if (!settlement.IsVillage && !settlement.IsTown)
        {
            return false;
        }

        if (!IsSuitableForVisiting(party, settlement) || WasRecentlyVisited(party, settlement))
        {
            return false;
        }

        if (!profile.RecruitFromEnemySettlements
            && FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
        {
            return false;
        }

        return RecruitmentRules.CollectEligibleVolunteers(party, settlement, profile, current).Count > 0;
    }

    private bool WasRecentlyVisited(MobileParty party, Settlement settlement)
        => _recentlyRecruitedFrom.Any(log => log.Settlement == settlement && log.Party == party);

    /// <summary>Simplified version of the vanilla AiVisitSettlementBehavior suitability check.</summary>
    private static bool IsSuitableForVisiting(MobileParty party, Settlement settlement)
    {
        if (settlement.Party.MapEvent is not null)
        {
            return false;
        }

        if (settlement.Party.SiegeEvent is not null
            && (settlement.Party.SiegeEvent.IsBlockadeActive || !party.HasNavalNavigationCapability))
        {
            return false;
        }

        return !settlement.IsVillage || settlement.Village.VillageState == Village.VillageStates.Normal;
    }

    /// <summary>Save id 5. Records which party recruited where so it does not return immediately.</summary>
    public sealed class SettlementVisitLog
    {
        [SaveableProperty(1)] public Settlement Settlement { get; private set; }
        [SaveableProperty(2)] public CampaignTime Visited { get; private set; }
        [SaveableProperty(3)] public MobileParty Party { get; private set; }

        public SettlementVisitLog(Settlement settlement, CampaignTime visited, MobileParty party)
        {
            Settlement = settlement;
            Visited = visited;
            Party = party;
        }
    }
}
