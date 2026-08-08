using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.CampaignBehaviors;

internal class PartyAITroopRecruiter : CampaignBehaviorBase
{
    private bool _firingEvent = false;

    public override void SyncData(IDataStore dataStore)
    {
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnTroopRecruited);
        CampaignEvents.OnLootDistributedToPartyEvent.AddNonSerializedListener(this, OnLootDistributedToParty);
        CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, DailyTickParty);
        CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, DailyTickSettlement);
        CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
    }

    private void OnHourlyTickParty(MobileParty party)
    {
        var hero = party?.LeaderHero;
        if (!SubModule.PartySettingsManager.IsHeroManageable(hero))
        {
            return;
        }

        var settings = SubModule.PartySettingsManager.Settings(hero);
        DismissUnwantedTroops(settings, party);
    }

    private void OnLootDistributedToParty(PartyBase winnerParty, PartyBase defeatedParty, ItemRoster lootedItems)
    {
        if ((!SubModule.PartySettingsManager.AllowTroopConversion
             || !SubModule.PartySettingsManager.IsManageable(winnerParty?.LeaderHero))
            && !SubModule.PartySettingsManager.AllowCaravanConversion(winnerParty?.LeaderHero))
        {
            return;
        }

        if (winnerParty?.LeaderHero == null)
        {
            return;
        }

        var heroSettings = SubModule.PartySettingsManager.Settings(winnerParty.LeaderHero);
        if (heroSettings?.PartyTemplate == null)
        {
            return;
        }

        ExchangeRoster(winnerParty.MemberRoster, heroSettings, winnerParty.LeaderHero, null);
    }

    private void DailyTickSettlement(Settlement settlement)
    {
        if (settlement?.Town?.GarrisonParty?.MemberRoster == null || settlement?.Owner == null)
        {
            return;
        }

        if (settlement.IsUnderSiege || settlement.InRebelliousState)
        {
            return;
        }

        if (!SubModule.PartySettingsManager.AllowTroopConversionForGarrisons
            || !SubModule.PartySettingsManager.IsGarrisonManageable(settlement))
        {
            return;
        }

        PartyAiEntitySettings settings = SubModule.PartySettingsManager.Settings(settlement);
        if (settings.PartyTemplate == null)
        {
            return;
        }

        ExchangeRoster(settlement.Town.GarrisonParty.MemberRoster, settings, null, settlement);
    }

    private void DailyTickParty(MobileParty party)
    {
        if ((!SubModule.PartySettingsManager.AllowTroopConversion
            || !SubModule.PartySettingsManager.IsManageable(party?.LeaderHero))
            && !SubModule.PartySettingsManager.AllowCaravanConversion(party?.LeaderHero))
        {
            return;
        }

        if (party.MapEvent != null) {
            return;
        }

        PartyAiEntitySettings heroSettings = SubModule.PartySettingsManager.Settings(party.LeaderHero);
        if (heroSettings.PartyTemplate == null)
        {
            return;
        }

        ExchangeRoster(party.MemberRoster, heroSettings, party.LeaderHero, null);
    }

    private void OnTroopRecruited(Hero recruiter, Settlement settlement, Hero recruitmentSource, CharacterObject troop, int count)
    {
        if (_firingEvent
            || (!SubModule.PartySettingsManager.AllowTroopConversion
            && !SubModule.PartySettingsManager.AllowCaravanConversion(recruiter)))
        {
            return;
        }

        if (SubModule.PartySettingsManager.IsManageable(recruiter))
        {
            PartyAiEntitySettings heroSettings = SubModule.PartySettingsManager.Settings(recruiter);
            if (heroSettings.PartyTemplate != null && heroSettings.TroopsConvertibleToday > 0)
            {
                ExchangeClanTroops(recruiter, recruiter?.PartyBelongedTo?.MemberRoster, troop, count, true);
                return;
            }
        }
    }

    private void DismissUnwantedTroops(PartyAiEntitySettings settings, MobileParty? party)
    {
        if (party is null
            || !settings.DismissUnwantedTroops
            || party.PartySizeRatio < settings.DismissUnwantedTroopsPercentage)
        {
            return;
        }

        int max = (int)((party.PartySizeRatio - settings.DismissUnwantedTroopsPercentage) * party.Party.PartySizeLimit);
        if (max <= 0)
        {
            return;
        }

        TroopRoster roster = party.MemberRoster;
        if (roster is null || party.Party is null)
        {
            return;
        }

        int gotRidOf = 0;
        while (gotRidOf < max)
        {
            List<TroopRosterElement> troops = roster.GetTroopRoster().ToList();
            troops.Shuffle();
            int thisRun = 0;
            foreach (TroopRosterElement e in troops)
            {
                if (e.Character.IsHero) { continue; }
                if (gotRidOf >= max) { return; }
                if ((settings.PartyTemplate != null
                    && !settings.PartyTemplate.Troops.Contains(e.Character))
                    || Recruitment.IsOverMaxTier(e.Character, settings.MaxTroopTier))
                {
                    roster.RemoveTroop(e.Character, 1);
                    gotRidOf++;
                    thisRun++;
                    roster.RemoveZeroCounts();
                }
            }
            if (thisRun == 0)
            {
                break;
            }
        }

        PartyComposition comp = Recruitment.GetPartyComposition(party.Party, settings);
        Dictionary<FormationClass, int> overages = new();
        foreach (FormationClass formation in new FormationClass[] { FormationClass.Infantry, FormationClass.Ranged, FormationClass.Cavalry, FormationClass.HorseArcher })
        {
            float overage = comp[formation] - settings.Composition[formation];
            int count = (int)(overage * party.Party.PartySizeLimit);
            if (settings.Composition[formation] == 0f && count == 0 && overage * party.Party.PartySizeLimit > 0.9f)
            {
                count = 1;
            }
            overages[formation] = count;
        }

        foreach (KeyValuePair<FormationClass, int> overage in overages.Where(o => o.Value > 0))
        {
            List<TroopRosterElement> troops = roster.GetTroopRoster().ToList();
            troops.Shuffle();

            foreach (TroopRosterElement e in troops)
            {
                if (e.Character.IsHero)
                {
                    continue;
                }


                var upgradeTargets = Recruitment.UpgradeTargets(
                    e.Character,
                    maxTierOnly: true,
                    template: settings.PartyTemplate)
                    .ConvertAll(t => t.DefaultFormationClass.FallbackClass());

                if (!upgradeTargets.Contains(overage.Key))
                {
                    continue;
                }

                // if another formation needs this troop to upgrade to it, don't dismiss it
                if (upgradeTargets.Any(t => overages[t] < 0))
                {
                    continue;
                }

                while (gotRidOf < max && roster.GetTroopCount(e.Character) > 0)
                {
                    roster.RemoveTroop(e.Character, 1);
                    gotRidOf++;
                    roster.RemoveZeroCounts();
                }
            }
        }
    }

    private void ExchangeRoster(TroopRoster roster, PartyAiEntitySettings settings, Hero hero, Settlement settlement)
    {
        List<TroopRosterElement> troops = roster.GetTroopRoster().ToList();
        troops.Shuffle();
        foreach (TroopRosterElement e in troops)
        {
            if (!settings.PartyTemplate.Troops.Contains(e.Character)
                || Recruitment.IsOverMaxTier(e.Character, settings.MaxTroopTier))
            {
                if (settings.TroopsConvertibleToday <= 0)
                {
                    break;
                }

                ExchangeClanTroops(hero, roster, e.Character, e.Number - e.WoundedNumber, false, settlement);
            }
        }
    }

    private void ExchangeClanTroops(Hero owner, TroopRoster roster, CharacterObject troop, int count, bool fireEvent, Settlement settlement = null)
    {
        if (owner?.PartyBelongedTo?.Party == null && settlement == null)
        {
            return;
        }

        if (!SubModule.PartySettingsManager.IsManageable(owner)
            && !SubModule.PartySettingsManager.IsGarrisonManageable(settlement))
        {
            return;
        }

        if (roster == null
            || troop.IsHero
            || roster.GetTroopCount(troop) < count
            || count <= 0)
        {
            return;
        }

        PartyBase party;
        PartyAiEntitySettings heroSettings;
        PAICustomTemplate template;
        if (settlement != null)
        {
            party = settlement.Town.GarrisonParty.Party;
            heroSettings = SubModule.PartySettingsManager.Settings(settlement);
            template = heroSettings.PartyTemplate;
        }
        else
        {
            party = owner.PartyBelongedTo.Party;
            heroSettings = SubModule.PartySettingsManager.Settings(owner);
            template = heroSettings.PartyTemplate;
        }

        if (template == null)
        {
            return;
        }

        if (heroSettings.TroopsConvertibleToday <= 0)
        {
            return;
        }

        while (count > 0 && heroSettings.TroopsConvertibleToday > 0)
        {
            PartyComposition comp = Recruitment.GetPartyComposition(party, heroSettings, troop);
            List<CharacterObject> eligible = template.Troops
                .Where(t => Recruitment.ShouldRecruit(comp, heroSettings, t, party))
                .ToList();

            CharacterObject replacement = DetermineReplacement(eligible, troop.Tier, Recruitment.IsEliteTroop(troop));

            if (replacement == null)
            {
                eligible = template.Troops
                    .Where(t => Recruitment.ShouldRecruit(comp, heroSettings, t, party, false))
                    .ToList();
                replacement = DetermineReplacement(eligible, troop.Tier, Recruitment.IsEliteTroop(troop));
                replacement ??= DetermineReplacement(eligible, troop.Tier, !Recruitment.IsEliteTroop(troop));
            }

            if (replacement == null && !template.Troops.Contains(troop))
            {
                replacement = DetermineReplacement(template.Troops, troop.Tier, Recruitment.IsEliteTroop(troop));
                replacement ??= DetermineReplacement(template.Troops, troop.Tier, !Recruitment.IsEliteTroop(troop));
            }

            if (replacement == null) { return; }

            IEnumerable<FormationClass> targets = Recruitment.UpgradeTargets(replacement, true, heroSettings.PartyTemplate)
                .ConvertAll(c => FormationClassExtensions.FallbackClass(c.DefaultFormationClass))
                .Distinct();
            int amount = Math.Max(1, targets.Sum(t => (int)Math.Floor((heroSettings.Composition[t] - comp[t]) * party.PartySizeLimit)));
            amount = Math.Min(amount, heroSettings.TroopsConvertibleToday);
            if (amount > count)
            {
                amount = count;
            }

            roster.RemoveTroop(troop, amount);
            roster.AddToCounts(replacement, amount);
            roster.RemoveZeroCounts();
            count -= amount;
            heroSettings.DeductTroopsConvertibleToday(amount);

            if (settlement == null
                && replacement.Tier != troop.Tier
                || Recruitment.IsEliteTroop(replacement) != Recruitment.IsEliteTroop(troop))
            {
                // adjust recruitment gold
                var troopCostExpl = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, owner);
                var replacementCostExpl = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(replacement, owner);
                int troopCost = troopCostExpl.RoundedResultNumber;
                int replacementCost = replacementCostExpl.RoundedResultNumber;
                GiveGoldAction.ApplyBetweenCharacters(null, owner, troopCost - replacementCost);
            }

            if (fireEvent)
            {
                _firingEvent = true;
                CampaignEventDispatcher.Instance.OnTroopRecruited(owner, null, null, replacement, amount);
                _firingEvent = false;
            }
        }
    }

    private CharacterObject DetermineReplacement(List<CharacterObject> templateCharacters, int troopTier, bool useElite)
    {
        CharacterObject replacement = null;
        foreach (bool elite in new bool[] { useElite, !useElite })
        {
            if (replacement != null)
            {
                break;
            }

            int tier = troopTier;
            replacement = Extensions.GetRandomElement(templateCharacters
                .Where(t => t.Tier == tier && Recruitment.IsEliteTroop(t) == elite)
                .ToList());

            for (int i = 1; replacement == null; i++)
            {
                replacement ??= Extensions.GetRandomElement(templateCharacters
                    .Where(t => t.Tier == tier - i && Recruitment.IsEliteTroop(t) == elite)
                    .ToList());

                replacement ??= Extensions.GetRandomElement(templateCharacters
                    .Where(t => t.Tier == tier + i && Recruitment.IsEliteTroop(t) == elite)
                    .ToList());

                if (tier - i <= 0 && tier + i > Campaign.Current.Models.CharacterStatsModel.MaxCharacterTier)
                {
                    break;
                }
            }
        }

        return replacement;
    }
}
