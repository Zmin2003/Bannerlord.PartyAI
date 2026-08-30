using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Domain;

internal static class PlayerTroopUpgrader
{
    internal static int UpgradeReadyTroops()
    {
        PartyBase party = PartyBase.MainParty;
        if (party?.MemberRoster is null || Hero.MainHero is null)
        {
            return 0;
        }

        List<CharacterObject> troops = party.MemberRoster
            .GetTroopRoster()
            .Where(element => !element.Character.IsHero)
            .Select(element => element.Character)
            .ToList();

        int upgraded = 0;
        foreach (CharacterObject troop in troops)
        {
            int rosterIndex = party.MemberRoster.FindIndexOfTroop(troop);
            if (rosterIndex < 0 || troop.UpgradeTargets.Length == 0)
            {
                continue;
            }

            TroopRosterElement element = party.MemberRoster.GetElementCopyAtIndex(rosterIndex);
            UpgradeOption? option = BestUpgrade(party, troop, element);
            if (!option.HasValue)
            {
                continue;
            }

            int count = option.Value.Count;
            if (count <= 0)
            {
                continue;
            }

            if (option.Value.RequiredItemCategory is not null)
            {
                RemoveRequiredItems(party, option.Value.RequiredItemCategory, count);
            }

            party.MemberRoster.SetElementXp(
                rosterIndex,
                element.Xp - option.Value.XpCost * count);
            party.MemberRoster.AddToCounts(troop, -count);
            party.MemberRoster.AddToCounts(option.Value.Target, count);

            if (option.Value.GoldCost > 0)
            {
                GiveGoldAction.ApplyBetweenCharacters(
                    Hero.MainHero,
                    null,
                    option.Value.GoldCost * count,
                    disableNotification: true);
            }

            CampaignEventDispatcher.Instance.OnPlayerUpgradedTroops(
                troop,
                option.Value.Target,
                count);
            upgraded += count;
        }

        party.MemberRoster.RemoveZeroCounts();
        return upgraded;
    }

    private static UpgradeOption? BestUpgrade(
        PartyBase party,
        CharacterObject troop,
        TroopRosterElement element)
    {
        var model = Campaign.Current.Models.PartyTroopUpgradeModel;
        List<UpgradeOption> options = new();
        if (!model.IsTroopUpgradeable(party, troop))
        {
            return null;
        }

        for (int index = 0; index < troop.UpgradeTargets.Length; index++)
        {
            CharacterObject target = troop.UpgradeTargets[index];
            float chance = model.GetUpgradeChanceForTroopUpgrade(party, troop, index);
            if (chance <= 0f
                || !model.CanPartyUpgradeTroopToTarget(party, troop, target)
                || !model.DoesPartyHaveRequiredItemsForUpgrade(party, target)
                || !model.DoesPartyHaveRequiredPerksForUpgrade(party, troop, target, out PerkObject _))
            {
                continue;
            }

            int xpCost = model.GetXpCostForUpgrade(party, troop, target);
            int goldCost = model.GetGoldCostForUpgrade(party, troop, target).RoundedResultNumber;
            int count = element.Number - element.WoundedNumber;
            if (xpCost > 0)
            {
                count = Math.Min(count, element.Xp / xpCost);
            }
            if (goldCost > 0)
            {
                count = Math.Min(count, Hero.MainHero.Gold / goldCost);
            }
            if (target.UpgradeRequiresItemFromCategory is not null)
            {
                count = Math.Min(
                    count,
                    CountRequiredItems(party, target.UpgradeRequiresItemFromCategory));
            }
            if (count <= 0)
            {
                continue;
            }

            options.Add(new UpgradeOption(
                target,
                xpCost,
                goldCost,
                chance,
                target.UpgradeRequiresItemFromCategory,
                count));
        }

        return options
            .OrderByDescending(option => option.Chance)
            .ThenByDescending(option => option.Target.Tier)
            .ThenByDescending(option => option.Target.GetBattlePower())
            .Select(option => (UpgradeOption?)option)
            .FirstOrDefault();
    }

    private static int CountRequiredItems(PartyBase party, ItemCategory category)
    {
        return party.ItemRoster
            .Where(element => element.EquipmentElement.Item?.ItemCategory == category)
            .Sum(element => element.Amount);
    }

    private static void RemoveRequiredItems(PartyBase party, ItemCategory category, int count)
    {
        foreach (ItemRosterElement element in party.ItemRoster
            .Where(element => element.EquipmentElement.Item?.ItemCategory == category)
            .OrderBy(element => element.EquipmentElement.Item.Value)
            .ToList())
        {
            int remove = Math.Min(count, element.Amount);
            party.ItemRoster.AddToCounts(element.EquipmentElement, -remove);
            count -= remove;
            if (count <= 0)
            {
                return;
            }
        }
    }

    private readonly record struct UpgradeOption(
        CharacterObject Target,
        int XpCost,
        int GoldCost,
        float Chance,
        ItemCategory? RequiredItemCategory,
        int Count);
}
