using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dropdowns;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.MenuOptionVMs;

public class PartyAICaravanOptionsVM : ViewModel
{
    private readonly Action _onClosePartyOptions;
    private PartyAIOptionToggleVM _filterSettlementsToggle = null!;
    private string _filteredSettlementsCount = string.Empty;
    private HintViewModel _filteredSettlementsCountHint = new();
    private readonly PartyAiEntitySettings _settings;
    internal static List<Settlement> FilteredSettlements = new();
    internal static bool IsSelectFilteredSettlements = false;

    public PartyAICaravanOptionsVM(PartyAiEntitySettings settings, Action callback)
    {
        _settings = settings;

        if (_settings.Hero != null)
        {
            TitleText = new TextObject("{=PAIC76hjKpD}Edit Caravan Options for {HERO}'s party").SetTextVariable("HERO", _settings.Hero.Name.ToString()).ToString();
        }
        else
        {
            TitleText = new TextObject("{=PAI2mEbIPHQ}Edit Caravan Options").ToString();
        }

        MaxTroopTierDropdown = new(settings.MaxTroopTier);
        AutomationLevelDropdown = new(settings.SettlementAutomation);

        _onClosePartyOptions = callback;
        FilterSettlementsToggle = new(new("{=PAI7L3x9T3p}Filter Trading Settlements"), settings.FilterSettlements, new("{=PAIRrqrDxYm}The caravan will only visit settlements in this list."));
        FilteredSettlements = settings.FilteredSettlements?.ToList() ?? new();

        RefreshValues();
    }

    [DataSourceProperty]
    public string FilterSettlementsText => new TextObject("{=PAIjkqd24XT} - Selected: ").ToString();

    [DataSourceProperty]
    public string FilteredSettlementsCount
    {
        get
        {
            return _filteredSettlementsCount;
        }
        set
        {
            if (value != _filteredSettlementsCount)
            {
                _filteredSettlementsCount = value;
                OnPropertyChangedWithValue(value, "FilteredSettlementsCount");
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel FilteredSettlementsCountHint
    {
        get
        {
            return _filteredSettlementsCountHint;
        }
        set
        {
            if (value != _filteredSettlementsCountHint)
            {
                _filteredSettlementsCountHint = value;
                OnPropertyChangedWithValue(value, "FilteredSettlementsCountHint");
            }
        }
    }


    [DataSourceProperty]
    public PartyAIOptionToggleVM FilterSettlementsToggle
    {
        get
        {
            return _filterSettlementsToggle;
        }
        set
        {
            if (value != _filterSettlementsToggle)
            {
                _filterSettlementsToggle = value;
                OnPropertyChangedWithValue(value, "FilterSettlementsToggle");
            }
        }
    }

    [DataSourceProperty] public string AcceptText => new TextObject("{=bV75iwKa}Save").ToString();
    [DataSourceProperty] public string CancelText => GameTexts.FindText("str_cancel").ToString();
    [DataSourceProperty] public string TitleText { get; private set; }
    [DataSourceProperty] public PartyAIMaxTroopTierDropdownVM MaxTroopTierDropdown { get; private set; }
    [DataSourceProperty] public PartyAIAutomationLevelDropdownVM AutomationLevelDropdown { get; private set; }
    [DataSourceProperty] public string AutomationLevelText => new TextObject("{=PAI_AUTOMATION_LEVEL}Settlement Automation").ToString();
    [DataSourceProperty] public HintViewModel AutomationLevelHint => new(new TextObject("{=PAI_AUTOMATION_LEVEL_HINT}Choose what happens automatically after this party enters a town or village. Full Auto also balances the template and equips party heroes from inventory."));
    [DataSourceProperty] public string MaxTroopTierText => new TextObject("{=PAIn4UJJg3a}Max Troop Tier").ToString();
    [DataSourceProperty] public HintViewModel MaxTroopTierHint => new(new TextObject("{=PAIKeTFa2PX}Maximum troop tier to upgrade troops to. If you lower this setting while there are higher tier troops in the party, they will be downgraded."));

    public override void RefreshValues()
    {
        base.RefreshValues();
        string settlements = string.Empty;
        foreach (Settlement s in FilteredSettlements)
        {
            if (s != FilteredSettlements.First())
            {
                settlements += Environment.NewLine;
            }
            settlements += s.ToString();
        }
        FilteredSettlementsCountHint = new(new("{=!}" + settlements));

        FilteredSettlementsCount = FilteredSettlements.Count().ToString();
    }

    public void EditFilteredSettlements()
    {
        string title = new TextObject("{=PAIr1WS36dp}Settlements to Visit").ToString();
        List<Settlement> settlements = Settlement.All.Where(s => s.IsTown).OrderBy(s => s.Name.ToString()).ToList();
        List<InquiryElement> list = new();
        foreach (Settlement settlement in settlements)
        {
            TextObject north = new("{=PAImajjVs8d}north");
            TextObject south = new("{=PAISzVDwcWu}south");
            TextObject east = new("{=PAIHQQPyo2M}east");
            TextObject west = new("{=PAIWGp1Ti1N}west");
            TextObject hint = new TextObject("{=PAI7v81Hher}Currently {DIRECTION} of you.").SetTextVariable("DIRECTION", "");
            string direction = string.Empty;
            if (settlement.GetPosition2D.y > MobileParty.MainParty.GetPosition2D.y)
            {
                direction += north;
            }
            if (settlement.GetPosition2D.y < MobileParty.MainParty.GetPosition2D.y)
            {
                direction += south;
            }
            if (settlement.GetPosition2D.x > MobileParty.MainParty.GetPosition2D.x)
            {
                direction += east;
            }
            if (settlement.GetPosition2D.x < MobileParty.MainParty.GetPosition2D.x)
            {
                direction += west;
            }
            hint.SetTextVariable("DIRECTION", direction);
            list.Add(new InquiryElement(settlement, settlement.Name.ToString(), new BannerImageIdentifier(settlement.MapFaction?.Banner, false), true, hint.ToString()));
        }
        IsSelectFilteredSettlements = true;
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, null, list, isExitShown: true, 1, list.Count,
          GameTexts.FindText("str_done").ToString(), GameTexts.FindText("str_cancel").ToString(), (List<InquiryElement> results) =>
          {
              FilteredSettlements = results
                  .Select(result => result.Identifier as Settlement)
                  .Where(settlement => settlement is not null)
                  .Cast<Settlement>()
                  .ToList();
              RefreshValues();
          }, null, null, true)
        );
    }

    public void AcceptEditPartyOptions()
    {
        _settings.MaxTroopTier = MaxTroopTierDropdown.SortOptions.SelectedItem.Max;
        _settings.SettlementAutomation = AutomationLevelDropdown.SortOptions.SelectedItem.Level;
        _settings.FilterSettlements = FilterSettlementsToggle.IsSelected;
        _settings.FilteredSettlements = FilteredSettlements.ToList();

        _onClosePartyOptions?.Invoke();
    }

    public void CancelEditPartyOptions()
    {
        _onClosePartyOptions?.Invoke();
    }
}
