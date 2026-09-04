using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Finance;

/// <summary>
/// Scores workshop types for a town the way the vanilla AI does when it opens a workshop:
/// raw-material supply from the town's bound villages, cheap inputs on the local market,
/// well-priced outputs, and a penalty for competing workshops already in town.
/// </summary>
public static class WorkshopAdvisor
{
    public readonly record struct Candidate(WorkshopType Type, float Score);

    /// <summary>All visible workshop types for <paramref name="town"/>, best first.</summary>
    public static List<Candidate> Rank(Town town)
    {
        Dictionary<ItemCategory, float> supply = VillageSupply(town.Settlement);
        return WorkshopType.All
            .Where(type => !type.IsHidden)
            .Select(type => new Candidate(type, Score(town, type, supply)))
            .OrderByDescending(candidate => candidate.Score)
            .ToList();
    }

    /// <summary>The best type for the town other than <paramref name="exclude"/>, if any.</summary>
    public static Candidate? Best(Town town, WorkshopType? exclude = null)
    {
        var ranked = Rank(town).Where(candidate => candidate.Type != exclude).ToList();
        return ranked.Count == 0 ? null : ranked[0];
    }

    public static float Score(Town town, WorkshopType type)
        => Score(town, type, VillageSupply(town.Settlement));

    public readonly record struct Purchase(Workshop Workshop, float Score, int Cost);

    /// <summary>
    /// The most promising notable-owned workshop the player could buy in <paramref name="towns"/>:
    /// its current production must rank near the top for its town, and richer towns count for more.
    /// </summary>
    public static Purchase? BestPurchase(IEnumerable<Town> towns)
    {
        Purchase? best = null;
        foreach (Town town in towns)
        {
            List<Candidate> ranked = Rank(town);
            if (ranked.Count == 0)
            {
                continue;
            }

            float top = ranked[0].Score;
            float prosperityFactor = Math.Min(1.5f, 0.5f + town.Prosperity / 4000f);
            foreach (Workshop workshop in town.Workshops)
            {
                if (workshop.WorkshopType is null
                    || workshop.WorkshopType.IsHidden
                    || workshop.Owner is null
                    || workshop.Owner == Hero.MainHero
                    || !workshop.Owner.IsNotable)
                {
                    continue;
                }

                float score = ranked.FirstOrDefault(candidate => candidate.Type == workshop.WorkshopType).Score;
                if (score < top * 0.75f)
                {
                    continue;
                }

                score *= prosperityFactor;
                if (best is null || score > best.Value.Score)
                {
                    best = new Purchase(workshop, score, Campaign.Current.Models.WorkshopModel.GetCostForPlayer(workshop));
                }
            }
        }

        return best;
    }

    private static float Score(Town town, WorkshopType type, Dictionary<ItemCategory, float> supply)
    {
        float competition = 0f;
        foreach (Workshop other in town.Workshops)
        {
            if (other.WorkshopType is null || other.WorkshopType.IsHidden)
            {
                continue;
            }

            if (other.WorkshopType == type)
            {
                competition += 1f;
            }
            else if (SharesCategory(type, other.WorkshopType))
            {
                competition += 0.5f;
            }
        }

        float inputScore = 0.01f;
        float inputPriceBonus = 0f;
        float outputPriceBonus = 0f;
        int outputs = 0;

        foreach (WorkshopType.Production production in type.Productions)
        {
            if (!production.Outputs.Any(output => output.Item1.IsTradeGood))
            {
                continue;
            }

            foreach ((ItemCategory category, int count) in production.Inputs)
            {
                if (supply.TryGetValue(category, out float amount))
                {
                    inputScore += amount / (production.ConversionSpeed * count);
                }

                inputPriceBonus += Math.Max(0f, 1f - town.MarketData.GetPriceFactor(category));
            }

            foreach ((ItemCategory category, int _) in production.Outputs)
            {
                outputPriceBonus += town.MarketData.GetPriceFactor(category);
                outputs++;
            }
        }

        float crowding = 1f + competition * 6f;
        float density = inputScore * type.Frequency / (float)Math.Pow(crowding, 3.0) + inputPriceBonus;
        float demand = outputs > 0 ? outputPriceBonus / outputs : 1f;

        return (float)Math.Pow(density, 0.6) * (0.5f + 0.5f * Math.Min(2f, demand));
    }

    /// <summary>Daily raw-material output of the villages that trade with the town, by category.</summary>
    private static Dictionary<ItemCategory, float> VillageSupply(Settlement town)
    {
        var supply = new Dictionary<ItemCategory, float>();
        foreach (Village village in Village.All.Where(village => village.TradeBound == town))
        {
            foreach ((ItemObject item, float amount) in village.VillageType.Productions)
            {
                ItemCategory category = item.ItemCategory;
                if (category == DefaultItemCategories.Grain && village.VillageType.PrimaryProduction != DefaultItems.Grain)
                {
                    continue;
                }

                if (category == DefaultItemCategories.Cow)
                {
                    category = DefaultItemCategories.Hides;
                }
                else if (category == DefaultItemCategories.Sheep)
                {
                    category = DefaultItemCategories.Wool;
                }

                supply[category] = supply.TryGetValue(category, out float existing) ? existing + amount : amount;
            }
        }

        return supply;
    }

    private static bool SharesCategory(WorkshopType a, WorkshopType b)
    {
        var inputsA = new HashSet<ItemCategory>(a.Productions.SelectMany(production => production.Inputs.Select(input => input.Item1)));
        var outputsA = new HashSet<ItemCategory>(a.Productions.SelectMany(production => production.Outputs.Select(output => output.Item1)));
        return b.Productions.Any(production =>
            production.Inputs.Any(input => inputsA.Contains(input.Item1))
            || production.Outputs.Any(output => outputsA.Contains(output.Item1)));
    }
}
