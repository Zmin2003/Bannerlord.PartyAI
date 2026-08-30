using Bannerlord.PartyAI.CampaignBehaviors;
using Bannerlord.PartyAI.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Domain;

internal static class BuiltinTemplateCatalog
{
    private const string StrongestTemplateId = "builtin:v1.5.2:strongest";

    internal static void EnsureBuiltInTemplates(PartyAIClanPartySettingsManager manager)
    {
        List<CharacterObject> candidates = Recruitment.GetAllTopTierTroops()
            .GetTroopRoster()
            .Select(element => element.Character)
            .Where(IsEligible)
            .Distinct()
            .ToList();

        AddTemplate(
            manager,
            StrongestTemplateId,
            new TextObject("{=PAI_BUILTIN_STRONGEST}Built-in: Strongest v1.5.2").ToString(),
            SelectBestByFormation(candidates),
            new PartyComposition(0.25f, 0.25f, 0.25f, 0.25f));

        foreach (IGrouping<CultureObject, CharacterObject> cultureGroup in candidates
            .Where(character => character.Culture?.IsMainCulture == true)
            .GroupBy(character => character.Culture)
            .OrderBy(group => group.Key.StringId))
        {
            string sourceId = $"builtin:v1.5.2:culture:{cultureGroup.Key.StringId}";
            TextObject name = new("{=PAI_BUILTIN_CULTURE}Built-in: {CULTURE} optimal");
            name.SetTextVariable("CULTURE", cultureGroup.Key.Name);

            AddTemplate(
                manager,
                sourceId,
                name.ToString(),
                SelectBestByFormation(cultureGroup),
                RecommendedComposition(cultureGroup.Key.StringId));
        }
    }

    private static bool IsEligible(CharacterObject character)
    {
        return character is not null
            && !character.IsHero
            && character.Culture is not null
            && character.Culture.IsMainCulture
            && !character.Culture.IsBandit
            && character.Occupation == Occupation.Soldier
            && character.UpgradeTargets.Length == 0;
    }

    private static List<CharacterObject> SelectBestByFormation(IEnumerable<CharacterObject> candidates)
    {
        return candidates
            .GroupBy(character => character.DefaultFormationClass.FallbackClass())
            .Where(group => group.Key is FormationClass.Infantry
                or FormationClass.Ranged
                or FormationClass.Cavalry
                or FormationClass.HorseArcher)
            .Select(group => group
                .OrderByDescending(character => character.Tier)
                .ThenByDescending(BattlePower)
                .ThenBy(character => character.StringId)
                .First())
            .ToList();
    }

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

    private static PartyComposition RecommendedComposition(string cultureId)
    {
        return cultureId switch
        {
            "vlandia" => new PartyComposition(0.30f, 0.30f, 0.40f, 0f),
            "sturgia" => new PartyComposition(0.50f, 0.25f, 0.25f, 0f),
            "empire" => new PartyComposition(0.35f, 0.30f, 0.25f, 0.10f),
            "aserai" => new PartyComposition(0.30f, 0.30f, 0.20f, 0.20f),
            "khuzait" => new PartyComposition(0.20f, 0.10f, 0.25f, 0.45f),
            "battania" => new PartyComposition(0.25f, 0.55f, 0.20f, 0f),
            _ => new PartyComposition(0.35f, 0.30f, 0.20f, 0.15f)
        };
    }

    private static void AddTemplate(
        PartyAIClanPartySettingsManager manager,
        string sourceId,
        string name,
        IReadOnlyCollection<CharacterObject> targets,
        PartyComposition composition)
    {
        if (targets.Count == 0
            || manager.AllTemplates.Any(template => template.SourceId == sourceId))
        {
            return;
        }

        TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
        foreach (CharacterObject target in targets)
        {
            roster.AddToCounts(target, 1);
        }

        _ = new PAICustomTemplate(name, roster, composition, sourceId);
    }
}
