using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dropdowns;
using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels;

public sealed class TownManagementOptionsVM : ViewModel
{
    private readonly Action _onClose;
    private readonly TownManagementOptions _options;

    public TownManagementOptionsVM(Action callback)
    {
        _onClose = callback;
        _options = SubModule.TownManagementBehavior.Options.DeepCopy();

        EnabledToggle = Toggle(
            "{=PAI_TOWN_ENABLED}Enable Town Management",
            _options.Enabled,
            "{=PAI_TOWN_ENABLED_HINT}Allow Party AI to manage player-clan towns and castles using the settings below.");
        ManageBuildingQueueToggle = Toggle(
            "{=PAI_TOWN_DEFAULT_BUILD_QUEUE}Default: Manage Building Queue",
            _options.ManageBuildingQueue,
            "{=PAI_TOWN_DEFAULT_BUILD_QUEUE_HINT}Enable building-queue management when Town AI settings are first created for a settlement.");
        ManageDailyProjectsToggle = Toggle(
            "{=PAI_TOWN_DEFAULT_DAILY_PROJECTS}Default: Manage Daily Projects",
            _options.ManageDailyProjects,
            "{=PAI_TOWN_DEFAULT_DAILY_PROJECTS_HINT}Enable daily-project management when Town AI settings are first created for a settlement.");
        AutoFundConstructionToggle = Toggle(
            "{=PAI_TOWN_DEFAULT_AUTO_FUND}Default: Fund Construction",
            _options.AutoFundConstruction,
            "{=PAI_TOWN_DEFAULT_AUTO_FUND_HINT}Enable automatic construction funding when Town AI settings are first created for a settlement.");
        AllowGovernorReassignmentToggle = Toggle(
            "{=PAI_TOWN_DEFAULT_GOVERNOR_REASSIGN}Default: Allow Governor Reassignment",
            _options.AllowGovernorReassignment,
            "{=PAI_TOWN_DEFAULT_GOVERNOR_REASSIGN_HINT}Allow governor reassignment by default when Town AI settings are first created for a settlement.");
        AutoDefenseToggle = Toggle(
            "{=PAI_TOWN_AUTO_DEFENSE}Dispatch Defenders Automatically",
            _options.AutoDefenseEnabled,
            "{=PAI_TOWN_AUTO_DEFENSE_HINT}Temporarily redirect suitable player-clan parties to threatened settlements and restore their previous orders afterwards.");
        AutoDonateTroopsToggle = Toggle(
            "{=PAI_TOWN_AUTO_DONATE}Donate Garrison Troops Automatically",
            _options.AutoDonateTroops,
            "{=PAI_TOWN_AUTO_DONATE_HINT}Allow assigned defenders whose party options permit donations to transfer troops once on arrival while preserving their minimum force.");
        DefaultTownDefenseToggle = Toggle(
            "{=PAI_TOWN_DEFAULT_DEFENSE}Default: Enable Automatic Defense",
            _options.DefaultTownDefenseEnabled,
            "{=PAI_TOWN_DEFAULT_DEFENSE_HINT}Use automatic defense by default when settings are first created for a player-clan town or castle.");

        DefaultStrategyDropdown = new TownManagementStrategyDropdownVM(_options.DefaultStrategy);
        GovernorModeDropdown = new AutoGovernorModeDropdownVM(_options.GovernorMode);
        DefaultDefensePriorityDropdown = new TownDefensePriorityDropdownVM(_options.DefaultDefensePriority);
    }

    private static PartyAIOptionToggleVM Toggle(string text, bool selected, string hint)
        => new(new TextObject(text), selected, new TextObject(hint));

    [DataSourceProperty] public string TitleText => new TextObject("{=PAI_TOWN_OPTIONS_TITLE}Town Management").ToString();
    [DataSourceProperty] public string AcceptText => new TextObject("{=bV75iwKa}Save").ToString();
    [DataSourceProperty] public string CancelText => GameTexts.FindText("str_cancel").ToString();
    [DataSourceProperty] public string GeneralHeader => new TextObject("{=PAI_TOWN_GENERAL_HEADER}General").ToString();
    [DataSourceProperty] public string ConstructionHeader => new TextObject("{=PAI_TOWN_CONSTRUCTION_HEADER}Construction").ToString();
    [DataSourceProperty] public string GovernorHeader => new TextObject("{=PAI_TOWN_GOVERNOR_HEADER}Governors").ToString();
    [DataSourceProperty] public string DefenseHeader => new TextObject("{=PAI_TOWN_DEFENSE_HEADER}Defense Dispatch").ToString();
    [DataSourceProperty] public string DonationHeader => new TextObject("{=PAI_TOWN_DONATION_HEADER}Garrison Reinforcement").ToString();

    [DataSourceProperty] public PartyAIOptionToggleVM EnabledToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM ManageBuildingQueueToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM ManageDailyProjectsToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM AutoFundConstructionToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM AllowGovernorReassignmentToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM AutoDefenseToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM AutoDonateTroopsToggle { get; }
    [DataSourceProperty] public PartyAIOptionToggleVM DefaultTownDefenseToggle { get; }
    [DataSourceProperty] public TownManagementStrategyDropdownVM DefaultStrategyDropdown { get; }
    [DataSourceProperty] public AutoGovernorModeDropdownVM GovernorModeDropdown { get; }
    [DataSourceProperty] public TownDefensePriorityDropdownVM DefaultDefensePriorityDropdown { get; }

    [DataSourceProperty] public string DefaultStrategyText => new TextObject("{=PAI_TOWN_DEFAULT_STRATEGY}Default Strategy").ToString();
    [DataSourceProperty] public string GovernorModeText => new TextObject("{=PAI_TOWN_DEFAULT_GOVERNOR_MODE}Default Governor Mode").ToString();
    [DataSourceProperty] public string DefaultDefensePriorityText => new TextObject("{=PAI_TOWN_DEFAULT_DEFENSE_PRIORITY}Default Defense Priority").ToString();
    [DataSourceProperty] public HintViewModel DefaultStrategyHint => Hint("{=PAI_TOWN_DEFAULT_STRATEGY_HINT}Strategy copied to a settlement when its Town AI settings are first created.");
    [DataSourceProperty] public HintViewModel GovernorModeHint => Hint("{=PAI_TOWN_DEFAULT_GOVERNOR_MODE_HINT}Governor mode copied to a settlement when its Town AI settings are first created.");
    [DataSourceProperty] public HintViewModel DefaultDefensePriorityHint => Hint("{=PAI_TOWN_DEFAULT_DEFENSE_PRIORITY_HINT}Priority copied to a settlement when its Town AI settings are first created.");
    [DataSourceProperty] public HintViewModel ChangeHint => Hint("{=PAIXIv9UgAt}Change");

    [DataSourceProperty] public string PlayerGoldReserveText => new TextObject("{=PAI_TOWN_PLAYER_GOLD_RESERVE}Player Gold Reserve").ToString();
    [DataSourceProperty] public string PlayerGoldReserveAmount => _options.PlayerGoldReserve.ToString();
    [DataSourceProperty] public HintViewModel PlayerGoldReserveHint => Hint("{=PAI_TOWN_PLAYER_GOLD_RESERVE_HINT}Automatic construction funding never reduces player gold below this amount.");

    [DataSourceProperty] public string TownConstructionReserveTargetText => new TextObject("{=PAI_TOWN_DEFAULT_RESERVE_TARGET}Default Construction Reserve Target").ToString();
    [DataSourceProperty] public string TownConstructionReserveTargetAmount => _options.TownConstructionReserveTarget.ToString();
    [DataSourceProperty] public HintViewModel TownConstructionReserveTargetHint => Hint("{=PAI_TOWN_DEFAULT_RESERVE_TARGET_HINT}Construction reserve target copied to newly created settlement settings.");

    [DataSourceProperty] public string DailyConstructionDepositLimitText => new TextObject("{=PAI_TOWN_DEFAULT_DAILY_DEPOSIT_LIMIT}Default Daily Funding Limit").ToString();
    [DataSourceProperty] public string DailyConstructionDepositLimitAmount => _options.DailyConstructionDepositLimit.ToString();
    [DataSourceProperty] public HintViewModel DailyConstructionDepositLimitHint => Hint("{=PAI_TOWN_DEFAULT_DAILY_DEPOSIT_LIMIT_HINT}Daily construction funding limit copied to newly created settlement settings.");

    [DataSourceProperty] public string LoyaltyEmergencyThresholdText => new TextObject("{=PAI_TOWN_DEFAULT_LOYALTY_THRESHOLD}Default Loyalty Emergency Threshold").ToString();
    [DataSourceProperty] public string LoyaltyEmergencyThresholdAmount => ((int)_options.LoyaltyEmergencyThreshold).ToString();
    [DataSourceProperty] public HintViewModel LoyaltyEmergencyThresholdHint => Hint("{=PAI_TOWN_DEFAULT_LOYALTY_THRESHOLD_HINT}Loyalty emergency threshold copied to newly created settlement settings.");

    [DataSourceProperty] public string FoodShortageDaysText => new TextObject("{=PAI_TOWN_DEFAULT_FOOD_SHORTAGE_DAYS}Default Food Emergency Days").ToString();
    [DataSourceProperty] public string FoodShortageDaysAmount => _options.FoodShortageDays.ToString();
    [DataSourceProperty] public HintViewModel FoodShortageDaysHint => Hint("{=PAI_TOWN_DEFAULT_FOOD_SHORTAGE_DAYS_HINT}Food-emergency horizon copied to newly created settlement settings.");

    [DataSourceProperty] public string GovernorCooldownText => new TextObject("{=PAI_TOWN_DEFAULT_GOVERNOR_COOLDOWN}Default Governor Cooldown").ToString();
    [DataSourceProperty] public string GovernorCooldownAmount => Days(_options.GovernorAssignmentCooldownDays);
    [DataSourceProperty] public HintViewModel GovernorCooldownHint => Hint("{=PAI_TOWN_DEFAULT_GOVERNOR_COOLDOWN_HINT}Governor reassignment cooldown copied to newly created settlement settings.");

    [DataSourceProperty] public string ThreatRadiusText => new TextObject("{=PAI_TOWN_THREAT_RADIUS}Threat Radius").ToString();
    [DataSourceProperty] public string ThreatRadiusAmount => ((int)_options.ThreatRadius).ToString();
    [DataSourceProperty] public HintViewModel ThreatRadiusHint => Hint("{=PAI_TOWN_THREAT_RADIUS_HINT}Map radius used to count hostile parties near a settlement.");

    [DataSourceProperty] public string DispatchThreatThresholdText => new TextObject("{=PAI_TOWN_DISPATCH_THRESHOLD}Dispatch Threat Ratio").ToString();
    [DataSourceProperty] public string DispatchThreatThresholdAmount => Percentage(_options.DispatchThreatThreshold);
    [DataSourceProperty] public HintViewModel DispatchThreatThresholdHint => Hint("{=PAI_TOWN_DISPATCH_THRESHOLD_HINT}Dispatch defenders when nearby hostile strength reaches this share of local defense strength.");

    [DataSourceProperty] public string ReleaseThreatThresholdText => new TextObject("{=PAI_TOWN_RELEASE_THRESHOLD}Release Threat Ratio").ToString();
    [DataSourceProperty] public string ReleaseThreatThresholdAmount => Percentage(_options.ReleaseThreatThreshold);
    [DataSourceProperty] public HintViewModel ReleaseThreatThresholdHint => Hint("{=PAI_TOWN_RELEASE_THRESHOLD_HINT}Restore defenders' previous orders after threat falls below this ratio. Keep it lower than the dispatch ratio.");

    [DataSourceProperty] public string MinimumPartyStrengthRatioText => new TextObject("{=PAI_TOWN_MIN_PARTY_RATIO}Minimum Party Strength").ToString();
    [DataSourceProperty] public string MinimumPartyStrengthRatioAmount => Percentage(_options.MinimumPartyStrengthRatio);
    [DataSourceProperty] public HintViewModel MinimumPartyStrengthRatioHint => Hint("{=PAI_TOWN_MIN_PARTY_RATIO_HINT}Only dispatch parties at or above this share of their party-size limit.");

    [DataSourceProperty] public string ReserveMobilePartiesText => new TextObject("{=PAI_TOWN_RESERVE_PARTIES}Reserve Mobile Parties").ToString();
    [DataSourceProperty] public string ReserveMobilePartiesAmount => _options.ReserveMobileParties.ToString();
    [DataSourceProperty] public HintViewModel ReserveMobilePartiesHint => Hint("{=PAI_TOWN_RESERVE_PARTIES_HINT}Number of eligible clan parties Town AI must leave unassigned as a mobile reserve.");

    [DataSourceProperty] public string MaxDefendingPartiesText => new TextObject("{=PAI_TOWN_MAX_DEFENDERS}Maximum Defenders per Fief").ToString();
    [DataSourceProperty] public string MaxDefendingPartiesAmount => _options.MaxDefendingPartiesPerTown.ToString();
    [DataSourceProperty] public HintViewModel MaxDefendingPartiesHint => Hint("{=PAI_TOWN_MAX_DEFENDERS_HINT}Maximum clan parties Town AI may assign to one settlement at the same time.");

    [DataSourceProperty] public string MinimumGarrisonDaysText => new TextObject("{=PAI_TOWN_MIN_ASSIGNMENT_DAYS}Minimum Assignment Days").ToString();
    [DataSourceProperty] public string MinimumGarrisonDaysAmount => Days(_options.MinimumGarrisonDays);
    [DataSourceProperty] public HintViewModel MinimumGarrisonDaysHint => Hint("{=PAI_TOWN_MIN_ASSIGNMENT_DAYS_HINT}Keep a defender assigned for at least this many days unless its order becomes invalid.");

    [DataSourceProperty] public string ReassignmentCooldownText => new TextObject("{=PAI_TOWN_REASSIGNMENT_COOLDOWN}Party Reassignment Cooldown").ToString();
    [DataSourceProperty] public string ReassignmentCooldownAmount => Days(_options.ReassignmentCooldownDays);
    [DataSourceProperty] public HintViewModel ReassignmentCooldownHint => Hint("{=PAI_TOWN_REASSIGNMENT_COOLDOWN_HINT}Wait this many days after release before automatically assigning the same party again.");

    [DataSourceProperty] public string DefaultTargetDefenseStrengthText => new TextObject("{=PAI_TOWN_DEFAULT_DEFENSE_STRENGTH}Default Target Defense Strength").ToString();
    [DataSourceProperty] public string DefaultTargetDefenseStrengthAmount => ((int)_options.DefaultTargetDefenseStrength).ToString();
    [DataSourceProperty] public HintViewModel DefaultTargetDefenseStrengthHint => Hint("{=PAI_TOWN_DEFAULT_DEFENSE_STRENGTH_HINT}Desired combined defense strength copied to new settlement settings. Zero lets Town AI calculate a target.");

    [DataSourceProperty] public string DonationTargetTroopsText => new TextObject("{=PAI_TOWN_DEFAULT_GARRISON_TARGET}Default Target Garrison Troops").ToString();
    [DataSourceProperty] public string DonationTargetTroopsAmount => _options.DonationTargetTroops.ToString();
    [DataSourceProperty] public HintViewModel DonationTargetTroopsHint => Hint("{=PAI_TOWN_DEFAULT_GARRISON_TARGET_HINT}Target garrison troop count copied to newly created settlement settings. Zero disables the fixed target.");

    [DataSourceProperty] public string MaxDonationRatioText => new TextObject("{=PAI_TOWN_MAX_DONATION_RATIO}Maximum Donation Share").ToString();
    [DataSourceProperty] public string MaxDonationRatioAmount => Percentage(_options.MaxDonationRatio);
    [DataSourceProperty] public HintViewModel MaxDonationRatioHint => Hint("{=PAI_TOWN_MAX_DONATION_RATIO_HINT}Maximum share of an assigned party's troops that may be donated during one assignment.");

    [DataSourceProperty] public string MinimumTroopsAfterDonationText => new TextObject("{=PAI_TOWN_MIN_REMAINING_TROOPS}Minimum Remaining Party Troops").ToString();
    [DataSourceProperty] public string MinimumTroopsAfterDonationAmount => _options.MinimumTroopsAfterDonation.ToString();
    [DataSourceProperty] public HintViewModel MinimumTroopsAfterDonationHint => Hint("{=PAI_TOWN_MIN_REMAINING_TROOPS_HINT}Never reduce an assigned party below this troop count when donating to a garrison.");

    public void EditPlayerGoldReserve() => EditInteger(
        _options.PlayerGoldReserve, 0, 2000000,
        "{=PAI_TOWN_PLAYER_GOLD_RESERVE}Player Gold Reserve",
        value => { _options.PlayerGoldReserve = value; Changed(nameof(PlayerGoldReserveAmount)); });

    public void EditTownConstructionReserveTarget() => EditInteger(
        _options.TownConstructionReserveTarget, 0, 2000000,
        "{=PAI_TOWN_DEFAULT_RESERVE_TARGET}Default Construction Reserve Target",
        value => { _options.TownConstructionReserveTarget = value; Changed(nameof(TownConstructionReserveTargetAmount)); });

    public void EditDailyConstructionDepositLimit() => EditInteger(
        _options.DailyConstructionDepositLimit, 0, 200000,
        "{=PAI_TOWN_DEFAULT_DAILY_DEPOSIT_LIMIT}Default Daily Funding Limit",
        value => { _options.DailyConstructionDepositLimit = value; Changed(nameof(DailyConstructionDepositLimitAmount)); });

    public void EditLoyaltyEmergencyThreshold() => EditInteger(
        (int)_options.LoyaltyEmergencyThreshold, 0, 100,
        "{=PAI_TOWN_DEFAULT_LOYALTY_THRESHOLD}Default Loyalty Emergency Threshold",
        value => { _options.LoyaltyEmergencyThreshold = value; Changed(nameof(LoyaltyEmergencyThresholdAmount)); });

    public void EditFoodShortageDays() => EditInteger(
        _options.FoodShortageDays, 1, 30,
        "{=PAI_TOWN_DEFAULT_FOOD_SHORTAGE_DAYS}Default Food Emergency Days",
        value => { _options.FoodShortageDays = value; Changed(nameof(FoodShortageDaysAmount)); });

    public void EditGovernorCooldown() => EditInteger(
        _options.GovernorAssignmentCooldownDays, 0, 365,
        "{=PAI_TOWN_DEFAULT_GOVERNOR_COOLDOWN}Default Governor Cooldown",
        value => { _options.GovernorAssignmentCooldownDays = value; Changed(nameof(GovernorCooldownAmount)); });

    public void EditThreatRadius() => EditInteger(
        (int)_options.ThreatRadius, 1, 500,
        "{=PAI_TOWN_THREAT_RADIUS}Threat Radius",
        value => { _options.ThreatRadius = value; Changed(nameof(ThreatRadiusAmount)); });

    public void EditDispatchThreatThreshold() => EditRatio(
        _options.DispatchThreatThreshold,
        "{=PAI_TOWN_DISPATCH_THRESHOLD}Dispatch Threat Ratio",
        value => { _options.DispatchThreatThreshold = value; Changed(nameof(DispatchThreatThresholdAmount)); });

    public void EditReleaseThreatThreshold() => EditRatio(
        _options.ReleaseThreatThreshold,
        "{=PAI_TOWN_RELEASE_THRESHOLD}Release Threat Ratio",
        value => { _options.ReleaseThreatThreshold = value; Changed(nameof(ReleaseThreatThresholdAmount)); });

    public void EditMinimumPartyStrengthRatio() => EditRatio(
        _options.MinimumPartyStrengthRatio,
        100,
        "{=PAI_TOWN_MIN_PARTY_RATIO}Minimum Party Strength",
        value => { _options.MinimumPartyStrengthRatio = value; Changed(nameof(MinimumPartyStrengthRatioAmount)); });

    public void EditReserveMobileParties() => EditInteger(
        _options.ReserveMobileParties, 0, 20,
        "{=PAI_TOWN_RESERVE_PARTIES}Reserve Mobile Parties",
        value => { _options.ReserveMobileParties = value; Changed(nameof(ReserveMobilePartiesAmount)); });

    public void EditMaxDefendingParties() => EditInteger(
        _options.MaxDefendingPartiesPerTown, 0, 20,
        "{=PAI_TOWN_MAX_DEFENDERS}Maximum Defenders per Fief",
        value => { _options.MaxDefendingPartiesPerTown = value; Changed(nameof(MaxDefendingPartiesAmount)); });

    public void EditMinimumGarrisonDays() => EditInteger(
        _options.MinimumGarrisonDays, 0, 365,
        "{=PAI_TOWN_MIN_ASSIGNMENT_DAYS}Minimum Assignment Days",
        value => { _options.MinimumGarrisonDays = value; Changed(nameof(MinimumGarrisonDaysAmount)); });

    public void EditReassignmentCooldown() => EditInteger(
        _options.ReassignmentCooldownDays, 0, 365,
        "{=PAI_TOWN_REASSIGNMENT_COOLDOWN}Party Reassignment Cooldown",
        value => { _options.ReassignmentCooldownDays = value; Changed(nameof(ReassignmentCooldownAmount)); });

    public void EditDefaultTargetDefenseStrength() => EditInteger(
        (int)_options.DefaultTargetDefenseStrength, 0, 100000,
        "{=PAI_TOWN_DEFAULT_DEFENSE_STRENGTH}Default Target Defense Strength",
        value => { _options.DefaultTargetDefenseStrength = value; Changed(nameof(DefaultTargetDefenseStrengthAmount)); });

    public void EditDonationTargetTroops() => EditInteger(
        _options.DonationTargetTroops, 0, 5000,
        "{=PAI_TOWN_DEFAULT_GARRISON_TARGET}Default Target Garrison Troops",
        value => { _options.DonationTargetTroops = value; Changed(nameof(DonationTargetTroopsAmount)); });

    public void EditMaxDonationRatio() => EditRatio(
        _options.MaxDonationRatio,
        100,
        "{=PAI_TOWN_MAX_DONATION_RATIO}Maximum Donation Share",
        value => { _options.MaxDonationRatio = value; Changed(nameof(MaxDonationRatioAmount)); });

    public void EditMinimumTroopsAfterDonation() => EditInteger(
        _options.MinimumTroopsAfterDonation, 0, 5000,
        "{=PAI_TOWN_MIN_REMAINING_TROOPS}Minimum Remaining Party Troops",
        value => { _options.MinimumTroopsAfterDonation = value; Changed(nameof(MinimumTroopsAfterDonationAmount)); });

    public void AcceptEditTownManagementOptions()
    {
        _options.Enabled = EnabledToggle.IsSelected;
        _options.DefaultStrategy = DefaultStrategyDropdown.SortOptions.SelectedItem.Strategy;
        _options.ManageBuildingQueue = ManageBuildingQueueToggle.IsSelected;
        _options.ManageDailyProjects = ManageDailyProjectsToggle.IsSelected;
        _options.AutoFundConstruction = AutoFundConstructionToggle.IsSelected;
        _options.GovernorMode = GovernorModeDropdown.SortOptions.SelectedItem.Mode;
        _options.AllowGovernorReassignment = AllowGovernorReassignmentToggle.IsSelected;
        _options.AutoDefenseEnabled = AutoDefenseToggle.IsSelected;
        _options.AutoDonateTroops = AutoDonateTroopsToggle.IsSelected;
        _options.DefaultTownDefenseEnabled = DefaultTownDefenseToggle.IsSelected;
        _options.DefaultDefensePriority = DefaultDefensePriorityDropdown.SortOptions.SelectedItem.Priority;

        SubModule.TownManagementBehavior.UpdateOptions(_options);
        _onClose?.Invoke();
    }

    public void CancelEditTownManagementOptions() => _onClose?.Invoke();

    private static HintViewModel Hint(string text) => new(new TextObject(text));
    private static string Percentage(float value) => ((int)Math.Round(value * 100f)).ToString() + "%";
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

    private void EditRatio(
        float value,
        string title,
        Action<float> onChanged)
        => EditRatio(value, 500, title, onChanged);

    private void EditRatio(
        float value,
        int maximumPercentage,
        string title,
        Action<float> onChanged)
    {
        SubModule.InformationManager.ShowNumberPickerInquiry(
            (int)Math.Round(value * 100f),
            0,
            maximumPercentage,
            new TextObject(title).ToString(),
            string.Empty,
            result => onChanged(result / 100f),
            isPercentage: true);
    }
}
