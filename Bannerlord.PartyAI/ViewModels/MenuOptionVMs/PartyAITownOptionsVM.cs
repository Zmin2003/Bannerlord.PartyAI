using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dropdowns;
using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.MenuOptionVMs;

public sealed class PartyAITownOptionsVM : ViewModel
{
    private readonly Action _onClose;
    private readonly Settlement _settlement;
    private readonly PartyAiEntitySettings _garrisonSettings;
    private readonly TownManagementSettlementSettings _settings;

    public PartyAITownOptionsVM(
        Settlement settlement,
        PartyAiEntitySettings garrisonSettings,
        TownManagementSettlementSettings settings,
        Action callback)
    {
        _onClose = callback;
        _settlement = settlement;
        _garrisonSettings = garrisonSettings;
        _settings = settings.DeepCopy();

        TitleText = new TextObject("{=PAI_TOWN_SETTLEMENT_OPTIONS_TITLE}Town Management for {SETTLEMENT}")
            .SetTextVariable("SETTLEMENT", settlement.Name)
            .ToString();

        EnabledToggle = Toggle(
            "{=PAI_TOWN_SETTLEMENT_ENABLED}Enable Town AI for This Fief",
            _settings.Enabled,
            "{=PAI_TOWN_SETTLEMENT_ENABLED_HINT}Allow Town AI to manage this settlement while global town management is enabled.");
        ManageBuildingQueueToggle = Toggle(
            "{=PAI_TOWN_BUILD_QUEUE}Manage Building Queue",
            _settings.ManageBuildingQueue,
            "{=PAI_TOWN_BUILD_QUEUE_HINT}Choose and reorder construction projects according to the selected strategy and current emergencies.");
        ManageDailyProjectsToggle = Toggle(
            "{=PAI_TOWN_DAILY_PROJECTS}Manage Daily Projects",
            _settings.ManageDailyProjects,
            "{=PAI_TOWN_DAILY_PROJECTS_HINT}Select a continuous project when no building should be constructed or an emergency needs attention.");
        AutoFundConstructionToggle = Toggle(
            "{=PAI_TOWN_AUTO_FUND}Fund Construction Automatically",
            _settings.AutoFundConstruction,
            "{=PAI_TOWN_AUTO_FUND_SETTLEMENT_HINT}Move player gold into this settlement's construction reserve within the global player-gold limit.");
        AllowGovernorReassignmentToggle = Toggle(
            "{=PAI_TOWN_GOVERNOR_REASSIGN}Allow Governor Reassignment",
            _settings.AllowGovernorReassignment,
            "{=PAI_TOWN_GOVERNOR_REASSIGN_HINT}Allow automatic governor mode to replace an existing governor when a meaningfully better candidate is available.");
        AutoDefenseToggle = Toggle(
            "{=PAI_TOWN_SETTLEMENT_AUTO_DEFENSE}Defend This Fief Automatically",
            _settings.AutoDefenseEnabled,
            "{=PAI_TOWN_SETTLEMENT_AUTO_DEFENSE_HINT}Allow Town AI to assign suitable player-clan parties when this settlement is threatened.");

        StrategyDropdown = new TownManagementStrategyDropdownVM(_settings.Strategy);
        GovernorModeDropdown = new AutoGovernorModeDropdownVM(_settings.GovernorMode);
        DefensePriorityDropdown = new TownDefensePriorityDropdownVM(_settings.DefensePriority);
        MaxTroopTierDropdown = new PartyAIMaxTroopTierDropdownVM(_garrisonSettings.MaxTroopTier);
    }

    private static PartyAIOptionToggleVM Toggle(string text, bool selected, string hint)
        => new(new TextObject(text), selected, new TextObject(hint));

    [DataSourceProperty] public string TitleText { get; }
    [DataSourceProperty] public string AcceptText => new TextObject("{=bV75iwKa}Save").ToString();
    [DataSourceProperty] public string CancelText => GameTexts.FindText("str_cancel").ToString();
    [DataSourceProperty] public string GarrisonHeader => new TextObject("{=PAI_TOWN_GARRISON_HEADER}Garrison").ToString();
    [DataSourceProperty] public string GeneralHeader => new TextObject("{=PAI_TOWN_GENERAL_HEADER}General").ToString();
    [DataSourceProperty] public string ConstructionHeader => new TextObject("{=PAI_TOWN_CONSTRUCTION_HEADER}Construction").ToString();
    [DataSourceProperty] public string GovernorHeader => new TextObject("{=PAI_TOWN_GOVERNOR_HEADER}Governors").ToString();
    [DataSourceProperty] public string DefenseHeader => new TextObject("{=PAI_TOWN_DEFENSE_HEADER}Defense Dispatch").ToString();

    [DataSourceProperty] public PartyAIOptionToggleVM EnabledToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM ManageBuildingQueueToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM ManageDailyProjectsToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM AutoFundConstructionToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM AllowGovernorReassignmentToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM AutoDefenseToggle { get; }
    [DataSourceProperty] public TownManagementStrategyDropdownVM StrategyDropdown { get; }
    [DataSourceProperty] public AutoGovernorModeDropdownVM GovernorModeDropdown { get; }
    [DataSourceProperty] public TownDefensePriorityDropdownVM DefensePriorityDropdown { get; }
    [DataSourceProperty] public PartyAIMaxTroopTierDropdownVM MaxTroopTierDropdown { get; }

    [DataSourceProperty] public string StrategyText => new TextObject("{=PAI_TOWN_STRATEGY}Management Strategy").ToString();
    [DataSourceProperty] public string GovernorModeText => new TextObject("{=PAI_TOWN_GOVERNOR_MODE}Governor Mode").ToString();
    [DataSourceProperty] public string DefensePriorityText => new TextObject("{=PAI_TOWN_DEFENSE_PRIORITY}Defense Priority").ToString();
    [DataSourceProperty] public string MaxTroopTierText => new TextObject("{=PAIn4UJJg3a}Max Troop Tier").ToString();
    [DataSourceProperty] public HintViewModel StrategyHint => Hint("{=PAI_TOWN_STRATEGY_HINT}Balanced develops the weakest areas; Stability protects loyalty, security and food; Economy favors prosperity; Military favors walls and militia.");
    [DataSourceProperty] public HintViewModel GovernorModeHint => Hint("{=PAI_TOWN_GOVERNOR_MODE_HINT}Recommend suitable governors or assign them automatically. Automatic assignment only uses eligible player-clan heroes.");
    [DataSourceProperty] public HintViewModel DefensePriorityHint => Hint("{=PAI_TOWN_DEFENSE_PRIORITY_HINT}Higher-priority settlements receive available defenders before lower-priority settlements with similar threats.");
    [DataSourceProperty] public HintViewModel MaxTroopTierHint => Hint("{=PAIKeTFa2PX}Maximum troop tier to upgrade troops to. If you lower this setting while there are higher tier troops in the party, they will be downgraded.");
    [DataSourceProperty] public HintViewModel ChangeHint => Hint("{=PAIXIv9UgAt}Change");

    [DataSourceProperty] public string ConstructionReserveTargetText => new TextObject("{=PAI_TOWN_RESERVE_TARGET}Construction Reserve Target").ToString();
    [DataSourceProperty] public string ConstructionReserveTargetAmount => _settings.ConstructionReserveTarget.ToString();
    [DataSourceProperty] public HintViewModel ConstructionReserveTargetHint => Hint("{=PAI_TOWN_SETTLEMENT_RESERVE_TARGET_HINT}Stop adding construction funds after this settlement's reserve reaches this amount.");

    [DataSourceProperty] public string DailyConstructionDepositLimitText => new TextObject("{=PAI_TOWN_DAILY_DEPOSIT_LIMIT}Daily Funding Limit").ToString();
    [DataSourceProperty] public string DailyConstructionDepositLimitAmount => _settings.DailyConstructionDepositLimit.ToString();
    [DataSourceProperty] public HintViewModel DailyConstructionDepositLimitHint => Hint("{=PAI_TOWN_DAILY_DEPOSIT_LIMIT_HINT}Maximum construction funds that Town AI may add to one settlement per day.");

    [DataSourceProperty] public string LoyaltyEmergencyThresholdText => new TextObject("{=PAI_TOWN_LOYALTY_THRESHOLD}Loyalty Emergency Threshold").ToString();
    [DataSourceProperty] public string LoyaltyEmergencyThresholdAmount => ((int)_settings.LoyaltyEmergencyThreshold).ToString();
    [DataSourceProperty] public HintViewModel LoyaltyEmergencyThresholdHint => Hint("{=PAI_TOWN_LOYALTY_THRESHOLD_HINT}Below this loyalty value, stability projects take precedence over the selected strategy.");

    [DataSourceProperty] public string FoodShortageDaysText => new TextObject("{=PAI_TOWN_FOOD_SHORTAGE_DAYS}Food Emergency Days").ToString();
    [DataSourceProperty] public string FoodShortageDaysAmount => _settings.FoodShortageDays.ToString();
    [DataSourceProperty] public HintViewModel FoodShortageDaysHint => Hint("{=PAI_TOWN_FOOD_SHORTAGE_DAYS_HINT}Treat a projected food shortage within this many days as an emergency.");

    [DataSourceProperty] public string GovernorCooldownText => new TextObject("{=PAI_TOWN_GOVERNOR_COOLDOWN}Governor Reassignment Cooldown").ToString();
    [DataSourceProperty] public string GovernorCooldownAmount => Days(_settings.GovernorAssignmentCooldownDays);
    [DataSourceProperty] public HintViewModel GovernorCooldownHint => Hint("{=PAI_TOWN_GOVERNOR_COOLDOWN_HINT}Minimum days before Town AI may replace a governor it assigned.");

    [DataSourceProperty] public string TargetDefenseStrengthText => new TextObject("{=PAI_TOWN_TARGET_DEFENSE_STRENGTH}Target Defense Strength").ToString();
    [DataSourceProperty] public string TargetDefenseStrengthAmount => ((int)_settings.TargetDefenseStrength).ToString();
    [DataSourceProperty] public HintViewModel TargetDefenseStrengthHint => Hint("{=PAI_TOWN_TARGET_DEFENSE_STRENGTH_HINT}Desired combined local defense strength. Zero lets Town AI calculate a target.");

    [DataSourceProperty] public string TargetGarrisonTroopsText => new TextObject("{=PAI_TOWN_TARGET_GARRISON_TROOPS}Target Garrison Troops").ToString();
    [DataSourceProperty] public string TargetGarrisonTroopsAmount => _settings.TargetGarrisonTroops.ToString();
    [DataSourceProperty] public HintViewModel TargetGarrisonTroopsHint => Hint("{=PAI_TOWN_TARGET_GARRISON_TROOPS_HINT}Desired garrison troop count for automatic reinforcement. Zero disables the fixed target.");

    public void EditConstructionReserveTarget() => EditInteger(
        _settings.ConstructionReserveTarget, 0, 2000000,
        "{=PAI_TOWN_RESERVE_TARGET}Construction Reserve Target",
        value => { _settings.ConstructionReserveTarget = value; Changed(nameof(ConstructionReserveTargetAmount)); });

    public void EditDailyConstructionDepositLimit() => EditInteger(
        _settings.DailyConstructionDepositLimit, 0, 200000,
        "{=PAI_TOWN_DAILY_DEPOSIT_LIMIT}Daily Funding Limit",
        value => { _settings.DailyConstructionDepositLimit = value; Changed(nameof(DailyConstructionDepositLimitAmount)); });

    public void EditLoyaltyEmergencyThreshold() => EditInteger(
        (int)_settings.LoyaltyEmergencyThreshold, 0, 100,
        "{=PAI_TOWN_LOYALTY_THRESHOLD}Loyalty Emergency Threshold",
        value => { _settings.LoyaltyEmergencyThreshold = value; Changed(nameof(LoyaltyEmergencyThresholdAmount)); });

    public void EditFoodShortageDays() => EditInteger(
        _settings.FoodShortageDays, 1, 30,
        "{=PAI_TOWN_FOOD_SHORTAGE_DAYS}Food Emergency Days",
        value => { _settings.FoodShortageDays = value; Changed(nameof(FoodShortageDaysAmount)); });

    public void EditGovernorCooldown() => EditInteger(
        _settings.GovernorAssignmentCooldownDays, 0, 365,
        "{=PAI_TOWN_GOVERNOR_COOLDOWN}Governor Reassignment Cooldown",
        value => { _settings.GovernorAssignmentCooldownDays = value; Changed(nameof(GovernorCooldownAmount)); });

    public void EditTargetDefenseStrength() => EditInteger(
        (int)_settings.TargetDefenseStrength, 0, 1000000,
        "{=PAI_TOWN_TARGET_DEFENSE_STRENGTH}Target Defense Strength",
        value => { _settings.TargetDefenseStrength = value; Changed(nameof(TargetDefenseStrengthAmount)); });

    public void EditTargetGarrisonTroops() => EditInteger(
        _settings.TargetGarrisonTroops, 0, 5000,
        "{=PAI_TOWN_TARGET_GARRISON_TROOPS}Target Garrison Troops",
        value => { _settings.TargetGarrisonTroops = value; Changed(nameof(TargetGarrisonTroopsAmount)); });

    public void AcceptEditTownOptions()
    {
        _garrisonSettings.MaxTroopTier = MaxTroopTierDropdown.SortOptions.SelectedItem.Max;
        _settings.Enabled = EnabledToggle.IsSelected;
        _settings.Strategy = StrategyDropdown.SortOptions.SelectedItem.Strategy;
        _settings.ManageBuildingQueue = ManageBuildingQueueToggle.IsSelected;
        _settings.ManageDailyProjects = ManageDailyProjectsToggle.IsSelected;
        _settings.AutoFundConstruction = AutoFundConstructionToggle.IsSelected;
        _settings.GovernorMode = GovernorModeDropdown.SortOptions.SelectedItem.Mode;
        _settings.AllowGovernorReassignment = AllowGovernorReassignmentToggle.IsSelected;
        _settings.AutoDefenseEnabled = AutoDefenseToggle.IsSelected;
        _settings.DefensePriority = DefensePriorityDropdown.SortOptions.SelectedItem.Priority;

        SubModule.TownManagementBehavior.UpdateSettings(_settlement, _settings);
        _onClose?.Invoke();
    }

    public void CancelEditTownOptions() => _onClose?.Invoke();

    private static HintViewModel Hint(string text) => new(new TextObject(text));
    private static string Days(int value) => new TextObject("{=PAI_TOWN_DAYS}{DAYS} days")
        .SetTextVariable("DAYS", value)
        .ToString();
    private void Changed(string propertyName) => OnPropertyChanged(propertyName);

    private void EditInteger(int value, int min, int max, string title, Action<int> onChanged)
    {
        SubModule.InformationManager.ShowNumberPickerInquiry(
            value,
            min,
            max,
            new TextObject(title).ToString(),
            string.Empty,
            onChanged,
            isPercentage: false);
    }
}
