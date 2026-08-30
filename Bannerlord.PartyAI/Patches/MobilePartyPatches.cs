using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

internal class MobilePartyPatches
{
    public static void Apply(Harmony harmony)
    {
        // TODO: Instead of MobileParty, patch SandBox.View.Map.Visuals.MapEntityVisual<> types,
        // patching SetMove(...) methods doesn't work for settlements
        // or parties that are on water when we are on land or vice versa.
        harmony.Patch<MobileParty>()
            .Method(x => x.SetMoveGoToPoint(default, default))
                .Postfix(SetMoveGoToPointPostfix)
            .Method(x => x.SetMoveEngageParty(default, default))
                .Postfix(SetMoveEngagePartyPostfix)
            .Method(x => x.SetMoveEscortParty(default, default, default))
                .Postfix(SetMoveEscortPartyPostfix)
            .Method(x => x.SetMoveGoToSettlement(default, default, default))
                .Postfix(SetMoveGoToSettlementPostfix);
    }

    private static void SetMoveGoToPointPostfix(
        MobileParty __instance,
        CampaignVec2 point,
        MobileParty.NavigationType navigationType)
    {
        SubModule.ControlAssumptionBehavior.EscortMainParty(__instance, point, navigationType);
    }

    private static void SetMoveEngagePartyPostfix(
        MobileParty __instance,
        MobileParty party)
    {
        SubModule.ControlAssumptionBehavior.AttackOrEscortParty(__instance, party);
    }

    private static void SetMoveEscortPartyPostfix(
        MobileParty __instance,
        MobileParty mobileParty)
    {
        SubModule.ControlAssumptionBehavior.AttackOrEscortParty(__instance, mobileParty);
    }

    private static void SetMoveGoToSettlementPostfix(
        MobileParty __instance,
        Settlement settlement)
    {
        SubModule.ControlAssumptionBehavior.TargetSettlement(__instance, settlement);
    }
}