using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Components;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels;

public class PartyAICompositionSlidersVM : ViewModel
{
    private static readonly FormationClass[] FormationClasses =
    [
        FormationClass.Infantry,
        FormationClass.Ranged,
        FormationClass.Cavalry,
        FormationClass.HorseArcher
    ];

    private readonly Action<PartyComposition> _onSavePartyComposition;
    private readonly PartyAiEntitySettings _settings;
    private CompositionSliderRowVM _infantrySliderVm = null!;
    private CompositionSliderRowVM _rangedSliderVm = null!;
    private CompositionSliderRowVM _cavalrySliderVm = null!;
    private CompositionSliderRowVM _horseArcherSliderVm = null!;

    public PartyAICompositionSlidersVM(PartyAiEntitySettings settings, Action<PartyComposition> callback)
    {
        SlidersTitleText = new TextObject("{=PAgaRahFHeV}Edit Party Composition").ToString();

        _settings = settings;
        _onSavePartyComposition = callback;

        PartyComposition composition = new PartyComposition(settings.Composition);
        composition.ApplyTemplate(settings.PartyTemplate, out var formationTypes);
        composition.Scale(100);

        InfantrySliderVm = new CompositionSliderRowVM(
            (int)Math.Round(composition.Infantry),
            formationTypes.Contains(FormationClass.Infantry),
            @"General\TroopTypeIcons\icon_troop_type_infantry");
        RangedSliderVm = new CompositionSliderRowVM(
            (int)Math.Round(composition.Ranged),
            formationTypes.Contains(FormationClass.Ranged),
            @"General\TroopTypeIcons\icon_troop_type_bow");
        CavalrySliderVm = new CompositionSliderRowVM(
            (int)Math.Round(composition.Cavalry),
            formationTypes.Contains(FormationClass.Cavalry),
            @"General\TroopTypeIcons\icon_troop_type_cavalry");
        HorseArcherSliderVm = new CompositionSliderRowVM(
            (int)Math.Round(composition.HorseArcher),
            formationTypes.Contains(FormationClass.HorseArcher),
            @"General\TroopTypeIcons\icon_troop_type_horse_archer");

        InfantrySliderVm.UserChangedValue += HandleUserChangedValue;
        RangedSliderVm.UserChangedValue += HandleUserChangedValue;
        CavalrySliderVm.UserChangedValue += HandleUserChangedValue;
        HorseArcherSliderVm.UserChangedValue += HandleUserChangedValue;

        RefreshValues();
    }

    [DataSourceProperty]
    public string AcceptText => new TextObject("{=bV75iwKa}Save").ToString();

    [DataSourceProperty]
    public string CancelText => GameTexts.FindText("str_cancel").ToString();

    [DataSourceProperty]
    public string SlidersTitleText { get; set; }

    [DataSourceProperty]
    public CompositionSliderRowVM InfantrySliderVm
    {
        get => _infantrySliderVm;
        set
        {
            if (value != _infantrySliderVm)
            {
                _infantrySliderVm = value;
                OnPropertyChangedWithValue(value, nameof(InfantrySliderVm));
            }
        }
    }

    [DataSourceProperty]
    public CompositionSliderRowVM RangedSliderVm
    {
        get => _rangedSliderVm;
        set
        {
            if (value != _rangedSliderVm)
            {
                _rangedSliderVm = value;
                OnPropertyChangedWithValue(value, nameof(RangedSliderVm));
            }
        }
    }

    [DataSourceProperty]
    public CompositionSliderRowVM CavalrySliderVm
    {
        get => _cavalrySliderVm;
        set
        {
            if (value != _cavalrySliderVm)
            {
                _cavalrySliderVm = value;
                OnPropertyChangedWithValue(value, nameof(CavalrySliderVm));
            }
        }
    }

    [DataSourceProperty]
    public CompositionSliderRowVM HorseArcherSliderVm
    {
        get => _horseArcherSliderVm;
        set
        {
            if (value != _horseArcherSliderVm)
            {
                _horseArcherSliderVm = value;
                OnPropertyChangedWithValue(value, nameof(HorseArcherSliderVm));
            }
        }
    }

    public void AcceptEditPartyComposition()
    {
        PartyComposition composition = new()
        {
            Infantry = InfantrySliderVm.Value,
            Ranged = RangedSliderVm.Value,
            Cavalry = CavalrySliderVm.Value,
            HorseArcher = HorseArcherSliderVm.Value
        };
        composition.Scale(0.01f);

        _onSavePartyComposition.Invoke(composition);
    }

    public void CancelEditPartyComposition()
    {
        _onSavePartyComposition.Invoke(new PartyComposition(_settings.Composition));
    }

    private void HandleUserChangedValue(CompositionSliderRowVM sender)
    {
        var formationClass = sender switch
        {
            var s when ReferenceEquals(s, InfantrySliderVm) => FormationClass.Infantry,
            var s when ReferenceEquals(s, RangedSliderVm) => FormationClass.Ranged,
            var s when ReferenceEquals(s, CavalrySliderVm) => FormationClass.Cavalry,
            var s when ReferenceEquals(s, HorseArcherSliderVm) => FormationClass.HorseArcher,
            _ => FormationClass.Infantry
        };

        ClampTo100(formationClass);
    }

    private void ClampTo100(FormationClass changedType)
    {
        int total = FormationClasses.Sum(GetValue);
        if (total <= 100)
        {
            return;
        }

        int[] values = FormationClasses.Select(GetValue).ToArray();
        bool[] locked = FormationClasses.Select(GetLocked).ToArray();

        int excess = total - 100;

        foreach (bool allowMain in new[] { false, true })
        {
            while (excess > 0)
            {
                bool actionTaken = false;
                foreach (FormationClass type in FormationClasses)
                {
                    if (!allowMain && type == changedType)
                    {
                        continue;
                    }

                    if (locked[(int)type])
                    {
                        continue;
                    }

                    if (values[(int)type] <= 0)
                    {
                        continue;
                    }

                    values[(int)type]--;
                    excess--;
                    actionTaken = true;

                    if (excess == 0)
                    {
                        break;
                    }
                }

                if (!actionTaken)
                {
                    break;
                }
            }

            if (excess == 0)
            {
                break;
            }
        }

        foreach (FormationClass type in FormationClasses)
        {
            SetValueSilently(type, values[(int)type]);
        }
    }

    private int GetValue(FormationClass type)
    {
        return type switch
        {
            FormationClass.Infantry => InfantrySliderVm.Value,
            FormationClass.Ranged => RangedSliderVm.Value,
            FormationClass.Cavalry => CavalrySliderVm.Value,
            FormationClass.HorseArcher => HorseArcherSliderVm.Value,
            _ => 0,
        };
    }

    private void SetValueSilently(FormationClass type, int value)
    {
        switch (type)
        {
            case FormationClass.Infantry: InfantrySliderVm.SetValueSilently(value); break;
            case FormationClass.Ranged: RangedSliderVm.SetValueSilently(value); break;
            case FormationClass.Cavalry: CavalrySliderVm.SetValueSilently(value); break;
            case FormationClass.HorseArcher: HorseArcherSliderVm.SetValueSilently(value); break;
        }
    }

    private bool GetLocked(FormationClass type)
    {
        return type switch
        {
            FormationClass.Infantry => InfantrySliderVm.IsLocked,
            FormationClass.Ranged => RangedSliderVm.IsLocked,
            FormationClass.Cavalry => CavalrySliderVm.IsLocked,
            FormationClass.HorseArcher => HorseArcherSliderVm.IsLocked,
            _ => false,
        };
    }
}
