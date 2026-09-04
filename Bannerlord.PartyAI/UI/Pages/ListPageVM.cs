using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.UI.Components;
using Bannerlord.PartyAI.UI.Detail;
using Bannerlord.PartyAI.UI.Dialogs;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Pages;

/// <summary>
/// Master-detail page: a list of <see cref="EntryVM"/> on the left, a <see cref="DetailVM"/> for
/// the selected entry on the right, plus copy/paste of settings between compatible entries.
/// </summary>
public abstract class ListPageVM : ViewModel
{
    private readonly SettingsClipboard _clipboard = new();
    private MBBindingList<EntryVM> _entries = new();
    private EntryVM? _selected;
    private DetailVM? _detail;
    private bool _isVisible;
    private string _selectionStatus = string.Empty;

    protected ListPageVM(string title)
    {
        Title = L.S(title);
    }

    [DataSourceProperty] public string Title { get; }

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (value != _isVisible)
            {
                _isVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsVisible));
                if (value)
                {
                    Rebuild();
                }
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<EntryVM> Entries
    {
        get => _entries;
        private set
        {
            if (value != _entries)
            {
                _entries = value;
                OnPropertyChangedWithValue(value, nameof(Entries));
            }
        }
    }

    [DataSourceProperty]
    public DetailVM? Detail
    {
        get => _detail;
        private set
        {
            if (value != _detail)
            {
                _detail = value;
                OnPropertyChangedWithValue(value, nameof(Detail));
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    [DataSourceProperty] public bool HasSelection => _detail is not null;
    [DataSourceProperty] public bool IsEmpty => _entries.Count == 0;
    [DataSourceProperty] public abstract string EmptyText { get; }

    // ---- Copy / paste ---------------------------------------------------------------------------

    [DataSourceProperty] public bool IsPasteMode => _clipboard.HasContent;
    [DataSourceProperty] public bool CanCopy => !IsPasteMode && _selected is { IsDefaults: false };
    [DataSourceProperty] public bool CanPaste => IsPasteMode && _entries.Any(entry => entry.IsChecked);
    [DataSourceProperty] public string CopyText => L.S("{=PAI_UI_COPY}Copy settings");
    [DataSourceProperty] public string PasteText => L.S("{=PAI_UI_PASTE}Paste to checked");
    [DataSourceProperty] public string CancelText => L.Game("str_cancel");
    [DataSourceProperty] public string SelectAllText => L.S("{=PAIxKOXkgPU}Select All");
    [DataSourceProperty] public HintViewModel CopyHint => new(L.T("{=PAI_COPY_HINT}Copy the selected entry's settings (Ctrl+C), then check the entries to paste onto."));
    [DataSourceProperty] public HintViewModel PasteHint => new(L.T("{=PAI_PASTE_HINT}Paste the copied settings onto every checked entry (Ctrl+V)."));

    [DataSourceProperty]
    public string SelectionStatus
    {
        get => _selectionStatus;
        private set
        {
            if (value != _selectionStatus)
            {
                _selectionStatus = value;
                OnPropertyChangedWithValue(value, nameof(SelectionStatus));
            }
        }
    }

    public void ExecuteCopy()
    {
        if (_selected is null || !CanCopy)
        {
            return;
        }

        _clipboard.Copy(_selected, () =>
        {
            foreach (EntryVM entry in _entries)
            {
                entry.SetCheckedSilently(false);
                entry.CanCheck = entry != _selected && _selected.IsCompatibleWith(entry)
                    && (!_clipboard.CopiesTownManagement || entry.Kind == EntryKind.Fief);
            }

            RefreshCopyPasteState();
        });
    }

    public void ExecutePaste()
    {
        if (!CanPaste)
        {
            return;
        }

        int count = 0;
        foreach (EntryVM entry in _entries.Where(entry => entry.IsChecked).ToList())
        {
            _clipboard.PasteTo(entry);
            count++;
        }

        Notify.Success(L.T("{=PAI_PASTED}Settings pasted onto {COUNT} entries.", "COUNT", count));
        ExecuteCancelPaste();
        Rebuild();
    }

    public void ExecuteCancelPaste()
    {
        _clipboard.Clear();
        foreach (EntryVM entry in _entries)
        {
            entry.SetCheckedSilently(false);
            entry.CanCheck = false;
        }

        RefreshCopyPasteState();
    }

    public void ExecuteSelectAll()
    {
        bool allChecked = _entries.Where(entry => entry.CanCheck).All(entry => entry.IsChecked);
        foreach (EntryVM entry in _entries.Where(entry => entry.CanCheck))
        {
            entry.SetCheckedSilently(!allChecked);
        }

        RefreshCopyPasteState();
    }

    private void OnEntryChecked(EntryVM entry, bool isChecked) => RefreshCopyPasteState();

    private void RefreshCopyPasteState()
    {
        OnPropertyChanged(nameof(IsPasteMode));
        OnPropertyChanged(nameof(CanCopy));
        OnPropertyChanged(nameof(CanPaste));

        if (!IsPasteMode)
        {
            SelectionStatus = _selected is { IsDefaults: false }
                ? L.S("{=PAI_UI_COPY_READY}Copy this entry's settings to apply them to others.")
                : string.Empty;
            return;
        }

        int checkedCount = _entries.Count(entry => entry.IsChecked);
        SelectionStatus = checkedCount == 0
            ? L.T("{=PAI_UI_PASTE_IDLE}Copied from {SOURCE}. Check the entries to paste onto.", "SOURCE", _clipboard.SourceName).ToString()
            : L.T("{=PAI_UI_PASTE_READY}Copied from {SOURCE}. {COUNT} entries checked.")
                .SetTextVariable("SOURCE", _clipboard.SourceName)
                .SetTextVariable("COUNT", checkedCount)
                .ToString();
    }

    // ---- Selection ------------------------------------------------------------------------------

    protected void Select(EntryVM? entry)
    {
        if (_selected is not null)
        {
            _selected.IsSelected = false;
        }

        _selected = entry;
        if (entry is not null)
        {
            entry.IsSelected = true;
            Detail = new DetailVM(entry, OnDetailChanged);
        }
        else
        {
            Detail = null;
        }

        RefreshCopyPasteState();
    }

    private void OnDetailChanged()
    {
        foreach (EntryVM entry in _entries)
        {
            entry.RefreshValues();
        }
    }

    // ---- Building -------------------------------------------------------------------------------

    protected abstract IEnumerable<EntryVM> BuildEntries();

    /// <summary>Rebuilds the list, keeping the current selection when its entry still exists.</summary>
    public void Rebuild()
    {
        object? selectedKey = _selected?.Hero ?? (object?)_selected?.Settlement ?? _selected?.Profile;

        var list = new MBBindingList<EntryVM>();
        foreach (EntryVM entry in BuildEntries())
        {
            list.Add(entry);
        }

        Entries = list;
        OnPropertyChanged(nameof(IsEmpty));

        EntryVM? reselect = selectedKey is null
            ? null
            : list.FirstOrDefault(entry => (entry.Hero ?? (object?)entry.Settlement ?? entry.Profile) == selectedKey);
        Select(reselect ?? list.FirstOrDefault());

        if (IsPasteMode)
        {
            ExecuteCancelPaste();
        }
    }

    protected EntryVM EntryForHero(TaleWorlds.CampaignSystem.Hero hero) => EntryVM.ForHero(hero, Select, OnEntryChecked);

    protected EntryVM EntryForSettlement(TaleWorlds.CampaignSystem.Settlements.Settlement settlement) => EntryVM.ForSettlement(settlement, Select, OnEntryChecked);

    protected EntryVM EntryForDefaults(Parties.PartyProfile profile, string name, string subtitle) => EntryVM.ForDefaults(profile, L.S(name), L.S(subtitle), Select);

    protected EntryVM EntryForGlobalTown() => EntryVM.ForGlobalTown(Select);

    public override void RefreshValues()
    {
        base.RefreshValues();
        foreach (EntryVM entry in _entries)
        {
            entry.RefreshValues();
        }
    }
}
