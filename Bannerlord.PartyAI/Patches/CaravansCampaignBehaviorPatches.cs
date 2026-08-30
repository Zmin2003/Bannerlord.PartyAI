using Bannerlord.PartyAI.Models;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

internal class CaravansCampaignBehaviorPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch<CaravansCampaignBehavior>()
            .Method("GetTradeScoreForTown")
                .Postfix(GetTradeScoreForTownPostfix);
    }

    private static void GetTradeScoreForTownPostfix(ref float __result, MobileParty caravanParty, Town town, CampaignTime lastHomeVisitTimeOfCaravan, float caravanFullness, bool distanceCut)
    {
        if (caravanParty?.LeaderHero is not Hero leader
            || town?.Settlement is not Settlement settlement
            || !SubModule.PartySettingsManager.IsCaravanManageable(leader))
        {
            return;
        }

        PartyAiEntitySettings settings = SubModule.PartySettingsManager.Settings(leader);
        if (!settings.FilterSettlements || settings.FilteredSettlements?.Count < 2)
        {
            return;
        }

        if (!(settings.FilteredSettlements?.Contains(settlement) ?? false))
        {
            __result = -1f;
        }
    }
}
