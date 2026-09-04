using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Templates;
using Bannerlord.PartyAI.UI.Components;
using Bannerlord.PartyAI.UI.Dialogs;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Pages;

/// <summary>One template in the list.</summary>
public sealed class TemplateItemVM : ViewModel
{
    private readonly Action<TemplateItemVM> _onSelect;
    private bool _isSelected;

    public TemplateItemVM(TroopTemplate template, Action<TemplateItemVM> onSelect)
    {
        Template = template;
        _onSelect = onSelect;
        Name = template.Name;
        Portrait = template.Portrait is null ? null : new CharacterImageIdentifierVM(CampaignUIHelper.GetCharacterCode(template.Portrait));
        Subtitle = template.IsBuiltIn
            ? L.S("{=PAI_TEMPLATE_BUILTIN}Built-in")
            : template.SourceId is not null
                ? L.S("{=PAI_TEMPLATE_IMPORTED}Imported")
                : L.S("{=PAI_TEMPLATE_CUSTOM}Custom");
        UsedBy = L.T("{=PAI_TEMPLATE_USED_BY}Used by {COUNT}", "COUNT",
            PartyAi.Parties.AllProfiles().Count(profile => profile.Template == template)).ToString();
    }

    public TroopTemplate Template { get; }

    [DataSourceProperty] public string Name { get; }
    [DataSourceProperty] public string Subtitle { get; }
    [DataSourceProperty] public string UsedBy { get; }
    [DataSourceProperty] public ImageIdentifierVM? Portrait { get; }
    [DataSourceProperty] public bool ShowPortrait => Portrait is not null;

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value != _isSelected)
            {
                _isSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsSelected));
            }
        }
    }

    public void ExecuteSelect() => _onSelect(this);
}

/// <summary>One troop line in the template detail.</summary>
public sealed class TemplateTroopVM : ViewModel
{
    public TemplateTroopVM(CharacterObject troop, bool isTarget)
    {
        Name = troop.Name.ToString();
        Tier = L.T("{=PAI_TIER}Tier {TIER}", "TIER", troop.Tier).ToString();
        Culture = troop.Culture?.Name.ToString() ?? string.Empty;
        Portrait = new CharacterImageIdentifierVM(CampaignUIHelper.GetCharacterCode(troop));
        IsTarget = isTarget;
    }

    [DataSourceProperty] public string Name { get; }
    [DataSourceProperty] public string Tier { get; }
    [DataSourceProperty] public string Culture { get; }
    [DataSourceProperty] public ImageIdentifierVM Portrait { get; }
    [DataSourceProperty] public bool IsTarget { get; }
}

/// <summary>Create, import, fine-tune, inspect and delete troop templates.</summary>
public sealed class TemplatesPageVM : ViewModel
{
    private MBBindingList<TemplateItemVM> _templates = new();
    private MBBindingList<TemplateTroopVM> _troops = new();
    private TemplateItemVM? _selected;
    private bool _isVisible;

    public TemplatesPageVM()
    {
        Rebuild();
    }

    [DataSourceProperty] public string Title => L.S("{=PAI_TAB_TEMPLATES}Templates");
    [DataSourceProperty] public string CreateText => L.S("{=PAIVTBDYD5s}Create Template");
    [DataSourceProperty] public string ImportText => L.S("{=PAI_TEMPLATE_IMPORT_BUTTON}Online Template");
    [DataSourceProperty] public string FineTuneText => L.S("{=PAIwK2enPSp}Fine Tune Template");
    [DataSourceProperty] public string ViewText => L.S("{=PAkCYmU0Qtl}View");
    [DataSourceProperty] public string DeleteText => L.S("{=PAR1D0VvXKZ}Delete Template");
    [DataSourceProperty] public string TargetsHeader => L.S("{=PAI_TEMPLATE_TARGETS}Target troops");
    [DataSourceProperty] public string TroopsHeader => L.S("{=PAI_TEMPLATE_TROOPS}Troops on the upgrade paths");
    [DataSourceProperty] public string EmptyText => L.S("{=PAI_UI_EMPTY_TEMPLATES}No templates yet. Create one from the game's troop trees or import one from a URL.");
    [DataSourceProperty] public HintViewModel ImportHint => new(L.T("{=PAI_TEMPLATE_IMPORT_HINT}Import a validated troop template and composition from an HTTPS JSON URL."));
    [DataSourceProperty] public HintViewModel FineTuneHint => new(L.T("{=PAIzjCcuvQw}Select which troops along the upgrade paths you've chosen to be included in the party template. The topmost troop in the list will be the portrait next to the template."));
    [DataSourceProperty] public HintViewModel DeleteHint => new(L.T("{=PAI_TEMPLATE_DELETE_HINT}Parties using this template are reset to no template."));

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
    public MBBindingList<TemplateItemVM> Templates
    {
        get => _templates;
        private set
        {
            if (value != _templates)
            {
                _templates = value;
                OnPropertyChangedWithValue(value, nameof(Templates));
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<TemplateTroopVM> Troops
    {
        get => _troops;
        private set
        {
            if (value != _troops)
            {
                _troops = value;
                OnPropertyChangedWithValue(value, nameof(Troops));
            }
        }
    }

    [DataSourceProperty] public bool IsEmpty => _templates.Count == 0;
    [DataSourceProperty] public bool HasSelection => _selected is not null;
    [DataSourceProperty] public bool CanEdit => _selected is { Template.IsBuiltIn: false };
    [DataSourceProperty] public string SelectedName => _selected?.Name ?? string.Empty;
    [DataSourceProperty] public string SelectedSubtitle => _selected?.Subtitle ?? string.Empty;
    [DataSourceProperty] public string SelectedUsedBy => _selected?.UsedBy ?? string.Empty;
    [DataSourceProperty] public string SelectedCultures => _selected is null
        ? string.Empty
        : string.Join(", ", _selected.Template.TroopCultures.Select(culture => culture.Name.ToString()));
    [DataSourceProperty] public CompositionDisplayVM? RecommendedComposition => _selected?.Template.RecommendedComposition is null
        ? null
        : new CompositionDisplayVM(_selected.Template.RecommendedComposition);
    [DataSourceProperty] public bool HasRecommendedComposition => _selected?.Template.RecommendedComposition is not null;
    [DataSourceProperty] public string RecommendedCompositionText => L.S("{=PAI_RECOMMENDED_COMPOSITION}Recommended composition");

    public void ExecuteCreate() => TemplateDialogs.Create(_ => Rebuild());

    public void ExecuteImport() => TemplateDialogs.Import(Rebuild);

    public void ExecuteFineTune()
    {
        if (_selected is { Template.IsBuiltIn: false })
        {
            TemplateDialogs.FineTune(_selected.Template, Rebuild);
        }
    }

    public void ExecuteView()
    {
        if (_selected is not null)
        {
            TemplateDialogs.View(_selected.Template);
        }
    }

    public void ExecuteDelete()
    {
        if (_selected is { Template.IsBuiltIn: false })
        {
            TemplateDialogs.ConfirmDelete(_selected.Template, Rebuild);
        }
    }

    private void Select(TemplateItemVM? item)
    {
        if (_selected is not null)
        {
            _selected.IsSelected = false;
        }

        _selected = item;
        if (item is not null)
        {
            item.IsSelected = true;
        }

        var troops = new MBBindingList<TemplateTroopVM>();
        if (item is not null)
        {
            var targets = new System.Collections.Generic.HashSet<CharacterObject>(
                item.Template.UpgradeTargets.GetTroopRoster().Select(element => element.Character));
            foreach (CharacterObject troop in item.Template.Troops
                .OrderByDescending(troop => targets.Contains(troop))
                .ThenBy(troop => troop.Culture?.StringId)
                .ThenByDescending(troop => troop.Tier)
                .ThenBy(troop => troop.Name.ToString()))
            {
                troops.Add(new TemplateTroopVM(troop, targets.Contains(troop)));
            }
        }

        Troops = troops;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(SelectedSubtitle));
        OnPropertyChanged(nameof(SelectedUsedBy));
        OnPropertyChanged(nameof(SelectedCultures));
        OnPropertyChanged(nameof(RecommendedComposition));
        OnPropertyChanged(nameof(HasRecommendedComposition));
    }

    public void Rebuild()
    {
        TroopTemplate? keep = _selected?.Template;
        var list = new MBBindingList<TemplateItemVM>();
        foreach (TroopTemplate template in PartyAi.Parties.Templates
            .OrderBy(template => template.IsBuiltIn)
            .ThenBy(template => template.Name))
        {
            list.Add(new TemplateItemVM(template, Select));
        }

        Templates = list;
        OnPropertyChanged(nameof(IsEmpty));
        Select(list.FirstOrDefault(item => item.Template == keep) ?? list.FirstOrDefault());
    }
}
