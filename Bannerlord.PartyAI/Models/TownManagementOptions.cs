using System;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Models;

public class TownManagementOptions
{
    private const float MinimumThreatThresholdGap = 0.01f;

    [SaveableProperty(1)] public bool Enabled { get; set; }
    [SaveableProperty(2)] public TownManagementStrategy DefaultStrategy { get; set; } = TownManagementStrategy.Balanced;
    [SaveableProperty(3)] public bool ManageBuildingQueue { get; set; } = true;
    [SaveableProperty(4)] public bool ManageDailyProjects { get; set; } = true;
    [SaveableProperty(5)] public bool AutoFundConstruction { get; set; }
    [SaveableProperty(6)] public int PlayerGoldReserve { get; set; } = 30000;
    [SaveableProperty(7)] public int TownConstructionReserveTarget { get; set; } = 5000;
    [SaveableProperty(8)] public int DailyConstructionDepositLimit { get; set; } = 1000;
    [SaveableProperty(9)] public float LoyaltyEmergencyThreshold { get; set; } = 35f;
    [SaveableProperty(10)] public int FoodShortageDays { get; set; } = 5;
    [SaveableProperty(11)] public AutoGovernorMode GovernorMode { get; set; } = AutoGovernorMode.Recommend;
    [SaveableProperty(12)] public bool AllowGovernorReassignment { get; set; }
    [SaveableProperty(13)] public int GovernorAssignmentCooldownDays { get; set; } = 30;
    [SaveableProperty(14)] public bool AutoDefenseEnabled { get; set; }
    [SaveableProperty(15)] public bool AutoDonateTroops { get; set; }
    [SaveableProperty(16)] public float ThreatRadius { get; set; } = 80f;
    [SaveableProperty(17)] public float DispatchThreatThreshold { get; set; } = 1f;
    [SaveableProperty(18)] public float ReleaseThreatThreshold { get; set; } = 0.25f;
    [SaveableProperty(19)] public float MinimumPartyStrengthRatio { get; set; } = 0.5f;
    [SaveableProperty(20)] public int ReserveMobileParties { get; set; } = 1;
    [SaveableProperty(21)] public int MaxDefendingPartiesPerTown { get; set; } = 1;
    [SaveableProperty(22)] public int MinimumGarrisonDays { get; set; } = 3;
    [SaveableProperty(23)] public int ReassignmentCooldownDays { get; set; } = 7;
    [SaveableProperty(24)] public int DonationTargetTroops { get; set; } = 300;
    [SaveableProperty(25)] public float MaxDonationRatio { get; set; } = 0.25f;
    [SaveableProperty(26)] public int MinimumTroopsAfterDonation { get; set; } = 80;
    [SaveableProperty(27)] public bool DefaultTownDefenseEnabled { get; set; } = true;
    [SaveableProperty(28)] public TownDefensePriority DefaultDefensePriority { get; set; } = TownDefensePriority.Normal;
    [SaveableProperty(29)] public float DefaultTargetDefenseStrength { get; set; } = 500f;

    public TownManagementOptions()
    {
    }

    public TownManagementOptions(TownManagementOptions source)
    {
        Enabled = source.Enabled;
        DefaultStrategy = source.DefaultStrategy;
        ManageBuildingQueue = source.ManageBuildingQueue;
        ManageDailyProjects = source.ManageDailyProjects;
        AutoFundConstruction = source.AutoFundConstruction;
        PlayerGoldReserve = source.PlayerGoldReserve;
        TownConstructionReserveTarget = source.TownConstructionReserveTarget;
        DailyConstructionDepositLimit = source.DailyConstructionDepositLimit;
        LoyaltyEmergencyThreshold = source.LoyaltyEmergencyThreshold;
        FoodShortageDays = source.FoodShortageDays;
        GovernorMode = source.GovernorMode;
        AllowGovernorReassignment = source.AllowGovernorReassignment;
        GovernorAssignmentCooldownDays = source.GovernorAssignmentCooldownDays;
        AutoDefenseEnabled = source.AutoDefenseEnabled;
        AutoDonateTroops = source.AutoDonateTroops;
        ThreatRadius = source.ThreatRadius;
        DispatchThreatThreshold = source.DispatchThreatThreshold;
        ReleaseThreatThreshold = source.ReleaseThreatThreshold;
        MinimumPartyStrengthRatio = source.MinimumPartyStrengthRatio;
        ReserveMobileParties = source.ReserveMobileParties;
        MaxDefendingPartiesPerTown = source.MaxDefendingPartiesPerTown;
        MinimumGarrisonDays = source.MinimumGarrisonDays;
        ReassignmentCooldownDays = source.ReassignmentCooldownDays;
        DonationTargetTroops = source.DonationTargetTroops;
        MaxDonationRatio = source.MaxDonationRatio;
        MinimumTroopsAfterDonation = source.MinimumTroopsAfterDonation;
        DefaultTownDefenseEnabled = source.DefaultTownDefenseEnabled;
        DefaultDefensePriority = source.DefaultDefensePriority;
        DefaultTargetDefenseStrength = source.DefaultTargetDefenseStrength;
        Normalize();
    }

    public TownManagementOptions DeepCopy() => new(this);

    public void Normalize()
    {
        if (DefaultStrategy < TownManagementStrategy.Balanced || DefaultStrategy > TownManagementStrategy.Military)
        {
            DefaultStrategy = TownManagementStrategy.Balanced;
        }

        if (GovernorMode < AutoGovernorMode.Off || GovernorMode > AutoGovernorMode.Assign)
        {
            GovernorMode = AutoGovernorMode.Off;
        }

        if (DefaultDefensePriority < TownDefensePriority.Low || DefaultDefensePriority > TownDefensePriority.Critical)
        {
            DefaultDefensePriority = TownDefensePriority.Normal;
        }

        PlayerGoldReserve = Math.Max(0, PlayerGoldReserve);
        TownConstructionReserveTarget = Math.Max(0, TownConstructionReserveTarget);
        DailyConstructionDepositLimit = Math.Max(0, DailyConstructionDepositLimit);
        LoyaltyEmergencyThreshold = Clamp(LoyaltyEmergencyThreshold, 0f, 100f, 35f);
        FoodShortageDays = Math.Max(0, Math.Min(100, FoodShortageDays));
        GovernorAssignmentCooldownDays = Math.Max(0, GovernorAssignmentCooldownDays);
        ThreatRadius = Clamp(ThreatRadius, 1f, 500f, 80f);
        DispatchThreatThreshold = Clamp(
            DispatchThreatThreshold,
            MinimumThreatThresholdGap,
            5f,
            1f);
        ReleaseThreatThreshold = Math.Min(
            DispatchThreatThreshold - MinimumThreatThresholdGap,
            Clamp(ReleaseThreatThreshold, 0f, 5f, 0.25f));
        MinimumPartyStrengthRatio = Clamp(MinimumPartyStrengthRatio, 0f, 1f, 0.5f);
        ReserveMobileParties = Math.Max(0, ReserveMobileParties);
        MaxDefendingPartiesPerTown = Math.Max(0, MaxDefendingPartiesPerTown);
        MinimumGarrisonDays = Math.Max(0, MinimumGarrisonDays);
        ReassignmentCooldownDays = Math.Max(0, ReassignmentCooldownDays);
        DonationTargetTroops = Math.Max(0, DonationTargetTroops);
        MaxDonationRatio = Clamp(MaxDonationRatio, 0f, 1f, 0.25f);
        MinimumTroopsAfterDonation = Math.Max(0, MinimumTroopsAfterDonation);
        DefaultTargetDefenseStrength = Clamp(DefaultTargetDefenseStrength, 0f, 100000f, 500f);
    }

    private static float Clamp(float value, float minimum, float maximum, float fallback)
    {
        if (float.IsNaN(value))
        {
            return fallback;
        }

        return Math.Max(minimum, Math.Min(maximum, value));
    }
}
