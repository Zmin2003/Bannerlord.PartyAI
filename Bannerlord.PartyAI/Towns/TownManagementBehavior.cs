using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Finance;
using Bannerlord.PartyAI.Parties;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Towns;

/// <summary>
/// Daily management of player-clan towns and castles: building queue, daily project,
/// construction funding and governor assignment, driven by a strategy per fief.
/// </summary>
public sealed class TownManagementBehavior : CampaignBehaviorBase
{
    private TownSettings _settings = new();
    private Dictionary<Settlement, FiefSettings> _fiefs = new();
    private Dictionary<Settlement, GovernorState> _governorStates = new();

    /// <summary>Global defaults and the master switch. Edited in place by the UI.</summary>
    public TownSettings Settings => _settings;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickTownEvent.AddNonSerializedListener(this, OnDailyTickTown);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (!dataStore.SyncData("_townManagementOptions", ref _settings) && dataStore.IsLoading)
        {
            _settings = new TownSettings { Enabled = false };
        }

        dataStore.SyncData("_townManagementSettlementSettings", ref _fiefs);
        dataStore.SyncData("_townGovernorAssignmentStates", ref _governorStates);

        _settings ??= new TownSettings { Enabled = false };
        _fiefs ??= new();
        _governorStates ??= new();

        _settings.Normalize();

        foreach (Settlement settlement in _fiefs.Where(pair => pair.Value is null).Select(pair => pair.Key).ToList())
        {
            _fiefs.Remove(settlement);
        }

        foreach (KeyValuePair<Settlement, FiefSettings> pair in _fiefs)
        {
            pair.Value.AttachTo(pair.Key);
            pair.Value.Normalize();
        }

        foreach (Settlement settlement in _governorStates.Where(pair => pair.Value is null).Select(pair => pair.Key).ToList())
        {
            _governorStates.Remove(settlement);
        }

        foreach (GovernorState state in _governorStates.Values)
        {
            state.Normalize();
        }
    }

    // ---- Settings access ----------------------------------------------------------------------

    /// <summary>The stored (editable) settings of a fief; created from global defaults on first use.</summary>
    public FiefSettings Fief(Settlement settlement)
    {
        if (!_fiefs.TryGetValue(settlement, out FiefSettings? fief))
        {
            fief = FiefSettings.FromOptions(settlement, _settings);
            _fiefs.Add(settlement, fief);
        }
        else
        {
            fief.AttachTo(settlement);
            fief.Normalize();
        }

        return fief;
    }

    /// <summary>The settings that actually apply: stored values, or global defaults when the fief follows them.</summary>
    public FiefSettings Effective(Settlement settlement)
    {
        FiefSettings resolved = Fief(settlement).Resolve(_settings);
        resolved.AttachTo(settlement);
        return resolved;
    }

    public bool IsTownManageable(Settlement? settlement)
        => settlement?.Town is not null && settlement.OwnerClan == Clan.PlayerClan;

    public bool IsTownManageable(Town town) => IsTownManageable(town.Settlement);

    /// <summary>Player fiefs, towns first, richest first.</summary>
    public List<Settlement> ManageableFiefs()
        => Settlement.All
            .Where(IsTownManageable)
            .OrderByDescending(settlement => settlement.IsTown)
            .ThenByDescending(settlement => settlement.Town?.Prosperity ?? 0f)
            .ThenBy(settlement => settlement.StringId)
            .ToList();

    /// <summary>Makes every fief follow the current global defaults again, keeping each fief's on/off state.</summary>
    public void ApplyGlobalDefaultsToAllFiefs()
    {
        List<Settlement> fiefs = ManageableFiefs();
        foreach (Settlement settlement in fiefs)
        {
            FiefSettings fief = Fief(settlement);
            bool enabled = fief.Enabled;
            fief.ApplyDefaults(_settings);
            fief.Enabled = enabled;
            fief.UseGlobalDefaults = true;
            fief.Normalize();
        }

        foreach (Settlement settlement in fiefs)
        {
            TryAssignMissingGovernor(settlement);
        }
    }

    public void TryAssignMissingGovernorsFollowingGlobalDefaults()
    {
        if (!_settings.Enabled || _settings.GovernorMode != GovernorMode.Assign)
        {
            return;
        }

        foreach (Settlement settlement in ManageableFiefs())
        {
            if (Fief(settlement).UseGlobalDefaults)
            {
                TryAssignMissingGovernor(settlement);
            }
        }
    }

    public void TryAssignMissingGovernor(Settlement settlement)
    {
        if (!_settings.Enabled || !IsTownManageable(settlement) || settlement.Town.Governor is not null)
        {
            return;
        }

        FiefSettings fief = Effective(settlement);
        if (!fief.Enabled || fief.GovernorMode != GovernorMode.Assign)
        {
            return;
        }

        ManageGovernor(settlement.Town, GovernorMode.Assign, fief.AllowGovernorReassignment, fief.GovernorAssignmentCooldownDays);
    }

    // ---- Daily tick ---------------------------------------------------------------------------

    private void OnDailyTickTown(Town town)
    {
        if (!_settings.Enabled || !IsTownManageable(town))
        {
            return;
        }

        FiefSettings fief = Effective(town.Settlement);
        if (!fief.Enabled)
        {
            return;
        }

        if (fief.ManageDailyProjects)
        {
            ManageDailyProject(town, fief);
        }

        if (fief.ManageBuildingQueue)
        {
            ManageBuildingQueue(town, fief, fief.ManageDailyProjects);
        }

        if (fief.AutoFundConstruction)
        {
            FundConstruction(town, fief);
        }

        ManageGovernor(town, fief.GovernorMode, fief.AllowGovernorReassignment, fief.GovernorAssignmentCooldownDays);
    }

    private void OnSettlementOwnerChanged(
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturer,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        => _governorStates.Remove(settlement);

    // ---- Governors (public helpers for the UI) ------------------------------------------------

    /// <summary>The hero the mod would appoint for <paramref name="town"/> under the given settings, if any.</summary>
    public Hero? GovernorCandidate(Town town, FiefSettings fief)
    {
        FiefSettings effective = fief.Resolve(_settings);
        return FindGovernorCandidate(town, effective.AllowGovernorReassignment, effective.Strategy);
    }

    private static void ManageDailyProject(
        Town town,
        FiefSettings settings)
    {
        List<Building> projects = town.Buildings
            .Where(building => building.BuildingType.IsDailyProject)
            .ToList();
        List<Building> emergencyProjects = projects
            .Where(building => ProvidesEmergencyBenefit(building, town, settings))
            .ToList();
        if (emergencyProjects.Count > 0)
        {
            projects = emergencyProjects;
        }

        Building? bestProject = projects
            .OrderByDescending(building => ScoreDailyProject(building, town, settings))
            .ThenBy(building => building.BuildingType.StringId)
            .FirstOrDefault();

        if (bestProject is not null && town.CurrentDefaultBuilding != bestProject)
        {
            BuildingHelper.ChangeDefaultBuilding(bestProject, town);
        }
    }

    private static void ManageBuildingQueue(
        Town town,
        FiefSettings settings,
        bool manageDailyProjects)
    {
        List<Building> currentQueue = town.BuildingsInProgress.ToList();
        bool pauseForEmergency = manageDailyProjects
            && HasUsefulEmergencyDailyProject(town, settings);
        bool hasUsefulEmergencyBuilding = HasUsefulEmergencyBuilding(town, settings);
        var existingIndexes = currentQueue
            .Select((building, index) => new { building, index })
            .GroupBy(pair => pair.building)
            .ToDictionary(group => group.Key, group => group.Min(pair => pair.index));

        List<Building> desiredQueue = pauseForEmergency
            ? new List<Building>()
            : town.Buildings
                .Where(building => !building.BuildingType.IsDailyProject
                    && building.CurrentLevel < BuildingType.MaxLevel)
                .Distinct()
                .OrderByDescending(building => hasUsefulEmergencyBuilding
                    && ProvidesEmergencyBenefit(building, town, settings))
                .ThenByDescending(building => ScoreBuilding(
                    building,
                    town,
                    settings,
                    existingIndexes.TryGetValue(building, out int index) ? index : int.MaxValue))
                .ThenBy(building => building.BuildingType.StringId)
                .ToList();

        // Keep the project the player (or Town AI on the previous tick) already
        // started unless an emergency requires an immediate change.
        Building? activeProject = currentQueue.FirstOrDefault();
        if (!pauseForEmergency
            && !hasUsefulEmergencyBuilding
            && activeProject is not null
            && desiredQueue.Remove(activeProject))
        {
            desiredQueue.Insert(0, activeProject);
        }

        if (!currentQueue.SequenceEqual(desiredQueue))
        {
            BuildingHelper.ChangeCurrentBuildingQueue(desiredQueue, town);
        }
    }

    private void FundConstruction(
        Town town,
        FiefSettings settings)
    {
        if (town.BuildingsInProgress.Count == 0
            || town.IsUnderSiege
            || town.InRebelliousState
            || settings.ConstructionReserveTarget <= town.BoostBuildingProcess
            || settings.DailyConstructionDepositLimit <= 0)
        {
            return;
        }

        int availableGold = Treasury.Spendable;
        int missingReserve = settings.ConstructionReserveTarget - town.BoostBuildingProcess;
        int deposit = Math.Min(
            availableGold,
            Math.Min(missingReserve, settings.DailyConstructionDepositLimit));

        if (deposit > 0)
        {
            BuildingHelper.BoostBuildingProcessWithGold(
                town.BoostBuildingProcess + deposit,
                town);
        }
    }

    private void ManageGovernor(
        Town town,
        GovernorMode governorMode,
        bool allowGovernorReassignment,
        int governorCooldownDays)
    {
        if (governorMode == GovernorMode.Off)
        {
            return;
        }

        GovernorState state = StateFor(town.Settlement);
        Hero? candidate = FindGovernorCandidate(
            town,
            allowGovernorReassignment);
        Hero? recommendation = candidate != town.Governor ? candidate : null;

        if (governorMode == GovernorMode.Recommend)
        {
            if (state.RecommendedGovernor == recommendation)
            {
                return;
            }

            state.RecommendedGovernor = recommendation;
            state.LastRecommendationTime = CampaignTime.Now;
            if (recommendation is not null)
            {
                DisplayGovernorRecommendation(town, recommendation);
            }

            return;
        }

        state.RecommendedGovernor = recommendation;
        if (candidate is null
            || candidate == town.Governor
            || (town.Governor is not null
                && IsGovernorAssignmentCoolingDown(state, governorCooldownDays)))
        {
            return;
        }

        if (candidate.GovernorOf is Town previousTown && previousTown != town)
        {
            if (!allowGovernorReassignment)
            {
                return;
            }

            ChangeGovernorAction.RemoveGovernorOf(candidate);
        }

        ChangeGovernorAction.Apply(town, candidate);
        state.LastAssignedGovernor = candidate;
        state.LastAssignmentTime = CampaignTime.Now;
        DisplayGovernorAssigned(town, candidate);
    }

    private GovernorState StateFor(Settlement settlement)
    {
        if (!_governorStates.TryGetValue(settlement, out GovernorState? state)
            || state is null)
        {
            state = new GovernorState();
            _governorStates[settlement] = state;
        }

        return state;
    }

    private Hero? FindGovernorCandidate(
        Town town,
        bool allowGovernorReassignment,
        TownStrategy? preferredStrategy = null)
    {
        IEnumerable<Hero> candidates = Clan.PlayerClan.Heroes
            .Union(Clan.PlayerClan.Companions)
            .Distinct()
            .Where(hero => IsGovernorCandidate(
                hero,
                town,
                allowGovernorReassignment));

        // Filling a vacancy must not create another vacancy elsewhere. Existing
        // governors are considered only when replacing this town's governor and
        // there is no suitable unassigned hero.
        TownStrategy strategy = preferredStrategy
            ?? Effective(town.Settlement).Strategy;
        Hero? best = candidates
            .Where(hero => hero.GovernorOf is null || hero.GovernorOf == town)
            .OrderByDescending(hero => GovernorScore(hero, town, strategy))
            .ThenBy(hero => hero.StringId)
            .FirstOrDefault();

        if (best is null && town.Governor is not null && allowGovernorReassignment)
        {
            best = candidates
                .OrderByDescending(hero => GovernorScore(hero, town, strategy))
                .ThenBy(hero => hero.StringId)
                .FirstOrDefault();
        }

        if (best is null || town.Governor is null || best == town.Governor)
        {
            return best;
        }

        if (!allowGovernorReassignment)
        {
            return null;
        }

        float currentScore = GovernorScore(town.Governor, town, strategy);
        float bestScore = GovernorScore(best, town, strategy);
        return bestScore >= currentScore * 1.2f + 5f ? best : null;
    }

    private bool IsGovernorCandidate(
        Hero hero,
        Town town,
        bool allowGovernorReassignment)
    {
        if (hero == Hero.MainHero
            || hero.Clan != Clan.PlayerClan
            || !hero.IsActive
            || hero.IsDisabled
            || hero.IsChild
            || hero.IsPrisoner
            || (hero.PartyBelongedTo is not null
                && hero.PartyBelongedTo != MobileParty.MainParty)
            || !Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(hero))
        {
            return false;
        }

        if (hero.GovernorOf is null || hero.GovernorOf == town)
        {
            return true;
        }

        Town previousTown = hero.GovernorOf;
        FiefSettings previousSettings = Effective(
            previousTown.Settlement);
        return allowGovernorReassignment
            && previousSettings.Enabled
            && previousSettings.AllowGovernorReassignment
            && !WasGovernorAssignedRecently(
                previousTown,
                hero,
                previousSettings.GovernorAssignmentCooldownDays);
    }

    private static float GovernorScore(
        Hero hero,
        Town town,
        TownStrategy strategy)
    {
        float score = Campaign.Current.Models.DiplomacyModel
            .GetHeroGoverningStrengthForClan(hero);

        switch (strategy)
        {
            case TownStrategy.Economy:
                score += hero.GetSkillValue(DefaultSkills.Trade) * 0.18f;
                score += hero.GetSkillValue(DefaultSkills.Steward) * 0.08f;
                break;
            case TownStrategy.Stability:
                score += hero.GetSkillValue(DefaultSkills.Steward) * 0.15f;
                score += hero.GetSkillValue(DefaultSkills.Charm) * 0.08f;
                break;
            case TownStrategy.Military:
                score += hero.GetSkillValue(DefaultSkills.Leadership) * 0.12f;
                score += hero.GetSkillValue(DefaultSkills.Tactics) * 0.08f;
                score += hero.GetSkillValue(DefaultSkills.Engineering) * 0.08f;
                break;
            default:
                score += hero.GetSkillValue(DefaultSkills.Steward) * 0.1f;
                break;
        }

        if (hero.Culture == town.Culture)
        {
            score = score * 1.25f + 10f;
        }

        if (hero.GovernorOf == town)
        {
            score += 5f;
        }
        else if (hero.GovernorOf is not null)
        {
            score *= 0.8f;
        }

        return score;
    }

    private static bool IsGovernorAssignmentCoolingDown(
        GovernorState state,
        int cooldownDays)
    {
        return IsCoolingDown(state.LastAssignmentTime, cooldownDays);
    }

    private bool WasGovernorAssignedRecently(
        Town town,
        Hero hero,
        int cooldownDays)
    {
        return _governorStates.TryGetValue(
                town.Settlement,
                out GovernorState? state)
            && state is not null
            && state.LastAssignedGovernor == hero
            && IsCoolingDown(state.LastAssignmentTime, cooldownDays);
    }

    private static bool IsCoolingDown(CampaignTime assignmentTime, int cooldownDays)
        => cooldownDays > 0
            && (assignmentTime.IsNow
                || (assignmentTime.IsPast
                    && assignmentTime.ElapsedDaysUntilNow < cooldownDays));

    private static float ScoreDailyProject(
        Building building,
        Town town,
        FiefSettings settings)
    {
        float score = building == town.CurrentDefaultBuilding ? 1f : 0f;
        bool loyaltyEmergency = IsLoyaltyEmergency(town, settings);
        bool foodEmergency = IsFoodEmergency(town, settings.FoodShortageDays);
        bool foodPressure = town.FoodChange <= 0f;

        if (loyaltyEmergency)
        {
            AddEffectScore(building, BuildingEffectEnum.Loyalty, 10000f, ref score);
            AddEffectScore(building, BuildingEffectEnum.SecurityPerDay, 1000f, ref score);
        }

        if (foodEmergency)
        {
            AddEffectScore(building, BuildingEffectEnum.FoodProduction, 9000f, ref score);
            AddEffectScore(building, BuildingEffectEnum.FoodConsumption, 8000f, ref score);
        }

        switch (settings.Strategy)
        {
            case TownStrategy.Stability:
                AddEffectScore(building, BuildingEffectEnum.Loyalty, 700f, ref score);
                AddEffectScore(building, BuildingEffectEnum.SecurityPerDay, 550f, ref score);
                AddEffectScore(building, BuildingEffectEnum.FoodProduction, 300f, ref score);
                AddEffectScore(building, BuildingEffectEnum.Militia, 200f, ref score);
                break;
            case TownStrategy.Economy:
                AddEffectScore(building, BuildingEffectEnum.Prosperity, 1200f, ref score);
                AddEffectScore(building, BuildingEffectEnum.TaxPerDay, 1100f, ref score);
                AddEffectScore(building, BuildingEffectEnum.TariffIncome, 1000f, ref score);
                AddEffectScore(building, BuildingEffectEnum.WorkshopProduction, 900f, ref score);
                AddEffectScore(building, BuildingEffectEnum.VillageProduction, 850f, ref score);
                AddEffectScore(building, BuildingEffectEnum.FoodProduction,
                    foodPressure ? 1400f : 650f, ref score);
                AddEffectScore(building, BuildingEffectEnum.GarrisonWageReduction, 700f, ref score);
                AddEffectScore(building, BuildingEffectEnum.Militia, 200f, ref score);
                AddEffectScore(building, BuildingEffectEnum.GarrisonAutoRecruitment, -250f, ref score);
                break;
            case TownStrategy.Military:
                AddEffectScore(building, BuildingEffectEnum.Militia, 700f, ref score);
                AddEffectScore(building, BuildingEffectEnum.ExperiencePerDay, 600f, ref score);
                AddEffectScore(building, BuildingEffectEnum.GarrisonAutoRecruitment, 450f, ref score);
                AddEffectScore(building, BuildingEffectEnum.GarrisonWageReduction, 300f, ref score);
                break;
            default:
                AddEffectScore(building, BuildingEffectEnum.Loyalty, 400f, ref score);
                AddEffectScore(building, BuildingEffectEnum.FoodProduction, 400f, ref score);
                AddEffectScore(building, BuildingEffectEnum.Prosperity, 350f, ref score);
                AddEffectScore(building, BuildingEffectEnum.Militia, 250f, ref score);
                break;
        }

        return score;
    }

    private static float ScoreBuilding(
        Building building,
        Town town,
        FiefSettings settings,
        int existingQueueIndex)
    {
        float score = (BuildingType.MaxLevel - building.CurrentLevel) * 5f;
        if (existingQueueIndex != int.MaxValue)
        {
            score += 1f / (existingQueueIndex + 1f);
        }

        bool loyaltyEmergency = IsLoyaltyEmergency(town, settings);
        bool foodEmergency = IsFoodEmergency(town, settings.FoodShortageDays);
        bool foodPressure = town.FoodChange <= 0f;

        if (loyaltyEmergency)
        {
            AddEffectScore(building, BuildingEffectEnum.Loyalty, 10000f, ref score);
            AddEffectScore(building, BuildingEffectEnum.SecurityPerDay, 5000f, ref score);
        }

        if (foodEmergency)
        {
            AddEffectScore(building, BuildingEffectEnum.FoodStock, 9000f, ref score);
            AddEffectScore(building, BuildingEffectEnum.FoodProduction, 8000f, ref score);
            AddEffectScore(building, BuildingEffectEnum.FoodConsumption, 7000f, ref score);
            AddEffectScore(building, BuildingEffectEnum.VillageProduction, 4000f, ref score);
        }

        AddEffectScore(building, BuildingEffectEnum.ConstructionPerDay, 450f, ref score);

        switch (settings.Strategy)
        {
            case TownStrategy.Stability:
                AddEffectScore(building, BuildingEffectEnum.Loyalty, 800f, ref score);
                AddEffectScore(building, BuildingEffectEnum.SecurityPerDay, 700f, ref score);
                AddEffectScore(building, BuildingEffectEnum.FoodStock, 550f, ref score);
                AddEffectScore(building, BuildingEffectEnum.GarrisonCapacity, 250f, ref score);
                break;
            case TownStrategy.Economy:
                AddEffectScore(building, BuildingEffectEnum.TaxPerDay, 1400f, ref score);
                AddEffectScore(building, BuildingEffectEnum.TariffIncome, 1250f, ref score);
                AddEffectScore(building, BuildingEffectEnum.Prosperity, 1150f, ref score);
                AddEffectScore(building, BuildingEffectEnum.WorkshopProduction, 1050f, ref score);
                AddEffectScore(building, BuildingEffectEnum.VillageProduction, 950f, ref score);
                AddEffectScore(building, BuildingEffectEnum.FoodStock,
                    foodPressure ? 1500f : 600f, ref score);
                AddEffectScore(building, BuildingEffectEnum.FoodProduction,
                    foodPressure ? 1450f : 550f, ref score);
                AddEffectScore(building, BuildingEffectEnum.GarrisonWageReduction, 900f, ref score);
                AddEffectScore(building, BuildingEffectEnum.Militia, 250f, ref score);
                AddEffectScore(building, BuildingEffectEnum.GarrisonAutoRecruitment, -350f, ref score);
                break;
            case TownStrategy.Military:
                AddEffectScore(building, BuildingEffectEnum.GarrisonCapacity, 800f, ref score);
                AddEffectScore(building, BuildingEffectEnum.Militia, 750f, ref score);
                AddEffectScore(building, BuildingEffectEnum.ExperiencePerDay, 650f, ref score);
                AddEffectScore(building, BuildingEffectEnum.SiegeEngineSpeed, 550f, ref score);
                AddEffectScore(building, BuildingEffectEnum.WallRepairSpeed, 500f, ref score);
                break;
            default:
                AddEffectScore(building, BuildingEffectEnum.FoodStock, 550f, ref score);
                AddEffectScore(building, BuildingEffectEnum.SecurityPerDay, 500f, ref score);
                AddEffectScore(building, BuildingEffectEnum.Prosperity, 450f, ref score);
                AddEffectScore(building, BuildingEffectEnum.GarrisonCapacity, 350f, ref score);
                break;
        }

        score -= building.GetConstructionCost() * 0.001f;
        return score;
    }

    public static bool IsLoyaltyEmergency(
        Town town,
        FiefSettings settings)
        => town.InRebelliousState
            || town.Loyalty <= settings.LoyaltyEmergencyThreshold;

    public static bool IsFoodEmergency(Town town, int shortageDays)
    {
        if (town.FoodStocks <= 0f && town.FoodChange <= 0f)
        {
            return true;
        }

        if (shortageDays <= 0)
        {
            return false;
        }

        float dailyLoss = Math.Max(0f, -town.FoodChange);
        return dailyLoss > 0f && town.FoodStocks <= dailyLoss * shortageDays;
    }

    public static float FoodDaysRemaining(Town town)
    {
        if (town.FoodStocks <= 0f && town.FoodChange <= 0f)
        {
            return 0f;
        }

        float dailyLoss = Math.Max(0f, -town.FoodChange);
        return dailyLoss <= 0f
            ? float.PositiveInfinity
            : Math.Max(0f, town.FoodStocks) / dailyLoss;
    }

    private static bool HasUsefulEmergencyDailyProject(
        Town town,
        FiefSettings settings)
        => town.Buildings.Any(building => building.BuildingType.IsDailyProject
            && ProvidesEmergencyBenefit(building, town, settings));

    private static bool HasUsefulEmergencyBuilding(
        Town town,
        FiefSettings settings)
        => town.Buildings.Any(building => !building.BuildingType.IsDailyProject
            && building.CurrentLevel < BuildingType.MaxLevel
            && ProvidesEmergencyBenefit(building, town, settings));

    private static bool ProvidesEmergencyBenefit(
        Building building,
        Town town,
        FiefSettings settings)
    {
        bool loyaltyEmergency = IsLoyaltyEmergency(town, settings);
        bool foodEmergency = IsFoodEmergency(town, settings.FoodShortageDays);
        return (loyaltyEmergency
                && (building.BuildingType.HasEffect(BuildingEffectEnum.Loyalty)
                    || building.BuildingType.HasEffect(BuildingEffectEnum.SecurityPerDay)))
            || (foodEmergency
                && (building.BuildingType.HasEffect(BuildingEffectEnum.FoodStock)
                    || building.BuildingType.HasEffect(BuildingEffectEnum.FoodProduction)
                    || building.BuildingType.HasEffect(BuildingEffectEnum.FoodConsumption)
                    || building.BuildingType.HasEffect(BuildingEffectEnum.VillageProduction)));
    }

    private static void AddEffectScore(
        Building building,
        BuildingEffectEnum effect,
        float value,
        ref float score)
    {
        if (!building.BuildingType.HasEffect(effect))
        {
            return;
        }

        int nextLevel = Math.Min(BuildingType.MaxLevel, building.CurrentLevel + 1);
        float nextAmount = building.BuildingType.GetBaseBuildingEffectAmount(
            effect,
            nextLevel);
        float effectAmount;
        if (building.BuildingType.IsDailyProject)
        {
            effectAmount = Math.Abs(nextAmount);
        }
        else
        {
            float currentAmount = building.CurrentLevel > 0
                ? building.BuildingType.GetBaseBuildingEffectAmount(
                    effect,
                    building.CurrentLevel)
                : 0f;
            effectAmount = Math.Abs(nextAmount - currentAmount);
        }

        if (effectAmount <= 0.001f)
        {
            return;
        }

        float magnitude = 1f + Math.Min(
            3f,
            (float)Math.Log10(1f + effectAmount));
        score += value * magnitude;
    }

    private static void DisplayGovernorRecommendation(Town town, Hero governor)
        => Notify.Info(L.T("{=PAI_town_governor_recommendation}{HERO} is recommended as governor of {SETTLEMENT}.")
            .SetTextVariable("HERO", governor.Name)
            .SetTextVariable("SETTLEMENT", town.Settlement.Name));

    private static void DisplayGovernorAssigned(Town town, Hero governor)
        => Notify.Success(L.T("{=PAI_town_governor_assigned}{HERO} has been assigned as governor of {SETTLEMENT}.")
            .SetTextVariable("HERO", governor.Name)
            .SetTextVariable("SETTLEMENT", town.Settlement.Name));
}