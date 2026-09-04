using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.UI.Components;

/// <summary>One formation slider of the composition editor.</summary>
public sealed class CompositionSliderVM : ViewModel
{
    private readonly Action<CompositionSliderVM> _onUserChange;
    private bool _suppress;
    private int _value;
    private bool _isLocked;

    public CompositionSliderVM(FormationClass formation, string icon, string name, int value, bool available, Action<CompositionSliderVM> onUserChange)
    {
        Formation = formation;
        Icon = icon;
        Name = name;
        IsAvailable = available;
        _isLocked = !available;
        _value = value;
        _onUserChange = onUserChange;
        LockHint = new HintViewModel(available
            ? L.T("{=PAI_LOCK_FORMATION_HINT}Lock this formation so other sliders cannot change it.")
            : L.T("{=PAI_locked_formation}This formation class is not used by this party's template."));
    }

    public FormationClass Formation { get; }

    [DataSourceProperty] public string Icon { get; }
    [DataSourceProperty] public string Name { get; }
    [DataSourceProperty] public bool IsAvailable { get; }
    [DataSourceProperty] public HintViewModel LockHint { get; }
    [DataSourceProperty] public string Percentage => $"{_value}%";

    [DataSourceProperty]
    public int Value
    {
        get => _value;
        set
        {
            if (value == _value)
            {
                return;
            }

            _value = value;
            OnPropertyChangedWithValue(value, nameof(Value));
            OnPropertyChanged(nameof(Percentage));
            if (!_suppress)
            {
                _onUserChange(this);
            }
        }
    }

    [DataSourceProperty]
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (value != _isLocked && IsAvailable)
            {
                _isLocked = value;
                OnPropertyChangedWithValue(value, nameof(IsLocked));
                OnPropertyChanged(nameof(IsSliderEnabled));
            }
        }
    }

    [DataSourceProperty] public bool IsSliderEnabled => IsAvailable && !_isLocked;

    internal void SetSilently(int value)
    {
        _suppress = true;
        Value = value;
        _suppress = false;
    }
}

/// <summary>
/// Four sliders that always sum to 100%. Moving one slider takes the difference from the other
/// unlocked sliders. Writes straight into the profile's <see cref="PartyComposition"/>.
/// </summary>
public sealed class CompositionEditorVM : ViewModel
{
    private readonly PartyProfile _profile;
    private readonly Action? _onChanged;

    public CompositionEditorVM(PartyProfile profile, Action? onChanged = null)
    {
        _profile = profile;
        _onChanged = onChanged;

        var composition = new PartyComposition(profile.Composition);
        FormationClass[] available = composition.ApplyTemplate(profile.Template);
        composition.Scale(100f);

        Infantry = Create(FormationClass.Infantry, @"General\TroopTypeIcons\icon_troop_type_infantry", L.S("{=1Bm1Wk1v}Infantry"), composition, available);
        Ranged = Create(FormationClass.Ranged, @"General\TroopTypeIcons\icon_troop_type_bow", L.S("{=bIiBytSB}Archers"), composition, available);
        Cavalry = Create(FormationClass.Cavalry, @"General\TroopTypeIcons\icon_troop_type_cavalry", L.S("{=YVGtcLHF}Cavalry"), composition, available);
        HorseArcher = Create(FormationClass.HorseArcher, @"General\TroopTypeIcons\icon_troop_type_horse_archer", L.S("{=I1CMeL9R}Mounted Archers"), composition, available);
    }

    [DataSourceProperty] public CompositionSliderVM Infantry { get; }
    [DataSourceProperty] public CompositionSliderVM Ranged { get; }
    [DataSourceProperty] public CompositionSliderVM Cavalry { get; }
    [DataSourceProperty] public CompositionSliderVM HorseArcher { get; }
    [DataSourceProperty] public string Title => L.S("{=PAI42PrfM04}Party Composition");
    [DataSourceProperty] public HintViewModel TitleHint => new(L.T("{=PAI_COMPOSITION_HINT}Target share of each troop type. Recruitment, upgrades and conversion all steer towards these percentages."));

    private CompositionSliderVM[] Sliders => [Infantry, Ranged, Cavalry, HorseArcher];

    private CompositionSliderVM Create(FormationClass formation, string icon, string name, PartyComposition composition, FormationClass[] available)
        => new(formation, icon, name, (int)Math.Round(composition[formation]), available.Contains(formation), OnUserChanged);

    private void OnUserChanged(CompositionSliderVM changed)
    {
        Rebalance(changed);
        Commit();
    }

    /// <summary>Removes any excess over 100% from the other unlocked sliders, then from the changed one.</summary>
    private void Rebalance(CompositionSliderVM changed)
    {
        int excess = Sliders.Sum(slider => slider.Value) - 100;
        if (excess <= 0)
        {
            return;
        }

        foreach (bool includeChanged in new[] { false, true })
        {
            while (excess > 0)
            {
                bool tookSome = false;
                foreach (CompositionSliderVM slider in Sliders)
                {
                    if ((!includeChanged && slider == changed) || slider.IsLocked || slider.Value <= 0)
                    {
                        continue;
                    }

                    slider.SetSilently(slider.Value - 1);
                    excess--;
                    tookSome = true;
                    if (excess == 0)
                    {
                        break;
                    }
                }

                if (!tookSome)
                {
                    break;
                }
            }

            if (excess == 0)
            {
                break;
            }
        }
    }

    private void Commit()
    {
        var composition = new PartyComposition(Infantry.Value, Ranged.Value, Cavalry.Value, HorseArcher.Value);
        composition.Scale(0.01f);
        _profile.Composition = composition;
        _onChanged?.Invoke();
    }
}
