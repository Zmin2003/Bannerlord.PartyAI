using Bannerlord.PartyAI.Core;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries;

namespace Bannerlord.PartyAI.UI.Mixins;

/// <summary>Adds a "Select All" button to the game's multi-selection popup when it allows selecting everything.</summary>
[ViewModelMixin(nameof(MultiSelectionQueryPopUpVM.SetData))]
internal sealed class MultiSelectionQueryPopupVMMixin : BaseViewModelMixin<MultiSelectionQueryPopUpVM>
{
    private readonly MultiSelectionQueryPopUpVM _vm;
    private bool _isSelectAllVisible;

    public MultiSelectionQueryPopupVMMixin(MultiSelectionQueryPopUpVM vm) : base(vm)
    {
        _vm = vm;
        OnRefresh();
    }

    [DataSourceProperty] public string SelectAllText => L.S("{=PAIxKOXkgPU}Select All");

    [DataSourceProperty]
    public bool IsSelectAllVisible
    {
        get => _isSelectAllVisible;
        set
        {
            if (value != _isSelectAllVisible)
            {
                _isSelectAllVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsSelectAllVisible));
            }
        }
    }

    [DataSourceMethod]
    public void SelectAll()
    {
        foreach (InquiryElementVM element in _vm.InquiryElements)
        {
            if (element.IsEnabled)
            {
                element.IsSelected = true;
                element.RefreshValues();
            }
        }
    }

    public override void OnRefresh()
    {
        base.OnRefresh();
        IsSelectAllVisible = _vm.InquiryElements is not null
            && _vm.InquiryElements.Count > 1
            && _vm.InquiryElements.Count <= _vm.MaxSelectableOptionCount
            && _vm.MaxSelectableOptionCount - _vm.MinSelectableOptionCount > 1;
    }
}

internal static class MultiSelectionQueryPopupPrefabExtension
{
    [PrefabExtension("MultiSelectionQueryPopup", "descendant::ListPanel[@Id='MultiSelectionContentList']/Children")]
    internal sealed class SelectAllButton : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;
        public override int Index => 2;

        [PrefabExtensionFileName]
        public string PatchFileName => "MultiSelectionQueryPopupInject";
    }
}
