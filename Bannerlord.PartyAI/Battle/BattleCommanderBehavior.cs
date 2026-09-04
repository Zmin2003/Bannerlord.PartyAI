using Bannerlord.PartyAI.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.PartyAI.Battle;

internal sealed class BattleCommanderBehavior : MissionLogic
{
    private const float EnhancedTickInterval = 0.5f;

    private readonly ModSettings? _settings;
    private readonly Dictionary<Formation, bool> _originalControlStates = new();
    private readonly Dictionary<Formation, FiringOrder> _fireDisciplineOriginalOrders = new();
    private bool _autoEnableAttempted;
    private bool _isDelegated;
    private bool _battleAssessmentShown;
    private float _nextEnhancedTickTime;
    private SmartPlayerTactic? _smartTactic;

    internal BattleCommanderBehavior(ModSettings? settings)
    {
        _settings = settings;
    }

    public override void OnAfterDeploymentFinished()
    {
        TryAutoEnable();
    }

    public override void OnMissionTick(float dt)
    {
        TryAutoEnable();

        if (IsTogglePressed())
        {
            if (_isDelegated)
            {
                RestorePlayerControl();
            }
            else
            {
                EnableAiCommand(showUnavailableMessage: true);
            }
        }

        if (_isDelegated)
        {
            TickEnhancedAi();
        }
    }

    private void TryAutoEnable()
    {
        if (_autoEnableAttempted
            || !AutoDelegateBattleCommand
            || Mission.Mode != MissionMode.Battle
            || !Mission.IsDeploymentFinished)
        {
            return;
        }

        _autoEnableAttempted = true;
        EnableAiCommand(showUnavailableMessage: false);
    }

    private bool EnableAiCommand(bool showUnavailableMessage)
    {
        if (!TryGetPlayerTeam(out Team team))
        {
            if (showUnavailableMessage)
            {
                DisplayMessage(
                    new TextObject("{=PAI_BATTLE_COMMANDER_UNAVAILABLE}Battle AI command is unavailable in this mission."),
                    Colors.Yellow);
            }

            return false;
        }

        _originalControlStates.Clear();
        _fireDisciplineOriginalOrders.Clear();
        foreach (Formation formation in team.FormationsIncludingEmpty)
        {
            _originalControlStates[formation] = formation.IsAIControlled;
        }

        team.DelegateCommandToAI();
        _isDelegated = true;
        EnableSmartCommander(team);
        ShowBattleAssessment(team);

        TextObject message = new(
            "{=PAI_BATTLE_COMMANDER_ENABLED}The battle AI now commands your formations. You still control your character. Press {KEYBIND} to resume command.");
        message.SetTextVariable("KEYBIND", GetKeybindText());
        DisplayMessage(message, Colors.Green);
        return true;
    }

    private bool RestorePlayerControl()
    {
        Team playerTeam = Mission.PlayerTeam;
        Agent player = Mission.MainAgent;
        if (playerTeam == null
            || player == null
            || !player.IsActive())
        {
            DisplayMessage(
                new TextObject("{=PAI_BATTLE_COMMANDER_CANNOT_RESUME}You cannot resume formation command while your character is incapacitated."),
                Colors.Yellow);
            return false;
        }

        DisableSmartCommander(playerTeam);

        foreach (KeyValuePair<Formation, FiringOrder> state in _fireDisciplineOriginalOrders)
        {
            if (state.Key.Team == playerTeam && state.Key.IsAIControlled)
            {
                state.Key.SetFiringOrder(state.Value);
            }
        }

        foreach (KeyValuePair<Formation, bool> state in _originalControlStates)
        {
            if (state.Key.Team != playerTeam)
            {
                continue;
            }

            bool isPlayerFormation = playerTeam.IsPlayerGeneral
                || (playerTeam.IsPlayerSergeant
                    && (state.Key.PlayerOwner == player || state.Key == player.Formation));
            state.Key.SetControlledByAI(isPlayerFormation ? false : state.Value);
        }

        _originalControlStates.Clear();
        _fireDisciplineOriginalOrders.Clear();
        _isDelegated = false;
        DisplayMessage(
            new TextObject("{=PAI_BATTLE_COMMANDER_DISABLED}You have resumed command of your formations."),
            Colors.Green);
        return true;
    }

    private bool TryGetPlayerTeam(out Team team)
    {
        team = Mission.PlayerTeam;
        return team != null
            && team.HasTeamAi
            && Mission.Mode == MissionMode.Battle
            && Mission.MissionTeamAIType != Mission.MissionTeamAITypeEnum.NoTeamAI
            && Mission.IsDeploymentFinished;
    }

    private void EnableSmartCommander(Team team)
    {
        if (!EnhancedBattleAi
            || !Mission.IsFieldBattle
            || HasExternalBattleAi())
        {
            return;
        }

        team.RemoveTacticOption(typeof(SmartPlayerTactic));
        _smartTactic = new SmartPlayerTactic(team);
        team.AddTacticOption(_smartTactic);
        team.TeamAI.ResetTactic(keepCurrentTactic: false);
        _nextEnhancedTickTime = Mission.CurrentTime;
    }

    private void DisableSmartCommander(Team team)
    {
        if (_smartTactic == null || team.TeamAI == null)
        {
            return;
        }

        team.RemoveTacticOption(typeof(SmartPlayerTactic));
        team.TeamAI.ResetTactic(keepCurrentTactic: false);
        _smartTactic = null;
    }

    private void TickEnhancedAi()
    {
        Team playerTeam = Mission.PlayerTeam;
        if (_smartTactic == null
            || playerTeam == null
            || Mission.CurrentTime < _nextEnhancedTickTime)
        {
            return;
        }

        _nextEnhancedTickTime = Mission.CurrentTime + EnhancedTickInterval;
        foreach (Formation formation in playerTeam.FormationsIncludingEmpty)
        {
            ApplyFireDiscipline(formation);
        }
    }

    private void ApplyFireDiscipline(Formation formation)
    {
        if (formation == null
            || formation.CountOfUnits == 0
            || !formation.IsAIControlled
            || (!formation.QuerySystem.IsRangedFormation && !formation.QuerySystem.IsRangedCavalryFormation))
        {
            return;
        }

        float effectiveRange = formation.QuerySystem.MissileRangeAdjusted;
        float distanceSquared = formation.CachedClosestEnemyFormationDistanceSquared;
        if (effectiveRange <= 1f || float.IsNaN(distanceSquared))
        {
            return;
        }

        float distance = MathF.Sqrt(distanceSquared);
        float openFireDistance = effectiveRange * 0.82f;
        float holdFireDistance = effectiveRange * 0.98f;

        if (distance <= openFireDistance
            && formation.FiringOrder != FiringOrder.FiringOrderFireAtWill)
        {
            RememberFiringOrder(formation);
            formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        }
        else if (distance >= holdFireDistance
            && formation.FiringOrder != FiringOrder.FiringOrderHoldYourFire)
        {
            RememberFiringOrder(formation);
            formation.SetFiringOrder(FiringOrder.FiringOrderHoldYourFire);
        }
    }

    private void RememberFiringOrder(Formation formation)
    {
        if (!_fireDisciplineOriginalOrders.ContainsKey(formation))
        {
            _fireDisciplineOriginalOrders[formation] = formation.FiringOrder;
        }
    }

    private void ShowBattleAssessment(Team team)
    {
        if (_battleAssessmentShown || !Mission.IsFieldBattle)
        {
            return;
        }

        _battleAssessmentShown = true;
        int allies = team.QuerySystem.AllyUnitCount;
        int enemies = team.QuerySystem.EnemyUnitCount;
        int powerPercent = (int)MathF.Round(team.QuerySystem.TotalPowerRatio * 100f);

        TextObject assessment = new(
            "{=PAI_BATTLE_ASSESSMENT}Battle estimate: {ALLY} vs {ENEMY} troops; allied combat power is about {POWER}% of the enemy.");
        assessment.SetTextVariable("ALLY", allies);
        assessment.SetTextVariable("ENEMY", enemies);
        assessment.SetTextVariable("POWER", powerPercent);
        DisplayMessage(
            assessment,
            powerPercent < 90 ? Colors.Yellow : Colors.Gray);

        float headcountRatio = (allies + 1f) / (enemies + 1f);
        if (headcountRatio >= 0.85f
            && headcountRatio <= 1.15f
            && powerPercent < 90)
        {
            DisplayMessage(
                new TextObject("{=PAI_EQUAL_NUMBERS_WARNING}Headcounts are close, but troop tiers, equipment and unit matchups put your side at a real disadvantage."),
                Colors.Yellow);
        }
    }

    private bool IsTogglePressed() => ToggleHotkey.IsPressed();

    private string GetKeybindText() => ToggleHotkey.ToString();

    private bool AutoDelegateBattleCommand => _settings?.AutoDelegateBattleCommand ?? true;

    private bool EnhancedBattleAi => _settings?.EnhancedBattleAi ?? false;

    private Hotkey ToggleHotkey => _settings?.ToggleBattleCommander ?? new Hotkey(InputKey.LeftControl, InputKey.M);

    private static bool HasExternalBattleAi()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
        {
            string? name = assembly.GetName().Name;
            return name != null
                && (name.Equals("RBMAI", StringComparison.OrdinalIgnoreCase)
                    || name.IndexOf("RealisticBattleAi", StringComparison.OrdinalIgnoreCase) >= 0);
        });
    }

    private static void DisplayMessage(TextObject text, Color color)
    {
        TaleWorlds.Library.InformationManager.DisplayMessage(new InformationMessage(text.ToString(), color));
    }
}
