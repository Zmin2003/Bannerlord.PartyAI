using HarmonyLib;
using HarmonyLib.PatchBuilder;
using Bannerlord.PartyAI.Domain.AutoDefense;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

internal static class LeaveTroopsToSettlementActionPatch
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch<GarrisonTroopsCampaignBehavior>()
            .Method("LeaveTroopsToGarrison")
                .Prefix(LeaveTroopsToGarrisonPrefix)
            .Method("TakeTroopsFromGarrison")
                .Prefix(TakeTroopsFromGarrisonPrefix);
    }

    // =========================
    // BLOCK DONATING TO GARRISON
    // =========================
    private static bool LeaveTroopsToGarrisonPrefix(
        MobileParty mobileParty,
        Settlement settlement,
        int numberOfTroopsToLeave,
        bool archersAreHighPriority)
    {
        if (GarrisonTransferService.IsAutomatedTransferInProgress)
        {
            return true;
        }

        if (GarrisonTransferService.ShouldSuppressVanillaDonation(
            mobileParty,
            settlement))
        {
            return false;
        }

        // No hero → vanilla
        if (mobileParty?.LeaderHero == null)
            return true;

        var settings = SubModule.PartySettingsManager.Settings(mobileParty.LeaderHero);
        if (settings == null)
            return true;

        // If player has disabled donating troops for this hero, skip the method entirely
        return settings.AllowDonateTroops;
    }

    // =========================
    // BLOCK TAKING FROM GARRISON
    // =========================
    private static bool TakeTroopsFromGarrisonPrefix(
        MobileParty mobileParty,
        Settlement settlement,
        int numberOfTroopsToTake,
        bool archersAreHighPriority)
    {
        // No hero → vanilla
        if (mobileParty?.LeaderHero == null)
            return true;

        var settings = SubModule.PartySettingsManager.Settings(mobileParty.LeaderHero);
        if (settings == null)
            return true;

        // If player has disabled taking troops for this hero, skip the method
        return settings.AllowTakeTroopsFromSettlement;
    }
}
