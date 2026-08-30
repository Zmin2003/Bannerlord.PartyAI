using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Dropdowns;
using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.MenuOptionVMs;

public class PartyAIGarrisonOptionsVM : ViewModel
{
    private readonly Action _onClosePartyOptions;
    private readonly PartyAiEntitySettings _settings;

    public PartyAIGarrisonOptionsVM(PartyAiEntitySettings settings, Action callback)
    {
        _settings = settings;

        if (_settings.Hero != null)
        {
            TitleText = new TextObject("{=PAI5VM6usUh}Edit Garrison Options for {SETTLEMENT}")
                .SetTextVariable("SETTLEMENT", settings.Settlement?.Name ?? TextObject.GetEmpty())
                .ToString();
        }
        else
        {
            TitleText = new TextObject("{=PAIy61wxVLV}Edit Garrison Options").ToString();
        }

        MaxTroopTierDropdown = new(settings.MaxTroopTier);

        _onClosePartyOptions = callback;

        RefreshValues();
    }

    [DataSourceProperty] public string AcceptText => new TextObject("{=bV75iwKa}Save").ToString();
    [DataSourceProperty] public string CancelText => GameTexts.FindText("str_cancel").ToString();
    [DataSourceProperty] public string TitleText { get; private set; }
    [DataSourceProperty] public PartyAIMaxTroopTierDropdownVM MaxTroopTierDropdown { get; private set; }
    [DataSourceProperty] public string MaxTroopTierText => new TextObject("{=PAIn4UJJg3a}Max Troop Tier").ToString();
    [DataSourceProperty] public HintViewModel MaxTroopTierHint => new(new TextObject("{=PAIKeTFa2PX}Maximum troop tier to upgrade troops to. If you lower this setting while there are higher tier troops in the party, they will be downgraded."));

    public void AcceptEditPartyOptions()
    {
        _settings.MaxTroopTier = MaxTroopTierDropdown.SortOptions.SelectedItem.Max;

        _onClosePartyOptions?.Invoke();
    }

    public void CancelEditPartyOptions()
    {
        _onClosePartyOptions?.Invoke();
    }
}
