using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dialogs;
using Bannerlord.PartyAI.ViewModels.Dropdowns;
using Bannerlord.PartyAI.ViewModels.MenuItemVMs;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static Bannerlord.PartyAI.ViewModels.Dropdowns.PartyAISortDirectionDropdownVM;
using static Bannerlord.PartyAI.ViewModels.Dropdowns.PartyAISortDropdownVM;

namespace Bannerlord.PartyAI.ViewModels;

public class PartyAIControlsMenuVM : ViewModel
{
    private MBBindingList<PartyAIControlsMenuPartyVM> _partyList = null!;
    private HintViewModel _createClanPartyHint = null!;
    private bool _canCreateNewParty;
    private HintViewModel _showAllHeroesHint = null!;
    private bool _showAllHeroes;
    private List<InquiryElement>? _copySource;
    private string _copySourceName = string.Empty;
    private string _selectionStatusText = string.Empty;

    public PartyAIControlsMenuVM()
    {
        PartyList = new MBBindingList<PartyAIControlsMenuPartyVM>();
        CreateClanPartyHint = new HintViewModel();
        ShowAllHeroesHint = new HintViewModel(new TextObject("{=PAIqJ0819Nl}Show all heroes that can lead parties. Useful for assigning settings for any potential hero that might be a leader."));
        SortController = new PartyAISortDropdownVM(OnSortChanged);
        SortDirectionController = new PartyAISortDirectionDropdownVM(OnSortDirectionChanged);
        SortText = new TextObject("{=PAIuPlFS64X}Sort").ToString();
        SelectAllToggle = new(new TextObject(""), false, new TextObject(""), SelectAll);
        SelectAllToggle.IsDisabled = true;

        RefreshPartyList();
    }

    [DataSourceProperty]
    public bool EnablePartyList => PartyList.Count > 0;

    [DataSourceProperty]
    public bool ShowEmptyState => !EnablePartyList;

    [DataSourceProperty]
    public bool ShowAllHeroes
    {
        get
        {
            return _showAllHeroes;
        }
        set
        {
            if (value != _showAllHeroes)
            {
                _showAllHeroes = value;
                OnPropertyChangedWithValue(value, "ShowAllHeroes");
                RefreshPartyList();
            }
        }
    }

    [DataSourceProperty] public PartyAISortDropdownVM SortController { get; private set; }
    [DataSourceProperty] public PartyAISortDirectionDropdownVM SortDirectionController { get; private set; }
    [DataSourceProperty] public PartyAIOptionToggleVM SelectAllToggle { get; set; }

    [DataSourceProperty] public string SortText { get; private set; }
    [DataSourceProperty] public bool AllowCopy { get; private set; }
    [DataSourceProperty] public bool AllowPaste { get; private set; }
    [DataSourceProperty] public bool CanCancelCopy { get; private set; }
    [DataSourceProperty] public string CopyText => new TextObject("{=PAI_UI_COPY}Copy").ToString();
    [DataSourceProperty] public string PasteText => new TextObject("{=PAI_UI_PASTE}Paste").ToString();
    [DataSourceProperty] public string CancelCopyText => GameTexts.FindText("str_cancel").ToString();
    [DataSourceProperty] public string SelectAllText => new TextObject("{=PAI_UI_SELECT_ALL}All").ToString();
    [DataSourceProperty] public string EmptyStateTitle => new TextObject("{=PAI_UI_EMPTY_TITLE}No manageable parties or fiefs").ToString();
    [DataSourceProperty] public string EmptyStateText => new TextObject("{=PAI_UI_EMPTY_TEXT}Create a clan party, enable caravan management, or acquire a fief to manage it here.").ToString();
    [DataSourceProperty] public string NameColumnText => new TextObject("{=PAI_UI_COLUMN_NAME}Party / Fief").ToString();
    [DataSourceProperty] public string StrengthColumnText => new TextObject("{=PAI_UI_COLUMN_STRENGTH}Strength").ToString();
    [DataSourceProperty] public string CompositionColumnText => new TextObject("{=PAI_UI_COLUMN_COMPOSITION}Composition").ToString();
    [DataSourceProperty] public string TemplateColumnText => new TextObject("{=PAI_UI_COLUMN_TEMPLATE}Template").ToString();
    [DataSourceProperty] public string StatusColumnText => new TextObject("{=PAI_UI_COLUMN_STATUS}Order / Town status").ToString();

    [DataSourceProperty]
    public string SelectionStatusText
    {
        get => _selectionStatusText;
        private set
        {
            if (value != _selectionStatusText)
            {
                _selectionStatusText = value;
                OnPropertyChangedWithValue(value, nameof(SelectionStatusText));
            }
        }
    }

    [DataSourceProperty]
    public bool CanCreateNewParty
    {
        get
        {
            return _canCreateNewParty;
        }
        set
        {
            if (value != _canCreateNewParty)
            {
                _canCreateNewParty = value;
                OnPropertyChangedWithValue(value, "CanCreateNewParty");
            }
        }
    }

    [DataSourceProperty] public HintViewModel CopyHint => new(new("{=PAIY2tmN6Vq}Copy settings from one party to another. Select the checkbox next to a party and press CTRL-C or this button."));
    [DataSourceProperty] public HintViewModel PasteHint => new(new("{=PAIlmndQPWI}Paste settings from one party to another. After copying, select the checkboxes next to all parties you want to paste to and press CTRL-V or this button."));
    [DataSourceProperty] public HintViewModel CancelCopyHint => new(new("{=PAImg9cMpVB}Cancel Copy/Paste operation"));
    [DataSourceProperty] public HintViewModel SelectAllHint => new(new("{=PAIQlzQNwtn}Select All"));

    [DataSourceProperty]
    public string MainHeadingText => new TextObject("{=PAIe2AmH8ga}Party AI Controls").ToString();

    [DataSourceProperty]
    public string CreateTemplateText => new TextObject("{=PAIVTBDYD5s}Create Template").ToString();

    [DataSourceProperty]
    public string ImportTemplateText => new TextObject("{=PAI_TEMPLATE_IMPORT_BUTTON}Online Template").ToString();

    [DataSourceProperty]
    public HintViewModel ImportTemplateHint => new(new TextObject("{=PAI_TEMPLATE_IMPORT_HINT}Import a validated troop template and composition from an HTTPS JSON URL."));

    [DataSourceProperty]
    public string DeletePartyTemplateText => new TextObject("{=PAR1D0VvXKZ}Delete Template").ToString();

    [DataSourceProperty]
    public string FineTunePartyTemplateText => new TextObject("{=PAIwK2enPSp}Fine Tune Template").ToString();

    [DataSourceProperty]
    public HintViewModel FineTunePartyTemplateHint => new(new TextObject("{=PAIzjCcuvQw}Select which troops along the upgrade paths you've chosen to be included in the party template. The topmost troop in the list will be the portrait next to the template."));

    [DataSourceProperty]
    public string ModOptionsText => new TextObject("{=PAIyBVEFgXu}Mod Options").ToString();

    [DataSourceProperty]
    public string CreateClanPartyText => GameTexts.FindText("str_clan_create_new_party").ToString();

    [DataSourceProperty]
    public string EditDefaultSettingsText => new TextObject("{=PAI34RDUeMT}Default Settings").ToString();

    [DataSourceProperty]
    public string TownManagementText => new TextObject("{=PAI_TOWN_MANAGEMENT_BUTTON}Town Management").ToString();

    [DataSourceProperty]
    public string DoneText => GameTexts.FindText("str_done").ToString();

    [DataSourceProperty]
    public string ShowAllHeroesText => new TextObject("{=PAIlKT8heH9}Show All Heroes").ToString();

    [DataSourceProperty]
    public MBBindingList<PartyAIControlsMenuPartyVM> PartyList
    {
        get
        {
            return _partyList;
        }
        set
        {
            if (value != _partyList)
            {
                _partyList = value;
                OnPropertyChangedWithValue(value, "PartyList");
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel CreateClanPartyHint
    {
        get
        {
            return _createClanPartyHint;
        }
        set
        {
            if (value != _createClanPartyHint)
            {
                _createClanPartyHint = value;
                OnPropertyChangedWithValue(value, "CreateClanPartyHint");
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel ShowAllHeroesHint
    {
        get
        {
            return _showAllHeroesHint;
        }
        set
        {
            if (value != _showAllHeroesHint)
            {
                _showAllHeroesHint = value;
                OnPropertyChangedWithValue(value, "ShowAllHeroesHint");
            }
        }
    }

    public void ExecuteDone()
    {
        GameStateManager.Current.PopState();
    }

    public void CreatePartyTemplate() => CreateTemplate.Create();

    public void ImportPartyTemplate() => ImportTemplate.Show(RefreshPartyList);

    public void DeletePartyTemplate() => DeleteTemplate.Delete(RefreshPartyList);

    public void FineTunePartyTemplate() => Dialogs.FineTune.Tune();

    public void OpenModOptions() => SubModule.InformationManager.ShowModOptionsInquiry(RefreshPartyList);

    public void EditDefaultSettings() => SubModule.InformationManager.ShowDefaultSettingsInquiry();

    public void OpenTownManagementOptions()
        => SubModule.InformationManager.ShowTownManagementOptionsInquiry(RefreshPartyList);

    private void OnNewPartySelectionOver()
    {
        RefreshPartyList();
    }

    public void CreateClanParty()
    {
        new ClanPartiesVM(() => { }, new Action<Hero>(CreateClanPartyCallback), new Action(OnNewPartySelectionOver), (i) => { }).ExecuteCreateNewParty();
    }

    private void CreateClanPartyCallback(Hero hero)
    {
        PartyScreenHelper.OpenScreenAsCreateClanPartyForHero(hero);
    }

    public static void GetManageableHeroes(List<Hero> list, bool clanOnly, bool showAll)
    {
        if (Hero.MainHero?.PartyBelongedTo is not null && Hero.MainHero.IsPartyLeader)
        {
            list.Add(Hero.MainHero);
        }

        foreach (Hero hero in Hero.AllAliveHeroes.Where(l => l != null && l != Hero.MainHero && l.CanLeadParty() && SubModule.PartySettingsManager.IsManageable(l) && (!clanOnly || l.Clan == Clan.PlayerClan)).ToList())
        {
            if (showAll || (hero.PartyBelongedTo != null && hero.IsPartyLeader))
            {
                if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.IsCaravan)
                {
                    if (SubModule.PartySettingsManager.ManageCaravans)
                    {
                        list.Add(hero);
                    }
                    continue;
                }
                list.Add(hero);
            }
        }
    }

    private void GetManageablePartyVMs(MBBindingList<PartyAIControlsMenuPartyVM> list, bool clanOnly)
    {
        List<Hero> heroes = new();
        GetManageableHeroes(heroes, clanOnly, ShowAllHeroes);
        foreach (Hero hero in heroes)
        {
            if (hero == Hero.MainHero)
            {
                list.Add(new PartyAIControlsMenuPlayerVM(hero, this));
            }
            else if (hero.PartyBelongedTo?.IsCaravan ?? false)
            {
                list.Add(new PartyAIControlsMenuCaravanVM(hero, this));
            }
            else
            {
                list.Add(new PartyAIControlsMenuPartyVM(hero, this));
            }
        }
        foreach (Settlement settlement in Settlement.All)
        {
            bool isManagedGarrison = SubModule.PartySettingsManager.IsGarrisonManageable(settlement)
                && settlement.Town?.GarrisonParty?.Party != null;
            bool isManagedTown = SubModule.TownManagementBehavior.IsTownManageable(settlement);
            if (isManagedGarrison || isManagedTown)
            {
                list.Add(new PartyAIControlsMenuSettlementVM(settlement, this));
            }
        }
    }

    public void RefreshPartyList()
    {
        PartyList.Clear();

        GetManageablePartyVMs(PartyList, false);

        OnSortChanged(SortType);

        ClanPartiesVM stockVM = new(() => { }, _ => { }, () => { }, (i) => { });
        CanCreateNewParty = stockVM.CanCreateNewParty;
        CreateClanPartyHint.HintText = stockVM.CreateNewPartyActionHint?.HintText ?? new TextObject("");

        OnPropertyChanged("EnablePartyList");
        OnPropertyChanged("ShowEmptyState");
        OnPropertyChanged("PartyList");

        AllowCopy = false;
        AllowPaste = false;
        CanCancelCopy = false;
        _copySource = null;
        _copySourceName = string.Empty;
        OnPropertyChanged("AllowCopy");
        OnPropertyChanged("CanCancelCopy");
        OnPropertyChanged("AllowPaste");
        SelectAllToggle.IsDisabled = true;
        SelectAllToggle.IsSelected = false;
        OnPropertyChanged("SelectAllToggle");

        RefreshValues();
        UpdateSelectionStatus();
    }

    private void OnSortDirectionChanged(PartySortDirection direction)
    {
        OnSortChanged(SortType);
    }

    private void OnSortChanged(PartySortType sortType)
    {
        List<PartyAIControlsMenuPartyVM> newParties = new();

        switch (sortType)
        {
            case PartySortType.Clan:
                newParties = PartyList.OrderByDescending(p => p.Leader.Clan.Equals(Clan.PlayerClan)).ThenByDescending(p => p.Leader.Clan.Tier).ThenByDescending(p => p.Leader.IsClanLeader).ToList();
                break;
            case PartySortType.Army:
                newParties = PartyList.OrderByDescending(p => p.Army != null).ThenBy(p => p.Army?.Name?.ToString() ?? String.Empty).ThenByDescending(p => p.IsArmyLeader).ToList();
                break;
            case PartySortType.Alphabetical:
                newParties = PartyList.OrderBy(p => (p.IsLordParty || p.IsCaravan) ? p.Leader.Name?.ToString() : (p.Settlement?.Name.ToString()) ?? string.Empty).ToList();
                break;
            case PartySortType.Troops:
                newParties = PartyList.OrderBy(p => p.Party?.NumberOfAllMembers).ToList();
                break;
            case PartySortType.Type:
                newParties = PartyList.OrderByDescending(p => p.IsLordParty).ThenByDescending(p => p.IsCaravan).ThenByDescending(p => p.IsSettlement).ToList();
                break;
            case PartySortType.Template:
                newParties = PartyList.OrderBy(p => p.Settings.PartyTemplate?.Name).ToList();
                break;
            default:
                break;
        }
        if (SortDirection == PartySortDirection.DESC)
        {
            newParties.Reverse();
        }

        PartyList.Clear();
        foreach (PartyAIControlsMenuPartyVM party in newParties)
        {
            PartyList.Add(party);
        }

        OnPropertyChanged("PartyList");
        RefreshValues();
    }

    internal void OnCopyPasteToggle(PartyAIControlsMenuPartyVM vm, bool status)
    {
        AllowCopy = false;
        if (_copySource == null)
        {
            foreach (PartyAIControlsMenuPartyVM item in PartyList)
            {
                if (item != vm)
                {
                    if (status)
                    {
                        AllowCopy = true;
                        CanCancelCopy = true;
                        item.CopyPasteToggle.IsSelected = false;
                        item.CopyPasteToggle.IsDisabled = true;
                    }
                    else
                    {
                        item.CopyPasteToggle.IsSelected = false;
                        item.CopyPasteToggle.IsDisabled = false;
                    }
                }
            }

            CanCancelCopy = status;
            OnPropertyChanged("CanCancelCopy");
        }
        else
        {
            AllowPaste = SelectedCountWithPendingChange(vm, status) > 0;
            OnPropertyChanged("AllowPaste");
        }
        OnPropertyChanged("AllowCopy");
        UpdateSelectionStatus(vm, status);
    }

    internal void Copy()
    {
        PartyAIControlsMenuPartyVM vm = PartyList.FirstOrDefault(p => p.CopyPasteToggle.IsSelected);
        if (vm == null) { return; }
        if (_copySource != null) { return; }
        CopyPaste.CopyCallback(vm.Settings, (List<InquiryElement> list) =>
        {
            _copySource = list;
            _copySourceName = vm.LeaderName;
            bool copiesTownManagement = list.Any(source => source.Identifier
                is Bannerlord.PartyAI.Models.TownManagementSettlementSettings);
            vm.CopyPasteToggle.IsSelected = false;
            vm.CopyPasteToggle.IsDisabled = true;
            AllowCopy = false;
            AllowPaste = false;

            foreach (PartyAIControlsMenuPartyVM item in PartyList)
            {
                if (item != vm)
                {
                    item.CopyPasteToggle.IsSelected = false;
                    bool sameEntityType = item.IsLordParty == vm.IsLordParty
                        && item.IsCaravan == vm.IsCaravan
                        && item.IsSettlement == vm.IsSettlement;
                    bool acceptsTownManagement = !copiesTownManagement
                        || (item.Settlement is Settlement settlement
                            && SubModule.TownManagementBehavior.IsTownManageable(settlement));
                    item.CopyPasteToggle.IsDisabled = !sameEntityType
                        || !acceptsTownManagement;
                }
            }

            OnPropertyChanged("AllowCopy");
            OnPropertyChanged("CanCancelCopy");
            OnPropertyChanged("AllowPaste");
            SelectAllToggle.IsDisabled = !PartyList.Any(item =>
                !item.CopyPasteToggle.IsDisabled);
            OnPropertyChanged("SelectAllToggle");
            UpdateSelectionStatus();
        });
    }

    internal void Paste()
    {
        if (_copySource == null) { return; }
        IEnumerable<PartyAIControlsMenuPartyVM> targets = PartyList.Where(p => p.CopyPasteToggle.IsSelected);
        if (targets.Count() == 0) { return; }
        foreach (PartyAIControlsMenuPartyVM target in targets)
        {
            foreach (InquiryElement source in _copySource)
            {
                CopyPaste.CopySettings(target.Settings, source);
            }
        }
        _copySource = null;
        _copySourceName = string.Empty;
        AllowPaste = false;
        CanCancelCopy = false;
        OnPropertyChanged("AllowPaste");
        OnPropertyChanged("CanCancelCopy");
        SelectAllToggle.IsDisabled = true;
        OnPropertyChanged("SelectAllToggle");
        RefreshPartyList();
    }

    private void SelectAll(bool selected)
    {
        foreach (PartyAIControlsMenuPartyVM p in PartyList)
        {
            if (!p.CopyPasteToggle.IsDisabled)
            {
                p.CopyPasteToggle.IsSelected = selected;
            }
        }

        UpdateSelectionStatus();
    }

    private int SelectedCountWithPendingChange(PartyAIControlsMenuPartyVM? pendingVm = null, bool? pendingStatus = null)
    {
        int count = PartyList.Count(p => p.CopyPasteToggle.IsSelected);
        if (pendingVm is not null
            && pendingStatus.HasValue
            && pendingVm.CopyPasteToggle.IsSelected != pendingStatus.Value)
        {
            count += pendingStatus.Value ? 1 : -1;
        }

        return Math.Max(0, count);
    }

    private void UpdateSelectionStatus(PartyAIControlsMenuPartyVM? pendingVm = null, bool? pendingStatus = null)
    {
        if (!EnablePartyList)
        {
            SelectionStatusText = string.Empty;
            return;
        }

        int selectedCount = SelectedCountWithPendingChange(pendingVm, pendingStatus);
        if (_copySource is null)
        {
            SelectionStatusText = selectedCount == 0
                ? new TextObject("{=PAI_UI_COPY_IDLE}Select one row to copy its settings.").ToString()
                : new TextObject("{=PAI_UI_COPY_READY}One source selected. Copy to continue.").ToString();
            return;
        }

        TextObject status = selectedCount == 0
            ? new TextObject("{=PAI_UI_PASTE_IDLE}Copied from {SOURCE}. Select one or more compatible targets.")
            : new TextObject("{=PAI_UI_PASTE_READY}Copied from {SOURCE}. {COUNT} target(s) selected.");
        SelectionStatusText = status
            .SetTextVariable("SOURCE", _copySourceName)
            .SetTextVariable("COUNT", selectedCount)
            .ToString();
    }

    public override void RefreshValues()
    {
        base.RefreshValues();

        foreach (PartyAIControlsMenuPartyVM item in _partyList)
        {
            item.RefreshValues();
        }
    }
}
