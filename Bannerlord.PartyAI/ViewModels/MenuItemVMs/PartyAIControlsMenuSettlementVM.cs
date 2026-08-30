using Bannerlord.PartyAI.Models;
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
    [DataSourceProperty] public override string ActiveOrder => "";
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
}
