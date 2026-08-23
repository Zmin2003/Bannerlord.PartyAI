#if !LOWER_THAN_1_5
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PartyAI.Patches;

public class GarrisonTroopsCampaginBehaviorPatches
{
    private static FieldInfo _partyGarrisonTransferDataArgsMobilePartyField = null!;

    public static void Apply(Harmony harmony)
    {
        var partyGarrisonTransferDataArgsType = typeof(GarrisonTroopsCampaignBehavior)
            .GetNestedType("PartyGarrisonTransferDataArgs", BindingFlags.NonPublic);
        var armyGarrisonTransferDataArgsType = typeof(GarrisonTroopsCampaignBehavior)
            .GetNestedType("ArmyGarrisonTransferDataArgs", BindingFlags.NonPublic);

        _partyGarrisonTransferDataArgsMobilePartyField = partyGarrisonTransferDataArgsType.Field("MobileParty");

        var leaveTroopsPartyMethod = AccessTools.Method(
            partyGarrisonTransferDataArgsType,
            "GetNumberOfTroopsToLeaveForParty");
        var leaveTroopsPartyPostfix = AccessTools.Method(
            typeof(GarrisonTroopsCampaginBehaviorPatches),
            nameof(GetNumberOfTroopsToLeaveForPartyPostfix));

        harmony.Patch(leaveTroopsPartyMethod, postfix: leaveTroopsPartyPostfix);

        var takeTroopsPartyMethod = AccessTools.Method(
            partyGarrisonTransferDataArgsType,
            "GetNumberOfTroopsToTakeForParty");
        var takeTroopsPartyPostfix = AccessTools.Method(
            typeof(GarrisonTroopsCampaginBehaviorPatches),
            nameof(GetNumberOfTroopsToTakeForPartyPostfix));

        harmony.Patch(takeTroopsPartyMethod, postfix: takeTroopsPartyPostfix);

        var leaveTroopsArmyMethod = AccessTools.Method(
            armyGarrisonTransferDataArgsType,
            "GetTroopsToLeaveDataForArmy");
        var leaveTroopsArmyPostfix = AccessTools.Method(
            typeof(GarrisonTroopsCampaginBehaviorPatches),
            nameof(GetTroopsToLeaveDataForArmyPostfix));

        harmony.Patch(leaveTroopsArmyMethod, postfix: leaveTroopsArmyPostfix);

        var takeTroopsArmyMethod = AccessTools.Method(
            armyGarrisonTransferDataArgsType,
            "GetTroopsToTakeDataForArmy");
        var takeTroopsArmyPostfix = AccessTools.Method(
            typeof(GarrisonTroopsCampaginBehaviorPatches),
            nameof(GetTroopsToTakeDataForArmyPostfix));

        harmony.Patch(takeTroopsArmyMethod, postfix: takeTroopsArmyPostfix);
    }

    private static void GetNumberOfTroopsToLeaveForPartyPostfix(ref object __instance, ref int __result)
    {
        var mobileParty = (MobileParty)_partyGarrisonTransferDataArgsMobilePartyField
            .GetValue(__instance);

        if (!CanDonateTroops(mobileParty))
        {
            __result = 0;
        }
    }

    private static void GetNumberOfTroopsToTakeForPartyPostfix(ref object __instance, ref int __result)
    {
        var mobileParty = (MobileParty)_partyGarrisonTransferDataArgsMobilePartyField
            .GetValue(__instance);

        if (!CanTakeTroops(mobileParty))
        {
            __result = 0;
        }
    }

    private static void GetTroopsToLeaveDataForArmyPostfix(ref List<(MobileParty Party, int)> __result)
    {
        __result = [.. __result.Where(tuple => CanDonateTroops(tuple.Party))];
    }

    private static void GetTroopsToTakeDataForArmyPostfix(ref List<(MobileParty Party, int)> __result)
    {
        __result = [.. __result.Where(tuple => CanTakeTroops(tuple.Party))];
    }

    private static bool CanDonateTroops(MobileParty party)
    {
        if (!SubModule.PartySettingsManager.IsHeroManageable(party.LeaderHero))
        {
            return true;
        }

        var heroSettings = SubModule.PartySettingsManager.Settings(party.LeaderHero);
        return heroSettings.AllowDonateTroops;
    }

    private static bool CanTakeTroops(MobileParty party)
    {
        if (!SubModule.PartySettingsManager.IsHeroManageable(party.LeaderHero))
        {
            return true;
        }

        var heroSettings = SubModule.PartySettingsManager.Settings(party.LeaderHero);
        return heroSettings.AllowTakeTroopsFromSettlement
            && heroSettings.AllowRecruitment;
    }
}
#endif