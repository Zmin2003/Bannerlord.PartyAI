using Bannerlord.PartyAI.Models;
using System;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dropdowns;

public sealed class AutoGovernorModeDropdownVM : ViewModel
{
    public sealed class GovernorModeItemVM : SelectorItemVM
    {
        public AutoGovernorMode Mode { get; }

        internal GovernorModeItemVM(TextObject name, AutoGovernorMode mode)
            : base(name)
        {
            Mode = mode;
        }
    }

    private SelectorVM<GovernorModeItemVM> _sortOptions = null!;
    private readonly Action<AutoGovernorMode>? _onChanged;

    public AutoGovernorModeDropdownVM(
        AutoGovernorMode selected,
        Action<AutoGovernorMode>? onChanged = null)
    {
        SortOptions = new SelectorVM<GovernorModeItemVM>(-1, OnSelected);
        SortOptions.AddItem(new GovernorModeItemVM(
            new TextObject("{=PAI_TOWN_GOVERNOR_OFF}Off"),
            AutoGovernorMode.Off));
        SortOptions.AddItem(new GovernorModeItemVM(
            new TextObject("{=PAI_TOWN_GOVERNOR_RECOMMEND}Recommend"),
            AutoGovernorMode.Recommend));
        SortOptions.AddItem(new GovernorModeItemVM(
            new TextObject("{=PAI_TOWN_GOVERNOR_ASSIGN}Assign Automatically"),
            AutoGovernorMode.Assign));
        SortOptions.SelectedIndex = Math.Max(0, Math.Min(SortOptions.ItemList.Count - 1, (int)selected));
        _onChanged = onChanged;
    }

    private void OnSelected(SelectorVM<GovernorModeItemVM> selector)
    {
        if (selector.SelectedItem is GovernorModeItemVM selected)
        {
            _onChanged?.Invoke(selected.Mode);
        }
    }

    [DataSourceProperty]
    public SelectorVM<GovernorModeItemVM> SortOptions
    {
        get => _sortOptions;
        private set
        {
            if (value != _sortOptions)
            {
                _sortOptions = value;
                OnPropertyChangedWithValue(value, nameof(SortOptions));
            }
        }
    }
}
