using Bannerlord.PartyAI.Models;
using System;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dropdowns;

public sealed class TownManagementStrategyDropdownVM : ViewModel
{
    public sealed class StrategyItemVM : SelectorItemVM
    {
        public TownManagementStrategy Strategy { get; }

        internal StrategyItemVM(TextObject name, TownManagementStrategy strategy)
            : base(name)
        {
            Strategy = strategy;
        }
    }

    private SelectorVM<StrategyItemVM> _sortOptions = null!;
    private readonly Action<TownManagementStrategy>? _onChanged;

    public TownManagementStrategyDropdownVM(
        TownManagementStrategy selected,
        Action<TownManagementStrategy>? onChanged = null)
    {
        SortOptions = new SelectorVM<StrategyItemVM>(-1, OnSelected);
        SortOptions.AddItem(new StrategyItemVM(
            new TextObject("{=PAI_TOWN_STRATEGY_BALANCED}Balanced"),
            TownManagementStrategy.Balanced));
        SortOptions.AddItem(new StrategyItemVM(
            new TextObject("{=PAI_TOWN_STRATEGY_STABILITY}Stability"),
            TownManagementStrategy.Stability));
        SortOptions.AddItem(new StrategyItemVM(
            new TextObject("{=PAI_TOWN_STRATEGY_ECONOMY}Economy"),
            TownManagementStrategy.Economy));
        SortOptions.AddItem(new StrategyItemVM(
            new TextObject("{=PAI_TOWN_STRATEGY_MILITARY}Military"),
            TownManagementStrategy.Military));
        SortOptions.SelectedIndex = Math.Max(0, Math.Min(SortOptions.ItemList.Count - 1, (int)selected));
        _onChanged = onChanged;
    }

    private void OnSelected(SelectorVM<StrategyItemVM> selector)
    {
        if (selector.SelectedItem is StrategyItemVM selected)
        {
            _onChanged?.Invoke(selected.Strategy);
        }
    }

    [DataSourceProperty]
    public SelectorVM<StrategyItemVM> SortOptions
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
