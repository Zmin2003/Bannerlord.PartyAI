using Bannerlord.PartyAI.Core;

namespace Bannerlord.PartyAI.Towns;

/// <summary>Display names for town-management enums.</summary>
public static class TownText
{
    public static string Strategy(TownStrategy strategy) => strategy switch
    {
        TownStrategy.Stability => L.S("{=PAI_TOWN_STRATEGY_STABILITY}Stability"),
        TownStrategy.Economy => L.S("{=PAI_TOWN_STRATEGY_ECONOMY}Economy"),
        TownStrategy.Military => L.S("{=PAI_TOWN_STRATEGY_MILITARY}Military"),
        _ => L.S("{=PAI_TOWN_STRATEGY_BALANCED}Balanced")
    };

    public static string Governor(GovernorMode mode) => mode switch
    {
        GovernorMode.Recommend => L.S("{=PAI_GOVERNOR_RECOMMEND}Recommend"),
        GovernorMode.Assign => L.S("{=PAI_GOVERNOR_ASSIGN}Assign automatically"),
        _ => L.S("{=PAI_GOVERNOR_OFF}Off")
    };

    public static string Priority(DefensePriority priority) => priority switch
    {
        DefensePriority.Low => L.S("{=PAI_PRIORITY_LOW}Low"),
        DefensePriority.High => L.S("{=PAI_PRIORITY_HIGH}High"),
        DefensePriority.Critical => L.S("{=PAI_PRIORITY_CRITICAL}Critical"),
        _ => L.S("{=PAI_PRIORITY_NORMAL}Normal")
    };
}
