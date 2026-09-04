using Bannerlord.PartyAI.Parties;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PartyAI.Patches;

/// <summary>Zeroes raid and siege scores for parties that are not allowed to do either.</summary>
internal static class AiMilitaryBehaviorPatches
{
    public static void Apply(Harmony harmony)
        => harmony.Patch<AiMilitaryBehavior>()
            .Method("AiHourlyTick")
                .Postfix(AiHourlyTickPostfix);

    private static void AiHourlyTickPostfix(MobileParty mobileParty, PartyThinkParams p)
    {
        if (!PartyAi.IsActive || !PartyAi.Parties.IsHeroManageable(mobileParty.LeaderHero))
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(mobileParty.LeaderHero);
        if (profile.AllowRaidVillages && profile.AllowSieging)
        {
            return;
        }

        var forbidden = new List<AIBehaviorData>();
        foreach ((AIBehaviorData data, float _) in p.AIBehaviorScores)
        {
            if ((data.AiBehavior == AiBehavior.RaidSettlement && !profile.AllowRaidVillages)
                || (data.AiBehavior == AiBehavior.BesiegeSettlement && !profile.AllowSieging))
            {
                forbidden.Add(data);
            }
        }

        foreach (AIBehaviorData data in forbidden)
        {
            p.SetBehaviorScore(data, 0f);
        }
    }
}
