using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.CampaignBehaviors;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View;

namespace Bannerlord.PartyAI.ViewModels.MenuItemVMs;

public class PartyAIControlsMenuSettlementVM : PartyAIControlsMenuPartyVM
{
    private bool _isInspected = false;

    public PartyAIControlsMenuSettlementVM(Settlement settlement, PartyAIControlsMenuVM menu) : base(settlement.OwnerClan.Leader, menu)
    {
        Settlement = settlement;
        Party = settlement.Town?.GarrisonParty?.Party;
        AllowEditComposition = Party is not null;
        AllowEditTemplate = Party is not null;
        CopyPasteToggle.IsDisabled = Party is null;
        RefreshValues();
    }

    internal override PartyAiEntitySettings Settings => SubModule.PartySettingsManager.Settings(Settlement!);

    [DataSourceProperty] public override string LeaderName => Settlement?.Name?.ToString() ?? Party?.Name?.ToString() ?? string.Empty;
    [DataSourceProperty] public override bool CanShowLocationOfHero => true;
    [DataSourceProperty] public override bool IsSettlement => true;
    [DataSourceProperty] public override bool IsLordParty => false;
    [DataSourceProperty] public override bool ShowPortrait => false;
    [DataSourceProperty] public int WallsLevel => Settlement?.Town?.GetWallLevel() ?? 1;
    [DataSourceProperty]
    public override string ActiveOrder
    {
        get
        {
            if (Settlement?.Town is not Town town)
            {
                return string.Empty;
            }

            if (!SubModule.TownManagementBehavior.IsTownManageable(Settlement))
            {
                return new TextObject(
                    "{=PAI_TOWN_GARRISON_ONLY}Garrison management only")
                    .ToString();
            }

            TownManagementSettlementSettings settings = SubModule.TownManagementBehavior.SettingsSnapshot(Settlement);
            string management = SubModule.TownManagementBehavior.Options.Enabled && settings.Enabled
                ? StrategyName(settings.Strategy)
                : new TextObject("{=PAI_TOWN_STATUS_DISABLED}Town AI off").ToString();
            string governor = town.Governor?.Name?.ToString()
                ?? new TextObject("{=PAI_TOWN_NO_GOVERNOR}No governor").ToString();

            return new TextObject("{=PAI_TOWN_ROW_SUMMARY}{MANAGEMENT} | {GOVERNOR} | Prosperity {PROSPERITY}")
                .SetTextVariable("MANAGEMENT", management)
                .SetTextVariable("GOVERNOR", governor)
                .SetTextVariable("PROSPERITY", (int)town.Prosperity)
                .ToString();
        }
    }

    [DataSourceProperty]
    public override bool StatusNeedsAttention
    {
        get
        {
            if (Settlement?.Town is not Town town
                || !SubModule.TownManagementBehavior.IsTownManageable(Settlement))
            {
                return false;
            }

            TownManagementSettlementSettings settings = SubModule.TownManagementBehavior.SettingsSnapshot(Settlement);
            return town.Governor is null
                || town.Loyalty <= settings.LoyaltyEmergencyThreshold
                || TownManagementBehavior.IsFoodEmergency(town, settings.FoodShortageDays);
        }
    }

    [DataSourceProperty]
    public override HintViewModel StatusHint
    {
        get
        {
            if (Settlement?.Town is not Town town)
            {
                return new HintViewModel();
            }

            if (!SubModule.TownManagementBehavior.IsTownManageable(Settlement))
            {
                return new HintViewModel(new TextObject(
                    "{=PAI_TOWN_GARRISON_ONLY_HINT}Only garrison troops are managed here because this fief does not belong to the player clan."));
            }

            TownManagementSettlementSettings settings = SubModule.TownManagementBehavior.SettingsSnapshot(Settlement);
            string management = SubModule.TownManagementBehavior.Options.Enabled && settings.Enabled
                ? StrategyName(settings.Strategy)
                : new TextObject("{=PAI_TOWN_STATUS_DISABLED}Town AI off").ToString();
            string governor = town.Governor?.Name?.ToString()
                ?? new TextObject("{=PAI_TOWN_NO_GOVERNOR}No governor").ToString();
            string food = town.FoodChange < 0f
                ? new TextObject("{=PAI_TOWN_FOOD_FALLING}{FOOD} ({CHANGE}/day)")
                    .SetTextVariable("FOOD", (int)town.FoodStocks)
                    .SetTextVariable("CHANGE", town.FoodChange.ToString("0.0"))
                    .ToString()
                : ((int)town.FoodStocks).ToString();

            TextObject hint = new("{=PAI_TOWN_ROW_HINT}{SETTLEMENT}\nManagement: {MANAGEMENT}\nGovernor: {GOVERNOR}\nProsperity: {PROSPERITY}\nLoyalty: {LOYALTY}\nFood: {FOOD}\nGarrison wage: {WAGE}");
            hint.SetTextVariable("SETTLEMENT", Settlement.Name);
            hint.SetTextVariable("MANAGEMENT", management);
            hint.SetTextVariable("GOVERNOR", governor);
            hint.SetTextVariable("PROSPERITY", (int)town.Prosperity);
            hint.SetTextVariable("LOYALTY", town.Loyalty.ToString("0.0"));
            hint.SetTextVariable("FOOD", food);
            hint.SetTextVariable("WAGE", town.GarrisonParty?.TotalWage ?? 0);
            return new HintViewModel(hint);
        }
    }
    [DataSourceProperty] public override string PartySize => Party is null
        ? new TextObject("{=PAI_TOWN_NO_GARRISON}No Garrison").ToString()
        : base.PartySize;
    [DataSourceProperty] public BasicTooltipViewModel WallsHint => new(() => CampaignUIHelper.GetTownWallsTooltip(Settlement!.Town!));

    public override void EditPartyOptions()
    {
        if (Settlement is not null && SubModule.TownManagementBehavior.IsTownManageable(Settlement))
        {
            SubModule.InformationManager.ShowTownOptionsInquiry(Settlement, Settings, RefreshValues);
        }
        else
        {
            SubModule.InformationManager.ShowGarrisonOptionsInquiry(Settings, RefreshValues);
        }
    }

    public override void ShowHeroOnMap()
    {
        Game.Current.GameStateManager.PopState();
        UISoundsHelper.PlayUISound("event:/ui/default");
        MapScreen.Instance.FastMoveCameraToPosition(Settlement!.Position);
    }

    public override void OpenEncyclopediaLink()
    {
        if (Settlement != null && Campaign.Current.EncyclopediaManager.GetPageOf(typeof(Settlement)).IsValidEncyclopediaItem(Settlement))
        {
            Campaign.Current.EncyclopediaManager.GoToLink(Settlement.EncyclopediaLink);
        }
    }

    public override void PartySizeBeginHint()
    {
        if (Settlement != null)
        {
            _isInspected = Settlement.IsInspected;
            Settlement.IsInspected = true;
            InformationManager.ShowTooltip(typeof(Settlement), Settlement, true);
        }
    }

    public override void PartySizeEndHint()
    {
        if (Settlement != null)
        {
            InformationManager.HideTooltip();
            Settlement.IsInspected = _isInspected;
        }
    }

    private static string StrategyName(TownManagementStrategy strategy)
    {
        return strategy switch
        {
            TownManagementStrategy.Stability => new TextObject("{=PAI_TOWN_STRATEGY_STABILITY}Stability").ToString(),
            TownManagementStrategy.Economy => new TextObject("{=PAI_TOWN_STRATEGY_ECONOMY}Economy").ToString(),
            TownManagementStrategy.Military => new TextObject("{=PAI_TOWN_STRATEGY_MILITARY}Military").ToString(),
            _ => new TextObject("{=PAI_TOWN_STRATEGY_BALANCED}Balanced").ToString()
        };
    }
}
