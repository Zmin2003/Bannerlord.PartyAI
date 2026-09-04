using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Parties;

/// <summary>Keeps managed parties mounted: buys horses for men on foot, sells large surpluses.</summary>
internal static class HorseTrading
{
    private const int SurplusBeforeSelling = 10;

    /// <summary>Returns true when the mod handled horse trading and vanilla logic should be skipped.</summary>
    public static bool TryTrade(MobileParty party, Settlement settlement)
    {
        if (party?.LeaderHero is null
            || settlement is null
            || party == MobileParty.MainParty
            || party.IsDisbanding
            || (!settlement.IsTown && !settlement.IsVillage)
            || !PartyAi.Parties.IsHeroManageable(party.LeaderHero))
        {
            return false;
        }

        PartyProfile profile = PartyAi.Parties.Profile(party.LeaderHero);
        if (!profile.BuyHorses)
        {
            return false;
        }

        int surplus = party.Party.NumberOfMounts - party.Party.NumberOfMenWithoutHorse;
        if (surplus > SurplusBeforeSelling)
        {
            Sell(party, settlement, surplus);
        }
        else if (surplus < 0)
        {
            Buy(party, settlement, profile);
        }

        return true;
    }

    private static void Sell(MobileParty party, Settlement settlement, int amount)
    {
        var horses = party.ItemRoster
            .Where(element => element.EquipmentElement.Item.IsMountable)
            .OrderByDescending(element => Price(element, party, settlement));

        foreach (ItemRosterElement horse in horses)
        {
            int count = Math.Min(amount, horse.Amount);
            SellItemsAction.Apply(party.Party, settlement.Party, horse, count, settlement);
            amount -= count;
            if (amount <= 0)
            {
                break;
            }
        }
    }

    private static void Buy(MobileParty party, Settlement settlement, PartyProfile profile)
    {
        var horses = settlement.Party.ItemRoster
            .Where(element => element.EquipmentElement.Item.HasHorseComponent
                && element.EquipmentElement.Item.HorseComponent.IsMount
                && element.EquipmentElement.ItemModifier is null)
            .OrderBy(element => Price(element, party, settlement));

        int goldBefore = party.LeaderHero.Gold;
        int budget = Math.Min(profile.BuyHorsesBudgetToday, goldBefore);
        int spent = 0;

        foreach (ItemRosterElement element in horses)
        {
            int price = Price(element, party, settlement);
            int amount = element.Amount;
            if (amount * price * 1.05f > budget)
            {
                amount = MathF.Floor(budget / (float)price);
            }

            amount = Math.Min(amount, party.Party.NumberOfMenWithoutHorse - party.Party.NumberOfMounts);
            if (amount <= 0)
            {
                break;
            }

            SellItemsAction.Apply(settlement.Party, party.Party, element, amount, settlement);

            int goldAfter = party.LeaderHero.Gold;
            spent += goldBefore - goldAfter;
            budget -= goldBefore - goldAfter;
            goldBefore = goldAfter;
        }

        profile.DeductHorseBudget(spent);
    }

    private static int Price(ItemRosterElement element, MobileParty party, Settlement settlement)
        => settlement.IsTown
            ? settlement.Town.GetItemPrice(element.EquipmentElement, party)
            : settlement.Village.GetItemPrice(element.EquipmentElement, party);
}
