using Bannerlord.PartyAI.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dropdowns;

public class PartyAIMaxPartiesDropdownVM : ViewModel
{
    public class PartyAIMaxPartiesSelectorItemVM : SelectorItemVM
    {
        public int Max { get; private set; }

        public PartyAIMaxPartiesSelectorItemVM(TextObject s, int max)
          : base(s)
        {
            Max = max;
        }
    }

    private SelectorVM<PartyAIMaxPartiesSelectorItemVM> _sortOptions = null!;

    [DataSourceProperty]
    public SelectorVM<PartyAIMaxPartiesSelectorItemVM> SortOptions
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
    public PartyAIMaxPartiesDropdownVM()
    {
        SortOptions = new SelectorVM<PartyAIMaxPartiesSelectorItemVM>(-1, static _ => { });

        SortOptions.AddItem(new PartyAIMaxPartiesSelectorItemVM(new TextObject("{=PAIIqVpFFAi}Max"), 0));

        var partyAutoCreationBehavior = Campaign.Current.GetCampaignBehavior<PartyAutoCreationBehavior>();

        for (int i = 1; i <= Clan.PlayerClan.WarPartyLimit || i <= partyAutoCreationBehavior.AutoCreateClanPartiesMax; i++)
        {
            SortOptions.AddItem(new PartyAIMaxPartiesSelectorItemVM(new TextObject("{=!}" + i.ToString()), i));
        }
        SortOptions.SelectedIndex = partyAutoCreationBehavior.AutoCreateClanPartiesMax;
    }

}
