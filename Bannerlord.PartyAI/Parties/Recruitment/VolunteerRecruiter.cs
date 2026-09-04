using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Parties.Recruitment;

/// <summary>
/// Recruits volunteers for a managed party using the vanilla recruitment behavior's private
/// per-notable recruit method, so all vanilla side effects (relation, gold, events) apply.
/// </summary>
internal static class VolunteerRecruiter
{
    private static readonly MethodInfo RecruitFromIndividual = AccessTools.Method(
        typeof(RecruitmentCampaignBehavior),
        "GetRecruitVolunteerFromIndividual")
        ?? throw new MissingMethodException("RecruitmentCampaignBehavior.GetRecruitVolunteerFromIndividual is missing.");

    /// <summary>Recruits every affordable, composition-improving volunteer in priority order. Returns troops recruited.</summary>
    public static int Recruit(MobileParty party, Settlement settlement, PartyProfile profile)
    {
        RecruitmentCampaignBehavior? behavior = Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>();
        if (behavior is null || party?.Party is null || settlement is null || !profile.AllowRecruitment)
        {
            return 0;
        }

        int freeSlots = party.Party.PartySizeLimit - party.Party.NumberOfAllMembers;
        if (freeSlots <= 0)
        {
            return 0;
        }

        PartyComposition composition = RecruitmentRules.GetPartyComposition(party.Party, profile);
        List<NotableVolunteer> volunteers = RecruitmentRules
            .CollectEligibleVolunteers(party, settlement, profile, composition)
            .OrderByDescending(volunteer => RecruitmentRules.RecruitmentPriority(composition, profile, volunteer.Troop))
            .ToList();

        int recruited = 0;
        foreach (NotableVolunteer volunteer in volunteers)
        {
            if (recruited >= freeSlots || party.Party.NumberOfAllMembers >= party.Party.PartySizeLimit)
            {
                break;
            }

            // The slot may have been taken by an earlier recruit from the same notable.
            if (volunteer.Index < 0
                || volunteer.Index >= volunteer.Notable.VolunteerTypes.Length
                || volunteer.Notable.VolunteerTypes[volunteer.Index] != volunteer.Troop)
            {
                continue;
            }

            PartyComposition now = RecruitmentRules.GetPartyComposition(party.Party, profile);
            if (!RecruitmentRules.CanAffordVolunteer(party, volunteer.Troop)
                || !RecruitmentRules.ShouldRecruit(now, profile, volunteer.Troop, party.Party, allowConversionFallback: true))
            {
                continue;
            }

            int before = party.Party.NumberOfAllMembers;
            RecruitFromIndividual.Invoke(behavior, [party, volunteer.Troop, volunteer.Notable, volunteer.Index]);
            recruited += Math.Max(0, party.Party.NumberOfAllMembers - before);
        }

        return recruited;
    }
}
