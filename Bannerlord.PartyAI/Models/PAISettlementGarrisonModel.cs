#if LOWER_THAN_1_5
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Models;

internal class PAISettlementGarrisonModel : SettlementGarrisonModel
{
    public override ExplainedNumber CalculateBaseGarrisonChange(Settlement settlement, bool includeDescriptions = false)
    {
        return BaseModel.CalculateBaseGarrisonChange(settlement, includeDescriptions);
    }

    public override int GetMaximumDailyAutoRecruitmentCount(Town town)
    {
        return BaseModel.GetMaximumDailyAutoRecruitmentCount(town);
    }

    public override float GetMaximumDailyRepairAmount(Settlement settlement)
    {
        return BaseModel.GetMaximumDailyRepairAmount(settlement);
    }

    public override int FindNumberOfTroopsToLeaveToGarrison(MobileParty mobileParty, Settlement settlement)
    {
        int result = BaseModel.FindNumberOfTroopsToLeaveToGarrison(mobileParty, settlement);

        if (!SubModule.PartySettingsManager.IsHeroManageable(mobileParty.LeaderHero))
        {
            return result;
        }

        PartyAiEntitySettings heroSettings = SubModule.PartySettingsManager.Settings(mobileParty.LeaderHero);

        if (!heroSettings.AllowDonateTroops)
        {
            result = 0;
        }

        return result;
    }

    public override int FindNumberOfTroopsToTakeFromGarrison(MobileParty mobileParty, Settlement settlement, float idealGarrisonStrengthPerWalledCenter = 0)
    {
        int result = BaseModel.FindNumberOfTroopsToTakeFromGarrison(mobileParty, settlement, idealGarrisonStrengthPerWalledCenter);

        if (!SubModule.PartySettingsManager.IsHeroManageable(mobileParty.LeaderHero))
        {
            return result;
        }

        PartyAiEntitySettings heroSettings = SubModule.PartySettingsManager.Settings(mobileParty.LeaderHero);

        if (!heroSettings.AllowTakeTroopsFromSettlement)
        {
            result = 0;
        }

        if (!heroSettings.AllowRecruitment)
        {
            result = 0;
        }

        return result;
    }
}
#endif