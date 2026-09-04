using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Towns;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

/// <summary>
/// Enforces the per-party "may donate to / take from garrisons" permissions in every vanilla
/// garrison transfer path, and yields to the mod's own scheduled donations.
/// </summary>
internal static class GarrisonTroopsPatches
{
    private static FieldInfo _partyArgsMobilePartyField = null!;

    public static void Apply(Harmony harmony)
    {
        harmony.Patch<GarrisonTroopsCampaignBehavior>()
            .Method("LeaveTroopsToGarrison")
                .Prefix(LeaveTroopsToGarrisonPrefix)
            .Method("TakeTroopsFromGarrison")
                .Prefix(TakeTroopsFromGarrisonPrefix);

        var partyArgs = typeof(GarrisonTroopsCampaignBehavior).GetNestedType("PartyGarrisonTransferDataArgs", BindingFlags.NonPublic);
        var armyArgs = typeof(GarrisonTroopsCampaignBehavior).GetNestedType("ArmyGarrisonTransferDataArgs", BindingFlags.NonPublic);
        _partyArgsMobilePartyField = partyArgs.Field("MobileParty");

        harmony.Patch(
            AccessTools.Method(partyArgs, "GetNumberOfTroopsToLeaveForParty"),
            postfix: new HarmonyMethod(typeof(GarrisonTroopsPatches), nameof(TroopsToLeaveForPartyPostfix)));
        harmony.Patch(
            AccessTools.Method(partyArgs, "GetNumberOfTroopsToTakeForParty"),
            postfix: new HarmonyMethod(typeof(GarrisonTroopsPatches), nameof(TroopsToTakeForPartyPostfix)));
        harmony.Patch(
            AccessTools.Method(armyArgs, "GetTroopsToLeaveDataForArmy"),
            postfix: new HarmonyMethod(typeof(GarrisonTroopsPatches), nameof(TroopsToLeaveForArmyPostfix)));
        harmony.Patch(
            AccessTools.Method(armyArgs, "GetTroopsToTakeDataForArmy"),
            postfix: new HarmonyMethod(typeof(GarrisonTroopsPatches), nameof(TroopsToTakeForArmyPostfix)));
    }

    private static bool LeaveTroopsToGarrisonPrefix(MobileParty mobileParty, Settlement settlement)
    {
        if (GarrisonTransfer.IsAutomatedTransferInProgress)
        {
            return true;
        }

        if (GarrisonTransfer.ShouldSuppressVanillaDonation(mobileParty, settlement))
        {
            return false;
        }

        return CanDonate(mobileParty);
    }

    private static bool TakeTroopsFromGarrisonPrefix(MobileParty mobileParty) => CanTake(mobileParty);

    private static void TroopsToLeaveForPartyPostfix(object __instance, ref int __result)
    {
        if (!CanDonate((MobileParty)_partyArgsMobilePartyField.GetValue(__instance)))
        {
            __result = 0;
        }
    }

    private static void TroopsToTakeForPartyPostfix(object __instance, ref int __result)
    {
        if (!CanTake((MobileParty)_partyArgsMobilePartyField.GetValue(__instance)))
        {
            __result = 0;
        }
    }

    private static void TroopsToLeaveForArmyPostfix(ref List<(MobileParty Party, int)> __result)
        => __result = __result.Where(pair => CanDonate(pair.Party)).ToList();

    private static void TroopsToTakeForArmyPostfix(ref List<(MobileParty Party, int)> __result)
        => __result = __result.Where(pair => CanTake(pair.Party)).ToList();

    private static bool CanDonate(MobileParty? party)
        => !TryGetProfile(party, out PartyProfile? profile) || profile.AllowDonateTroops;

    private static bool CanTake(MobileParty? party)
        => !TryGetProfile(party, out PartyProfile? profile) || (profile.AllowTakeTroopsFromSettlement && profile.AllowRecruitment);

    private static bool TryGetProfile(MobileParty? party, out PartyProfile profile)
    {
        profile = null!;
        if (!PartyAi.IsActive || !PartyAi.Parties.IsHeroManageable(party?.LeaderHero))
        {
            return false;
        }

        profile = PartyAi.Parties.Profile(party!.LeaderHero);
        return true;
    }
}
