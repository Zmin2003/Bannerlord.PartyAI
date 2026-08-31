using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.CampaignBehaviors;
using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dropdowns;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
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
    private bool _usesCustomSettings;
    private bool _governorNeedsAttention;
    private bool _economyNeedsAttention;
    private string _managementStatusText = string.Empty;
    private string _governorStatusText = string.Empty;
    private string _economyStatusText = string.Empty;

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

        _usesCustomSettings = !_settings.UseGlobalDefaults;
        UseGlobalDefaultsToggle = Toggle(
            "{=PAI_TOWN_USE_GLOBAL_DEFAULTS}Follow Global Town Defaults",
            _settings.UseGlobalDefaults,
            "{=PAI_TOWN_USE_GLOBAL_DEFAULTS_HINT}Keep this fief synchronized with global construction, governor and defense defaults. The fief enable switch remains independent.",
            OnUseGlobalDefaultsChanged);
        EnabledToggle = Toggle(
            "{=PAI_TOWN_SETTLEMENT_ENABLED}Enable Town AI for This Fief",
            _settings.Enabled,
            "{=PAI_TOWN_SETTLEMENT_ENABLED_HINT}Allow Town AI to manage this settlement while global town management is enabled.",
            OnEnabledChanged);
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

        StrategyDropdown = new TownManagementStrategyDropdownVM(
            _settings.Strategy,
            OnStrategyChanged);
        GovernorModeDropdown = new AutoGovernorModeDropdownVM(
            _settings.GovernorMode,
            OnGovernorModeChanged);
        DefensePriorityDropdown = new TownDefensePriorityDropdownVM(
            _settings.DefensePriority,
            priority => _settings.DefensePriority = priority);
        MaxTroopTierDropdown = new PartyAIMaxTroopTierDropdownVM(_garrisonSettings.MaxTroopTier);

        SetOverrideAvailability();
        RefreshStatus();
    }

    private static PartyAIOptionToggleVM Toggle(
        string text,
        bool selected,
        string hint,
        Action<bool>? onChange = null)
        => new(new TextObject(text), selected, new TextObject(hint), onChange);

    [DataSourceProperty] public string TitleText { get; }
    [DataSourceProperty] public string AcceptText => new TextObject("{=bV75iwKa}Save").ToString();
    [DataSourceProperty] public string CancelText => GameTexts.FindText("str_cancel").ToString();
    [DataSourceProperty] public string GarrisonHeader => new TextObject("{=PAI_TOWN_GARRISON_HEADER}Garrison").ToString();
    [DataSourceProperty] public string GeneralHeader => new TextObject("{=PAI_TOWN_GENERAL_HEADER}General").ToString();
    [DataSourceProperty] public string ConstructionHeader => new TextObject("{=PAI_TOWN_CONSTRUCTION_HEADER}Construction").ToString();
    [DataSourceProperty] public string GovernorHeader => new TextObject("{=PAI_TOWN_GOVERNOR_HEADER}Governors").ToString();
    [DataSourceProperty] public string DefenseHeader => new TextObject("{=PAI_TOWN_DEFENSE_HEADER}Defense Dispatch").ToString();

    [DataSourceProperty] public PartyAIOptionToggleVM UseGlobalDefaultsToggle { get; }
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

    [DataSourceProperty]
    public bool UsesCustomSettings
    {
        get => _usesCustomSettings;
        private set
        {
            if (value != _usesCustomSettings)
            {
                _usesCustomSettings = value;
                OnPropertyChangedWithValue(value, nameof(UsesCustomSettings));
            }
        }
    }

    [DataSourceProperty]
    public bool GovernorNeedsAttention
    {
        get => _governorNeedsAttention;
        private set
        {
            if (value != _governorNeedsAttention)
            {
                _governorNeedsAttention = value;
                OnPropertyChangedWithValue(value, nameof(GovernorNeedsAttention));
            }
        }
    }

    [DataSourceProperty]
    public bool EconomyNeedsAttention
    {
        get => _economyNeedsAttention;
        private set
        {
            if (value != _economyNeedsAttention)
            {
                _economyNeedsAttention = value;
                OnPropertyChangedWithValue(value, nameof(EconomyNeedsAttention));
            }
        }
    }

    [DataSourceProperty]
    public string ManagementStatusText
    {
        get => _managementStatusText;
        private set
        {
            if (value != _managementStatusText)
            {
                _managementStatusText = value;
                OnPropertyChangedWithValue(value, nameof(ManagementStatusText));
            }
        }
    }

    [DataSourceProperty]
    public string GovernorStatusText
    {
        get => _governorStatusText;
        private set
        {
            if (value != _governorStatusText)
            {
                _governorStatusText = value;
                OnPropertyChangedWithValue(value, nameof(GovernorStatusText));
            }
        }
    }

    [DataSourceProperty]
    public string EconomyStatusText
    {
        get => _economyStatusText;
        private set
        {
            if (value != _economyStatusText)
            {
                _economyStatusText = value;
                OnPropertyChangedWithValue(value, nameof(EconomyStatusText));
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel ManagementStatusHint => Hint(
        "{=PAI_TOWN_MANAGEMENT_STATUS_HINT}Global enable is the master switch. A fief may either follow the global defaults or keep explicit local settings.");

    [DataSourceProperty]
    public HintViewModel GovernorStatusHint => Hint(
        "{=PAI_TOWN_GOVERNOR_STATUS_HINT}Automatic mode assigns an eligible unassigned player-clan hero on the next daily town update. Recommend mode only reports the best candidate.");

    [DataSourceProperty]
    public HintViewModel EconomyStatusHint => Hint(
        "{=PAI_TOWN_ECONOMY_STATUS_HINT}Food or loyalty emergencies temporarily outrank profit. Economy strategy otherwise favors taxes, tariffs, prosperity, workshops, villages and lower garrison costs. Listed net income is an operational estimate, not the clan finance total.");

    [DataSourceProperty] public string StrategyText => new TextObject("{=PAI_TOWN_STRATEGY}Management Strategy").ToString();
    [DataSourceProperty] public string GovernorModeText => new TextObject("{=PAI_TOWN_GOVERNOR_MODE}Governor Mode").ToString();
    [DataSourceProperty] public string DefensePriorityText => new TextObject("{=PAI_TOWN_DEFENSE_PRIORITY}Defense Priority").ToString();
    [DataSourceProperty] public string MaxTroopTierText => new TextObject("{=PAIn4UJJg3a}Max Troop Tier").ToString();
    [DataSourceProperty] public HintViewModel StrategyHint => Hint("{=PAI_TOWN_STRATEGY_HINT}Balanced covers food, security, prosperity and defense; Stability protects loyalty, security and food; Economy prioritizes prosperity and revenue from taxes, tariffs, workshops and villages; Military prioritizes walls, militia and garrisons. Loyalty and food emergencies override the selected focus.");
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
        value =>
        {
            _settings.LoyaltyEmergencyThreshold = value;
            Changed(nameof(LoyaltyEmergencyThresholdAmount));
            RefreshStatus();
        });

    public void EditFoodShortageDays() => EditInteger(
        _settings.FoodShortageDays, 1, 30,
        "{=PAI_TOWN_FOOD_SHORTAGE_DAYS}Food Emergency Days",
        value =>
        {
            _settings.FoodShortageDays = value;
            Changed(nameof(FoodShortageDaysAmount));
            RefreshStatus();
        });

    public void EditGovernorCooldown() => EditInteger(
        _settings.GovernorAssignmentCooldownDays, 0, 365,
        "{=PAI_TOWN_GOVERNOR_COOLDOWN}Governor Reassignment Cooldown",
        value => { _settings.GovernorAssignmentCooldownDays = value; Changed(nameof(GovernorCooldownAmount)); });

    public void EditTargetDefenseStrength() => EditInteger(
        (int)_settings.TargetDefenseStrength, 0, 100000,
        "{=PAI_TOWN_TARGET_DEFENSE_STRENGTH}Target Defense Strength",
        value => { _settings.TargetDefenseStrength = value; Changed(nameof(TargetDefenseStrengthAmount)); });

    public void EditTargetGarrisonTroops() => EditInteger(
        _settings.TargetGarrisonTroops, 0, 5000,
        "{=PAI_TOWN_TARGET_GARRISON_TROOPS}Target Garrison Troops",
        value => { _settings.TargetGarrisonTroops = value; Changed(nameof(TargetGarrisonTroopsAmount)); });

    private void OnEnabledChanged(bool enabled)
    {
        _settings.Enabled = enabled;
        RefreshStatus();
    }

    private void OnStrategyChanged(TownManagementStrategy strategy)
    {
        _settings.Strategy = strategy;
        RefreshStatus();
    }

    private void OnGovernorModeChanged(AutoGovernorMode mode)
    {
        _settings.GovernorMode = mode;
        RefreshStatus();
    }

    private void OnUseGlobalDefaultsChanged(bool useGlobalDefaults)
    {
        _settings.UseGlobalDefaults = useGlobalDefaults;
        if (useGlobalDefaults)
        {
            _settings.ApplyDefaults(SubModule.TownManagementBehavior.Options);
            _settings.Normalize();
            SyncControlsFromSettings();
        }

        UsesCustomSettings = !useGlobalDefaults;
        SetOverrideAvailability();
        RefreshStatus();
    }

    private void SetOverrideAvailability()
    {
        bool disabled = !UsesCustomSettings;
        ManageBuildingQueueToggle.IsDisabled = disabled;
        ManageDailyProjectsToggle.IsDisabled = disabled;
        AutoFundConstructionToggle.IsDisabled = disabled;
        AllowGovernorReassignmentToggle.IsDisabled = disabled;
        AutoDefenseToggle.IsDisabled = disabled;
    }

    private void SyncControlsFromSettings()
    {
        ManageBuildingQueueToggle.IsSelected = _settings.ManageBuildingQueue;
        ManageDailyProjectsToggle.IsSelected = _settings.ManageDailyProjects;
        AutoFundConstructionToggle.IsSelected = _settings.AutoFundConstruction;
        AllowGovernorReassignmentToggle.IsSelected = _settings.AllowGovernorReassignment;
        AutoDefenseToggle.IsSelected = _settings.AutoDefenseEnabled;
        StrategyDropdown.SortOptions.SelectedIndex = (int)_settings.Strategy;
        GovernorModeDropdown.SortOptions.SelectedIndex = (int)_settings.GovernorMode;
        DefensePriorityDropdown.SortOptions.SelectedIndex = (int)_settings.DefensePriority;

        Changed(nameof(ConstructionReserveTargetAmount));
        Changed(nameof(DailyConstructionDepositLimitAmount));
        Changed(nameof(LoyaltyEmergencyThresholdAmount));
        Changed(nameof(FoodShortageDaysAmount));
        Changed(nameof(GovernorCooldownAmount));
        Changed(nameof(TargetDefenseStrengthAmount));
        Changed(nameof(TargetGarrisonTroopsAmount));
    }

    private void RefreshStatus()
    {
        TownManagementOptions options = SubModule.TownManagementBehavior.Options;
        TownManagementSettlementSettings effective = _settings.Resolve(options);
        bool enabled = _settings.Enabled;

        if (!options.Enabled)
        {
            ManagementStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_GLOBAL_PAUSED}Paused: global town management is off.")
                .ToString();
        }
        else if (!enabled)
        {
            ManagementStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_LOCAL_PAUSED}Paused for this fief.")
                .ToString();
        }
        else if (_settings.UseGlobalDefaults)
        {
            ManagementStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_GLOBAL_ACTIVE}Active: following global defaults.")
                .ToString();
        }
        else
        {
            ManagementStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_CUSTOM_ACTIVE}Active: using custom fief settings.")
                .ToString();
        }

        RefreshGovernorStatus(options, effective, enabled);
        RefreshEconomyStatus(effective);
    }

    private void RefreshGovernorStatus(
        TownManagementOptions options,
        TownManagementSettlementSettings settings,
        bool enabled)
    {
        Town town = _settlement.Town;
        GovernorNeedsAttention = town.Governor is null;
        if (town.Governor is Hero currentGovernor)
        {
            GovernorStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_GOVERNOR_ASSIGNED}Governor: {HERO}.")
                .SetTextVariable("HERO", currentGovernor.Name)
                .ToString();
            return;
        }

        if (!options.Enabled || !enabled)
        {
            GovernorStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_NO_GOVERNOR_PAUSED}No governor; Town AI is paused.")
                .ToString();
            return;
        }

        AutoGovernorMode mode = SubModule.TownManagementBehavior
            .EffectiveGovernorMode(settings);
        if (mode == AutoGovernorMode.Off)
        {
            GovernorStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_NO_GOVERNOR_OFF}No governor; governor management is off.")
                .ToString();
            return;
        }

        Hero? candidate = SubModule.TownManagementBehavior
            .GovernorCandidate(town, settings);
        if (candidate is null)
        {
            GovernorStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_NO_GOVERNOR_CANDIDATE}No governor; no eligible unassigned clan hero is available.")
                .ToString();
            return;
        }

        if (mode == AutoGovernorMode.Recommend)
        {
            GovernorStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_GOVERNOR_RECOMMENDED}No governor; recommended: {HERO}. Switch to Assign to appoint automatically.")
                .SetTextVariable("HERO", candidate.Name)
                .ToString();
            return;
        }

        GovernorStatusText = new TextObject(
            "{=PAI_TOWN_STATUS_GOVERNOR_PENDING}No governor; {HERO} will be assigned when these settings are saved or on the next daily update.")
            .SetTextVariable("HERO", candidate.Name)
            .ToString();
    }

    private void RefreshEconomyStatus(TownManagementSettlementSettings settings)
    {
        Town town = _settlement.Town;
        bool foodEmergency = TownManagementBehavior.IsFoodEmergency(
            town,
            settings.FoodShortageDays);
        bool loyaltyEmergency = TownManagementBehavior.IsLoyaltyEmergency(
            town,
            settings);
        EconomyNeedsAttention = foodEmergency || loyaltyEmergency;

        if (foodEmergency)
        {
            float days = TownManagementBehavior.FoodDaysRemaining(town);
            string daysText = float.IsPositiveInfinity(days)
                ? new TextObject("{=PAI_TOWN_FOOD_STABLE}infinite").ToString()
                : Math.Ceiling(days).ToString("0");
            EconomyStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_FOOD_EMERGENCY}Food emergency: {FOOD} stock, {CHANGE}/day; estimated days remaining: {DAYS}. Stabilization has priority.")
                .SetTextVariable("FOOD", town.FoodStocks.ToString("0"))
                .SetTextVariable("CHANGE", Signed(town.FoodChange))
                .SetTextVariable("DAYS", daysText)
                .ToString();
            return;
        }

        if (loyaltyEmergency)
        {
            EconomyStatusText = new TextObject(
                "{=PAI_TOWN_STATUS_LOYALTY_EMERGENCY}Loyalty emergency: {LOYALTY}. Stability has priority over profit.")
                .SetTextVariable("LOYALTY", town.Loyalty.ToString("0"))
                .ToString();
            return;
        }

        int tariffs = (int)Math.Round(Campaign.Current.Models.ClanFinanceModel
            .CalculateTownIncomeFromTariffs(Clan.PlayerClan, town, false)
            .ResultNumber);
        int projects = Campaign.Current.Models.ClanFinanceModel
            .CalculateTownIncomeFromProjects(town);
        int villages = _settlement.BoundVillages.Sum(village => Campaign.Current.Models
            .ClanFinanceModel.CalculateVillageIncome(Clan.PlayerClan, village, false));
        int garrisonWage = town.GarrisonParty?.TotalWage ?? 0;
        int listedNet = tariffs + projects + villages - garrisonWage;
        EconomyNeedsAttention = listedNet < 0;
        EconomyStatusText = new TextObject(
            "{=PAI_TOWN_STATUS_ECONOMY}Prosperity {PROSPERITY}; food change {FOOD}; tariffs {TARIFFS}, projects {PROJECTS}, villages {VILLAGES}, garrison wage {WAGE}; listed net {NET}.")
            .SetTextVariable("PROSPERITY", town.Prosperity.ToString("0"))
            .SetTextVariable("FOOD", Signed(town.FoodChange))
            .SetTextVariable("TARIFFS", tariffs)
            .SetTextVariable("PROJECTS", projects)
            .SetTextVariable("VILLAGES", villages)
            .SetTextVariable("WAGE", garrisonWage)
            .SetTextVariable("NET", listedNet)
            .ToString();
    }

    private static string Signed(float value)
        => value > 0f ? "+" + value.ToString("0.0") : value.ToString("0.0");

    public void AcceptEditTownOptions()
    {
        _garrisonSettings.MaxTroopTier = MaxTroopTierDropdown.SortOptions.SelectedItem.Max;
        _settings.Enabled = EnabledToggle.IsSelected;
        _settings.UseGlobalDefaults = UseGlobalDefaultsToggle.IsSelected;
        _settings.Strategy = StrategyDropdown.SortOptions.SelectedItem.Strategy;
        _settings.ManageBuildingQueue = ManageBuildingQueueToggle.IsSelected;
        _settings.ManageDailyProjects = ManageDailyProjectsToggle.IsSelected;
        _settings.AutoFundConstruction = AutoFundConstructionToggle.IsSelected;
        _settings.GovernorMode = GovernorModeDropdown.SortOptions.SelectedItem.Mode;
        _settings.AllowGovernorReassignment = AllowGovernorReassignmentToggle.IsSelected;
        _settings.AutoDefenseEnabled = AutoDefenseToggle.IsSelected;
        _settings.DefensePriority = DefensePriorityDropdown.SortOptions.SelectedItem.Priority;

        SubModule.TownManagementBehavior.UpdateSettings(_settlement, _settings);
        SubModule.TownManagementBehavior.TryAssignMissingGovernor(_settlement);
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
