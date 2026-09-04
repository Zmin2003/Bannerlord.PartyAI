namespace Bannerlord.PartyAI.Parties.Recruitment;

/// <summary>What a party does automatically when it enters a town or village.</summary>
public enum SettlementAutomationLevel
{
    Off = 0,
    /// <summary>Recruit volunteers matching the template and composition.</summary>
    Recruit = 1,
    /// <summary>Also spend XP, gold and required items on available upgrades.</summary>
    RecruitAndUpgrade = 2,
    /// <summary>Also convert off-template troops and re-equip heroes from the party inventory.</summary>
    Full = 3
}
