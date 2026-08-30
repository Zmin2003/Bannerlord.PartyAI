using Bannerlord.PartyAI.ViewModels;
using Bannerlord.PartyAI.ViewModels.MenuOptionVMs;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries;

namespace Bannerlord.PartyAI.Mixins;

[ViewModelMixin(nameof(MultiSelectionQueryPopUpVM.SetData))]
internal class MultiSelectionQueryPopupVMMixin : BaseViewModelMixin<MultiSelectionQueryPopUpVM>
{
    private readonly MultiSelectionQueryPopUpVM _vm;
    private string _selectAllText = string.Empty;
    private bool _isSelectAllVisible;
    internal static bool AddClanBanners;

    public MultiSelectionQueryPopupVMMixin(MultiSelectionQueryPopUpVM vm) : base(vm)
    {
        _vm = vm;

        SelectAllText = new TextObject("{=PAIxKOXkgPU}Select All").ToString();

        OnRefresh();
    }

    [DataSourceProperty]
    public string SelectAllText
    {
        get
        {
            return _selectAllText;
        }
        set
        {
            if (value != _selectAllText)
            {
                _selectAllText = value;
                OnPropertyChangedWithValue(value, "SelectAllText");
            }
        }
    }

    [DataSourceProperty]
    public bool IsSelectAllVisible
    {
        get
        {
            return _isSelectAllVisible;
        }
        set
        {
            if (value != _isSelectAllVisible)
            {
                _isSelectAllVisible = value;
                OnPropertyChangedWithValue(value, "IsSelectAllVisible");
            }
        }
    }

    [DataSourceMethod]
    public void SelectAll()
    {
        foreach (InquiryElementVM e in _vm.InquiryElements)
        {
            if (e.IsEnabled)
            {
                e.IsSelected = true;
                e.RefreshValues();
            }
        }
    }

    public override void OnRefresh()
    {
        base.OnRefresh();

        var inquiryElements = _vm.InquiryElements;
        if (inquiryElements is null)
        {
            return;
        }

        if (PartyAIModOptionsVM.IsAutoCreatePartyLeaderRosterSelection && inquiryElements.FirstOrDefault()?.InquiryElement?.Identifier is Hero)
        {
            foreach (InquiryElementVM e in inquiryElements)
            {
                if (PartyAIModOptionsVM.ChosenPartyLeaders.Contains((Hero)e.InquiryElement.Identifier))
                {
                    e.IsSelected = true;
                    e.RefreshValues();
                }
            }
        }
        PartyAIModOptionsVM.IsAutoCreatePartyLeaderRosterSelection = false;

        if (PartyAICaravanOptionsVM.IsSelectFilteredSettlements && inquiryElements.FirstOrDefault()?.InquiryElement?.Identifier is Settlement)
        {
            foreach (InquiryElementVM e in inquiryElements)
            {
                if (e.InquiryElement?.Identifier is Settlement settlement
                    && PartyAICaravanOptionsVM.FilteredSettlements.Contains(settlement))
                {
                    e.IsSelected = true;
                    e.RefreshValues();
                }
            }
        }
        PartyAICaravanOptionsVM.IsSelectFilteredSettlements = false;

        AddClanBanners = false;

        IsSelectAllVisible = inquiryElements.Count <= _vm.MaxSelectableOptionCount && inquiryElements.Count > 1 && _vm.MaxSelectableOptionCount - _vm.MinSelectableOptionCount > 1;

        _vm.SearchText = string.Empty;
    }
}
