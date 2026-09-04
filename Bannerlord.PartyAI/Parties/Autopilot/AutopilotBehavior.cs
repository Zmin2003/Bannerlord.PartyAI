using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties.Orders;
using Bannerlord.PartyAI.Parties.Recruitment;
using Helpers;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Parties.Autopilot;

/// <summary>
/// Drives the player's own party with the same order queue lord parties use, but only while the
/// player is not steering it. The vanilla AI never thinks for the main party, so this behavior
/// issues the map moves itself (exactly the calls a map click makes) and hands control back the
/// moment the player clicks anywhere.
/// </summary>
public sealed class AutopilotBehavior : CampaignBehaviorBase
{
    private const float TickInterval = 0.25f;

    private float _sinceLastTick;
    private float _idleSeconds;
    private bool _playerSteering = true;
    private bool _driving;
    private Settlement? _drivingInto;
    private bool _leaveAfterVisit;
    private bool _recruitOnArrival;
    private CampaignTime _patrolRolledAt = CampaignTime.Never;
    private CampaignVec2 _waypoint = CampaignVec2.Invalid;
    private Settlement? _announcedArrivalAt;

    /// <summary>True while the autopilot itself is calling a SetMove method, so the steering patches ignore it.</summary>
    public static bool Issuing { get; private set; }

    public override void RegisterEvents()
    {
        CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    /// <summary>The player's own auto-recruit threshold, mirrored from what lord parties do daily.</summary>
    private void OnDailyTick()
    {
        MobileParty? main = MobileParty.MainParty;
        if (!PartyAi.Settings.MainPartyAutopilot || main is null || main.Army is not null)
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(Hero.MainHero);
        bool wantsRecruits = profile.AutoRecruitment && main.PartySizeRatio < profile.AutoRecruitmentPercentage;
        bool alreadyRecruiting = profile.Order?.Behavior == PartyOrderType.RecruitFromTemplate
            || profile.OrderQueue.Any(order => order.Behavior == PartyOrderType.RecruitFromTemplate);
        if (wantsRecruits && !alreadyRecruiting && CanEnterSettlements())
        {
            profile.SetOrder(PartyOrderType.RecruitFromTemplate);
        }
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    // ---- Player interaction ----------------------------------------------------------------------

    /// <summary>Called from the SetMove patches whenever something other than the autopilot moves the main party.</summary>
    public void OnPlayerSteered()
    {
        if (Issuing)
        {
            return;
        }

        _playerSteering = true;
        _driving = false;
        _drivingInto = null;
        _leaveAfterVisit = false;
        _idleSeconds = 0f;
    }

    /// <summary>Short status line for the control panel.</summary>
    public TextObject Status
    {
        get
        {
            if (!PartyAi.Settings.MainPartyAutopilot)
            {
                return L.T("{=PAI_AUTOPILOT_OFF}Autopilot off");
            }

            if (_leaveAfterVisit)
            {
                return L.T("{=PAI_AUTOPILOT_LEAVING}Autopilot: finishing the visit");
            }

            if (_driving)
            {
                PartyOrder? order = PartyAi.Parties.Profile(Hero.MainHero).Order;
                return order is null
                    ? L.T("{=PAI_AUTOPILOT_DRIVING}Autopilot driving")
                    : L.T("{=PAI_AUTOPILOT_DRIVING_ORDER}Autopilot: {ORDER}", "ORDER", OrderText.Status(order));
            }

            return _playerSteering
                ? L.T("{=PAI_AUTOPILOT_WAITING}Autopilot paused: you are steering")
                : L.T("{=PAI_AUTOPILOT_IDLE}Autopilot idle: no order and no fallback order");
        }
    }

    // ---- Per-frame driver -----------------------------------------------------------------------

    /// <summary>Called from the module's application tick every frame while a campaign is loaded.</summary>
    public void ApplicationTick(float realDt)
    {
        _sinceLastTick += realDt;
        if (_sinceLastTick < TickInterval)
        {
            return;
        }

        float dt = _sinceLastTick;
        _sinceLastTick = 0f;

        if (!PartyAi.Settings.MainPartyAutopilot || Campaign.Current is null || !Campaign.Current.GameStarted)
        {
            Reset();
            return;
        }

        MobileParty main = MobileParty.MainParty;
        if (main is null || Hero.MainHero is null || !Hero.MainHero.IsActive)
        {
            return;
        }

        if (GameStateManager.Current.ActiveState is not MapState mapState)
        {
            return;
        }

        if (_leaveAfterVisit)
        {
            TryLeaveSettlement(main, mapState);
            return;
        }

        // Anything that already has the player's attention wins over the autopilot.
        if (mapState.AtMenu
            || PlayerEncounter.Current is not null
            || main.CurrentSettlement is not null
            || main.MapEvent is not null
            || main.BesiegerCamp is not null
            || (main.Army is not null && main.Army.LeaderParty != main)
            || Campaign.Current.TimeControlMode is CampaignTimeControlMode.Stop or CampaignTimeControlMode.FastForwardStop)
        {
            _idleSeconds = 0f;
            return;
        }

        bool idle = Campaign.Current.IsMainPartyWaiting;
        if (!idle)
        {
            _idleSeconds = 0f;
            return;
        }

        _idleSeconds += dt;
        if (_playerSteering)
        {
            if (_idleSeconds < PartyAi.Settings.AutopilotResumeSeconds)
            {
                return;
            }

            _playerSteering = false;
        }

        Drive(main);
    }

    private void Reset()
    {
        _playerSteering = true;
        _driving = false;
        _drivingInto = null;
        _leaveAfterVisit = false;
        _recruitOnArrival = false;
        _idleSeconds = 0f;
    }

    // ---- Orders --------------------------------------------------------------------------------

    private void Drive(MobileParty main)
    {
        PartyProfile profile = PartyAi.Parties.Profile(Hero.MainHero);
        if (!profile.HasActiveOrder)
        {
            PartyOrder? fallback = profile.FallbackOrder;
            if (fallback is null || fallback.Behavior == PartyOrderType.None)
            {
                _driving = false;
                return;
            }

            profile.SetOrder(fallback.Behavior, fallback.Target);
        }

        if (profile.Order is not PartyOrder order)
        {
            _driving = false;
            return;
        }

        bool hostile;
        switch (order.Behavior)
        {
            case PartyOrderType.RecruitFromTemplate:
                DriveRecruit(main, profile, order);
                break;

            case PartyOrderType.VisitSettlement:
            case PartyOrderType.StayInSettlement:
                if (order.Target is Settlement visit && IsFriendly(main, visit) && CanEnterSettlements())
                {
                    GoToSettlement(main, visit, enter: true, recruit: false);
                }
                else
                {
                    profile.ClearOrder();
                }
                break;

            case PartyOrderType.BesiegeSettlement:
                // The main party cannot besiege through the AI; bring the player to the gates instead.
                if (order.Target is Settlement siegeTarget && FactionManager.IsAtWarAgainstFaction(main.MapFaction, siegeTarget.MapFaction))
                {
                    GoToSettlement(main, siegeTarget, enter: false, recruit: false);
                }
                else
                {
                    profile.ClearOrder();
                }
                break;

            case PartyOrderType.DefendSettlement:
            case PartyOrderType.PatrolAroundPoint:
                if (order.Target is Settlement patrolTarget && IsFriendly(main, patrolTarget))
                {
                    Patrol(main, profile, patrolTarget, order.Behavior == PartyOrderType.DefendSettlement ? 0.5f : 1f);
                }
                else
                {
                    profile.ClearOrder();
                }
                break;

            case PartyOrderType.PatrolClanLands:
                DrivePatrolClanLands(main, profile, order);
                break;

            case PartyOrderType.EscortParty:
            case PartyOrderType.AttackParty:
                hostile = order.Behavior == PartyOrderType.AttackParty;
                if (order.Target is MobileParty target
                    && target.IsActive
                    && FactionManager.IsAtWarAgainstFaction(main.MapFaction, target.MapFaction) == hostile)
                {
                    FollowParty(main, target, hostile);
                }
                else
                {
                    profile.ClearOrder();
                }
                break;

            default:
                profile.ClearOrder();
                break;
        }
    }

    private void DriveRecruit(MobileParty main, PartyProfile profile, PartyOrder order)
    {
        if (RecruitOrder.IsSatisfied(main, profile) || !CanEnterSettlements())
        {
            profile.ClearOrder();
            return;
        }

        Settlement? target = PartyAi.Recruiting.FindTarget(main, profile, order.Target as Settlement);
        if (target is null)
        {
            Notify.Info(L.T("{=PAI_AUTOPILOT_NO_RECRUITS}Autopilot: no settlement nearby has recruits worth the trip."));
            profile.ClearOrder();
            return;
        }

        order.Target = target;
        GoToSettlement(main, target, enter: true, recruit: true);
    }

    private void DrivePatrolClanLands(MobileParty main, PartyProfile profile, PartyOrder order)
    {
        bool stale = _patrolRolledAt == CampaignTime.Never || _patrolRolledAt.ElapsedDaysUntilNow >= 1f;
        bool valid = order.Target is Settlement current && IsFriendly(main, current);
        if ((stale || !valid) && !PatrolClanLandsOrder.PickTarget(main, profile, order))
        {
            return;
        }

        if (stale || !valid)
        {
            _patrolRolledAt = CampaignTime.Now;
        }

        if (order.Target is Settlement settlement)
        {
            Patrol(main, profile, settlement, 1f);
        }
        else
        {
            profile.ClearOrder();
        }
    }

    // ---- Movement primitives --------------------------------------------------------------------

    private void GoToSettlement(MobileParty main, Settlement settlement, bool enter, bool recruit)
    {
        AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
            main, settlement, isTargetingPort: false,
            out MobileParty.NavigationType navigation, out _, out _);
        if (navigation == MobileParty.NavigationType.None)
        {
            Notify.Warning(L.T("{=PAI_AUTOPILOT_UNREACHABLE}Autopilot cannot find a route to {SETTLEMENT}.", "SETTLEMENT", settlement.Name));
            PartyAi.Parties.Profile(Hero.MainHero).ClearOrder();
            return;
        }

        if (!enter)
        {
            // Stop short of the gates so the encounter is the player's call.
            if (main.Position.Distance(settlement.GatePosition) < 4f)
            {
                if (_announcedArrivalAt != settlement)
                {
                    _announcedArrivalAt = settlement;
                    Notify.Info(L.T("{=PAI_AUTOPILOT_AT_GATES}Autopilot: your party has reached {SETTLEMENT}. Whether to besiege it is your decision; clear the order or click elsewhere to move on.", "SETTLEMENT", settlement.Name));
                }

                return;
            }

            CampaignVec2 point = NavigationHelper.FindReachablePointAroundPosition(settlement.GatePosition, main.NavigationCapability, 3f, 1.5f);
            if (!point.IsValid())
            {
                return;
            }

            Issue(() => main.SetMoveGoToPoint(point, main.NavigationCapability));
            _drivingInto = null;
            return;
        }

        _drivingInto = settlement;
        _recruitOnArrival = recruit;
        Issue(() => main.SetMoveGoToSettlement(settlement, navigation, isTargetingThePort: false));
    }

    private void Patrol(MobileParty main, PartyProfile profile, Settlement around, float radiusFactor)
    {
        float radius = Math.Max(4f, Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default)
            * 0.25f * profile.PatrolRadius * radiusFactor);

        CampaignVec2 center = around.GatePosition;
        if (main.Position.Distance(center) > radius * 2f)
        {
            _waypoint = NavigationHelper.FindReachablePointAroundPosition(center, main.NavigationCapability, radius * 0.5f, 1f);
        }
        else
        {
            _waypoint = NavigationHelper.FindReachablePointAroundPosition(center, main.NavigationCapability, radius, Math.Min(2f, radius * 0.3f));
        }

        if (!_waypoint.IsValid())
        {
            return;
        }

        _drivingInto = null;
        CampaignVec2 waypoint = _waypoint;
        Issue(() => main.SetMoveGoToPoint(waypoint, main.NavigationCapability));
    }

    private void FollowParty(MobileParty main, MobileParty target, bool hostile)
    {
        AiHelper.GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(main, target, out MobileParty.NavigationType navigation, out _);
        if (navigation == MobileParty.NavigationType.None)
        {
            PartyAi.Parties.Profile(Hero.MainHero).ClearOrder();
            return;
        }

        _drivingInto = null;
        if (hostile)
        {
            if (main.DefaultBehavior == AiBehavior.EngageParty && main.TargetParty == target)
            {
                return;
            }

            Issue(() => main.SetMoveEngageParty(target, navigation));
        }
        else
        {
            if (main.DefaultBehavior == AiBehavior.EscortParty && main.TargetParty == target)
            {
                return;
            }

            Issue(() => main.SetMoveEscortParty(target, navigation, isTargetingPort: false));
        }
    }

    private void Issue(Action move)
    {
        Issuing = true;
        try
        {
            move();
        }
        finally
        {
            Issuing = false;
        }

        _driving = true;
        _playerSteering = false;
        _idleSeconds = 0f;
    }

    // ---- Settlement visits ---------------------------------------------------------------------

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (party != MobileParty.MainParty || !PartyAi.Settings.MainPartyAutopilot || !_driving || _drivingInto != settlement)
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(Hero.MainHero);
        PartyOrder? order = profile.Order;

        if (_recruitOnArrival && order?.Behavior == PartyOrderType.RecruitFromTemplate)
        {
            // Settlement automation may already have recruited on entry; top up with the order's own rules.
            if (profile.SettlementAutomation == SettlementAutomationLevel.Off
                && (profile.RecruitFromEnemySettlements || !FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction)))
            {
                VolunteerRecruiter.Recruit(party, settlement, profile);
            }

            PartyAi.Recruiting.MarkVisited(party, settlement);
        }

        _drivingInto = null;
        _recruitOnArrival = false;

        if (order?.Behavior == PartyOrderType.StayInSettlement)
        {
            // "Wait here" means exactly that: the visit is the player's from here on.
            profile.ClearOrder();
            _driving = false;
            return;
        }

        if (order?.Behavior == PartyOrderType.VisitSettlement)
        {
            profile.ClearOrder();
        }

        _leaveAfterVisit = true;
    }

    private void TryLeaveSettlement(MobileParty main, MapState mapState)
    {
        if (main.CurrentSettlement is null)
        {
            _leaveAfterVisit = false;
            return;
        }

        // Wait for the settlement's own menu; leaving from a sub-menu or mid-encounter would confuse the game.
        if (!mapState.AtMenu || mapState.GameMenuId is not ("town" or "castle" or "village") || PlayerEncounter.Current is null)
        {
            return;
        }

        Settlement settlement = main.CurrentSettlement;
        Issuing = true;
        try
        {
            main.Position = main.IsCurrentlyAtSea ? settlement.PortPosition : settlement.GatePosition;
            if (main.Army is not null)
            {
                foreach (MobileParty attached in main.AttachedParties)
                {
                    attached.Position = main.Position;
                }
            }

            PlayerEncounter.LeaveSettlement();
            PlayerEncounter.Finish();
            main.SetMoveModeHold();
        }
        finally
        {
            Issuing = false;
        }

        _leaveAfterVisit = false;
        _driving = true;
        _playerSteering = false;
        _idleSeconds = PartyAi.Settings.AutopilotResumeSeconds;

        // Leaving a settlement normally pauses the map until the player clicks; this visit was ours, so keep going.
        if (Campaign.Current.TimeControlMode is CampaignTimeControlMode.Stop or CampaignTimeControlMode.FastForwardStop)
        {
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
        }
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    private static bool CanEnterSettlements() => PartyAi.Settings.AutopilotEntersSettlements;

    private static bool IsFriendly(MobileParty main, Settlement settlement)
        => !FactionManager.IsAtWarAgainstFaction(main.MapFaction, settlement.MapFaction);
}
