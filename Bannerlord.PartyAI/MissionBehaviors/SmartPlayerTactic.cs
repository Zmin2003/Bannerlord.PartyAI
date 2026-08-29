using System.Diagnostics.CodeAnalysis;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.PartyAI.MissionBehaviors;

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
        return 1000f;
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

        if (timeToContact > joinThreshold)
        {
            return Team.TeamAI.IsDefenseApplicable
                ? BattlePhase.Hold
                : BattlePhase.Approach;
        }

        bool hasDecisiveAdvantage = Team.QuerySystem.RemainingPowerRatio >= 1.35f;
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
                _mainInfantry.AI.SetBehaviorWeight<BehaviorHoldHighGround>(1.8f).RangedAllyFormation = _archers;
                _mainInfantry.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.5f);
                break;
            case BattlePhase.Approach:
                _mainInfantry.AI.SetBehaviorWeight<BehaviorCautiousAdvance>(1.8f);
                _mainInfantry.AI.SetBehaviorWeight<BehaviorAdvance>(0.7f);
                break;
            case BattlePhase.Engage:
                _mainInfantry.AI.SetBehaviorWeight<BehaviorCautiousAdvance>(0.8f);
                _mainInfantry.AI.SetBehaviorWeight<BehaviorTacticalCharge>(1.8f);
                break;
            case BattlePhase.Press:
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
                _archers.AI.SetBehaviorWeight<BehaviorScreenedSkirmish>(1.8f);
                _archers.AI.SetBehaviorWeight<BehaviorSkirmishLine>(1.5f);
                _archers.AI.SetBehaviorWeight<BehaviorSkirmish>(0.6f);
                break;
            case BattlePhase.Engage:
                _archers.AI.SetBehaviorWeight<BehaviorScreenedSkirmish>(1.5f);
                _archers.AI.SetBehaviorWeight<BehaviorSkirmish>(1.8f);
                _archers.AI.SetBehaviorWeight<BehaviorSkirmishLine>(1.2f);
                break;
            case BattlePhase.Press:
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

        if (_phase is BattlePhase.Hold or BattlePhase.Approach)
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
        if (_phase == BattlePhase.Press)
        {
            _rangedCavalry.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.8f);
        }
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
