using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using HarmonyLib;
using HarmonyLib.BUTR.Extensions;
using HarmonyLib.PatchBuilder;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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
        if (SubModule.PartySettingsManager.AllowTroopConversion && heroSettings.PartyTemplate != null)
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

        PartyComposition partyComposition = Recruitment.GetPartyComposition(mobileParty.Party, settings);
        var eligibleVolunteers = Recruitment.CollectEligibleVolunteers(mobileParty, settlement, settings, partyComposition);

        var howMany = eligibleVolunteers.Count == 1
            ? 1 // RandomInt(0, 1) seems to be biased towards 0, so let's just force it
            : MBRandom.RandomInt(0, eligibleVolunteers.Count);
        var randomVolunteers = GetRandomElements(eligibleVolunteers, howMany);

        foreach (var volunteer in randomVolunteers)
        {
            var troop = volunteer.Troop;
            var notable = volunteer.Notable;
            var troopIndex = volunteer.Index;
            GetRecruitVolunteerFromIndividualMethod.Invoke(__instance, [mobileParty, troop, notable, troopIndex]);
        }

        return false;
    }

    private static List<T> GetRandomElements<T>(List<T> source, int count)
    {
        List<T> pool = new List<T>(source);
        List<T> result = new List<T>();

        count = Math.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            T element = pool.GetRandomElement();
            result.Add(element);
            pool.Remove(element);
        }

        return result;
    }

}
