using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Parties.Recruitment;

internal static class HeroEquipmentOptimizer
{
    private static readonly EquipmentIndex[] EquipmentSlots =
    [
        EquipmentIndex.Head,
        EquipmentIndex.Body,
        EquipmentIndex.Leg,
        EquipmentIndex.Gloves,
        EquipmentIndex.Cape,
        EquipmentIndex.Horse,
        EquipmentIndex.HorseHarness,
        EquipmentIndex.Weapon0,
        EquipmentIndex.Weapon1,
        EquipmentIndex.Weapon2,
        EquipmentIndex.Weapon3
    ];

    internal static int OptimizeParty(MobileParty party)
    {
        if (party?.ItemRoster is null || party.MemberRoster is null)
        {
            return 0;
        }

        List<Hero> heroes = new[] { party.LeaderHero }
            .Concat(party.MemberRoster
                .GetTroopRoster()
                .Where(element => element.Character.IsHero)
                .Select(element => element.Character.HeroObject))
            .Where(hero => hero is not null
                && hero != Hero.MainHero
                && hero.CanHeroEquipmentBeChanged())
            .Distinct()
            .OrderByDescending(hero => hero.Level)
            .ToList();

        int changed = 0;
        foreach (Hero hero in heroes)
        {
            foreach (EquipmentIndex slot in EquipmentSlots)
            {
                if (TryUpgradeSlot(party, hero, slot))
                {
                    changed++;
                }
            }
        }

        return changed;
    }

    private static bool TryUpgradeSlot(MobileParty party, Hero hero, EquipmentIndex slot)
    {
        Equipment equipment = hero.BattleEquipment;
        EquipmentElement current = equipment[slot];
        if (!current.IsEmpty
            && (current.IsQuestItem || !current.Item.IsTransferable))
        {
            return false;
        }

        // Keep each hero's weapon role intact; empty weapon slots are not filled blindly.
        bool isWeaponSlot = slot >= EquipmentIndex.Weapon0 && slot <= EquipmentIndex.Weapon3;
        if (isWeaponSlot && current.IsEmpty)
        {
            return false;
        }

        ItemRosterElement? best = party.ItemRoster
            .Where(element => element.Amount > 0
                && !element.EquipmentElement.IsEmpty
                && !element.EquipmentElement.IsQuestItem
                && element.EquipmentElement.Item.IsTransferable
                && Equipment.IsItemFitsToSlot(slot, element.EquipmentElement.Item)
                && CanUse(hero, element.EquipmentElement.Item)
                && (!isWeaponSlot
                    || element.EquipmentElement.Item.ItemType == current.Item.ItemType))
            .OrderByDescending(element => Score(element.EquipmentElement, slot))
            .Select(element => (ItemRosterElement?)element)
            .FirstOrDefault();

        if (!best.HasValue
            || Score(best.Value.EquipmentElement, slot) <= Score(current, slot))
        {
            return false;
        }

        if (!current.IsEmpty)
        {
            party.ItemRoster.AddToCounts(current, 1);
        }

        party.ItemRoster.AddToCounts(best.Value.EquipmentElement, -1);
        equipment[slot] = best.Value.EquipmentElement;
        return true;
    }

    private static bool CanUse(Hero hero, ItemObject item)
    {
        return item.RelevantSkill is null
            || item.Difficulty <= 0
            || hero.GetSkillValue(item.RelevantSkill) >= item.Difficulty;
    }

    private static float Score(EquipmentElement element, EquipmentIndex slot)
    {
        if (element.IsEmpty)
        {
            return 0f;
        }

        float protection = slot switch
        {
            EquipmentIndex.Head => element.GetModifiedHeadArmor(),
            EquipmentIndex.Body => element.GetModifiedBodyArmor() + element.GetModifiedArmArmor() * 0.7f,
            EquipmentIndex.Leg => element.GetModifiedLegArmor(),
            EquipmentIndex.Gloves => element.GetModifiedArmArmor(),
            EquipmentIndex.Cape => element.GetModifiedBodyArmor() + element.GetModifiedArmArmor(),
            EquipmentIndex.HorseHarness => element.GetModifiedMountBodyArmor(),
            _ => 0f
        };

        return protection * 100000f
            + element.Item.Tierf * 10000f
            + element.Item.Effectiveness * 100f
            + element.ItemValue;
    }
}
