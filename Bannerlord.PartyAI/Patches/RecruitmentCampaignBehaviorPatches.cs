using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using HarmonyLib;
using HarmonyLib.BUTR.Extensions;
using HarmonyLib.PatchBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior;

namespace Bannerlord.PartyAI.Patches;

internal class RecruitmentCampaignBehaviorPatches
{
    private static MethodInfo GetRecruitVolunteerFromIndividualMethod = default!;

    public static void Apply(Harmony harmony)
    {
        GetRecruitVolunteerFromIndividualMethod = AccessTools2.Method(
            typeof(RecruitmentCampaignBehavior),
            "GetRecruitVolunteerFromIndividual")
            ?? throw new Exception("GetRecruitVolunteerFromIndividual is missing from RecruitmentCampaignBehavior");

        harmony.Patch<RecruitmentCampaignBehavior>()
            .Method("ApplyInternal")
                .Prefix(ApplyInternalPrefix)
            .Method("RecruitVolunteersFromNotable")
                .Prefix(RecruitVolunteersFromNotablePrefix);
    }

    private static bool ApplyInternalPrefix(MobileParty side1Party, Settlement settlement, Hero individual, CharacterObject troop, int number, int bitCode, RecruitingDetail detail)
    {
        if (!SubModule.PartySettingsManager.IsManageable(side1Party.LeaderHero))
        {
            return true;
        }

        PartyAiEntitySettings heroSettings = SubModule.PartySettingsManager.Settings(side1Party.LeaderHero);

        if (!heroSettings.AllowRecruitment)
        {
            return false;
        }

        // if we're going to convert the troop anyway, it doesn't matter
        if ((SubModule.PartySettingsManager.AllowTroopConversion
                || heroSettings.SettlementAutomation == SettlementAutomationLevel.Full)
            && heroSettings.PartyTemplate != null)
        {
            return true;
        }

        PartyComposition comp = Recruitment.GetPartyComposition(side1Party.Party, heroSettings);
        if (!Recruitment.ShouldRecruit(comp, heroSettings, troop, side1Party.Party))
        {
            return false;
        }

        return true;
    }

    private static bool RecruitVolunteersFromNotablePrefix(
        RecruitmentCampaignBehavior __instance,
        MobileParty mobileParty,
        Settlement settlement)
    {
        var hero = mobileParty.LeaderHero;
        if (hero is null || !SubModule.PartySettingsManager.IsManageable(hero))
        {
            return true;
        }

        var settings = SubModule.PartySettingsManager.Settings(hero);
        if (settings.Order?.Behavior != PartyAiOrderType.RecruitFromTemplate)
        {
            return true;
        }

        var missingMembers = mobileParty.Party.PartySizeLimit - mobileParty.Party.NumberOfAllMembers;
        if (missingMembers <= 0)
        {
            return true;
        }

        RecruitEligibleVolunteers(__instance, mobileParty, settlement, settings);

        return false;
    }

    internal static int RecruitEligibleVolunteers(
        RecruitmentCampaignBehavior behavior,
        MobileParty mobileParty,
        Settlement settlement,
        PartyAiEntitySettings settings)
    {
        if (behavior is null
            || mobileParty?.Party is null
            || settlement is null
            || !settings.AllowRecruitment)
        {
            return 0;
        }

        int freeSlots = mobileParty.Party.PartySizeLimit - mobileParty.Party.NumberOfAllMembers;
        if (freeSlots <= 0)
        {
            return 0;
        }

        PartyComposition composition = Recruitment.GetPartyComposition(mobileParty.Party, settings);
        List<NotableVolunteer> volunteers = Recruitment
            .CollectEligibleVolunteers(mobileParty, settlement, settings, composition)
            .OrderByDescending(volunteer => Recruitment.GetRecruitmentPriority(composition, settings, volunteer.Troop))
            .ToList();

        int recruited = 0;
        foreach (NotableVolunteer volunteer in volunteers)
        {
            if (recruited >= freeSlots
                || mobileParty.Party.NumberOfAllMembers >= mobileParty.Party.PartySizeLimit)
            {
                break;
            }

            if (volunteer.Index < 0
                || volunteer.Index >= volunteer.Notable.VolunteerTypes.Length
                || volunteer.Notable.VolunteerTypes[volunteer.Index] != volunteer.Troop)
            {
                continue;
            }

            PartyComposition currentComposition = Recruitment.GetPartyComposition(
                mobileParty.Party,
                settings);
            if (!Recruitment.CanAffordVolunteer(mobileParty, volunteer.Troop)
                || !Recruitment.ShouldRecruit(
                    currentComposition,
                    settings,
                    volunteer.Troop,
                    mobileParty.Party,
                    allowConversionFallback: true))
            {
                continue;
            }

            int partySizeBefore = mobileParty.Party.NumberOfAllMembers;
            GetRecruitVolunteerFromIndividualMethod.Invoke(
                behavior,
                [mobileParty, volunteer.Troop, volunteer.Notable, volunteer.Index]);
            recruited += Math.Max(
                0,
                mobileParty.Party.NumberOfAllMembers - partySizeBefore);
        }

        return recruited;
    }

}
