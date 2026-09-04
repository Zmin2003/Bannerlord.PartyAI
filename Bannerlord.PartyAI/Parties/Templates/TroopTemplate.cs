using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Parties.Templates;

/// <summary>
/// A set of top-tier troops the player wants a party to end up with. Every troop that can
/// upgrade into one of those targets is considered part of the template.
/// </summary>
public class TroopTemplate
{
    private const string BuiltInPrefix = "builtin:";

    [SaveableProperty(1)] public string Name { get; private set; }

    /// <summary>The chosen end-of-line troops (one of each).</summary>
    [SaveableProperty(2)] public TroopRoster UpgradeTargets { get; private set; }

    /// <summary>All troops on any upgrade path leading to <see cref="UpgradeTargets"/>.</summary>
    [SaveableProperty(3)] public List<CharacterObject> Troops { get; internal set; }

    [SaveableProperty(4)] public PartyComposition? RecommendedComposition { get; private set; }

    /// <summary>Stable identifier for generated/imported templates; null for hand-made ones.</summary>
    [SaveableProperty(5)] public string? SourceId { get; private set; }

    private HashSet<CultureObject>? _troopCultures;

    public TroopTemplate(
        string name,
        TroopRoster upgradeTargets,
        PartyComposition? recommendedComposition = null,
        string? sourceId = null)
    {
        Name = name;
        UpgradeTargets = upgradeTargets;
        SourceId = sourceId;
        Troops = ResolveTroops().ToList();

        if (recommendedComposition is not null)
        {
            RecommendedComposition = new PartyComposition(recommendedComposition);
            RecommendedComposition.ApplyTemplate(this);
        }
    }

    public bool IsBuiltIn => SourceId?.StartsWith(BuiltInPrefix) == true;

    public HashSet<CultureObject> TroopCultures
    {
        get
        {
            if (_troopCultures is null || _troopCultures.Count == 0)
            {
                _troopCultures = new HashSet<CultureObject>(
                    (Troops ?? new()).Select(troop => troop.Culture).Where(culture => culture is not null));
            }

            return _troopCultures;
        }
    }

    /// <summary>The troop used as the template's portrait.</summary>
    public CharacterObject? Portrait => Troops?.FirstOrDefault();

    internal IEnumerable<CharacterObject> ResolveTroops()
    {
        List<CharacterObject> targets = UpgradeTargets.GetTroopRoster().Select(element => element.Character).ToList();
        return CharacterObject.All
            .Where(troop => !troop.IsHero
                && troop.Culture?.IsBandit == false
                && targets.Any(target => UpgradesTo(troop, target)))
            .Distinct();
    }

    /// <summary>Rebuilds the troop list after the targets or the manual selection changed.</summary>
    internal void SetTroops(IEnumerable<CharacterObject> troops)
    {
        Troops = troops.Distinct().ToList();
        _troopCultures = null;
    }

    internal static bool UpgradesTo(CharacterObject troop, CharacterObject target)
    {
        if (troop == target)
        {
            return true;
        }

        foreach (CharacterObject next in troop.UpgradeTargets)
        {
            if (next.Tier > troop.Tier && UpgradesTo(next, target))
            {
                return true;
            }
        }

        return false;
    }
}
