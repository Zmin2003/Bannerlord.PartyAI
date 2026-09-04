using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.UI.Pages;
using System;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.UI;

/// <summary>Root of the control panel: tab bar plus the five pages.</summary>
public sealed class ControlPanelVM : ViewModel
{
    private enum Tab
    {
        Parties,
        Fiefs,
        Templates,
        Economy,
        Settings
    }

    private static Tab _lastTab = Tab.Parties;
    private readonly Action _close;
    private Tab _tab;

    public ControlPanelVM(Action close)
    {
        _close = close;
        Parties = new PartiesPageVM();
        Fiefs = new FiefsPageVM();
        Templates = new TemplatesPageVM();
        Economy = new EconomyPageVM();
        Settings = new SettingsPageVM();
        SetTab(_lastTab);
    }

    [DataSourceProperty] public string TitleText => L.S("{=PAIe2AmH8ga}Party AI Controls");
    [DataSourceProperty] public string DoneText => L.Game("str_done");

    [DataSourceProperty] public PartiesPageVM Parties { get; }
    [DataSourceProperty] public FiefsPageVM Fiefs { get; }
    [DataSourceProperty] public TemplatesPageVM Templates { get; }
    [DataSourceProperty] public EconomyPageVM Economy { get; }
    [DataSourceProperty] public SettingsPageVM Settings { get; }

    [DataSourceProperty] public bool IsPartiesSelected => _tab == Tab.Parties;
    [DataSourceProperty] public bool IsFiefsSelected => _tab == Tab.Fiefs;
    [DataSourceProperty] public bool IsTemplatesSelected => _tab == Tab.Templates;
    [DataSourceProperty] public bool IsEconomySelected => _tab == Tab.Economy;
    [DataSourceProperty] public bool IsSettingsSelected => _tab == Tab.Settings;

    public void SetSelectedTab(int index) => SetTab((Tab)index);

    public void ExecuteDone() => _close();

    /// <summary>Ctrl+C on the active list page.</summary>
    public void CopySelected() => ActiveListPage?.ExecuteCopy();

    /// <summary>Ctrl+V on the active list page.</summary>
    public void PasteToSelected() => ActiveListPage?.ExecutePaste();

    private ListPageVM? ActiveListPage => _tab switch
    {
        Tab.Parties => Parties,
        Tab.Fiefs => Fiefs,
        _ => null
    };

    private void SetTab(Tab tab)
    {
        _tab = tab;
        _lastTab = tab;

        Parties.IsVisible = tab == Tab.Parties;
        Fiefs.IsVisible = tab == Tab.Fiefs;
        Templates.IsVisible = tab == Tab.Templates;
        Economy.IsVisible = tab == Tab.Economy;
        Settings.IsVisible = tab == Tab.Settings;

        OnPropertyChanged(nameof(IsPartiesSelected));
        OnPropertyChanged(nameof(IsFiefsSelected));
        OnPropertyChanged(nameof(IsTemplatesSelected));
        OnPropertyChanged(nameof(IsEconomySelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
    }

    public override void OnFinalize()
    {
        Parties.OnFinalize();
        Fiefs.OnFinalize();
        Templates.OnFinalize();
        Economy.OnFinalize();
        Settings.OnFinalize();
        base.OnFinalize();
    }
}
