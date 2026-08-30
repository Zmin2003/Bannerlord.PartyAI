using System;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dropdowns;

public class PartyAISortDirectionDropdownVM : ViewModel
{
    public class PartyAISortDirectionSelectorItemVM : SelectorItemVM
    {
        public PartySortDirection SortDirection { get; private set; }

        public PartyAISortDirectionSelectorItemVM(TextObject s, PartySortDirection sortDirection)
          : base(s)
        {
            SortDirection = sortDirection;
        }
    }

    public enum PartySortDirection
    {
        ASC,
        DESC
    }

    public static PartySortDirection SortDirection = PartySortDirection.ASC;
    private readonly Action<PartySortDirection> _onSort;

    private SelectorVM<PartyAISortDirectionSelectorItemVM> _sortOptions = null!;

    [DataSourceProperty]
    public SelectorVM<PartyAISortDirectionSelectorItemVM> SortOptions
    {
        get
        {
            return _sortOptions;
        }
        set
        {
            if (value != _sortOptions)
            {
                _sortOptions = value;
                OnPropertyChangedWithValue(value, "SortOptions");
            }
        }
    }

    public PartyAISortDirectionDropdownVM(Action<PartySortDirection> onSort)
    {
        _onSort = onSort;

        SortOptions = new SelectorVM<PartyAISortDirectionSelectorItemVM>(-1, OnSortSelected);
        SortOptions.AddItem(new PartyAISortDirectionSelectorItemVM(new TextObject("{=PAI4pLNfNa1}Asc"), PartySortDirection.ASC));
        SortOptions.AddItem(new PartyAISortDirectionSelectorItemVM(new TextObject("{=PAIdc8T9Up8}Desc"), PartySortDirection.DESC));

        int i = 0;
        foreach (PartyAISortDirectionSelectorItemVM s in SortOptions.ItemList)
        {
            if (s.SortDirection == SortDirection)
            {
                SortOptions.SelectedIndex = i;
            }
            i++;
        }
    }

    private void OnSortSelected(SelectorVM<PartyAISortDirectionSelectorItemVM> selector)
    {
        SortDirection = selector.SelectedItem.SortDirection;
        _onSort?.Invoke(SortDirection);
    }
}
