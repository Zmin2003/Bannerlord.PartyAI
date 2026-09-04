using Bannerlord.PartyAI.Parties;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

/// <summary>Restricts a managed caravan's trade destinations to its filtered town list.</summary>
internal static class CaravansCampaignBehaviorPatches
{
    public static void Apply(Harmony harmony)
        => harmony.Patch<CaravansCampaignBehavior>()
            .Method("GetTradeScoreForTown")
                .Postfix(GetTradeScoreForTownPostfix);

    private static void GetTradeScoreForTownPostfix(ref float __result, MobileParty caravanParty, Town town)
    {
        if (caravanParty?.LeaderHero is not Hero leader
            || town?.Settlement is not Settlement settlement
            || !PartyAi.IsActive
            || !PartyAi.Parties.IsCaravanManageable(leader))
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(leader);
        if (!profile.FilterSettlements || profile.FilteredSettlements.Count < 2)
        {
            return;
        }

        if (!profile.FilteredSettlements.Contains(settlement))
        {
            __result = -1f;
        }
    }
}
