using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Orders;
using Bannerlord.PartyAI.Parties.Recruitment;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior;

namespace Bannerlord.PartyAI.Patches;

/// <summary>
/// Filters vanilla AI recruitment through the party's template/composition rules and takes over
/// recruitment entirely while a party is executing a recruit order.
/// </summary>
internal static class RecruitmentCampaignBehaviorPatches
{
    public static void Apply(Harmony harmony)
        => harmony.Patch<RecruitmentCampaignBehavior>()
            .Method("ApplyInternal")
                .Prefix(ApplyInternalPrefix)
            .Method("RecruitVolunteersFromNotable")
                .Prefix(RecruitVolunteersFromNotablePrefix);

    private static bool ApplyInternalPrefix(MobileParty side1Party, Settlement settlement, Hero individual, CharacterObject troop, int number, int bitCode, RecruitingDetail detail)
    {
        if (!PartyAi.IsActive || !PartyAi.Parties.IsManageable(side1Party.LeaderHero))
        {
            return true;
        }

        PartyProfile profile = PartyAi.Parties.Profile(side1Party.LeaderHero);
        if (!profile.AllowRecruitment)
        {
            return false;
        }

        if (PartyAi.Parties.AllowsConversion(profile))
        {
            return true;
        }

        PartyComposition composition = RecruitmentRules.GetPartyComposition(side1Party.Party, profile);
        return RecruitmentRules.ShouldRecruit(composition, profile, troop, side1Party.Party);
    }

    private static bool RecruitVolunteersFromNotablePrefix(MobileParty mobileParty, Settlement settlement)
    {
        Hero? hero = mobileParty.LeaderHero;
        if (!PartyAi.IsActive || !PartyAi.Parties.IsManageable(hero))
        {
            return true;
        }

        PartyProfile profile = PartyAi.Parties.Profile(hero);
        if (profile.Order?.Behavior != PartyOrderType.RecruitFromTemplate
            || mobileParty.Party.NumberOfAllMembers >= mobileParty.Party.PartySizeLimit)
        {
            return true;
        }

        VolunteerRecruiter.Recruit(mobileParty, settlement, profile);
        return false;
    }
}
