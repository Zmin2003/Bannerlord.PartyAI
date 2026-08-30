using System;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dropdowns;

public sealed class PartyAIMaxCaravansDropdownVM : ViewModel
{
    public sealed class MaxCaravansItemVM : SelectorItemVM
    {
        public int Max { get; }

        internal MaxCaravansItemVM(TextObject text, int max)
            : base(text)
        {
            Max = max;
        }
    }

    private SelectorVM<MaxCaravansItemVM> _sortOptions = null!;

    internal PartyAIMaxCaravansDropdownVM(int selected)
    {
        SortOptions = new SelectorVM<MaxCaravansItemVM>(-1, static _ => { });
        SortOptions.AddItem(new MaxCaravansItemVM(
            new TextObject("{=PAIIqVpFFAi}Max"),
            0));

        int largest = Math.Max(10, selected);
        for (int value = 1; value <= largest; value++)
        {
            SortOptions.AddItem(new MaxCaravansItemVM(
                new TextObject("{=!}" + value),
                value));
        }
        SortOptions.SelectedIndex = Math.Max(0, selected);
    }

    [DataSourceProperty]
    public SelectorVM<MaxCaravansItemVM> SortOptions
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
