using System.Diagnostics.CodeAnalysis;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.PartyAI.Battle;

internal sealed class SmartPlayerTactic : TacticComponent
{
    private enum BattlePhase
    {
        Uninitialized,
        Hold,
        Approach,
        Engage,
        Press
    }

    private BattlePhase _phase = BattlePhase.Uninitialized;

    internal SmartPlayerTactic(Team team)
        : base(team)
    {
    }

    protected override void ManageFormationCounts()
    {
        AssignTacticFormations1121();
    }

    protected override bool CheckAndSetAvailableFormationsChanged()
    {
        int controlledCount = Team.GetAIControlledFormationCount();
        bool changed = controlledCount != _AIControlledFormationCount;
        _AIControlledFormationCount = controlledCount;

        changed |= !IsValid(_mainInfantry, static formation => formation.QuerySystem.IsInfantryFormation);
        changed |= !IsValid(_archers, static formation => formation.QuerySystem.IsRangedFormation);
        changed |= !IsValid(_leftCavalry, static formation => formation.QuerySystem.IsCavalryFormation);
        changed |= !IsValid(_rightCavalry, static formation => formation.QuerySystem.IsCavalryFormation);
        changed |= !IsValid(_rangedCavalry, static formation => formation.QuerySystem.IsRangedCavalryFormation);

        if (changed)
        {
            IsTacticReapplyNeeded = true;
        }

        return changed;
    }

    public override void TickOccasionally()
    {
        if (!AreFormationsCreated)
        {
            base.TickOccasionally();
            return;
        }

        bool formationsChanged = CheckAndSetAvailableFormationsChanged();
        if (formationsChanged)
        {
            ManageFormationCounts();
        }

        BattlePhase nextPhase = DeterminePhase();
        if (formationsChanged || nextPhase != _phase || IsTacticReapplyNeeded)
        {
            _phase = nextPhase;
            ApplyPhase();
            IsTacticReapplyNeeded = false;
        }

        base.TickOccasionally();
    }

    protected override float GetTacticWeight()
    {
        float powerRatio = MBMath.ClampFloat(Team.QuerySystem.RemainingPowerRatio, 0.5f, 2f);
        int activeClasses = 0;
        activeClasses += Team.QuerySystem.InfantryRatio > 0.08f ? 1 : 0;
        activeClasses += Team.QuerySystem.RangedRatio > 0.08f ? 1 : 0;
        activeClasses += Team.QuerySystem.CavalryRatio > 0.08f ? 1 : 0;
        activeClasses += Team.QuerySystem.RangedCavalryRatio > 0.08f ? 1 : 0;

        float combinedArmsFactor = 0.82f + activeClasses * 0.07f;
        float defensivePenalty = powerRatio < 0.85f ? 0.72f : 1f;
        return combinedArmsFactor * defensivePenalty * MathF.Sqrt(powerRatio);
    }

    private BattlePhase DeterminePhase()
    {
        Formation? reference = _mainInfantry ?? _archers ?? _leftCavalry ?? _rightCavalry ?? _rangedCavalry;
        FormationQuerySystem? enemy = reference?.CachedClosestEnemyFormation;
        if (reference == null || enemy == null)
        {
            return Team.TeamAI.IsDefenseApplicable ? BattlePhase.Hold : BattlePhase.Approach;
        }

        float distance = MathF.Sqrt(reference.CachedClosestEnemyFormationDistanceSquared);
        float enemySpeed = MathF.Max(enemy.MovementSpeedMaximum, 1f);
        float timeToContact = distance / enemySpeed;
        float joinThreshold = _phase >= BattlePhase.Engage ? 12f : 8f;

        float powerRatio = Team.QuerySystem.RemainingPowerRatio;
        bool shouldDefend = Team.TeamAI.IsDefenseApplicable
            || powerRatio < 0.92f
            || (Team.QuerySystem.AllyRangedRatio
                > Team.QuerySystem.EnemyRangedRatio + 0.15f);

        if (timeToContact > joinThreshold)
        {
            return shouldDefend
                ? BattlePhase.Hold
                : BattlePhase.Approach;
        }

        bool hasDecisiveAdvantage = powerRatio >= 1.20f;
        bool enemyIsCollapsing = Team.QuerySystem.EnemyUnitCount <= MathF.Max(10f, Team.QuerySystem.AllyUnitCount * 0.45f);
        return hasDecisiveAdvantage || enemyIsCollapsing
            ? BattlePhase.Press
            : BattlePhase.Engage;
    }

    private void ApplyPhase()
    {
        ApplyInfantryPhase();
        ApplyRangedPhase();
        ApplyCavalryPhase(_leftCavalry, FormationAI.BehaviorSide.Left);
        ApplyCavalryPhase(_rightCavalry, FormationAI.BehaviorSide.Right);
        ApplyHorseArcherPhase();
    }

    private void ApplyInfantryPhase()
    {
        if (!Prepare(_mainInfantry))
        {
            return;
        }

        switch (_phase)
        {
            case BattlePhase.Hold:
                SetInfantryArrangement(defensive: true);
                _mainInfantry.AI.SetBehaviorWeight<BehaviorHoldHighGround>(1.8f).RangedAllyFormation = _archers;
                _mainInfantry.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.15f);
                break;
            case BattlePhase.Approach:
                SetInfantryArrangement(defensive: Team.QuerySystem.EnemyRangedRatio > 0.20f);
                _mainInfantry.AI.SetBehaviorWeight<BehaviorCautiousAdvance>(1.8f);
                _mainInfantry.AI.SetBehaviorWeight<BehaviorAdvance>(0.7f);
                break;
            case BattlePhase.Engage:
                SetInfantryArrangement(defensive: false);
                _mainInfantry.AI.SetBehaviorWeight<BehaviorCautiousAdvance>(0.8f);
                _mainInfantry.AI.SetBehaviorWeight<BehaviorTacticalCharge>(
                    Team.QuerySystem.RemainingPowerRatio < 0.85f ? 1.1f : 1.8f);
                break;
            case BattlePhase.Press:
                SetInfantryArrangement(defensive: false);
                _mainInfantry.AI.SetBehaviorWeight<BehaviorTacticalCharge>(2.2f);
                _mainInfantry.AI.SetBehaviorWeight<BehaviorCharge>(1.2f);
                break;
        }
    }

    private void ApplyRangedPhase()
    {
        if (!Prepare(_archers))
        {
            return;
        }

        switch (_phase)
        {
            case BattlePhase.Hold:
            case BattlePhase.Approach:
                _archers.SetArrangementOrder(new ArrangementOrder(
                    ArrangementOrder.ArrangementOrderEnum.Loose));
                _archers.AI.SetBehaviorWeight<BehaviorScreenedSkirmish>(1.8f);
                _archers.AI.SetBehaviorWeight<BehaviorSkirmishLine>(1.5f);
                _archers.AI.SetBehaviorWeight<BehaviorSkirmish>(0.6f);
                break;
            case BattlePhase.Engage:
                _archers.SetArrangementOrder(new ArrangementOrder(
                    ArrangementOrder.ArrangementOrderEnum.Loose));
                _archers.AI.SetBehaviorWeight<BehaviorScreenedSkirmish>(1.5f);
                _archers.AI.SetBehaviorWeight<BehaviorSkirmish>(1.8f);
                _archers.AI.SetBehaviorWeight<BehaviorSkirmishLine>(1.2f);
                break;
            case BattlePhase.Press:
                _archers.SetArrangementOrder(new ArrangementOrder(
                    ArrangementOrder.ArrangementOrderEnum.Line));
                _archers.AI.SetBehaviorWeight<BehaviorSkirmishLine>(1.4f);
                _archers.AI.SetBehaviorWeight<BehaviorAdvance>(1.1f);
                _archers.AI.SetBehaviorWeight<BehaviorSkirmish>(0.8f);
                break;
        }
    }

    private void ApplyCavalryPhase(Formation? cavalry, FormationAI.BehaviorSide side)
    {
        if (!Prepare(cavalry))
        {
            return;
        }

        cavalry.SetArrangementOrder(new ArrangementOrder(
            ArrangementOrder.ArrangementOrderEnum.Skein));

        if (_phase is BattlePhase.Hold or BattlePhase.Approach
            || Team.QuerySystem.RemainingPowerRatio < 0.85f)
        {
            cavalry.AI.SetBehaviorWeight<BehaviorProtectFlank>(1.8f).FlankSide = side;
            cavalry.AI.SetBehaviorWeight<BehaviorCavalryScreen>(1.4f);
            cavalry.AI.SetBehaviorWeight<BehaviorFlank>(0.6f);
            return;
        }

        cavalry.AI.SetBehaviorWeight<BehaviorFlank>(_phase == BattlePhase.Press ? 1.4f : 1.8f);
        cavalry.AI.SetBehaviorWeight<BehaviorTacticalCharge>(_phase == BattlePhase.Press ? 2.2f : 1.6f);
        cavalry.AI.SetBehaviorWeight<BehaviorProtectFlank>(0.4f).FlankSide = side;
    }

    private void ApplyHorseArcherPhase()
    {
        if (!Prepare(_rangedCavalry))
        {
            return;
        }

        _rangedCavalry.AI.SetBehaviorWeight<BehaviorMountedSkirmish>(1.8f);
        _rangedCavalry.AI.SetBehaviorWeight<BehaviorHorseArcherSkirmish>(2f);
        if (_phase == BattlePhase.Press
            && Team.QuerySystem.RemainingPowerRatio >= 1f)
        {
            _rangedCavalry.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.8f);
        }
    }

    private void SetInfantryArrangement(bool defensive)
    {
        if (_mainInfantry is null)
        {
            return;
        }

        bool useShieldWall = defensive
            && _mainInfantry.QuerySystem.HasShieldUnitRatio >= 0.30f;
        _mainInfantry.SetArrangementOrder(new ArrangementOrder(
            useShieldWall
                ? ArrangementOrder.ArrangementOrderEnum.ShieldWall
                : ArrangementOrder.ArrangementOrderEnum.Line));
    }

    private static bool Prepare([NotNullWhen(true)] Formation? formation)
    {
        if (formation == null || formation.CountOfUnits == 0 || !formation.IsAIControlled)
        {
            return false;
        }

        formation.AI.ResetBehaviorWeights();
        SetDefaultBehaviorWeights(formation);
        return true;
    }

    private static bool IsValid(Formation? formation, System.Func<Formation, bool> predicate)
    {
        return formation == null
            || (formation.CountOfUnits > 0 && predicate(formation));
    }
}
