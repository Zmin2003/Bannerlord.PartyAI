using Bannerlord.PartyAI.Parties.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Parties.Recruitment;

/// <summary>
/// Swaps troops that do not belong to a party's template for ones that do, within the daily
/// conversion budget, and dismisses surplus troops when a party is over its dismissal threshold.
/// </summary>
public sealed class TroopConverter : CampaignBehaviorBase
{
    private const string LegacyStringId = "PartyAITroopRecruiter";

    private bool _isFiringRecruitEvent;

    public TroopConverter() : base(LegacyStringId)
    {
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnTroopRecruited);
        CampaignEvents.OnLootDistributedToPartyEvent.AddNonSerializedListener(this, OnLootDistributed);
        CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, OnDailyTickParty);
        CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
        CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    // ---- Event handlers ----------------------------------------------------------------------

    private void OnHourlyTickParty(MobileParty party)
    {
        Hero? hero = party?.LeaderHero;
        if (PartyAi.Parties.IsHeroManageable(hero))
        {
            DismissUnwantedTroops(party!, PartyAi.Parties.Profile(hero));
        }
    }

    private void OnLootDistributed(PartyBase winner, PartyBase defeated, ItemRoster loot)
    {
        Hero? hero = winner?.LeaderHero;
        if (hero is not null && TryGetConvertibleProfile(hero, out PartyProfile? profile))
        {
            ConvertRoster(winner!.MemberRoster, profile, hero, null);
        }
    }

    private void OnDailyTickParty(MobileParty party)
    {
        Hero? hero = party?.LeaderHero;
        if (hero is not null
            && party!.MapEvent is null
            && TryGetConvertibleProfile(hero, out PartyProfile? profile))
        {
            ConvertRoster(party.MemberRoster, profile, hero, null);
        }
    }

    private void OnDailyTickSettlement(Settlement settlement)
    {
        if (settlement?.Town?.GarrisonParty?.MemberRoster is null
            || settlement.Owner is null
            || settlement.IsUnderSiege
            || settlement.InRebelliousState
            || !PartyAi.Settings.AllowTroopConversionForGarrisons
            || !PartyAi.Parties.IsGarrisonManageable(settlement))
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(settlement);
        if (profile.Template is not null)
        {
            ConvertRoster(settlement.Town.GarrisonParty.MemberRoster, profile, null, settlement);
        }
    }

    private void OnTroopRecruited(Hero recruiter, Settlement settlement, Hero source, CharacterObject troop, int count)
    {
        if (_isFiringRecruitEvent
            || recruiter is null
            || !TryGetConvertibleProfile(recruiter, out PartyProfile? profile)
            || profile.TroopsConvertibleToday <= 0)
        {
            return;
        }

        Convert(recruiter, recruiter.PartyBelongedTo?.MemberRoster, troop, count, fireEvent: true);
    }

    private static bool TryGetConvertibleProfile(Hero hero, out PartyProfile profile)
    {
        profile = PartyAi.Parties.Profile(hero);
        bool lordConversion = PartyAi.Settings.AllowTroopConversion && PartyAi.Parties.IsManageable(hero);
        bool caravanConversion = PartyAi.Parties.AllowsCaravanConversion(hero);
        return (lordConversion || caravanConversion) && profile.Template is not null;
    }

    // ---- Public entry point for settlement automation -------------------------------------------

    /// <summary>Converts off-template troops and rebalances formations right now. Returns troops converted.</summary>
    public int BalancePartyNow(MobileParty party, PartyProfile profile)
    {
        if (party?.MemberRoster is null || party.LeaderHero is null || profile.Template is null)
        {
            return 0;
        }

        int budgetBefore = profile.TroopsConvertibleToday;
        ConvertRoster(party.MemberRoster, profile, party.LeaderHero, null);
        BalanceComposition(party, profile);
        return Math.Max(0, budgetBefore - profile.TroopsConvertibleToday);
    }

    // ---- Conversion ------------------------------------------------------------------------------

    private void ConvertRoster(TroopRoster roster, PartyProfile profile, Hero? owner, Settlement? settlement)
    {
        TroopTemplate? template = profile.Template;
        if (template is null)
        {
            return;
        }

        List<TroopRosterElement> troops = roster.GetTroopRoster().ToList();
        troops.Shuffle();

        foreach (TroopRosterElement element in troops)
        {
            if (template.Troops.Contains(element.Character)
                && !RecruitmentRules.IsOverMaxTier(element.Character, profile.MaxTroopTier))
            {
                continue;
            }

            if (profile.TroopsConvertibleToday <= 0)
            {
                break;
            }

            Convert(owner, roster, element.Character, element.Number - element.WoundedNumber, fireEvent: false, settlement);
        }
    }

    private void BalanceComposition(MobileParty party, PartyProfile profile)
    {
        int maxIterations = Math.Max(16, party.MemberRoster.TotalManCount * 2);
        for (int iteration = 0; iteration < maxIterations && profile.TroopsConvertibleToday > 0; iteration++)
        {
            PartyComposition current = RecruitmentRules.GetPartyComposition(party.Party, profile);
            float occupied = current.Total;
            float minimumDifference = 1f / Math.Max(1, party.Party.PartySizeLimit);

            var overrepresented = new HashSet<FormationClass>(PartyComposition.Formations
                .Where(formation => current[formation] - profile.Composition[formation] * occupied >= minimumDifference));
            if (overrepresented.Count == 0)
            {
                return;
            }

            bool converted = false;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster().ToList())
            {
                if (element.Character.IsHero || element.Number <= element.WoundedNumber)
                {
                    continue;
                }

                FormationClass[] targets = RecruitmentRules.FinalFormations(element.Character, profile.Template);
                if (targets.Length != 1 || !overrepresented.Contains(targets[0]))
                {
                    continue;
                }

                int budgetBefore = profile.TroopsConvertibleToday;
                Convert(party.LeaderHero, party.MemberRoster, element.Character, element.Number - element.WoundedNumber,
                    fireEvent: false, settlement: null, excludedFormation: targets[0]);

                if (profile.TroopsConvertibleToday < budgetBefore)
                {
                    converted = true;
                    break;
                }
            }

            if (!converted)
            {
                return;
            }
        }
    }

    private void Convert(
        Hero? owner,
        TroopRoster? roster,
        CharacterObject troop,
        int count,
        bool fireEvent,
        Settlement? settlement = null,
        FormationClass? excludedFormation = null)
    {
        if (roster is null || troop.IsHero || count <= 0 || roster.GetTroopCount(troop) < count)
        {
            return;
        }

        PartyBase party;
        PartyProfile profile;
        if (settlement is not null)
        {
            if (!PartyAi.Parties.IsGarrisonManageable(settlement) || settlement.Town?.GarrisonParty is null)
            {
                return;
            }

            party = settlement.Town.GarrisonParty.Party;
            profile = PartyAi.Parties.Profile(settlement);
        }
        else
        {
            if (owner?.PartyBelongedTo?.Party is null || !PartyAi.Parties.IsAutomationEligible(owner))
            {
                return;
            }

            party = owner.PartyBelongedTo.Party;
            profile = PartyAi.Parties.Profile(owner);
        }

        TroopTemplate? template = profile.Template;
        if (template is null || profile.TroopsConvertibleToday <= 0)
        {
            return;
        }

        while (count > 0 && profile.TroopsConvertibleToday > 0)
        {
            PartyComposition composition = RecruitmentRules.GetPartyComposition(party, profile, troop);
            CharacterObject? replacement = ChooseReplacement(template, troop, party, profile, composition, excludedFormation);
            if (replacement is null)
            {
                return;
            }

            int amount = Math.Max(1, RecruitmentRules.FinalFormations(replacement, template)
                .Sum(formation => (int)Math.Floor((profile.Composition[formation] - composition[formation]) * party.PartySizeLimit)));
            amount = Math.Min(amount, Math.Min(profile.TroopsConvertibleToday, count));

            int costDifference = 0;
            if (owner is not null && settlement is null)
            {
                var wages = Campaign.Current.Models.PartyWageModel;
                costDifference = wages.GetTroopRecruitmentCost(replacement, owner).RoundedResultNumber
                    - wages.GetTroopRecruitmentCost(troop, owner).RoundedResultNumber;
                if (costDifference > 0)
                {
                    int budget = owner == Hero.MainHero ? Finance.Treasury.Spendable : owner.Gold;
                    amount = Math.Min(amount, budget / costDifference);
                    if (amount <= 0)
                    {
                        return;
                    }
                }
            }

            roster.RemoveTroop(troop, amount);
            roster.AddToCounts(replacement, amount);
            roster.RemoveZeroCounts();
            count -= amount;
            profile.DeductTroopsConvertibleToday(amount);

            if (owner is not null && settlement is null && costDifference != 0)
            {
                if (costDifference > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(owner, null, costDifference * amount, disableNotification: true);
                }
                else
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, owner, -costDifference * amount, disableNotification: true);
                }
            }

            if (fireEvent && owner is not null)
            {
                _isFiringRecruitEvent = true;
                CampaignEventDispatcher.Instance.OnTroopRecruited(owner, null, null, replacement, amount);
                _isFiringRecruitEvent = false;
            }
        }
    }

    private static CharacterObject? ChooseReplacement(
        TroopTemplate template,
        CharacterObject troop,
        PartyBase party,
        PartyProfile profile,
        PartyComposition composition,
        FormationClass? excludedFormation)
    {
        bool elite = RecruitmentRules.IsEliteTroop(troop);

        bool Eligible(CharacterObject candidate)
            => candidate != troop
                && IsAllowedFormation(candidate, profile.Template, excludedFormation)
                && ImprovesComposition(candidate, composition, profile);

        // 1) needed for composition, 2) at least somewhat needed, 3) anything in the template if the troop is foreign to it.
        List<CharacterObject> strict = template.Troops
            .Where(candidate => Eligible(candidate) && RecruitmentRules.ShouldRecruit(composition, profile, candidate, party))
            .ToList();
        CharacterObject? replacement = ClosestTier(strict, troop.Tier, elite);

        if (replacement is null)
        {
            List<CharacterObject> loose = template.Troops
                .Where(candidate => Eligible(candidate) && RecruitmentRules.ShouldRecruit(composition, profile, candidate, party, mustBeOnePlus: false))
                .ToList();
            replacement = ClosestTier(loose, troop.Tier, elite) ?? ClosestTier(loose, troop.Tier, !elite);
        }

        if (replacement is null && !template.Troops.Contains(troop))
        {
            List<CharacterObject> any = template.Troops
                .Where(candidate => candidate != troop && IsAllowedFormation(candidate, profile.Template, excludedFormation))
                .ToList();
            replacement = ClosestTier(any, troop.Tier, elite) ?? ClosestTier(any, troop.Tier, !elite);
        }

        return replacement;
    }

    private static bool ImprovesComposition(CharacterObject candidate, PartyComposition composition, PartyProfile profile)
    {
        float occupied = composition.Total;
        return occupied <= 0f
            || RecruitmentRules.FinalFormations(candidate, profile.Template)
                .Any(formation => composition[formation] < profile.Composition[formation] * occupied);
    }

    private static bool IsAllowedFormation(CharacterObject candidate, TroopTemplate? template, FormationClass? excluded)
    {
        if (!excluded.HasValue)
        {
            return true;
        }

        FormationClass[] formations = RecruitmentRules.FinalFormations(candidate, template);
        return formations.Length > 0 && !formations.Contains(excluded.Value);
    }

    /// <summary>Random candidate of the requested elite-ness whose tier is closest to <paramref name="tier"/>.</summary>
    private static CharacterObject? ClosestTier(List<CharacterObject> candidates, int tier, bool elite)
    {
        int maxTier = Campaign.Current.Models.CharacterStatsModel.MaxCharacterTier;
        for (int distance = 0; tier - distance > 0 || tier + distance <= maxTier; distance++)
        {
            CharacterObject? pick = Pick(tier - distance) ?? (distance > 0 ? Pick(tier + distance) : null);
            if (pick is not null)
            {
                return pick;
            }
        }

        return null;

        CharacterObject? Pick(int wantedTier)
            => candidates.Where(candidate => candidate.Tier == wantedTier && RecruitmentRules.IsEliteTroop(candidate) == elite)
                .ToList()
                .GetRandomElement();
    }

    // ---- Dismissal -------------------------------------------------------------------------------

    private static void DismissUnwantedTroops(MobileParty party, PartyProfile profile)
    {
        if (!profile.DismissUnwantedTroops
            || party.PartySizeRatio < profile.DismissUnwantedTroopsPercentage
            || party.MemberRoster is null)
        {
            return;
        }

        int budget = (int)((party.PartySizeRatio - profile.DismissUnwantedTroopsPercentage) * party.Party.PartySizeLimit);
        if (budget <= 0)
        {
            return;
        }

        TroopRoster roster = party.MemberRoster;
        int dismissed = 0;

        // First pass: troops outside the template or above the max tier.
        bool removedAny = true;
        while (dismissed < budget && removedAny)
        {
            removedAny = false;
            List<TroopRosterElement> troops = roster.GetTroopRoster().ToList();
            troops.Shuffle();
            foreach (TroopRosterElement element in troops)
            {
                if (dismissed >= budget)
                {
                    return;
                }

                bool unwanted = element.Character.IsHero == false
                    && ((profile.Template is not null && !profile.Template.Troops.Contains(element.Character))
                        || RecruitmentRules.IsOverMaxTier(element.Character, profile.MaxTroopTier));
                if (unwanted)
                {
                    roster.RemoveTroop(element.Character, 1);
                    roster.RemoveZeroCounts();
                    dismissed++;
                    removedAny = true;
                }
            }
        }

        // Second pass: formations over their composition target.
        PartyComposition composition = RecruitmentRules.GetPartyComposition(party.Party, profile);
        var overages = new Dictionary<FormationClass, int>();
        foreach (FormationClass formation in PartyComposition.Formations)
        {
            float overage = composition[formation] - profile.Composition[formation];
            int count = (int)(overage * party.Party.PartySizeLimit);
            if (profile.Composition[formation] == 0f && count == 0 && overage * party.Party.PartySizeLimit > 0.9f)
            {
                count = 1;
            }

            overages[formation] = count;
        }

        foreach (KeyValuePair<FormationClass, int> overage in overages.Where(pair => pair.Value > 0))
        {
            List<TroopRosterElement> troops = roster.GetTroopRoster().ToList();
            troops.Shuffle();
            foreach (TroopRosterElement element in troops)
            {
                if (element.Character.IsHero)
                {
                    continue;
                }

                FormationClass[] targets = RecruitmentRules.FinalFormations(element.Character, profile.Template);
                if (!targets.Contains(overage.Key) || targets.Any(target => overages[target] < 0))
                {
                    continue;
                }

                while (dismissed < budget && roster.GetTroopCount(element.Character) > 0)
                {
                    roster.RemoveTroop(element.Character, 1);
                    roster.RemoveZeroCounts();
                    dismissed++;
                }
            }
        }
    }
}
