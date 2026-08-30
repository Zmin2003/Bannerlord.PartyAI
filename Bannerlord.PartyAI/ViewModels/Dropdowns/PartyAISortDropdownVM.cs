using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dropdowns;

public class PartyAISortDropdownVM : ViewModel
{
    public class PartyAISortSelectorItemVM : SelectorItemVM
    {
        public PartySortType SortType { get; private set; }

        public PartyAISortSelectorItemVM(TextObject s, PartySortType sortType)
          : base(s)
        {
            SortType = sortType;
        }
    }

    public enum PartySortType
    {
        Clan,
        Alphabetical,
        Type,
        Troops,
        Army,
        Template
    }

    public static PartySortType SortType = PartySortType.Clan;
    private readonly Action<PartySortType> _onSort;

    private SelectorVM<PartyAISortSelectorItemVM> _sortOptions = null!;

    [DataSourceProperty]
    public SelectorVM<PartyAISortSelectorItemVM> SortOptions
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

    public PartyAISortDropdownVM(Action<PartySortType> onSort)
    {
        _onSort = onSort;

        SortOptions = new SelectorVM<PartyAISortSelectorItemVM>(-1, OnSortSelected);
        SortOptions.AddItem(new PartyAISortSelectorItemVM(GameTexts.FindText("str_clan"), PartySortType.Clan));
        SortOptions.AddItem(new PartyAISortSelectorItemVM(GameTexts.FindText("str_sort_by_name_label"), PartySortType.Alphabetical));
        SortOptions.AddItem(new PartyAISortSelectorItemVM(new TextObject("{=zMMqgxb1}Type"), PartySortType.Type));
        SortOptions.AddItem(new PartyAISortSelectorItemVM(new TextObject("{=5k4dxUEJ}Troops"), PartySortType.Troops));
        SortOptions.AddItem(new PartyAISortSelectorItemVM(new TextObject("{=j12VrGKz}Army"), PartySortType.Army));
        SortOptions.AddItem(new PartyAISortSelectorItemVM(new TextObject("{=PAIrkbpwijb}Template"), PartySortType.Template));

        int i = 0;
        foreach (PartyAISortSelectorItemVM s in SortOptions.ItemList)
        {
            if (s.SortType == SortType)
            {
                SortOptions.SelectedIndex = i;
            }
            i++;
        }
    }

    private void OnSortSelected(SelectorVM<PartyAISortSelectorItemVM> selector)
    {
        SortType = selector.SelectedItem.SortType;
        _onSort?.Invoke(SortType);
    }
}
