using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.UI.Components;

/// <summary>Compact read-only view of a composition: four icon + percentage pairs.</summary>
public sealed class CompositionDisplayVM : ViewModel
{
    private readonly PartyComposition _composition;

    public CompositionDisplayVM(PartyComposition composition)
    {
        _composition = new PartyComposition(composition);
        _composition.Scale(100f);
    }

    [DataSourceProperty] public string InfantryCount => Percent(_composition.Infantry);
    [DataSourceProperty] public string RangedCount => Percent(_composition.Ranged);
    [DataSourceProperty] public string CavalryCount => Percent(_composition.Cavalry);
    [DataSourceProperty] public string HorseArcherCount => Percent(_composition.HorseArcher);

    [DataSourceProperty] public HintViewModel InfantryHint => new(L.T("{=1Bm1Wk1v}Infantry"));
    [DataSourceProperty] public HintViewModel RangedHint => new(L.T("{=bIiBytSB}Archers"));
    [DataSourceProperty] public HintViewModel CavalryHint => new(L.T("{=YVGtcLHF}Cavalry"));
    [DataSourceProperty] public HintViewModel HorseArcherHint => new(L.T("{=I1CMeL9R}Mounted Archers"));

    private static string Percent(float value) => $"{(int)Math.Round(value)}%";
}
