using Bannerlord.PartyAI.Domain.Models;
using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Components;

public class PartyAICompositionDisplayVM : ViewModel
{
    private readonly PartyComposition _composition;

    private HintViewModel _infantryHint = null!;

    private HintViewModel _rangedHint = null!;

    private HintViewModel _cavalryHint = null!;

    private HintViewModel _horseArcherHint = null!;

    public PartyAICompositionDisplayVM(PartyComposition composition, int spacing = 0)
        : this()
    {
        _composition = new PartyComposition(composition);
        _composition.Scale(100);

        Spacing = spacing;
    }

    [DataSourceProperty]
    public string InfantryCount => ((int)Math.Round(_composition.Infantry)).ToString() + "%";

    [DataSourceProperty]
    public string RangedCount => ((int)Math.Round(_composition.Ranged)).ToString() + "%";

    [DataSourceProperty]
    public string CavalryCount => ((int)Math.Round(_composition.Cavalry)).ToString() + "%";

    [DataSourceProperty]
    public string HorseArcherCount => ((int)Math.Round(_composition.HorseArcher)).ToString() + "%";

    [DataSourceProperty]
    public HintViewModel InfantryHint
    {
        get
        {
            return _infantryHint;
        }
        set
        {
            if (value != _infantryHint)
            {
                _infantryHint = value;
                OnPropertyChangedWithValue(value, "InfantryHint");
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel RangedHint
    {
        get
        {
            return _rangedHint;
        }
        set
        {
            if (value != _rangedHint)
            {
                _rangedHint = value;
                OnPropertyChangedWithValue(value, "RangedHint");
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel CavalryHint
    {
        get
        {
            return _cavalryHint;
        }
        set
        {
            if (value != _cavalryHint)
            {
                _cavalryHint = value;
                OnPropertyChangedWithValue(value, "CavalryHint");
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel HorseArcherHint
    {
        get
        {
            return _horseArcherHint;
        }
        set
        {
            if (value != _horseArcherHint)
            {
                _horseArcherHint = value;
                OnPropertyChangedWithValue(value, "HorseArcherHint");
            }
        }
    }

    [DataSourceProperty]
    public int Spacing { get; private set; }

    public PartyAICompositionDisplayVM()
    {
        _composition = new PartyComposition();
        InfantryHint = new HintViewModel(new TextObject("{=1Bm1Wk1v}Infantry"));
        RangedHint = new HintViewModel(new TextObject("{=bIiBytSB}Archers"));
        CavalryHint = new HintViewModel(new TextObject("{=YVGtcLHF}Cavalry"));
        HorseArcherHint = new HintViewModel(new TextObject("{=I1CMeL9R}Mounted Archers"));
    }
}
