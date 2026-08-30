using Bannerlord.PartyAI.Models;
using System;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dropdowns;

public sealed class PartyAIAutomationLevelDropdownVM : ViewModel
{
    public sealed class AutomationLevelItemVM : SelectorItemVM
    {
        public SettlementAutomationLevel Level { get; }

        internal AutomationLevelItemVM(TextObject name, SettlementAutomationLevel level)
            : base(name)
        {
            Level = level;
        }
    }

    private SelectorVM<AutomationLevelItemVM> _sortOptions = null!;

    public PartyAIAutomationLevelDropdownVM(SettlementAutomationLevel selected)
    {
        SortOptions = new SelectorVM<AutomationLevelItemVM>(-1, static _ => { });
        SortOptions.AddItem(new AutomationLevelItemVM(
            new TextObject("{=PAI_AUTOMATION_OFF}Off"),
            SettlementAutomationLevel.Off));
        SortOptions.AddItem(new AutomationLevelItemVM(
            new TextObject("{=PAI_AUTOMATION_RECRUIT}Recruit"),
            SettlementAutomationLevel.Recruit));
        SortOptions.AddItem(new AutomationLevelItemVM(
            new TextObject("{=PAI_AUTOMATION_UPGRADE}Recruit + Upgrade"),
            SettlementAutomationLevel.RecruitAndUpgrade));
        SortOptions.AddItem(new AutomationLevelItemVM(
            new TextObject("{=PAI_AUTOMATION_FULL}Full Auto"),
            SettlementAutomationLevel.Full));
        SortOptions.SelectedIndex = Math.Max(0, Math.Min(3, (int)selected));
    }

    [DataSourceProperty]
    public SelectorVM<AutomationLevelItemVM> SortOptions
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
