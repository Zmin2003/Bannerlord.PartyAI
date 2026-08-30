using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dropdowns;

public class PartyAIMaxTroopTierDropdownVM : ViewModel
{
    public class PartyAIMaxTroopTierSelectorItemVM : SelectorItemVM
    {
        public int Max { get; private set; }

        public PartyAIMaxTroopTierSelectorItemVM(TextObject s, int max)
          : base(s)
        {
            Max = max;
        }
    }

    private SelectorVM<PartyAIMaxTroopTierSelectorItemVM> _sortOptions = null!;

    [DataSourceProperty]
    public SelectorVM<PartyAIMaxTroopTierSelectorItemVM> SortOptions
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
    public PartyAIMaxTroopTierDropdownVM(int selectedIndex)
    {
        SortOptions = new SelectorVM<PartyAIMaxTroopTierSelectorItemVM>(-1, static _ => { });

        SortOptions.AddItem(new PartyAIMaxTroopTierSelectorItemVM(new TextObject("{=PAIIqVpFFAi}Max"), 0));

        for (int i = 1; i <= Campaign.Current.Models.CharacterStatsModel.MaxCharacterTier; i++)
        {
            SortOptions.AddItem(new PartyAIMaxTroopTierSelectorItemVM(new TextObject("{=!}" + i.ToString()), i));
        }
        SortOptions.SelectedIndex = selectedIndex;
    }

}
