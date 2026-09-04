using Bannerlord.PartyAI.Parties.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Parties.Recruitment;

/// <summary>A volunteer slot at a notable that a party could recruit.</summary>
public sealed record NotableVolunteer(Hero Notable, CharacterObject Troop, int Index);

/// <summary>
/// Pure rules answering "should this party recruit / upgrade towards this troop?" based on the
/// party's template and composition targets.
/// </summary>
public static class RecruitmentRules
{
    private static readonly Dictionary<CharacterObject, List<CharacterObject>> UpgradeTreeCache = new();
    private static readonly Dictionary<CultureObject, List<CharacterObject>> EliteTreeCache = new();

    // ---- Decisions ---------------------------------------------------------------------------

    /// <summary>
    /// Whether recruiting <paramref name="troop"/> moves the party towards its composition targets.
    /// With <paramref name="allowConversionFallback"/>, any troop is acceptable when conversion will fix it later.
    /// </summary>
    public static bool ShouldRecruit(
        PartyComposition current,
        PartyProfile profile,
        CharacterObject troop,
        PartyBase party,
        bool mustBeOnePlus = true,
        bool allowConversionFallback = false)
    {
        if (allowConversionFallback && PartyAi.Parties.AllowsConversion(profile))
        {
            return true;
        }

        if (IsOverMaxTier(troop, profile.MaxTroopTier))
        {
            return false;
        }

        FormationClass[] formations = FinalFormations(troop, profile.Template);
        if (formations.Length == 0)
        {
            return false;
        }

        float threshold = mustBeOnePlus ? 1f : 0.4f;
        return formations.Any(formation =>
            (profile.Composition[formation] - current[formation]) * party.PartySizeLimit >= threshold);
    }

    /// <summary>Higher is better: prefers troops filling the largest composition deficit.</summary>
    public static float RecruitmentPriority(PartyComposition current, PartyProfile profile, CharacterObject troop)
    {
        FormationClass[] formations = FinalFormations(troop, profile.Template);
        if (formations.Length == 0)
        {
            return float.MinValue;
        }

        float largestDeficit = formations.Max(formation => profile.Composition[formation] - current[formation]);
        float templateBonus = profile.Template?.Troops.Contains(troop) == true ? 1f : 0f;
        return largestDeficit * 1000f + templateBonus * 100f + troop.Tier;
    }

    public static bool IsOverMaxTier(CharacterObject? troop, int maxTier)
        => maxTier > 0 && troop?.Tier > maxTier;

    // ---- Composition -----------------------------------------------------------------------

    /// <summary>
    /// The party's current composition as fractions of its size limit, counting each troop by the
    /// formation class it will end up in after upgrading along the template.
    /// </summary>
    public static PartyComposition GetPartyComposition(PartyBase party, PartyProfile profile, CharacterObject? ignore = null)
    {
        var result = new PartyComposition();
        float limit = party.PartySizeLimit;
        if (limit <= 0)
        {
            return result;
        }

        foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster())
        {
            CharacterObject character = element.Character;
            if (character.IsHero || character == ignore)
            {
                continue;
            }

            FormationClass[] formations = FinalFormations(character, profile.Template);
            FormationClass own = character.DefaultFormationClass.FallbackClass();

            if (formations.Length == 0)
            {
                result[own] += element.Number;
                continue;
            }

            if (formations.Length == 1)
            {
                result[formations[0]] += element.Number;
                continue;
            }

            // Troops that can branch into several classes are assigned to whichever class still needs men.
            int remaining = element.Number;
            foreach (FormationClass formation in formations)
            {
                while (remaining > 0
                    && (profile.Composition[formation] - result[formation] / limit) * limit >= 1f)
                {
                    result[formation] += 1f;
                    remaining--;
                }

                if (remaining == 0)
                {
                    break;
                }
            }

            if (remaining > 0)
            {
                result[own] += remaining;
            }
        }

        result.Scale(1f / limit);
        return result;
    }

    // ---- Upgrade trees ---------------------------------------------------------------------

    /// <summary>
    /// Everything <paramref name="troop"/> can upgrade into (including itself), restricted to the
    /// template. With <paramref name="finalOnly"/>, only troops with no further in-template upgrade.
    /// </summary>
    public static List<CharacterObject> UpgradeTargets(CharacterObject? troop, bool finalOnly = false, TroopTemplate? template = null)
    {
        if (troop is null)
        {
            return new List<CharacterObject>();
        }

        if (!UpgradeTreeCache.TryGetValue(troop, out List<CharacterObject>? tree))
        {
            tree = TraverseUpgradeTree(troop);
            UpgradeTreeCache[troop] = tree;
        }

        IEnumerable<CharacterObject> targets = tree.Where(character => IsInTemplate(character, template));
        if (finalOnly)
        {
            targets = targets.Where(character => !character.UpgradeTargets.Any(next => IsInTemplate(next, template)));
        }

        return targets.ToList();
    }

    /// <summary>Distinct formation classes of a troop's final in-template upgrades.</summary>
    public static FormationClass[] FinalFormations(CharacterObject troop, TroopTemplate? template)
        => UpgradeTargets(troop, finalOnly: true, template)
            .Select(target => target.DefaultFormationClass.FallbackClass())
            .Distinct()
            .ToArray();

    public static bool IsEliteTroop(CharacterObject? troop)
    {
        CultureObject? culture = troop?.Culture;
        if (culture?.EliteBasicTroop is null)
        {
            return false;
        }

        if (!EliteTreeCache.TryGetValue(culture, out List<CharacterObject>? tree))
        {
            tree = TraverseUpgradeTree(culture.EliteBasicTroop);
            EliteTreeCache[culture] = tree;
        }

        return tree.Contains(troop!);
    }

    /// <summary>Every end-of-line regular troop in the game, one of each, for the template editor.</summary>
    public static TroopRoster AllTopTierTroops()
    {
        Occupation[] occupations = [Occupation.Soldier, Occupation.Mercenary, Occupation.CaravanGuard];
        EncyclopediaPage page = Campaign.Current.EncyclopediaManager.GetPageOf(typeof(CharacterObject));

        IEnumerable<CharacterObject> topTier = CharacterObject.All
            .Where(troop => !troop.IsHero
                && troop.Culture is { IsBandit: false }
                && occupations.Contains(troop.Occupation))
            .SelectMany(TraverseUpgradeTree)
            .Where(troop => troop.UpgradeTargets.Length == 0)
            .Distinct()
            .Where(page.IsValidEncyclopediaItem)
            .OrderBy(troop => troop.Culture?.StringId);

        TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
        foreach (CharacterObject troop in topTier)
        {
            roster.AddToCounts(troop, 1);
        }

        return roster;
    }

    private static List<CharacterObject> TraverseUpgradeTree(CharacterObject root)
    {
        var visited = new List<CharacterObject> { root };
        var stack = new Stack<CharacterObject>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            CharacterObject current = stack.Pop();
            foreach (CharacterObject next in current.UpgradeTargets ?? Array.Empty<CharacterObject>())
            {
                if (!visited.Contains(next))
                {
                    visited.Add(next);
                    stack.Push(next);
                }
            }
        }

        return visited;
    }

    private static bool IsInTemplate(CharacterObject character, TroopTemplate? template)
        => template?.Troops is null || template.Troops.Contains(character);

    // ---- Volunteers ------------------------------------------------------------------------

    public static List<NotableVolunteer> CollectEligibleVolunteers(
        MobileParty party,
        Settlement settlement,
        PartyProfile profile,
        PartyComposition current)
    {
        var volunteers = new List<NotableVolunteer>();
        Hero? buyer = Buyer(party);
        if (buyer is null)
        {
            return volunteers;
        }

        foreach (Hero notable in settlement.Notables)
        {
            if (!notable.IsAlive)
            {
                continue;
            }

            int maxIndex = Math.Min(
                Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(buyer, notable),
                notable.VolunteerTypes.Length - 1);

            for (int index = 0; index <= maxIndex; index++)
            {
                CharacterObject? troop = notable.VolunteerTypes[index];
                if (troop is not null
                    && CanAffordVolunteer(party, troop, buyer)
                    && ShouldRecruit(current, profile, troop, party.Party, allowConversionFallback: true))
                {
                    volunteers.Add(new NotableVolunteer(notable, troop, index));
                }
            }
        }

        return volunteers;
    }

    public static bool CanAffordVolunteer(MobileParty party, CharacterObject troop, Hero? buyer = null)
    {
        buyer ??= Buyer(party);
        if (buyer is null)
        {
            return false;
        }

        int cost = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, buyer).RoundedResultNumber;
        int wage = Campaign.Current.Models.PartyWageModel.GetCharacterWage(troop);

        // The player's own purse stops at the treasury reserve; AI leaders spend their own gold.
        int buyerGold = buyer == Hero.MainHero ? Finance.Treasury.Spendable : buyer.Gold;
        int gold = party == MobileParty.MainParty ? buyerGold : Math.Min(buyerGold, party.PartyTradeGold);

        return gold >= cost && party.GetAvailableWageBudget() >= wage;
    }

    private static Hero? Buyer(MobileParty party) => party.IsGarrison ? party.Party.Owner : party.LeaderHero;
}
