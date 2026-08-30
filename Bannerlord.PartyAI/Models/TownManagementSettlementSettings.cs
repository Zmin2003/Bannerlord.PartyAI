using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Models;

public class TownManagementSettlementSettings
{
    [SaveableProperty(1)] public Settlement? Settlement { get; private set; }
    [SaveableProperty(2)] public bool Enabled { get; set; } = true;
    [SaveableProperty(3)] public TownManagementStrategy Strategy { get; set; } = TownManagementStrategy.Balanced;
    [SaveableProperty(4)] public bool ManageBuildingQueue { get; set; } = true;
    [SaveableProperty(5)] public bool ManageDailyProjects { get; set; } = true;
    [SaveableProperty(6)] public bool AutoFundConstruction { get; set; }
    [SaveableProperty(7)] public int ConstructionReserveTarget { get; set; } = 5000;
    [SaveableProperty(8)] public int DailyConstructionDepositLimit { get; set; } = 1000;
    [SaveableProperty(9)] public float LoyaltyEmergencyThreshold { get; set; } = 35f;
    [SaveableProperty(10)] public int FoodShortageDays { get; set; } = 5;
    [SaveableProperty(11)] public AutoGovernorMode GovernorMode { get; set; } = AutoGovernorMode.Recommend;
    [SaveableProperty(12)] public bool AllowGovernorReassignment { get; set; }
    [SaveableProperty(13)] public int GovernorAssignmentCooldownDays { get; set; } = 30;
    [SaveableProperty(14)] public bool AutoDefenseEnabled { get; set; } = true;
    [SaveableProperty(15)] public TownDefensePriority DefensePriority { get; set; } = TownDefensePriority.Normal;
    [SaveableProperty(16)] public float TargetDefenseStrength { get; set; } = 500f;
    [SaveableProperty(17)] public int TargetGarrisonTroops { get; set; } = 300;

    public TownManagementSettlementSettings()
    {
    }

    public TownManagementSettlementSettings(Settlement settlement, TownManagementOptions options)
    {
        Settlement = settlement;
        Enabled = true;
        Strategy = options.DefaultStrategy;
        ManageBuildingQueue = options.ManageBuildingQueue;
        ManageDailyProjects = options.ManageDailyProjects;
        AutoFundConstruction = options.AutoFundConstruction;
        ConstructionReserveTarget = options.TownConstructionReserveTarget;
        DailyConstructionDepositLimit = options.DailyConstructionDepositLimit;
        LoyaltyEmergencyThreshold = options.LoyaltyEmergencyThreshold;
        FoodShortageDays = options.FoodShortageDays;
        GovernorMode = options.GovernorMode;
        AllowGovernorReassignment = options.AllowGovernorReassignment;
        GovernorAssignmentCooldownDays = options.GovernorAssignmentCooldownDays;
        AutoDefenseEnabled = options.DefaultTownDefenseEnabled;
        DefensePriority = options.DefaultDefensePriority;
        TargetDefenseStrength = options.DefaultTargetDefenseStrength;
        TargetGarrisonTroops = options.DonationTargetTroops;
        Normalize();
    }

    public TownManagementSettlementSettings(TownManagementSettlementSettings source)
    {
        Settlement = source.Settlement;
        Enabled = source.Enabled;
        Strategy = source.Strategy;
        ManageBuildingQueue = source.ManageBuildingQueue;
        ManageDailyProjects = source.ManageDailyProjects;
        AutoFundConstruction = source.AutoFundConstruction;
        ConstructionReserveTarget = source.ConstructionReserveTarget;
        DailyConstructionDepositLimit = source.DailyConstructionDepositLimit;
        LoyaltyEmergencyThreshold = source.LoyaltyEmergencyThreshold;
        FoodShortageDays = source.FoodShortageDays;
        GovernorMode = source.GovernorMode;
        AllowGovernorReassignment = source.AllowGovernorReassignment;
        GovernorAssignmentCooldownDays = source.GovernorAssignmentCooldownDays;
        AutoDefenseEnabled = source.AutoDefenseEnabled;
        DefensePriority = source.DefensePriority;
        TargetDefenseStrength = source.TargetDefenseStrength;
        TargetGarrisonTroops = source.TargetGarrisonTroops;
        Normalize();
    }

    public static TownManagementSettlementSettings FromOptions(
        Settlement settlement,
        TownManagementOptions options) => new(settlement, options);

    public TownManagementSettlementSettings DeepCopy() => new(this);

    public void Normalize()
    {
        if (Strategy < TownManagementStrategy.Balanced || Strategy > TownManagementStrategy.Military)
        {
            Strategy = TownManagementStrategy.Balanced;
        }

        if (GovernorMode < AutoGovernorMode.Off || GovernorMode > AutoGovernorMode.Assign)
        {
            GovernorMode = AutoGovernorMode.Off;
        }

        if (DefensePriority < TownDefensePriority.Low || DefensePriority > TownDefensePriority.Critical)
        {
            DefensePriority = TownDefensePriority.Normal;
        }

        ConstructionReserveTarget = Math.Max(0, ConstructionReserveTarget);
        DailyConstructionDepositLimit = Math.Max(0, DailyConstructionDepositLimit);
        LoyaltyEmergencyThreshold = Clamp(LoyaltyEmergencyThreshold, 0f, 100f, 35f);
        FoodShortageDays = Math.Max(0, Math.Min(100, FoodShortageDays));
        GovernorAssignmentCooldownDays = Math.Max(0, GovernorAssignmentCooldownDays);
        TargetDefenseStrength = Clamp(TargetDefenseStrength, 0f, 1000000f, 500f);
        TargetGarrisonTroops = Math.Max(0, TargetGarrisonTroops);
    }

    internal void AttachTo(Settlement settlement) => Settlement = settlement;

    private static float Clamp(float value, float minimum, float maximum, float fallback)
    {
        if (float.IsNaN(value))
        {
            return fallback;
        }

        return Math.Max(minimum, Math.Min(maximum, value));
    }
}
