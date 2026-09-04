using System;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.GameModels;

/// <summary>Makes AI parties stock more food so long patrol and defense orders do not starve them.</summary>
internal sealed class FoodBuyingModel : PartyFoodBuyingModel
{
    public override float MinimumDaysFoodToLastWhileBuyingFoodFromTown
        => Math.Max(40f, BaseModel.MinimumDaysFoodToLastWhileBuyingFoodFromTown);

    public override float MinimumDaysFoodToLastWhileBuyingFoodFromVillage
        => Math.Max(15f, BaseModel.MinimumDaysFoodToLastWhileBuyingFoodFromVillage);

    public override float LowCostFoodPriceAverage => BaseModel.LowCostFoodPriceAverage;

    public override void FindItemToBuy(MobileParty mobileParty, Settlement settlement, out ItemRosterElement itemRosterElement, out float itemElementsPrice)
        => BaseModel.FindItemToBuy(mobileParty, settlement, out itemRosterElement, out itemElementsPrice);
}
