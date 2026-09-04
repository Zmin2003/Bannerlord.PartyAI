using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Recruitment;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Finance;

/// <summary>
/// Turns the clutter of a campaign into gold whenever the player's party enters a town: sells
/// prisoners the party will not recruit and equipment nobody in the party wears. Anything the
/// player locked in the inventory or party screen is never touched.
/// </summary>
public sealed class TownSalesBehavior : CampaignBehaviorBase
{
    public override void RegisterEvents()
    {
        CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (party != MobileParty.MainParty
            || settlement?.Town is null
            || !settlement.IsTown
            || settlement.IsUnderSiege
            || FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
        {
            return;
        }

        ModSettings settings = PartyAi.Settings;
        int prisonerGold = settings.AutoSellPrisoners ? SellPrisoners(party, settlement) : 0;
        int lootGold = settings.AutoSellLoot || settings.AutoSellTradeGoods ? SellLoot(party, settlement) : 0;

        if (prisonerGold + lootGold > 0)
        {
            Notify.Success(L.T("{=PAI_SALES_SUMMARY}Sold in {TOWN}: prisoners {PRISONERS} gold, goods {LOOT} gold.")
                .SetTextVariable("TOWN", settlement.Name)
                .SetTextVariable("PRISONERS", prisonerGold)
                .SetTextVariable("LOOT", lootGold));
        }
    }

    // ---- Prisoners -------------------------------------------------------------------------------

    private static int SellPrisoners(MobileParty party, Settlement settlement)
    {
        PartyProfile profile = PartyAi.Parties.Profile(Hero.MainHero);
        var locks = new HashSet<string>(ViewData?.GetPartyPrisonerLocks() ?? Enumerable.Empty<string>());
        TroopRoster toSell = TroopRoster.CreateDummyTroopRoster();
        int value = 0;

        foreach (TroopRosterElement element in party.PrisonRoster.GetTroopRoster())
        {
            CharacterObject troop = element.Character;
            if (troop.IsHero || element.Number <= 0 || locks.Contains(CampaignUIHelper.GetTroopLockStringID(element)))
            {
                continue;
            }

            if (PartyAi.Settings.SellPrisonersKeepTemplate
                && profile.Template is not null
                && RecruitmentRules.UpgradeTargets(troop, template: profile.Template).Count > 0)
            {
                continue;
            }

            toSell.AddToCounts(troop, element.Number, insertAtFront: false, element.WoundedNumber);
            value += element.Number * Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(troop, Hero.MainHero);
        }

        if (toSell.Count == 0)
        {
            return 0;
        }

        SellPrisonersAction.ApplyForSelectedPrisoners(party.Party, settlement.Party, toSell);
        return value;
    }

    // ---- Loot ------------------------------------------------------------------------------------

    private static int SellLoot(MobileParty party, Settlement settlement)
    {
        ModSettings settings = PartyAi.Settings;
        Town town = settlement.Town;
        var locks = new HashSet<string>(ViewData?.GetInventoryLocks() ?? Enumerable.Empty<string>());
        int earned = 0;

        foreach (ItemRosterElement element in party.ItemRoster.ToList())
        {
            ItemObject item = element.EquipmentElement.Item;
            if (item is null || element.Amount <= 0 || locks.Contains(CampaignUIHelper.GetItemLockStringID(element.EquipmentElement)))
            {
                continue;
            }

            bool sellable = item.IsTradeGood ? settings.AutoSellTradeGoods : settings.AutoSellLoot && IsEquipment(item);
            if (!sellable)
            {
                continue;
            }

            int unitPrice = town.GetItemPrice(element.EquipmentElement, party, isSelling: true);
            if (unitPrice <= 0)
            {
                continue;
            }

            int count = System.Math.Min(element.Amount, town.Gold / unitPrice);
            if (count <= 0)
            {
                continue;
            }

            SellItemsAction.Apply(party.Party, settlement.Party, element, count, settlement);
            earned += count * unitPrice;
        }

        return earned;
    }

    /// <summary>Weapons, armour and harness. Mounts, food, books and banners are kept.</summary>
    private static bool IsEquipment(ItemObject item) => item.ItemType switch
    {
        ItemObject.ItemTypeEnum.OneHandedWeapon
            or ItemObject.ItemTypeEnum.TwoHandedWeapon
            or ItemObject.ItemTypeEnum.Polearm
            or ItemObject.ItemTypeEnum.Arrows
            or ItemObject.ItemTypeEnum.Bolts
            or ItemObject.ItemTypeEnum.Bullets
            or ItemObject.ItemTypeEnum.Shield
            or ItemObject.ItemTypeEnum.Bow
            or ItemObject.ItemTypeEnum.Crossbow
            or ItemObject.ItemTypeEnum.Thrown
            or ItemObject.ItemTypeEnum.Pistol
            or ItemObject.ItemTypeEnum.Musket
            or ItemObject.ItemTypeEnum.HeadArmor
            or ItemObject.ItemTypeEnum.BodyArmor
            or ItemObject.ItemTypeEnum.LegArmor
            or ItemObject.ItemTypeEnum.HandArmor
            or ItemObject.ItemTypeEnum.ChestArmor
            or ItemObject.ItemTypeEnum.Cape
            or ItemObject.ItemTypeEnum.HorseHarness => true,
        _ => false
    };

    private static IViewDataTracker? ViewData => Campaign.Current.GetCampaignBehavior<IViewDataTracker>();
}
