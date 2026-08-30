using Bannerlord.PartyAI.Models;
using System;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dropdowns;

public sealed class TownDefensePriorityDropdownVM : ViewModel
{
    public sealed class DefensePriorityItemVM : SelectorItemVM
    {
        public TownDefensePriority Priority { get; }

        internal DefensePriorityItemVM(TextObject name, TownDefensePriority priority)
            : base(name)
        {
            Priority = priority;
        }
    }

    private SelectorVM<DefensePriorityItemVM> _sortOptions = null!;

    public TownDefensePriorityDropdownVM(TownDefensePriority selected)
    {
        SortOptions = new SelectorVM<DefensePriorityItemVM>(-1, static _ => { });
        SortOptions.AddItem(new DefensePriorityItemVM(
            new TextObject("{=PAI_TOWN_DEFENSE_PRIORITY_LOW}Low"),
            TownDefensePriority.Low));
        SortOptions.AddItem(new DefensePriorityItemVM(
            new TextObject("{=PAI_TOWN_DEFENSE_PRIORITY_NORMAL}Normal"),
            TownDefensePriority.Normal));
        SortOptions.AddItem(new DefensePriorityItemVM(
            new TextObject("{=PAI_TOWN_DEFENSE_PRIORITY_HIGH}High"),
            TownDefensePriority.High));
        SortOptions.AddItem(new DefensePriorityItemVM(
            new TextObject("{=PAI_TOWN_DEFENSE_PRIORITY_CRITICAL}Critical"),
            TownDefensePriority.Critical));
        SortOptions.SelectedIndex = Math.Max(0, Math.Min(SortOptions.ItemList.Count - 1, (int)selected));
    }

    [DataSourceProperty]
    public SelectorVM<DefensePriorityItemVM> SortOptions
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
