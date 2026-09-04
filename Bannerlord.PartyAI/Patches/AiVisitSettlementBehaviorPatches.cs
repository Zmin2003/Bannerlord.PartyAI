using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Recruitment;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Patches;

/// <summary>
/// Makes the vanilla "go recruit" AI count only volunteers the party actually wants, so managed
/// parties do not travel to settlements full of off-template troops.
/// </summary>
internal static class AiVisitSettlementBehaviorPatches
{
    public static void Apply(Harmony harmony)
        => harmony.Patch<AiVisitSettlementBehavior>()
            .Method("GetApproximateVolunteersCanBeRecruitedDataFromSettlement")
                .Postfix(VolunteerEstimatePostfix);

    private static void VolunteerEstimatePostfix(ref (int, float) __result, Hero hero, Settlement settlement)
    {
        if (!PartyAi.IsActive
            || !PartyAi.Parties.IsHeroManageable(hero)
            || hero.PartyBelongedTo is not MobileParty party
            || party.LeaderHero != hero)
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(hero);
        if (!profile.AllowRecruitment)
        {
            __result = (0, 0f);
            return;
        }

        // Conversion will fix whatever gets recruited, so the vanilla estimate is fine.
        if (PartyAi.Parties.AllowsConversion(profile))
        {
            return;
        }

        PartyComposition composition = RecruitmentRules.GetPartyComposition(party.Party, profile);
        int count = 0;
        int totalWage = 0;

        foreach (Hero notable in settlement.Notables)
        {
            if (!notable.IsAlive)
            {
                continue;
            }

            int maxIndex = Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(
                party.IsGarrison ? party.Party.Owner : party.LeaderHero,
                notable);

            for (int index = 0; index <= maxIndex && index < notable.VolunteerTypes.Length; index++)
            {
                CharacterObject? troop = notable.VolunteerTypes[index];
                if (troop is not null && RecruitmentRules.ShouldRecruit(composition, profile, troop, party.Party))
                {
                    count++;
                    totalWage += Campaign.Current.Models.PartyWageModel.GetCharacterWage(troop);
                }
            }
        }

        __result = count > 0 ? (count, totalWage / (float)count) : (0, 0f);
    }
}
