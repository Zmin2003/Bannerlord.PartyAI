
using Bannerlord.PartyAI.Parties.Orders;
using Bannerlord.PartyAI.Parties;
using Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Bannerlord.PartyAI.Towns;

public sealed class AutoDefenseBehavior : CampaignBehaviorBase
{
    private const float MilitiaStrengthFactor = 0.65f;
    private const float IncomingDefenderStrengthFactor = 0.60f;
    private const float DefenseSafetyMargin = 1.25f;
    private const float ReleaseGraceDays = 2f;
    private const float MinimumDefenseDenominator = 50f;

    private Dictionary<Hero, DefenseAssignment> _assignments = new();
    private Dictionary<Hero, CampaignTime> _lastReleasedAt = new();
    private int _nextAutomationToken = 1;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, OnSiegeStarted);
        CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.OnPartyJoinedArmyEvent.AddNonSerializedListener(this, OnPartyJoinedArmy);
        CampaignEvents.OnPartyLeftArmyEvent.AddNonSerializedListener(this, OnPartyLeftArmy);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
        CampaignEvents.OnPartyDisbandStartedEvent.AddNonSerializedListener(this, OnPartyDisbandStarted);
        CampaignEvents.OnPartyLeaderChangedEvent.AddNonSerializedListener(this, OnPartyLeaderChanged);
        CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_autoDefenseAssignments", ref _assignments);
        dataStore.SyncData("_autoDefenseLastReleasedAt", ref _lastReleasedAt);
        dataStore.SyncData("_autoDefenseNextToken", ref _nextAutomationToken);

        _assignments ??= new();
        _lastReleasedAt ??= new();
        if (_nextAutomationToken <= 0)
        {
            int highestToken = _assignments.Values
                .Where(assignment => assignment is not null)
                .Select(assignment => assignment.AutomationToken)
                .DefaultIfEmpty(0)
                .Max();
            _nextAutomationToken = highestToken >= int.MaxValue
                ? 1
                : Math.Max(1, highestToken + 1);
        }
    }

    public bool IsAutomaticallyDefending(Hero? hero)
        => TryGetAssignment(hero, out DefenseAssignment? assignment)
            && !assignment.PendingRestore
            && HasMatchingAutomaticOrder(hero!, assignment);

    public bool IsAutomaticallyDefending(MobileParty? party)
        => IsAutomaticallyDefending(party?.LeaderHero);

    public bool TryGetAssignment(
        Hero? hero,
        [NotNullWhen(true)] out DefenseAssignment? assignment)
    {
        assignment = null;
        if (hero is null
            || !_assignments.TryGetValue(hero, out DefenseAssignment? candidate)
            || candidate is null
            || candidate.Hero != hero
            || candidate.AutomationToken <= 0)
        {
            return false;
        }

        assignment = candidate;
        return true;
    }

    public bool CancelAutomaticDefense(Hero? hero)
        => hero is not null && RequestCancellation(hero, restoreSuspendedOrder: true);

    internal bool HandleAutomaticOrderFailure(Hero? hero, int automationToken)
    {
        if (hero is null
            || automationToken <= 0
            || !TryGetAssignment(hero, out DefenseAssignment? assignment)
            || assignment.AutomationToken != automationToken)
        {
            return false;
        }

        FinishCancellation(assignment, restoreSuspendedOrder: true);
        return true;
    }

    internal bool OwnsGarrisonDonation(
        MobileParty? party,
        Settlement? settlement)
    {
        if (party?.LeaderHero is not Hero hero
            || settlement is null
            || !TryGetAssignment(hero, out DefenseAssignment? assignment)
            || assignment.PendingRestore
            || assignment.TargetSettlement != settlement
            || !HasMatchingAutomaticOrder(hero, assignment))
        {
            return false;
        }

        TownSettings options = PartyAi.Towns.Settings;
        FiefSettings settings = PartyAi.Towns.Effective(settlement);
        return options.Enabled
            && options.AutoDefenseEnabled
            && options.AutoDonateTroops
            && settings.Enabled
            && settings.AutoDefenseEnabled;
    }

    private void OnDailyTick()
    {
        TownSettings options = PartyAi.Towns.Settings;
        options.Normalize();

        ReconcileAssignments(options);
        RemoveExpiredCooldowns(options);

        if (!options.Enabled || !options.AutoDefenseEnabled)
        {
            return;
        }

        DispatchDefenders(options, onlySettlement: null);
    }

    private void OnSiegeStarted(SiegeEvent siegeEvent)
    {
        TownSettings options = PartyAi.Towns.Settings;
        options.Normalize();
        ReconcileAssignments(options);
        RemoveExpiredCooldowns(options);

        if (!options.Enabled || !options.AutoDefenseEnabled)
        {
            return;
        }

        Settlement? settlement = siegeEvent?.BesiegedSettlement;
        if (settlement is not null)
        {
            DispatchDefenders(options, settlement);
        }
    }

    private void OnSettlementEntered(
        MobileParty party,
        Settlement settlement,
        Hero hero)
    {
        Hero? leader = party?.LeaderHero ?? hero;
        if (leader is null
            || !TryGetAssignment(leader, out DefenseAssignment? assignment)
            || assignment.PendingRestore
            || assignment.TargetSettlement != settlement
            || !HasMatchingAutomaticOrder(leader, assignment))
        {
            return;
        }

        assignment.MarkReachedTarget();
        TryDonate(assignment);
    }

    private void OnSettlementOwnerChanged(
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        foreach (Hero hero in _assignments.Values
            .OfType<DefenseAssignment>()
            .Where(assignment => assignment.TargetSettlement == settlement)
            .Select(assignment => assignment.Hero)
            .Where(hero => hero is not null)
            .ToList())
        {
            RequestCancellation(hero, restoreSuspendedOrder: true);
        }
    }

    private void OnPartyJoinedArmy(MobileParty party)
    {
        Hero? hero = party?.LeaderHero;
        if (hero is null
            || !TryGetAssignment(hero, out DefenseAssignment? assignment))
        {
            return;
        }

        if (assignment.PendingRestore)
        {
            return;
        }

        if (!HasMatchingAutomaticOrder(hero, assignment))
        {
            FinishCancellation(assignment, restoreSuspendedOrder: false);
            return;
        }

        PartyProfile settings = PartyAi.Parties.Profile(hero);
        settings.AbandonAutomaticOrder(assignment.AutomationToken);
        assignment.DeferRestore();
    }

    private void OnPartyLeftArmy(MobileParty party, Army army)
    {
        Hero? hero = party?.LeaderHero;
        if (hero is null
            || !TryGetAssignment(hero, out DefenseAssignment? assignment)
            || !assignment.PendingRestore
            || party?.Army is not null)
        {
            return;
        }

        FinishCancellation(assignment, restoreSuspendedOrder: true);
    }

    private void OnMobilePartyDestroyed(
        MobileParty destroyedParty,
        PartyBase destroyerParty)
    {
        Hero? leader = destroyedParty?.LeaderHero;
        if (leader is not null)
        {
            RemoveWithoutRestore(leader);
        }
    }

    private void OnPartyDisbandStarted(MobileParty party)
    {
        Hero? leader = party?.LeaderHero;
        if (leader is not null)
        {
            RemoveWithoutRestore(leader);
        }
    }

    private void OnPartyLeaderChanged(MobileParty party, Hero oldLeader)
    {
        if (oldLeader is not null)
        {
            RemoveWithoutRestore(oldLeader);
        }
    }

    private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
    {
        if (prisoner is not null)
        {
            RemoveWithoutRestore(prisoner);
        }
    }

    private void ReconcileAssignments(TownSettings options)
    {
        HashSet<Settlement> settlementsReleasingDefender = new();
        foreach (KeyValuePair<Hero, DefenseAssignment> pair in _assignments.ToList())
        {
            Hero hero = pair.Key;
            DefenseAssignment? assignment = pair.Value;
            if (assignment is null)
            {
                _assignments.Remove(hero);
                continue;
            }

            MobileParty? party = hero.PartyBelongedTo;

            if (!IsAssignmentPartyValid(assignment, party))
            {
                RemoveWithoutRestore(hero);
                continue;
            }

            if (assignment.PendingRestore)
            {
                if (party!.Army is null)
                {
                    FinishCancellation(assignment, restoreSuspendedOrder: true);
                }
                continue;
            }

            if (party!.Army is not null)
            {
                PartyProfile armySettings = PartyAi.Parties.Profile(hero);
                armySettings.AbandonAutomaticOrder(assignment.AutomationToken);
                assignment.DeferRestore();
                continue;
            }

            if (!HasMatchingAutomaticOrder(hero, assignment))
            {
                FinishCancellation(assignment, restoreSuspendedOrder: false);
                continue;
            }

            if (!options.Enabled
                || !options.AutoDefenseEnabled
                || !IsManagedDefenseTarget(assignment.TargetSettlement))
            {
                FinishCancellation(assignment, restoreSuspendedOrder: true);
                continue;
            }

            if (party.CurrentSettlement == assignment.TargetSettlement)
            {
                assignment.MarkReachedTarget();
            }

            DefenseNeed need = BuildDefenseNeed(assignment.TargetSettlement, options);
            float threatRatioAfterRelease = need.ThreatRatioAfterRemoving(party);
            if (need.IsUnderSiege
                || threatRatioAfterRelease > options.ReleaseThreatThreshold
                || need.NeedsTargetDefenseAfterRemoving(
                    party,
                    options.ReleaseThreatThreshold))
            {
                assignment.MarkThreatSeen();
            }

            TryDonate(assignment);

            bool servedMinimumTime = assignment.HasReachedTarget
                && assignment.ReachedTargetAt.ElapsedDaysUntilNow
                    >= options.MinimumGarrisonDays;
            bool safeForGracePeriod = assignment.LastThreatSeenAt.ElapsedDaysUntilNow
                >= ReleaseGraceDays;
            bool reinforcementStillTraveling = options.AutoDonateTroops
                && !assignment.DonationCompleted
                && need.TargetGarrisonTroops > need.ActualGarrisonTroops
                && party.CurrentSettlement != assignment.TargetSettlement;
            if (!need.IsUnderSiege
                && assignment.HasReachedTarget
                && threatRatioAfterRelease <= options.ReleaseThreatThreshold
                && !need.NeedsTargetDefenseAfterRemoving(
                    party,
                    options.ReleaseThreatThreshold)
                && servedMinimumTime
                && safeForGracePeriod
                && !reinforcementStillTraveling
                && !settlementsReleasingDefender.Contains(
                    assignment.TargetSettlement))
            {
                settlementsReleasingDefender.Add(assignment.TargetSettlement);
                FinishCancellation(assignment, restoreSuspendedOrder: true);
            }
        }
    }

    private void DispatchDefenders(
        TownSettings options,
        Settlement? onlySettlement)
    {
        int maximumPerTown = Math.Max(0, options.MaxDefendingPartiesPerTown);
        if (maximumPerTown == 0)
        {
            return;
        }

        IEnumerable<Settlement> settlements = onlySettlement is null
            ? Settlement.All.Where(settlement => settlement.IsFortification)
            : new[] { onlySettlement };

        List<DefenseNeed> needs = settlements
            .Where(IsManagedDefenseTarget)
            .Select(settlement => BuildDefenseNeed(settlement, options))
            .Where(need => need.RequiresDefender(options))
            .OrderByDescending(need => need.IsUnderSiege)
            .ThenByDescending(need => need.RequiresCombatReinforcement(options))
            .ThenByDescending(need => need.ThreatUrgency)
            .ThenByDescending(need => need.Priority)
            .ThenByDescending(need => need.DefenseDeficit)
            .ThenByDescending(need => need.GarrisonDeficit)
            .ThenBy(need => need.Settlement.StringId)
            .ToList();

        if (needs.Count == 0)
        {
            return;
        }

        List<DefenderCandidate> candidates = GetCandidates(options).ToList();
        int reserve = Math.Max(0, options.ReserveMobileParties);

        foreach (DefenseNeed need in needs)
        {
            int reserveForNeed = need.IsUnderSiege ? 0 : reserve;
            while (need.AutomaticDefenderCount < maximumPerTown
                && candidates.Count > reserveForNeed
                && need.RequiresDefender(options))
            {
                DefenderCandidate? candidate = SelectCandidate(
                    need,
                    candidates,
                    options);
                if (candidate is null)
                {
                    break;
                }

                candidates.Remove(candidate);
                if (!Assign(candidate.Party, need.Settlement))
                {
                    continue;
                }

                need.AddIncomingDefender(
                    candidate.Strength,
                    candidate.DonationCapacity);
            }
        }
    }

    private IEnumerable<DefenderCandidate> GetCandidates(TownSettings options)
    {
        foreach (var component in Clan.PlayerClan.WarPartyComponents)
        {
            MobileParty? party = component?.MobileParty;
            Hero? hero = party?.LeaderHero;
            if (party is null
                || hero is null
                || party == MobileParty.MainParty
                || !party.IsActive
                || party.IsDisbanding
                || party.IsCurrentlyUsedByAQuest
                || party.Army is not null
                || party.MapEvent is not null
                || party.SiegeEvent is not null
                || party.CurrentSettlement?.IsUnderSiege == true
                || hero.IsDead
                || hero.IsDisabled
                || hero.IsPrisoner
                || _assignments.ContainsKey(hero)
                || IsCoolingDown(hero, options)
                || party.PartySizeRatio < options.MinimumPartyStrengthRatio
                || !CanInterruptCurrentOrder(hero))
            {
                continue;
            }

            PartyProfile settings = PartyAi.Parties.Profile(hero);
            int donationCapacity = settings.AllowDonateTroops
                ? GarrisonTransfer.GetMaximumDonation(
                    party,
                    options.MaxDonationRatio,
                    options.MinimumTroopsAfterDonation)
                : 0;
            yield return new DefenderCandidate(party, donationCapacity);
        }
    }

    private static bool CanInterruptCurrentOrder(Hero hero)
    {
        PartyProfile settings = PartyAi.Parties.Profile(hero);
        if (!settings.HasActiveOrder)
        {
            return true;
        }

        PartyOrder? current = settings.Order;
        PartyOrder? fallback = settings.FallbackOrder;
        return current is not null
            && fallback is not null
            && current.AutomationToken == 0
            && current.Behavior == fallback.Behavior
            && current.Target == fallback.Target;
    }

    private static DefenderCandidate? SelectCandidate(
        DefenseNeed need,
        IEnumerable<DefenderCandidate> candidates,
        TownSettings options)
    {
        List<DefenderCandidate> available = candidates
            .Select(candidate =>
            {
                candidate.Distance = GetAdjustedDistance(
                    candidate.Party,
                    need.Settlement);
                return candidate;
            })
            .Where(candidate => candidate.Distance < float.MaxValue)
            .Where(candidate => !need.RequiresDonorOnly(options)
                || candidate.DonationCapacity > 0)
            .ToList();

        if (need.RequiresDonorOnly(options))
        {
            int desiredDonation = Math.Max(1, need.GarrisonDeficit);
            DefenderCandidate? sufficientDonor = available
                .Where(candidate =>
                    candidate.DonationCapacity >= desiredDonation)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => Math.Abs(
                    candidate.DonationCapacity - desiredDonation))
                .ThenBy(candidate => candidate.Party.LeaderHero?.StringId)
                .FirstOrDefault();
            if (sufficientDonor is not null)
            {
                return sufficientDonor;
            }

            return available
                .OrderByDescending(candidate => candidate.DonationCapacity)
                .ThenBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Party.LeaderHero?.StringId)
                .FirstOrDefault();
        }

        float desiredStrength = Math.Max(1f, need.DefenseDeficit);
        return available
            .OrderByDescending(candidate => candidate.Strength >= desiredStrength)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => Math.Abs(candidate.Strength - desiredStrength))
            .ThenBy(candidate => candidate.Party.LeaderHero?.StringId)
            .FirstOrDefault();
    }

    private bool Assign(MobileParty party, Settlement settlement)
    {
        Hero? hero = party?.LeaderHero;
        if (party is null || hero is null || _assignments.ContainsKey(hero))
        {
            return false;
        }

        int token = NextAutomationToken();
        PartyOrder automaticOrder = new(
            PartyOrderType.DefendSettlement,
            settlement,
            token);
        PartyProfile settings = PartyAi.Parties.Profile(hero);
        if (!settings.TryBeginAutomaticOrder(
            automaticOrder,
            out PartyOrder? suspendedOrder,
            out List<PartyOrder> suspendedQueue))
        {
            return false;
        }

        DefenseAssignment assignment = new(
            hero,
            settlement,
            CampaignTime.Now,
            suspendedOrder,
            suspendedQueue,
            token);
        if (party.CurrentSettlement == settlement)
        {
            assignment.MarkReachedTarget();
        }

        _assignments[hero] = assignment;
        return true;
    }

    private int NextAutomationToken()
    {
        if (_nextAutomationToken <= 0 || _nextAutomationToken == int.MaxValue)
        {
            _nextAutomationToken = 1;
        }

        while (_assignments.Values.Any(
            assignment => assignment is not null
                && assignment.AutomationToken == _nextAutomationToken))
        {
            _nextAutomationToken++;
            if (_nextAutomationToken <= 0)
            {
                _nextAutomationToken = 1;
            }
        }

        return _nextAutomationToken++;
    }

    private void TryDonate(DefenseAssignment assignment)
    {
        if (!HasMatchingAutomaticOrder(assignment.Hero, assignment))
        {
            return;
        }

        TownSettings options = PartyAi.Towns.Settings;
        FiefSettings settlementSettings = PartyAi.Towns.Effective(assignment.TargetSettlement);
        int targetTroops = EffectiveDonationTarget(settlementSettings);

        GarrisonTransfer.TryDonate(
            assignment,
            options.Enabled
                && options.AutoDefenseEnabled
                && options.AutoDonateTroops
                && settlementSettings.Enabled
                && settlementSettings.AutoDefenseEnabled,
            targetTroops,
            options.MaxDonationRatio,
            options.MinimumTroopsAfterDonation);
    }

    private bool RequestCancellation(
        Hero hero,
        bool restoreSuspendedOrder)
    {
        if (!TryGetAssignment(hero, out DefenseAssignment? assignment))
        {
            return false;
        }

        MobileParty? party = hero.PartyBelongedTo;
        if (restoreSuspendedOrder && party?.Army is not null)
        {
            PartyProfile settings = PartyAi.Parties.Profile(hero);
            settings.AbandonAutomaticOrder(assignment.AutomationToken);
            assignment.DeferRestore();
            return true;
        }

        FinishCancellation(assignment, restoreSuspendedOrder);
        return true;
    }

    private void FinishCancellation(
        DefenseAssignment assignment,
        bool restoreSuspendedOrder)
    {
        Hero hero = assignment.Hero;
        PartyProfile settings = PartyAi.Parties.Profile(hero);
        if (restoreSuspendedOrder)
        {
            settings.TryRestoreAutomaticOrder(
                assignment.AutomationToken,
                assignment.SuspendedOrder,
                assignment.SuspendedQueue);
        }
        else
        {
            settings.AbandonAutomaticOrder(assignment.AutomationToken);
        }

        _assignments.Remove(hero);
        _lastReleasedAt[hero] = CampaignTime.Now;
    }

    private void RemoveWithoutRestore(Hero hero)
    {
        if (_assignments.TryGetValue(hero, out DefenseAssignment? assignment))
        {
            if (assignment is not null && assignment.AutomationToken > 0)
            {
                PartyAi.Parties.Profile(hero)
                    .AbandonAutomaticOrder(assignment.AutomationToken);
            }
            _assignments.Remove(hero);
        }

        _lastReleasedAt.Remove(hero);
    }

    private bool IsCoolingDown(Hero hero, TownSettings options)
        => _lastReleasedAt.TryGetValue(hero, out CampaignTime releasedAt)
            && (releasedAt.IsNow || releasedAt.IsPast)
            && releasedAt.ElapsedDaysUntilNow < options.ReassignmentCooldownDays;

    private void RemoveExpiredCooldowns(TownSettings options)
    {
        foreach (Hero hero in _lastReleasedAt
            .Where(pair => (!pair.Value.IsNow && !pair.Value.IsPast)
                || pair.Value.ElapsedDaysUntilNow
                    >= options.ReassignmentCooldownDays)
            .Select(pair => pair.Key)
            .ToList())
        {
            _lastReleasedAt.Remove(hero);
        }
    }

    private static bool IsAssignmentPartyValid(
        DefenseAssignment assignment,
        MobileParty? party)
    {
        Hero? hero = assignment.Hero;
        return hero is not null
            && assignment.TargetSettlement is not null
            && assignment.AutomationToken > 0
            && party is not null
            && party.IsActive
            && !party.IsDisbanding
            && !party.IsCurrentlyUsedByAQuest
            && !party.IsCaravan
            && !party.IsGarrison
            && !party.IsMilitia
            && party.LeaderHero == hero
            && hero.Clan == Clan.PlayerClan
            && !hero.IsDead
            && !hero.IsDisabled
            && !hero.IsPrisoner;
    }

    private static bool IsManagedDefenseTarget(Settlement settlement)
    {
        if (settlement is null
            || !settlement.IsFortification
            || !PartyAi.Towns.IsTownManageable(settlement))
        {
            return false;
        }

        FiefSettings settings = PartyAi.Towns.Effective(settlement);
        return settings.Enabled && settings.AutoDefenseEnabled;
    }

    private DefenseNeed BuildDefenseNeed(
        Settlement settlement,
        TownSettings options)
    {
        FiefSettings settings = PartyAi.Towns.Effective(settlement);
        int targetGarrisonTroops = EffectiveDonationTarget(settings);

        float enemyThreat = 0f;
        float radius = Math.Max(1f, options.ThreatRadius);
        foreach (MobileParty enemy in MobileParty.AllLordParties)
        {
            if (enemy is null
                || !enemy.IsActive
                || enemy.IsDisbanding
                || enemy.MapFaction is null
                || !FactionManager.IsAtWarAgainstFaction(
                    enemy.MapFaction,
                    settlement.MapFaction))
            {
                continue;
            }

            float distance;
            try
            {
                distance = enemy.GetPosition2D.Distance(settlement.GetPosition2D);
            }
            catch (KeyNotFoundException)
            {
                continue;
            }

            if (distance > radius)
            {
                continue;
            }

            float distanceWeight = 0.25f
                + 0.75f * (1f - Math.Max(0f, Math.Min(1f, distance / radius)));
            float intentWeight = enemy.BesiegedSettlement == settlement
                ? 1.75f
                : enemy.TargetSettlement == settlement
                    || enemy.ShortTermTargetSettlement == settlement
                    ? 1.25f
                    : 1f;
            enemyThreat += enemy.Party.EstimatedStrength
                * distanceWeight
                * intentWeight;
        }

        MobileParty? garrison = settlement.Town?.GarrisonParty;
        float defenseStrength = garrison?.Party.EstimatedStrength ?? 0f;
        int actualGarrisonTroops = garrison?.MemberRoster.TotalManCount ?? 0;
        int projectedGarrisonTroops = actualGarrisonTroops;
        defenseStrength += settlement.Militia * MilitiaStrengthFactor;

        foreach (MobileParty defender in settlement.Parties)
        {
            if (defender is null
                || defender == garrison
                || defender.IsGarrison
                || defender.IsMilitia
                || defender.IsCaravan
                || defender.IsVillager
                || defender.MapFaction is null
                || FactionManager.IsAtWarAgainstFaction(
                    defender.MapFaction,
                    settlement.MapFaction))
            {
                continue;
            }

            defenseStrength += defender.Party.EstimatedStrength;
        }

        List<DefenseAssignment> incomingAssignments = _assignments.Values
            .OfType<DefenseAssignment>()
            .Where(assignment => !assignment.PendingRestore
                && assignment.TargetSettlement == settlement
                && HasMatchingAutomaticOrder(assignment.Hero, assignment))
            .ToList();
        foreach (DefenseAssignment assignment in incomingAssignments)
        {
            MobileParty? incoming = assignment.Hero?.PartyBelongedTo;
            if (incoming is not null && incoming.CurrentSettlement != settlement)
            {
                defenseStrength += incoming.Party.EstimatedStrength
                    * IncomingDefenderStrengthFactor;

                PartyProfile partySettings = PartyAi.Parties.Profile(assignment.Hero);
                if (!assignment.DonationCompleted && partySettings.AllowDonateTroops)
                {
                    projectedGarrisonTroops += GarrisonTransfer.GetMaximumDonation(
                        incoming,
                        options.MaxDonationRatio,
                        options.MinimumTroopsAfterDonation);
                }
            }
        }

        return new DefenseNeed(
            settlement,
            settings.DefensePriority,
            EffectiveDefenseTarget(settlement, settings),
            targetGarrisonTroops,
            enemyThreat,
            defenseStrength,
            actualGarrisonTroops,
            projectedGarrisonTroops,
            incomingAssignments.Count);
    }

    private static int EffectiveDonationTarget(
        FiefSettings settings)
        => Math.Max(0, settings.TargetGarrisonTroops);

    private static float EffectiveDefenseTarget(
        Settlement settlement,
        FiefSettings settings)
    {
        if (settings.TargetDefenseStrength > 0f)
        {
            return settings.TargetDefenseStrength;
        }

        float baseTarget = settlement.IsTown ? 450f : 300f;
        float prosperityAdjustment = Math.Min(
            250f,
            Math.Max(0f, settlement.Town?.Prosperity ?? 0f) * 0.03f);
        float priorityMultiplier = settings.DefensePriority switch
        {
            DefensePriority.Low => 0.8f,
            DefensePriority.High => 1.2f,
            DefensePriority.Critical => 1.5f,
            _ => 1f
        };
        return (baseTarget + prosperityAdjustment) * priorityMultiplier;
    }

    private static float GetAdjustedDistance(
        MobileParty party,
        Settlement settlement)
    {
        try
        {
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                party,
                settlement,
                isTargetingPort: false,
                out MobileParty.NavigationType navigationType,
                out float distance,
                out _);

            if (party.HasNavalNavigationCapability && settlement.HasPort)
            {
                AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                    party,
                    settlement,
                    isTargetingPort: true,
                    out MobileParty.NavigationType portNavigationType,
                    out float portDistance,
                    out _);
                if (portNavigationType != MobileParty.NavigationType.None
                    && portDistance < distance)
                {
                    navigationType = portNavigationType;
                    distance = portDistance;
                }
            }

            return navigationType == MobileParty.NavigationType.None
                ? float.MaxValue
                : distance;
        }
        catch (KeyNotFoundException)
        {
            return float.MaxValue;
        }
    }

    private sealed class DefenderCandidate
    {
        internal MobileParty Party { get; }
        internal float Strength { get; }
        internal int DonationCapacity { get; }
        internal float Distance { get; set; } = float.MaxValue;

        internal DefenderCandidate(MobileParty party, int donationCapacity)
        {
            Party = party;
            Strength = party.Party.EstimatedStrength;
            DonationCapacity = donationCapacity;
        }
    }

    private sealed class DefenseNeed
    {
        internal Settlement Settlement { get; }
        internal bool IsUnderSiege => Settlement.IsUnderSiege;
        internal DefensePriority Priority { get; }
        internal float TargetDefenseStrength { get; }
        internal int TargetGarrisonTroops { get; }
        internal float EnemyThreat { get; }
        internal float DefenseStrength { get; private set; }
        internal int ActualGarrisonTroops { get; }
        internal int ProjectedGarrisonTroops { get; private set; }
        internal int AutomaticDefenderCount { get; private set; }
        internal float ThreatRatio => EnemyThreat
            / Math.Max(MinimumDefenseDenominator, DefenseStrength);
        internal float DefenseDeficit => Math.Max(
            0f,
            Math.Max(TargetDefenseStrength, EnemyThreat * DefenseSafetyMargin)
                - DefenseStrength);
        internal int GarrisonDeficit => Math.Max(
            0,
            TargetGarrisonTroops - ProjectedGarrisonTroops);
        internal float ThreatUrgency => Math.Min(10f, ThreatRatio)
            * (1f + (int)Priority * 0.25f);

        internal DefenseNeed(
            Settlement settlement,
            DefensePriority priority,
            float targetDefenseStrength,
            int targetGarrisonTroops,
            float enemyThreat,
            float defenseStrength,
            int actualGarrisonTroops,
            int projectedGarrisonTroops,
            int automaticDefenderCount)
        {
            Settlement = settlement;
            Priority = priority;
            TargetDefenseStrength = targetDefenseStrength;
            TargetGarrisonTroops = targetGarrisonTroops;
            EnemyThreat = enemyThreat;
            DefenseStrength = defenseStrength;
            ActualGarrisonTroops = actualGarrisonTroops;
            ProjectedGarrisonTroops = projectedGarrisonTroops;
            AutomaticDefenderCount = automaticDefenderCount;
        }

        internal float ThreatRatioAfterRemoving(MobileParty party)
        {
            float contribution = party.CurrentSettlement == Settlement
                ? party.Party.EstimatedStrength
                : party.Party.EstimatedStrength * IncomingDefenderStrengthFactor;
            float remainingDefense = Math.Max(0f, DefenseStrength - contribution);
            return EnemyThreat
                / Math.Max(MinimumDefenseDenominator, remainingDefense);
        }

        internal bool NeedsTargetDefenseAfterRemoving(
            MobileParty party,
            float minimumThreatRatio)
        {
            if (EnemyThreat <= 0f)
            {
                return false;
            }

            float contribution = party.CurrentSettlement == Settlement
                ? party.Party.EstimatedStrength
                : party.Party.EstimatedStrength * IncomingDefenderStrengthFactor;
            float remainingDefense = Math.Max(0f, DefenseStrength - contribution);
            float remainingThreatRatio = EnemyThreat
                / Math.Max(MinimumDefenseDenominator, remainingDefense);
            return remainingThreatRatio > minimumThreatRatio
                && remainingDefense < TargetDefenseStrength;
        }

        internal bool RequiresCombatReinforcement(TownSettings options)
            => IsUnderSiege
                || ThreatRatio >= options.DispatchThreatThreshold
                || (EnemyThreat > 0f
                    && ThreatRatio > options.ReleaseThreatThreshold
                    && DefenseStrength < TargetDefenseStrength);

        internal bool RequiresDefender(TownSettings options)
            => RequiresCombatReinforcement(options)
                || (options.AutoDonateTroops
                    && ProjectedGarrisonTroops < TargetGarrisonTroops);

        internal bool RequiresDonorOnly(TownSettings options)
            => !RequiresCombatReinforcement(options)
                && options.AutoDonateTroops
                && ProjectedGarrisonTroops < TargetGarrisonTroops;

        internal void AddIncomingDefender(float strength, int donationCapacity)
        {
            AutomaticDefenderCount++;
            DefenseStrength += strength * IncomingDefenderStrengthFactor;
            ProjectedGarrisonTroops += Math.Max(0, donationCapacity);
        }
    }

    private static bool HasMatchingAutomaticOrder(
        Hero hero,
        DefenseAssignment assignment)
    {
        if (hero is null
            || assignment is null
            || assignment.AutomationToken <= 0
            || assignment.Hero != hero)
        {
            return false;
        }

        PartyOrder? order = PartyAi.Parties.Profile(hero).Order;
        return order is not null
            && order.AutomationToken == assignment.AutomationToken
            && order.Behavior == PartyOrderType.DefendSettlement
            && order.Target == assignment.TargetSettlement;
    }
}
