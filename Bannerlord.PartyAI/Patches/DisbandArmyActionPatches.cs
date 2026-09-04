using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Bannerlord.PartyAI.Patches;

internal static class DisbandArmyActionPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch()
            .Method(() => DisbandArmyAction.ApplyByUnknownReason(null))
                .Prefix(ApplyByUnknownReasonPrefix);
    }

    private static bool ApplyByUnknownReasonPrefix(Army army)
    {
        // this prevents disbanding for not having enough AI objectives which can be caused by the orders
        if (PartyAi.IsActive && PartyAi.Parties.HasActiveOrder(army?.LeaderParty?.LeaderHero))
        {
            return false;
        }

        return true;
    }
}
