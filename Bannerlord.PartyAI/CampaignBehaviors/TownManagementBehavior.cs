using Bannerlord.PartyAI.Models;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.CampaignBehaviors;

public class TownManagementBehavior : CampaignBehaviorBase
{
    private TownManagementOptions _options = new();
    private Dictionary<Settlement, TownManagementSettlementSettings> _settlementSettings = new();
    private Dictionary<Settlement, TownGovernorAssignmentState> _governorStates = new();

    internal TownManagementOptions Options => _options;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickTownEvent.AddNonSerializedListener(this, OnDailyTickTown);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(
            this,
            OnSettlementOwnerChanged);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (!dataStore.SyncData("_townManagementOptions", ref _options) && dataStore.IsLoading)
        {
            _options = new TownManagementOptions { Enabled = false };
        }

        dataStore.SyncData("_townManagementSettlementSettings", ref _settlementSettings);
        dataStore.SyncData("_townGovernorAssignmentStates", ref _governorStates);

        _options ??= new TownManagementOptions { Enabled = false };
        _settlementSettings ??= new Dictionary<Settlement, TownManagementSettlementSettings>();
        _governorStates ??= new Dictionary<Settlement, TownGovernorAssignmentState>();

        _options.Normalize();
        foreach (Settlement settlement in _settlementSettings
            .Where(pair => pair.Value is null)
            .Select(pair => pair.Key)
            .ToList())
        {
            _settlementSettings.Remove(settlement);
        }

        foreach (KeyValuePair<Settlement, TownManagementSettlementSettings> pair in _settlementSettings)
        {
            pair.Value.AttachTo(pair.Key);
            pair.Value.Normalize();
        }

        foreach (Settlement settlement in _governorStates
            .Where(pair => pair.Value is null)
            .Select(pair => pair.Key)
            .ToList())
        {
            _governorStates.Remove(settlement);
        }

        foreach (TownGovernorAssignmentState state in _governorStates.Values)
        {
            state.Normalize();
        }
    }

    internal TownManagementSettlementSettings Settings(Settlement settlement)
    {
        if (!_settlementSettings.TryGetValue(settlement, out TownManagementSettlementSettings? settings))
        {
            settings = TownManagementSettlementSettings.FromOptions(settlement, _options);
            _settlementSettings.Add(settlement, settings);
        }
        else
        {
            settings.AttachTo(settlement);
            settings.Normalize();
        }

        return settings;
    }

    internal TownManagementSettlementSettings SettingsSnapshot(Settlement settlement)
    {
        if (_settlementSettings.TryGetValue(
            settlement,
            out TownManagementSettlementSettings? settings))
        {
            var copy = settings.DeepCopy();
            copy.AttachTo(settlement);
            copy.Normalize();
            return copy;
        }

        return TownManagementSettlementSettings.FromOptions(settlement, _options);
    }

    internal void UpdateOptions(TownManagementOptions options)
    {
        _options = new TownManagementOptions(options);
    }

    internal void UpdateSettings(
        Settlement settlement,
        TownManagementSettlementSettings settings)
    {
        var copy = new TownManagementSettlementSettings(settings);
        copy.AttachTo(settlement);
        copy.Normalize();
        _settlementSettings[settlement] = copy;
    }

    internal bool IsTownManageable(Settlement settlement)
    {
        return settlement?.Town is not null
            && settlement.OwnerClan == Clan.PlayerClan;
    }

    internal bool IsTownManageable(Town town) => IsTownManageable(town.Settlement);

    private void OnDailyTickTown(Town town)
    {
        if (!_options.Enabled || !IsTownManageable(town))
        {
            return;
        }

        TownManagementSettlementSettings settings = Settings(town.Settlement);
        if (!settings.Enabled)
        {
            return;
        }

        bool manageDailyProjects = _options.ManageDailyProjects
            && settings.ManageDailyProjects;
        if (manageDailyProjects)
        {
            ManageDailyProject(town, settings);
        }

        if (_options.ManageBuildingQueue && settings.ManageBuildingQueue)
        {
            ManageBuildingQueue(town, settings, manageDailyProjects);
        }

        if (_options.AutoFundConstruction && settings.AutoFundConstruction)
        {
            FundConstruction(town, settings);
        }

        AutoGovernorMode governorMode = (AutoGovernorMode)Math.Min(
            (int)_options.GovernorMode,
            (int)settings.GovernorMode);
        bool allowGovernorReassignment = _options.AllowGovernorReassignment
            && settings.AllowGovernorReassignment;
        int governorCooldownDays = Math.Max(
            _options.GovernorAssignmentCooldownDays,
            settings.GovernorAssignmentCooldownDays);
        ManageGovernor(
            town,
            governorMode,
            allowGovernorReassignment,
            governorCooldownDays);
    }

    private void OnSettlementOwnerChanged(
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        _governorStates.Remove(settlement);
    }

    private static void ManageDailyProject(
        Town town,
        TownManagementSettlementSettings settings)
    {
        Building? bestProject = town.Buildings
            .Where(building => building.BuildingType.IsDailyProject)
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
        TownManagementSettlementSettings settings,
        bool manageDailyProjects)
    {
        List<Building> currentQueue = town.BuildingsInProgress.ToList();
        bool pauseForEmergency = manageDailyProjects
            && HasUsefulEmergencyDailyProject(town, settings);
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
                .OrderByDescending(building => ScoreBuilding(
                    building,
                    town,
                    settings,
                    existingIndexes.TryGetValue(building, out int index) ? index : int.MaxValue))
                .ThenBy(building => building.BuildingType.StringId)
                .ToList();

        if (!currentQueue.SequenceEqual(desiredQueue))
        {
            BuildingHelper.ChangeCurrentBuildingQueue(desiredQueue, town);
        }
    }

    private void FundConstruction(
        Town town,
        TownManagementSettlementSettings settings)
    {
        if (town.BuildingsInProgress.Count == 0
            || town.IsUnderSiege
            || town.InRebelliousState
            || settings.ConstructionReserveTarget <= town.BoostBuildingProcess
            || settings.DailyConstructionDepositLimit <= 0)
        {
            return;
        }

        int availableGold = Math.Max(0, Hero.MainHero.Gold - _options.PlayerGoldReserve);
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
        AutoGovernorMode governorMode,
        bool allowGovernorReassignment,
        int governorCooldownDays)
    {
        if (governorMode == AutoGovernorMode.Off)
        {
            return;
        }

        TownGovernorAssignmentState state = GovernorState(town.Settlement);
        Hero? candidate = FindGovernorCandidate(
            town,
            allowGovernorReassignment,
            governorCooldownDays);
        Hero? recommendation = candidate != town.Governor ? candidate : null;

        if (governorMode == AutoGovernorMode.Recommend)
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

    private TownGovernorAssignmentState GovernorState(Settlement settlement)
    {
        if (!_governorStates.TryGetValue(settlement, out TownGovernorAssignmentState? state))
        {
            state = new TownGovernorAssignmentState();
            _governorStates.Add(settlement, state);
        }

        return state;
    }

    private Hero? FindGovernorCandidate(
        Town town,
        bool allowGovernorReassignment,
        int governorCooldownDays)
    {
        IEnumerable<Hero> candidates = Clan.PlayerClan.Heroes
            .Union(Clan.PlayerClan.Companions)
            .Distinct()
            .Where(hero => IsGovernorCandidate(
                hero,
                town,
                allowGovernorReassignment,
                governorCooldownDays));

        Hero? best = candidates
            .OrderByDescending(hero => GovernorScore(hero, town))
            .ThenBy(hero => hero.StringId)
            .FirstOrDefault();

        if (best is null || town.Governor is null || best == town.Governor)
        {
            return best;
        }

        if (!allowGovernorReassignment)
        {
            return null;
        }

        float currentScore = GovernorScore(town.Governor, town);
        float bestScore = GovernorScore(best, town);
        return bestScore >= currentScore * 1.2f + 5f ? best : null;
    }

    private bool IsGovernorCandidate(
        Hero hero,
        Town town,
        bool allowGovernorReassignment,
        int governorCooldownDays)
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
        TownManagementSettlementSettings previousSettings = SettingsSnapshot(
            previousTown.Settlement);
        return allowGovernorReassignment
            && previousSettings.Enabled
            && previousSettings.AllowGovernorReassignment
            && !WasGovernorAssignedRecently(hero, governorCooldownDays);
    }

    private static float GovernorScore(Hero hero, Town town)
    {
        float score = Campaign.Current.Models.DiplomacyModel
            .GetHeroGoverningStrengthForClan(hero);

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
        TownGovernorAssignmentState state,
        int cooldownDays)
    {
        return state.LastAssignmentTime.IsPast
            && state.LastAssignmentTime.ElapsedDaysUntilNow < cooldownDays;
    }

    private bool WasGovernorAssignedRecently(Hero hero, int cooldownDays)
    {
        return _governorStates.Values.Any(state => state.LastAssignedGovernor == hero
            && (state.LastAssignmentTime.IsNow
                || (cooldownDays > 0
                    && state.LastAssignmentTime.IsPast
                    && state.LastAssignmentTime.ElapsedDaysUntilNow < cooldownDays)));
    }

    private static float ScoreDailyProject(
        Building building,
        Town town,
        TownManagementSettlementSettings settings)
    {
        float score = building == town.CurrentDefaultBuilding ? 1f : 0f;
        bool loyaltyEmergency = town.Loyalty <= settings.LoyaltyEmergencyThreshold;
        bool foodEmergency = IsFoodEmergency(town, settings.FoodShortageDays);

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
            case TownManagementStrategy.Stability:
                AddEffectScore(building, BuildingEffectEnum.Loyalty, 700f, ref score);
                AddEffectScore(building, BuildingEffectEnum.SecurityPerDay, 550f, ref score);
                AddEffectScore(building, BuildingEffectEnum.FoodProduction, 300f, ref score);
                AddEffectScore(building, BuildingEffectEnum.Militia, 200f, ref score);
                break;
            case TownManagementStrategy.Economy:
                AddEffectScore(building, BuildingEffectEnum.Prosperity, 700f, ref score);
                AddEffectScore(building, BuildingEffectEnum.FoodProduction, 550f, ref score);
                AddEffectScore(building, BuildingEffectEnum.VillageProduction, 350f, ref score);
                AddEffectScore(building, BuildingEffectEnum.TaxPerDay, 250f, ref score);
                break;
            case TownManagementStrategy.Military:
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
        TownManagementSettlementSettings settings,
        int existingQueueIndex)
    {
        float score = (BuildingType.MaxLevel - building.CurrentLevel) * 5f;
        if (existingQueueIndex != int.MaxValue)
        {
            score += 1f / (existingQueueIndex + 1f);
        }

        bool loyaltyEmergency = town.Loyalty <= settings.LoyaltyEmergencyThreshold;
        bool foodEmergency = IsFoodEmergency(town, settings.FoodShortageDays);

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
            case TownManagementStrategy.Stability:
                AddEffectScore(building, BuildingEffectEnum.Loyalty, 800f, ref score);
                AddEffectScore(building, BuildingEffectEnum.SecurityPerDay, 700f, ref score);
                AddEffectScore(building, BuildingEffectEnum.FoodStock, 550f, ref score);
                AddEffectScore(building, BuildingEffectEnum.GarrisonCapacity, 250f, ref score);
                break;
            case TownManagementStrategy.Economy:
                AddEffectScore(building, BuildingEffectEnum.Prosperity, 800f, ref score);
                AddEffectScore(building, BuildingEffectEnum.TaxPerDay, 700f, ref score);
                AddEffectScore(building, BuildingEffectEnum.TariffIncome, 600f, ref score);
                AddEffectScore(building, BuildingEffectEnum.WorkshopProduction, 550f, ref score);
                AddEffectScore(building, BuildingEffectEnum.VillageProduction, 500f, ref score);
                break;
            case TownManagementStrategy.Military:
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

    private static bool IsFoodEmergency(Town town, int shortageDays)
    {
        float dailyLoss = Math.Max(0f, -town.FoodChange);
        return dailyLoss > 0f && town.FoodStocks <= dailyLoss * shortageDays;
    }

    private static bool HasUsefulEmergencyDailyProject(
        Town town,
        TownManagementSettlementSettings settings)
    {
        bool loyaltyEmergency = town.Loyalty <= settings.LoyaltyEmergencyThreshold;
        bool foodEmergency = IsFoodEmergency(town, settings.FoodShortageDays);

        return town.Buildings.Any(building => building.BuildingType.IsDailyProject
            && ((loyaltyEmergency
                    && (building.BuildingType.HasEffect(BuildingEffectEnum.Loyalty)
                        || building.BuildingType.HasEffect(BuildingEffectEnum.SecurityPerDay)))
                || (foodEmergency
                    && (building.BuildingType.HasEffect(BuildingEffectEnum.FoodProduction)
                        || building.BuildingType.HasEffect(BuildingEffectEnum.FoodConsumption)))));
    }

    private static void AddEffectScore(
        Building building,
        BuildingEffectEnum effect,
        float value,
        ref float score)
    {
        if (building.BuildingType.HasEffect(effect))
        {
            score += value;
        }
    }

    private static void DisplayGovernorRecommendation(Town town, Hero governor)
    {
        var text = new TextObject(
            "{=PAI_town_governor_recommendation}{HERO} is recommended as governor of {SETTLEMENT}.")
            .SetTextVariable("HERO", governor.Name)
            .SetTextVariable("SETTLEMENT", town.Settlement.Name);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Cyan));
    }

    private static void DisplayGovernorAssigned(Town town, Hero governor)
    {
        var text = new TextObject(
            "{=PAI_town_governor_assigned}{HERO} has been assigned as governor of {SETTLEMENT}.")
            .SetTextVariable("HERO", governor.Name)
            .SetTextVariable("SETTLEMENT", town.Settlement.Name);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Green));
    }
}
