using Bannerlord.PartyAI.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>Human-readable text for orders.</summary>
public static class OrderText
{
    /// <summary>"Patrolling around X" — what the party is currently doing.</summary>
    public static TextObject Status(PartyOrder? order) => order?.Behavior switch
    {
        PartyOrderType.PatrolAroundPoint => L.T("{=yUVv3z5V}Patrolling around {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.BesiegeSettlement => L.T("{=JTxI3sW2}Besieging {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.DefendSettlement => L.T("{=rGy8vjOv}Defending {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.StayInSettlement => L.T("{=PAIdTWGYLu0}Staying in {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.EscortParty => L.T("{=OpzzCPiP}Following {TARGET_PARTY}", "TARGET_PARTY", TargetName(order)),
        PartyOrderType.AttackParty => L.T("{=exnL6SS7}Attacking {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.PatrolClanLands => L.T("{=PAI0oBFsSJO}Patrolling Clan Territory"),
        PartyOrderType.RecruitFromTemplate => L.T("{=PAIImuFNGIe}Recruiting Troops"),
        PartyOrderType.VisitSettlement => L.T("{=PAIzp4R8TTM}Visiting {SETTLEMENT}", "SETTLEMENT", TargetName(order)),
        _ => L.T("{=PAIZZ1tGdbA}No active order"),
    };

    /// <summary>"Patrol around X" — the imperative form used in the queue.</summary>
    public static TextObject Command(PartyOrder? order) => order?.Behavior switch
    {
        PartyOrderType.PatrolAroundPoint => L.T("{=PAIpc5Yu18Z}Patrol around {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.BesiegeSettlement => L.T("{=PAIPMS0nSSq}Besiege {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.DefendSettlement => L.T("{=PAITOricrPO}Defend {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.StayInSettlement => L.T("{=PAIj66iTjmT}Stay in {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.EscortParty => L.T("{=PAINt8jD9tc}Follow {TARGET_PARTY}", "TARGET_PARTY", TargetName(order)),
        PartyOrderType.AttackParty => L.T("{=PAIDycETWvm}Attack {TARGET_SETTLEMENT}", "TARGET_SETTLEMENT", TargetName(order)),
        PartyOrderType.PatrolClanLands => L.T("{=PAIgvZTEG1V}Patrol Clan Territory"),
        PartyOrderType.RecruitFromTemplate => L.T("{=PAIhBXucHBM}Recruit Troops"),
        PartyOrderType.VisitSettlement => L.T("{=PAIRyxa5pnP}Visit {SETTLEMENT}", "SETTLEMENT", TargetName(order)),
        _ => L.T("{=PAISXYCwfO9}No orders in queue"),
    };

    /// <summary>The order kind by itself, for the "add order" picker.</summary>
    public static TextObject Kind(PartyOrderType type) => type switch
    {
        PartyOrderType.PatrolAroundPoint => L.T("{=PAIaOu88dqT}Patrol an Area"),
        PartyOrderType.PatrolClanLands => L.T("{=PAIb2F6Hyfs}Patrol Clan Territory"),
        PartyOrderType.EscortParty => L.T("{=PAI1Et6heEa}Escort a Party"),
        PartyOrderType.VisitSettlement => L.T("{=PAIIL6JG6Na}Visit a Settlement"),
        PartyOrderType.StayInSettlement => L.T("{=PAIOzsG1s1J}Stay in a Settlement"),
        PartyOrderType.DefendSettlement => L.T("{=PAIgNGL6W5j}Defend a Settlement"),
        PartyOrderType.BesiegeSettlement => L.T("{=PAIgXDbzpdD}Besiege a Settlement"),
        PartyOrderType.RecruitFromTemplate => L.T("{=PAIyzzBSM4P}Recruit"),
        _ => L.T("{=koX9okuG}None"),
    };

    public static TextObject KindHint(PartyOrderType type) => type switch
    {
        PartyOrderType.PatrolAroundPoint => L.T("{=PAIPQxGUfhd}Patrol an area around the target settlement. The party will visit villages and towns to restock its troops and supplies. Bandits and other enemies will be chased down if the party leader believes they can be caught. The party will defend villages and castles/towns within its patrol radius from raids and sieges."),
        PartyOrderType.PatrolClanLands => L.T("{=PAI_ORDER_PATROL_CLAN_HINT}Roam between your clan's settlements, moving on to a new one every day."),
        PartyOrderType.EscortParty => L.T("{=PAIEI3gTLMP}Escort a party"),
        PartyOrderType.VisitSettlement => L.T("{=PAIljAEpAKF}Visit a settlement but don't stay there."),
        PartyOrderType.StayInSettlement => L.T("{=PAIVeQlQhCC}Stay in the settlement. Will not defend the settlement if it is under siege and the party is outside the walls."),
        PartyOrderType.DefendSettlement => L.T("{=PAITZmUFJSB}Stay in the garrison of the settlement. The party may make occassional visits to other settlements for food if there is not enough food in the settlement to buy."),
        PartyOrderType.BesiegeSettlement => L.T("{=PAIzxQXNul8}The party or army will besiege the target settlement. The order will be cleared upon capturing the city or by the attacking army being defeated."),
        PartyOrderType.RecruitFromTemplate => L.T("{=PAIHJFAtbk8}Order the party leader to only focus on recruiting troops. If they have an assigned troop template, they will only visit settlements that offer those troops. Keep in mind these settlements may be far away."),
        _ => TextObject.GetEmpty(),
    };

    private static TextObject TargetName(PartyOrder? order) => order?.Target?.Name ?? TextObject.GetEmpty();
}
