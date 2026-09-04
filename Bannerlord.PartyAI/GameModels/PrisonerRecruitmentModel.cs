using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Recruitment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PartyAI.GameModels;

/// <summary>Managed parties only recruit prisoners that fit their template and composition.</summary>
internal sealed class PrisonerRecruitmentModel : PrisonerRecruitmentCalculationModel
{
    public override int CalculateRecruitableNumber(PartyBase party, CharacterObject character)
        => BaseModel.CalculateRecruitableNumber(party, character);

    public override ExplainedNumber GetConformityChangePerHour(PartyBase party, CharacterObject character)
        => BaseModel.GetConformityChangePerHour(party, character);

    public override int GetConformityNeededToRecruitPrisoner(CharacterObject character)
        => BaseModel.GetConformityNeededToRecruitPrisoner(character);

    public override float GetPrisonerRecruitmentMoraleEffect(PartyBase party, CharacterObject character, int num)
        => BaseModel.GetPrisonerRecruitmentMoraleEffect(party, character, num);

    public override bool ShouldPartyRecruitPrisoners(PartyBase party)
        => BaseModel.ShouldPartyRecruitPrisoners(party);

    public override bool IsPrisonerRecruitable(PartyBase party, CharacterObject character, out int conformityNeeded)
    {
        bool result = BaseModel.IsPrisonerRecruitable(party, character, out conformityNeeded);
        if (!result || !PartyAi.IsActive || !PartyAi.Parties.IsHeroManageable(party.LeaderHero))
        {
            return result;
        }

        PartyProfile profile = PartyAi.Parties.Profile(party.LeaderHero);
        if (!profile.AllowRecruitment)
        {
            return false;
        }

        if (PartyAi.Parties.AllowsConversion(profile))
        {
            return true;
        }

        PartyComposition composition = RecruitmentRules.GetPartyComposition(party, profile);
        return RecruitmentRules.ShouldRecruit(composition, profile, character, party);
    }
}
