using Bannerlord.PartyAI.Parties;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.GameModels;

/// <summary>Keeps parties that may not join armies (or are busy defending) out of AI call-to-arms lists.</summary>
internal sealed class ArmyManagementModel : ArmyManagementCalculationModel
{
    public override bool CanLordCreateArmy(MobileParty leaderParty, out MBList<MobileParty> possibleArmyMembers)
    {
        bool result = BaseModel.CanLordCreateArmy(leaderParty, out possibleArmyMembers);
        if (PartyAi.IsActive)
        {
            ArmyRules.RemoveForbiddenParties(leaderParty, possibleArmyMembers);
        }

        return result;
    }

    public override float AIMobilePartySizeRatioToCallToArmy => BaseModel.AIMobilePartySizeRatioToCallToArmy;
    public override float PlayerMobilePartySizeRatioToCallToArmy => BaseModel.PlayerMobilePartySizeRatioToCallToArmy;
    public override float MinimumNeededFoodInDaysToCallToArmy => BaseModel.MinimumNeededFoodInDaysToCallToArmy;
    public override float MaximumDistanceToCallToArmy => BaseModel.MaximumDistanceToCallToArmy;
    public override int InfluenceValuePerGold => BaseModel.InfluenceValuePerGold;
    public override int AverageCallToArmyCost => BaseModel.AverageCallToArmyCost;
    public override int CohesionThresholdForDispersion => BaseModel.CohesionThresholdForDispersion;
    public override float MaximumWaitTime => BaseModel.MaximumWaitTime;

    public override ExplainedNumber CalculateDailyCohesionChange(Army army, bool includeDescriptions = false)
        => BaseModel.CalculateDailyCohesionChange(army, includeDescriptions);

    public override int CalculateNewCohesion(Army army, PartyBase newParty, int calculatedCohesion, int sign)
        => BaseModel.CalculateNewCohesion(army, newParty, calculatedCohesion, sign);

    public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
        => BaseModel.CalculatePartyInfluenceCost(armyLeaderParty, party);

    public override int CalculateTotalInfluenceCost(Army army, float percentage)
        => BaseModel.CalculateTotalInfluenceCost(army, percentage);

    public override bool CanPlayerCreateArmy(out TextObject disabledReason)
        => BaseModel.CanPlayerCreateArmy(out disabledReason);

    public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation)
        => BaseModel.CheckPartyEligibility(party, out explanation);

    public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
        => BaseModel.DailyBeingAtArmyInfluenceAward(armyMemberParty);

    public override int GetCohesionBoostInfluenceCost(Army army, int percentageToBoost = 100)
        => BaseModel.GetCohesionBoostInfluenceCost(army, percentageToBoost);

    public override int GetPartyRelation(Hero hero) => BaseModel.GetPartyRelation(hero);

    public override float GetPartySizeScore(MobileParty party) => BaseModel.GetPartySizeScore(party);
}
