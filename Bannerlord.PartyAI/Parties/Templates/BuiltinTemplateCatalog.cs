using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties.Recruitment;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Parties.Templates;

/// <summary>
/// Generates the built-in templates from live game data: one "strongest overall" and one per main
/// culture. Rankings use tier and computed battle power, never localized names.
/// </summary>
internal static class BuiltinTemplateCatalog
{
    private const string Version = "v1.5.2";
    private const string StrongestId = "builtin:" + Version + ":strongest";

    public static string CultureSourceId(CultureObject culture) => $"builtin:{Version}:culture:{culture.StringId}";

    public static void EnsureBuiltInTemplates(PartyRegistry registry)
    {
        List<CharacterObject> candidates = RecruitmentRules.AllTopTierTroops()
            .GetTroopRoster()
            .Select(element => element.Character)
            .Where(IsEligible)
            .Distinct()
            .ToList();

        Add(registry, StrongestId,
            L.S("{=PAI_BUILTIN_STRONGEST}Built-in: Strongest v1.5.2"),
            BestPerFormation(candidates),
            new PartyComposition(0.25f, 0.25f, 0.25f, 0.25f));

        foreach (IGrouping<CultureObject, CharacterObject> group in candidates
            .Where(character => character.Culture?.IsMainCulture == true)
            .GroupBy(character => character.Culture)
            .OrderBy(group => group.Key.StringId))
        {
            TextObject name = L.T("{=PAI_BUILTIN_CULTURE}Built-in: {CULTURE} optimal", "CULTURE", group.Key.Name);
            Add(registry, CultureSourceId(group.Key), name.ToString(), BestPerFormation(group), RecommendedComposition(group.Key.StringId));
        }
    }

    private static bool IsEligible(CharacterObject character)
        => character is { IsHero: false, Culture: { IsMainCulture: true, IsBandit: false }, Occupation: Occupation.Soldier }
            && character.UpgradeTargets.Length == 0;

    private static List<CharacterObject> BestPerFormation(IEnumerable<CharacterObject> candidates)
        => candidates
            .GroupBy(character => character.DefaultFormationClass.FallbackClass())
            .Where(group => PartyComposition.Formations.Contains(group.Key))
            .Select(group => group
                .OrderByDescending(character => character.Tier)
                .ThenByDescending(BattlePower)
                .ThenBy(character => character.StringId)
                .First())
            .ToList();

    private static float BattlePower(CharacterObject character)
    {
        try
        {
            return character.GetBattlePower();
        }
        catch
        {
            return character.GetPower();
        }
    }

    private static PartyComposition RecommendedComposition(string cultureId) => cultureId switch
    {
        "vlandia" => new PartyComposition(0.30f, 0.30f, 0.40f, 0f),
        "sturgia" => new PartyComposition(0.50f, 0.25f, 0.25f, 0f),
        "empire" => new PartyComposition(0.35f, 0.30f, 0.25f, 0.10f),
        "aserai" => new PartyComposition(0.30f, 0.30f, 0.20f, 0.20f),
        "khuzait" => new PartyComposition(0.20f, 0.10f, 0.25f, 0.45f),
        "battania" => new PartyComposition(0.25f, 0.55f, 0.20f, 0f),
        _ => PartyComposition.Default
    };

    private static void Add(
        PartyRegistry registry,
        string sourceId,
        string name,
        IReadOnlyCollection<CharacterObject> targets,
        PartyComposition composition)
    {
        if (targets.Count == 0 || registry.FindTemplateBySource(sourceId) is not null)
        {
            return;
        }

        TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
        foreach (CharacterObject target in targets)
        {
            roster.AddToCounts(target, 1);
        }

        registry.CreateTemplate(name, roster, composition, sourceId);
    }
}
