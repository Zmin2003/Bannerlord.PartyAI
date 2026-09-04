using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

/// <summary>
/// Observes the player's map moves. Held-key commands are forwarded to
/// <see cref="Parties.DirectCommandBehavior"/>; every move the autopilot did not issue itself
/// hands control of the main party back to the player.
/// </summary>
internal static class MobilePartyPatches
{
    public static void Apply(Harmony harmony)
        => harmony.Patch<MobileParty>()
            .Method(x => x.SetMoveGoToPoint(default, default))
                .Postfix(GoToPointPostfix)
            .Method(x => x.SetMoveEngageParty(default, default))
                .Postfix(EngagePartyPostfix)
            .Method(x => x.SetMoveEscortParty(default, default, default))
                .Postfix(EscortPartyPostfix)
            .Method(x => x.SetMoveGoToSettlement(default, default, default))
                .Postfix(GoToSettlementPostfix)
            .Method(x => x.SetMoveGoToInteractablePoint(default, default))
                .Postfix(InteractablePointPostfix)
            .Method(x => x.SetMoveModeHold())
                .Postfix(HoldPostfix);

    private static void NoteSteering(MobileParty party)
    {
        if (PartyAi.IsActive && party.IsMainParty)
        {
            PartyAi.Autopilot.OnPlayerSteered();
        }
    }

    private static void GoToPointPostfix(MobileParty __instance, CampaignVec2 point, MobileParty.NavigationType navigationType)
    {
        if (PartyAi.IsActive)
        {
            NoteSteering(__instance);
            PartyAi.DirectCommand.OnMainPartyMovesToPoint(__instance);
        }
    }

    private static void EngagePartyPostfix(MobileParty __instance, MobileParty party)
    {
        if (PartyAi.IsActive && party is not null)
        {
            NoteSteering(__instance);
            PartyAi.DirectCommand.OnMainPartyTargetsParty(__instance, party);
        }
    }

    private static void EscortPartyPostfix(MobileParty __instance, MobileParty mobileParty)
    {
        if (PartyAi.IsActive && mobileParty is not null)
        {
            NoteSteering(__instance);
            PartyAi.DirectCommand.OnMainPartyTargetsParty(__instance, mobileParty);
        }
    }

    private static void GoToSettlementPostfix(MobileParty __instance, Settlement settlement)
    {
        if (PartyAi.IsActive && settlement is not null)
        {
            NoteSteering(__instance);
            PartyAi.DirectCommand.OnMainPartyTargetsSettlement(__instance, settlement);
        }
    }

    private static void InteractablePointPostfix(MobileParty __instance, IInteractablePoint point)
    {
        if (PartyAi.IsActive)
        {
            NoteSteering(__instance);
        }
    }

    private static void HoldPostfix(MobileParty __instance)
    {
        if (PartyAi.IsActive)
        {
            NoteSteering(__instance);
        }
    }
}
