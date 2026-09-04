namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>Order kinds. Numeric values are part of the save format.</summary>
public enum PartyOrderType
{
    None = 0,
    PatrolAroundPoint = 1,
    BesiegeSettlement = 2,
    DefendSettlement = 3,
    PatrolClanLands = 4,
    EscortParty = 5,
    StayInSettlement = 6,
    AttackParty = 7,
    RecruitFromTemplate = 8,
    VisitSettlement = 9
}

public static class PartyOrderTypeExtensions
{
    /// <summary>Orders the player can pick from the order dialog.</summary>
    public static readonly PartyOrderType[] PlayerSelectable =
    [
        PartyOrderType.PatrolAroundPoint,
        PartyOrderType.PatrolClanLands,
        PartyOrderType.EscortParty,
        PartyOrderType.VisitSettlement,
        PartyOrderType.StayInSettlement,
        PartyOrderType.DefendSettlement,
        PartyOrderType.BesiegeSettlement,
        PartyOrderType.RecruitFromTemplate
    ];

    /// <summary>Whether the order needs a settlement or party target to be chosen.</summary>
    public static bool NeedsTarget(this PartyOrderType type) => type switch
    {
        PartyOrderType.None => false,
        PartyOrderType.PatrolClanLands => false,
        PartyOrderType.RecruitFromTemplate => false,
        _ => true
    };

    public static bool TargetsParty(this PartyOrderType type)
        => type is PartyOrderType.EscortParty or PartyOrderType.AttackParty;

    /// <summary>Orders that make sense as a standing fallback (no one-shot actions).</summary>
    public static bool CanBeFallback(this PartyOrderType type)
        => type is not (PartyOrderType.BesiegeSettlement or PartyOrderType.AttackParty);
}
