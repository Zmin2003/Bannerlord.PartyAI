namespace Bannerlord.PartyAI.Finance;

/// <summary>How the mod handles unprofitable player workshops. Values are part of the save format.</summary>
public enum WorkshopMode
{
    Off = 0,
    /// <summary>Report the better production type but leave the decision to the player.</summary>
    Recommend = 1,
    /// <summary>Switch production automatically when the treasury can afford the conversion.</summary>
    Auto = 2
}
