using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.War;

/// <summary>
/// Opportunistic sieges: when the clan's free parties clearly outmatch a nearby enemy fortification,
/// they are sent to take it (as one army when the clan belongs to a kingdom). The operation is
/// abandoned when a stronger relief force shows up, peace is made, or it simply drags on too long.
/// </summary>
public sealed class OffenseBehavior : CampaignBehaviorBase
{
    // Offense tokens live far above the defense counter so the two never collide.
    private const int TokenBase = 1_000_000;
    private const float MilitiaStrengthFactor = 0.65f;
    private const float NearbyEnemyWeight = 0.5f;
    private const float MinimumTargetDefense = 60f;

    private OffenseOperation? _operation;
    private int _nextToken = TokenBase;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeace);
        CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, (party, _) => RemoveParticipant(party?.LeaderHero));
        CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, (_, prisoner) => RemoveParticipant(prisoner));
        CampaignEvents.OnPartyDisbandStartedEvent.AddNonSerializedListener(this, party => RemoveParticipant(party?.LeaderHero));
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_offenseOperation", ref _operation);
        dataStore.SyncData("_offenseNextToken", ref _nextToken);
        if (_nextToken < TokenBase)
        {
            _nextToken = TokenBase;
        }
    }

    // ---- Queries ---------------------------------------------------------------------------------

    public OffenseOperation? Current => _operation;

    public bool IsParticipant(Hero? hero)
        => hero is not null && _operation is not null && _operation.Participants.Any(participant => participant.Hero == hero);

    /// <summary>Whether this army was formed by the offensive (so joining it must not be treated as being drafted).</summary>
    public bool OwnsArmy(Army? army)
        => army is not null && _operation?.ArmyLeader is Hero leader && army.LeaderParty?.LeaderHero == leader;

    public TextObject Status
    {
        get
        {
            if (!PartyAi.Settings.AutoOffense)
            {
                return L.T("{=PAI_OFFENSE_OFF}Automatic offense is off.");
            }

            if (_operation is null)
            {
                return L.T("{=PAI_OFFENSE_IDLE}No offensive under way; targets are re-evaluated daily.");
            }

            return L.T("{=PAI_OFFENSE_STATUS}{COUNT} parties besieging {TARGET} for {DAYS} days{ARMY}.")
                .SetTextVariable("COUNT", _operation.Participants.Count)
                .SetTextVariable("TARGET", _operation.Target.Name)
                .SetTextVariable("DAYS", (int)_operation.DaysRunning)
                .SetTextVariable("ARMY", _operation.ArmyLeader is null ? TextObject.GetEmpty() : L.T("{=PAI_OFFENSE_AS_ARMY} as one army"));
        }
    }

    /// <summary>Stops the current offensive and gives every party its previous orders back.</summary>
    public void Cancel()
    {
        if (_operation is not null)
        {
            End(L.T("{=PAI_OFFENSE_CANCELLED}Offensive against {TARGET} called off.", "TARGET", _operation.Target.Name));
        }
    }

    /// <summary>An automatic besiege order failed (unreachable, target changed hands, ...).</summary>
    internal bool HandleAutomaticOrderFailure(Hero? hero, int token)
    {
        OffenseParticipant? participant = _operation?.Participants.FirstOrDefault(p => p.Hero == hero && p.Token == token);
        if (participant is null)
        {
            return false;
        }

        Release(participant, restore: true);
        _operation!.Participants.Remove(participant);
        if (_operation.Participants.Count == 0)
        {
            End(L.T("{=PAI_OFFENSE_NO_PARTIES}Offensive against {TARGET} ended: no parties left.", "TARGET", _operation.Target.Name));
        }

        return true;
    }

    // ---- Ticks -----------------------------------------------------------------------------------

    private void OnDailyTick()
    {
        if (!PartyAi.Settings.AutoOffense)
        {
            if (_operation is not null)
            {
                End(L.T("{=PAI_OFFENSE_DISABLED}Automatic offense disabled; {TARGET} siege called off.", "TARGET", _operation.Target.Name));
            }

            return;
        }

        if (_operation is not null)
        {
            Reconcile();
            return;
        }

        if (HomeIsThreatened())
        {
            return;
        }

        TryLaunch();
    }

    private void OnHourlyTick()
    {
        if (_operation is not null)
        {
            Reconcile();
        }
    }

    private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (_operation is null || _operation.Target != settlement)
        {
            return;
        }

        if (newOwner?.MapFaction == Hero.MainHero.MapFaction)
        {
            End(L.T("{=PAI_OFFENSE_CAPTURED}{TARGET} has fallen. The offensive is over; parties resume their orders.", "TARGET", settlement.Name));
        }
        else
        {
            End(L.T("{=PAI_OFFENSE_TARGET_CHANGED}{TARGET} changed hands; the offensive is called off.", "TARGET", settlement.Name));
        }
    }

    private void OnPeace(IFaction a, IFaction b, MakePeaceAction.MakePeaceDetail detail)
    {
        if (_operation is null)
        {
            return;
        }

        IFaction? ours = Hero.MainHero.MapFaction;
        IFaction? theirs = _operation.Target.MapFaction;
        if ((a == ours && b == theirs) || (a == theirs && b == ours))
        {
            End(L.T("{=PAI_OFFENSE_PEACE}Peace with {FACTION}; the siege of {TARGET} is lifted.")
                .SetTextVariable("FACTION", theirs?.Name ?? TextObject.GetEmpty())
                .SetTextVariable("TARGET", _operation.Target.Name));
        }
    }

    private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy)
    {
        if (_operation is null || !OwnsArmy(army))
        {
            return;
        }

        // The army is gone; keep the siege going with individual orders.
        _operation.ArmyLeader = null;
        foreach (OffenseParticipant participant in _operation.Participants.ToList())
        {
            EnsureBesiegeOrder(participant);
        }
    }

    // ---- Reconciliation --------------------------------------------------------------------------

    private void Reconcile()
    {
        if (_operation is null)
        {
            return;
        }

        foreach (OffenseParticipant participant in _operation.Participants.ToList())
        {
            if (!IsUsable(participant.Hero))
            {
                Release(participant, restore: false);
                _operation.Participants.Remove(participant);
            }
        }

        if (_operation.Participants.Count == 0)
        {
            End(L.T("{=PAI_OFFENSE_NO_PARTIES}Offensive against {TARGET} ended: no parties left.", "TARGET", _operation.Target.Name));
            return;
        }

        Settlement target = _operation.Target;
        if (!FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, target.MapFaction))
        {
            End(L.T("{=PAI_OFFENSE_TARGET_FRIENDLY}{TARGET} is no longer hostile; the offensive is over.", "TARGET", target.Name));
            return;
        }

        if (_operation.DaysRunning > PartyAi.Settings.OffenseMaxDays)
        {
            End(L.T("{=PAI_OFFENSE_TIMEOUT}The siege of {TARGET} has dragged on for {DAYS} days; giving up.")
                .SetTextVariable("TARGET", target.Name)
                .SetTextVariable("DAYS", PartyAi.Settings.OffenseMaxDays));
            return;
        }

        float ours = _operation.Participants.Sum(p => p.Hero.PartyBelongedTo?.Party.EstimatedStrength ?? 0f);
        float relief = NearbyEnemyStrength(target, PartyAi.Settings.OffenseRadius * 0.6f, excludeInside: true);
        if (relief > ours)
        {
            End(L.T("{=PAI_OFFENSE_RELIEF}A relief force ({RELIEF}) outmatches our {OURS} at {TARGET}; the siege is abandoned.")
                .SetTextVariable("RELIEF", (int)relief)
                .SetTextVariable("OURS", (int)ours)
                .SetTextVariable("TARGET", target.Name));
            return;
        }

        if (_operation.ArmyLeader is Hero leader && leader.PartyBelongedTo?.Army?.LeaderParty?.LeaderHero != leader)
        {
            _operation.ArmyLeader = null;
            foreach (OffenseParticipant participant in _operation.Participants)
            {
                EnsureBesiegeOrder(participant);
            }
        }
        else if (_operation.ArmyLeader is null)
        {
            foreach (OffenseParticipant participant in _operation.Participants)
            {
                EnsureBesiegeOrder(participant);
            }
        }
    }

    /// <summary>Participants outside an army must each carry the automatic besiege order.</summary>
    private void EnsureBesiegeOrder(OffenseParticipant participant)
    {
        MobileParty? party = participant.Hero.PartyBelongedTo;
        if (party is null || party.Army is not null || _operation is null)
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(participant.Hero);
        if (profile.Order?.AutomationToken == participant.Token)
        {
            return;
        }

        if (profile.Order is { IsPlayerOrder: false })
        {
            return;
        }

        var order = new PartyOrder(PartyOrderType.BesiegeSettlement, _operation.Target, participant.Token);
        if (!profile.TryBeginAutomaticOrder(order, out _, out _))
        {
            profile.ClearAllOrders();
            profile.TryBeginAutomaticOrder(order, out _, out _);
        }
    }

    // ---- Launching -----------------------------------------------------------------------------

    private static bool HomeIsThreatened()
        => Settlement.All.Any(settlement => settlement.OwnerClan == Clan.PlayerClan && settlement.IsUnderSiege);

    private void TryLaunch()
    {
        ModSettings settings = PartyAi.Settings;
        List<MobileParty> candidates = Candidates().ToList();
        int usable = candidates.Count - Math.Max(0, PartyAi.Towns.Settings.ReserveMobileParties);
        if (usable < 1)
        {
            return;
        }

        IFaction ourFaction = Hero.MainHero.MapFaction;
        var homes = Settlement.All.Where(s => s.OwnerClan == Clan.PlayerClan).Select(s => s.GetPosition2D)
            .Concat(candidates.Select(p => p.GetPosition2D))
            .ToList();

        var targets = Settlement.All
            .Where(s => s.IsFortification
                && s.Town is not null
                && FactionManager.IsAtWarAgainstFaction(ourFaction, s.MapFaction)
                && (!s.IsUnderSiege || !FactionManager.IsAtWarAgainstFaction(ourFaction, s.SiegeEvent.BesiegerCamp.LeaderParty.MapFaction))
                && homes.Any(home => home.Distance(s.GetPosition2D) <= settings.OffenseRadius))
            .Select(s => (Settlement: s, Defense: Math.Max(MinimumTargetDefense, TargetDefense(s))))
            .OrderByDescending(t => (t.Settlement.IsTown ? 2f : 1f) / (1f + t.Defense / 500f))
            .ToList();

        foreach ((Settlement target, float defense) in targets)
        {
            float required = defense * settings.OffenseStrengthRatio;
            var picked = new List<MobileParty>();
            float strength = 0f;
            foreach (MobileParty party in candidates.OrderBy(p => p.GetPosition2D.Distance(target.GetPosition2D)))
            {
                if (picked.Count >= Math.Min(settings.OffenseMaxParties, usable))
                {
                    break;
                }

                picked.Add(party);
                strength += party.Party.EstimatedStrength;
                if (strength >= required)
                {
                    break;
                }
            }

            if (strength >= required && picked.Count > 0)
            {
                Launch(target, picked, defense, strength);
                return;
            }
        }
    }

    private IEnumerable<MobileParty> Candidates()
    {
        float minimumRatio = PartyAi.Towns.Settings.MinimumPartyStrengthRatio;
        foreach (var component in Clan.PlayerClan.WarPartyComponents)
        {
            MobileParty? party = component?.MobileParty;
            Hero? hero = party?.LeaderHero;
            if (party is null
                || hero is null
                || party == MobileParty.MainParty
                || !IsUsable(hero)
                || party.Army is not null
                || party.MapEvent is not null
                || party.SiegeEvent is not null
                || party.PartySizeRatio < minimumRatio
                || PartyAi.Defense.TryGetAssignment(hero, out _)
                || !PartyAi.Parties.Profile(hero).AllowSieging
                || !CanInterrupt(PartyAi.Parties.Profile(hero)))
            {
                continue;
            }

            yield return party;
        }
    }

    private static bool CanInterrupt(PartyProfile profile)
    {
        if (!profile.HasActiveOrder)
        {
            return true;
        }

        PartyOrder current = profile.Order;
        PartyOrder? fallback = profile.FallbackOrder;
        return current.IsPlayerOrder
            && fallback is not null
            && current.Behavior == fallback.Behavior
            && current.Target == fallback.Target;
    }

    private static bool IsUsable(Hero? hero)
        => hero is not null
            && !hero.IsDead
            && !hero.IsDisabled
            && !hero.IsPrisoner
            && hero.Clan == Clan.PlayerClan
            && hero.PartyBelongedTo is { IsActive: true, IsDisbanding: false, IsCurrentlyUsedByAQuest: false } party
            && party.LeaderHero == hero;

    private void Launch(Settlement target, List<MobileParty> parties, float defense, float strength)
    {
        var participants = new List<OffenseParticipant>();
        foreach (MobileParty party in parties)
        {
            int token = NextToken();
            PartyProfile profile = PartyAi.Parties.Profile(party.LeaderHero);
            var order = new PartyOrder(PartyOrderType.BesiegeSettlement, target, token);
            if (!profile.TryBeginAutomaticOrder(order, out PartyOrder? suspended, out List<PartyOrder> queue))
            {
                continue;
            }

            participants.Add(new OffenseParticipant(party.LeaderHero, suspended, queue, token));
        }

        if (participants.Count == 0)
        {
            return;
        }

        Hero? armyLeader = null;
        Kingdom? kingdom = Clan.PlayerClan.Kingdom;
        if (PartyAi.Settings.OffenseFormArmy
            && kingdom is not null
            && participants.Count >= 2
            && participants.All(p => PartyAi.Parties.Profile(p.Hero).AllowJoinArmies))
        {
            MobileParty leaderParty = participants
                .Select(p => p.Hero.PartyBelongedTo!)
                .OrderByDescending(p => p.Party.EstimatedStrength)
                .First();
            armyLeader = leaderParty.LeaderHero;
            _operation = new OffenseOperation(target, participants, armyLeader, defense);

            var members = new MBList<MobileParty>(participants
                .Select(p => p.Hero.PartyBelongedTo!)
                .Where(p => p != leaderParty)
                .ToList());
            kingdom.CreateArmy(armyLeader, target, Army.ArmyTypes.Besieger, members);

            if (leaderParty.Army is null)
            {
                _operation.ArmyLeader = null;
                armyLeader = null;
            }
        }
        else
        {
            _operation = new OffenseOperation(target, participants, null, defense);
        }

        Notify.Success(L.T("{=PAI_OFFENSE_LAUNCHED}Offensive: {COUNT} parties ({OURS} strength) march on {TARGET} (defense {DEFENSE}){ARMY}.")
            .SetTextVariable("COUNT", participants.Count)
            .SetTextVariable("OURS", (int)strength)
            .SetTextVariable("TARGET", target.Name)
            .SetTextVariable("DEFENSE", (int)defense)
            .SetTextVariable("ARMY", armyLeader is null ? TextObject.GetEmpty() : L.T("{=PAI_OFFENSE_LED_BY}, as an army led by {LEADER}", "LEADER", armyLeader.Name)));
    }

    private int NextToken()
    {
        if (_nextToken < TokenBase || _nextToken == int.MaxValue)
        {
            _nextToken = TokenBase;
        }

        return _nextToken++;
    }

    // ---- Ending ----------------------------------------------------------------------------------

    private void End(TextObject message)
    {
        OffenseOperation? operation = _operation;
        _operation = null;
        if (operation is null)
        {
            return;
        }

        if (operation.ArmyLeader?.PartyBelongedTo?.Army is Army army && army.LeaderParty?.LeaderHero == operation.ArmyLeader)
        {
            DisbandArmyAction.ApplyByObjectiveFinished(army);
        }

        foreach (OffenseParticipant participant in operation.Participants)
        {
            Release(participant, restore: true);
        }

        Notify.Info(message);
    }

    private static void Release(OffenseParticipant participant, bool restore)
    {
        Hero hero = participant.Hero;
        if (hero is null)
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(hero);
        if (!restore || !profile.TryRestoreAutomaticOrder(participant.Token, participant.SuspendedOrder, participant.SuspendedQueue))
        {
            profile.AbandonAutomaticOrder(participant.Token);
        }

        if (hero.PartyBelongedTo is MobileParty party && party.Army is not null && party.Army.LeaderParty != party)
        {
            party.Army = null;
        }
    }

    private void RemoveParticipant(Hero? hero)
    {
        OffenseParticipant? participant = _operation?.Participants.FirstOrDefault(p => p.Hero == hero);
        if (participant is null)
        {
            return;
        }

        _operation!.Participants.Remove(participant);
        PartyAi.Parties.Profile(hero).AbandonAutomaticOrder(participant.Token);
        if (_operation.Participants.Count == 0)
        {
            End(L.T("{=PAI_OFFENSE_NO_PARTIES}Offensive against {TARGET} ended: no parties left.", "TARGET", _operation.Target.Name));
        }
    }

    // ---- Strength estimates ----------------------------------------------------------------------

    /// <summary>Garrison, militia, hostile lords inside and (discounted) hostile lords close by.</summary>
    public static float TargetDefense(Settlement settlement)
    {
        float defense = settlement.Town?.GarrisonParty?.Party.EstimatedStrength ?? 0f;
        defense += settlement.Militia * MilitiaStrengthFactor;

        IFaction ours = Hero.MainHero.MapFaction;
        foreach (MobileParty inside in settlement.Parties)
        {
            if (inside.IsLordParty && inside.MapFaction is not null && FactionManager.IsAtWarAgainstFaction(ours, inside.MapFaction))
            {
                defense += inside.Party.EstimatedStrength;
            }
        }

        defense += NearbyEnemyStrength(settlement, PartyAi.Settings.OffenseRadius * 0.5f, excludeInside: true) * NearbyEnemyWeight;
        return defense;
    }

    private static float NearbyEnemyStrength(Settlement settlement, float radius, bool excludeInside)
    {
        IFaction ours = Hero.MainHero.MapFaction;
        float total = 0f;
        foreach (MobileParty enemy in MobileParty.AllLordParties)
        {
            if (!enemy.IsActive
                || enemy.MapFaction is null
                || !FactionManager.IsAtWarAgainstFaction(ours, enemy.MapFaction)
                || (excludeInside && enemy.CurrentSettlement == settlement))
            {
                continue;
            }

            if (enemy.GetPosition2D.Distance(settlement.GetPosition2D) <= radius)
            {
                total += enemy.Party.EstimatedStrength;
            }
        }

        return total;
    }
}
