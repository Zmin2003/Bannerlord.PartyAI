using Bannerlord.PartyAI.Parties.Templates;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Parties;

/// <summary>
/// Desired share of each formation class in a party, as fractions that normally sum to one.
/// </summary>
public class PartyComposition
{
    public static readonly FormationClass[] Formations =
    [
        FormationClass.Infantry,
        FormationClass.Ranged,
        FormationClass.Cavalry,
        FormationClass.HorseArcher
    ];

    public static PartyComposition Default => new(0.35f, 0.30f, 0.20f, 0.15f);

    [SaveableProperty(1)] public float Infantry { get; set; }
    [SaveableProperty(2)] public float Ranged { get; set; }
    [SaveableProperty(3)] public float Cavalry { get; set; }
    [SaveableProperty(4)] public float HorseArcher { get; set; }

    public PartyComposition(float infantry, float ranged, float cavalry, float horseArcher)
    {
        Infantry = infantry;
        Ranged = ranged;
        Cavalry = cavalry;
        HorseArcher = horseArcher;
    }

    public PartyComposition() : this(0, 0, 0, 0)
    {
    }

    public PartyComposition(PartyComposition original)
        : this(original.Infantry, original.Ranged, original.Cavalry, original.HorseArcher)
    {
    }

    public float this[FormationClass formation]
    {
        get => formation switch
        {
            FormationClass.Infantry => Infantry,
            FormationClass.Ranged => Ranged,
            FormationClass.Cavalry => Cavalry,
            FormationClass.HorseArcher => HorseArcher,
            _ => 0,
        };
        set
        {
            switch (formation)
            {
                case FormationClass.Infantry: Infantry = value; break;
                case FormationClass.Ranged: Ranged = value; break;
                case FormationClass.Cavalry: Cavalry = value; break;
                case FormationClass.HorseArcher: HorseArcher = value; break;
            }
        }
    }

    public float Total => Infantry + Ranged + Cavalry + HorseArcher;

    public void Scale(float scalar)
    {
        Infantry *= scalar;
        Ranged *= scalar;
        Cavalry *= scalar;
        HorseArcher *= scalar;
    }

    /// <summary>
    /// Zeroes formations the template cannot produce and renormalizes the rest to sum to one.
    /// Returns the formation classes the template can produce (all four when no template).
    /// </summary>
    public FormationClass[] ApplyTemplate(TroopTemplate? template)
    {
        if (template is null)
        {
            return Formations;
        }

        FormationClass[] available = template.UpgradeTargets
            .GetTroopRoster()
            .Select(element => element.Character.DefaultFormationClass.FallbackClass())
            .Distinct()
            .ToArray();

        if (available.Length == 0)
        {
            return Formations;
        }

        foreach (FormationClass formation in Formations)
        {
            if (!available.Contains(formation))
            {
                this[formation] = 0;
            }
        }

        float total = Total;
        if (total <= 0f)
        {
            float share = 1f / available.Length;
            foreach (FormationClass formation in available)
            {
                this[formation] = share;
            }
        }
        else
        {
            Scale(1f / total);
        }

        return available;
    }
}
