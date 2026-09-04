using Bannerlord.PartyAI.Parties;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

/// <summary>Replaces vanilla horse buying with the profile-driven <see cref="HorseTrading"/> for managed parties.</summary>
internal static class PartiesBuyHorsePatches
{
    public static void Apply(Harmony harmony)
        => harmony.Patch<PartiesBuyHorseCampaignBehavior>()
            .Method(x => x.OnSettlementEntered(default, default, default))
                .Prefix(OnSettlementEnteredPrefix);

    private static bool OnSettlementEnteredPrefix(MobileParty mobileParty, Settlement settlement)
        => !(PartyAi.IsActive && HorseTrading.TryTrade(mobileParty, settlement));
}
