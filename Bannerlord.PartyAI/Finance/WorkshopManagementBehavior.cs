using Bannerlord.PartyAI.Core;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Finance;

/// <summary>
/// Watches the player's workshops: tracks capital daily, recommends a better production type
/// when one keeps losing money, and (in automatic mode) pays for the switch from the treasury.
/// </summary>
public sealed class WorkshopManagementBehavior : CampaignBehaviorBase
{
    private const int ChangeCooldownDays = 30;
    private const float RequiredImprovement = 1.25f;

    private Dictionary<string, WorkshopLedger> _ledgers = new();

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.WorkshopTypeChangedEvent.AddNonSerializedListener(this, OnWorkshopTypeChanged);
        CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, (party, settlement, _) =>
        {
            if (party == MobileParty.MainParty && settlement.IsTown)
            {
                ConsiderPurchase();
            }
        });
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_workshopLedgers", ref _ledgers);
        _ledgers ??= new();
    }

    // ---- Queries for the UI ------------------------------------------------------------------

    public static IEnumerable<Workshop> PlayerWorkshops
        => Hero.MainHero?.OwnedWorkshops.Where(workshop => workshop.WorkshopType is not null) ?? Enumerable.Empty<Workshop>();

    public WorkshopLedger Ledger(Workshop workshop)
    {
        string key = Key(workshop);
        if (!_ledgers.TryGetValue(key, out WorkshopLedger? ledger))
        {
            ledger = new WorkshopLedger();
            _ledgers[key] = ledger;
        }

        return ledger;
    }

    /// <summary>Daily income the clan finance screen attributes to this workshop.</summary>
    public static int DailyIncome(Workshop workshop)
        => Campaign.Current.Models.ClanFinanceModel.CalculateOwnerIncomeFromWorkshop(workshop);

    /// <summary>Whether the workshop has been draining capital for the configured review period.</summary>
    public bool IsUnderperforming(Workshop workshop)
    {
        WorkshopLedger ledger = Ledger(workshop);
        int reviewDays = PartyAi.Settings.WorkshopReviewDays;
        if (ledger.DaysTracked <= reviewDays)
        {
            return false;
        }

        int? trend = ledger.Trend(reviewDays);
        return trend.HasValue && trend.Value <= 0 && DailyIncome(workshop) <= 0;
    }

    /// <summary>A meaningfully better production type for the workshop's town, or null.</summary>
    public WorkshopAdvisor.Candidate? Recommendation(Workshop workshop)
    {
        WorkshopAdvisor.Candidate? best = WorkshopAdvisor.Best(workshop.Settlement.Town, workshop.WorkshopType);
        if (best is null)
        {
            return null;
        }

        float current = WorkshopAdvisor.Score(workshop.Settlement.Town, workshop.WorkshopType);
        return best.Value.Score >= current * RequiredImprovement ? best : null;
    }

    public static int ConversionCost(WorkshopType type)
        => Campaign.Current.Models.WorkshopModel.GetConvertProductionCost(type);

    public static int SalePrice(Workshop workshop)
        => Campaign.Current.Models.WorkshopModel.GetCostForNotable(workshop);

    public static bool CanSell(Workshop workshop, out TextObject reason)
        => Campaign.Current.Models.WorkshopModel.CanPlayerSellWorkshop(workshop, out reason);

    // ---- Actions -------------------------------------------------------------------------------

    /// <summary>Switches production if the treasury can afford it. Returns false when it cannot.</summary>
    public bool TrySwitchProduction(Workshop workshop, WorkshopType type)
    {
        int cost = ConversionCost(type);
        if (!Treasury.CanSpend(cost))
        {
            Notify.Warning(L.T("{=PAI_WORKSHOP_CANNOT_AFFORD}Cannot switch {WORKSHOP} in {TOWN} to {TYPE}: {COST} gold would break the gold reserve.")
                .SetTextVariable("WORKSHOP", workshop.Name)
                .SetTextVariable("TOWN", workshop.Settlement.Name)
                .SetTextVariable("TYPE", type.Name)
                .SetTextVariable("COST", cost));
            return false;
        }

        ChangeProductionTypeOfWorkshopAction.Apply(workshop, type);
        Notify.Success(L.T("{=PAI_WORKSHOP_SWITCHED}{WORKSHOP} in {TOWN} now produces as a {TYPE} ({COST} gold).")
            .SetTextVariable("WORKSHOP", workshop.Name)
            .SetTextVariable("TOWN", workshop.Settlement.Name)
            .SetTextVariable("TYPE", type.Name)
            .SetTextVariable("COST", cost));
        return true;
    }

    public bool TrySell(Workshop workshop)
    {
        if (!CanSell(workshop, out TextObject reason))
        {
            Notify.Warning(reason ?? L.T("{=PAI_WORKSHOP_CANNOT_SELL}This workshop cannot be sold right now."));
            return false;
        }

        Hero buyer = Campaign.Current.Models.WorkshopModel.GetNotableOwnerForWorkshop(workshop);
        if (buyer is null)
        {
            return false;
        }

        int price = SalePrice(workshop);
        WorkshopType type = WorkshopAdvisor.Best(workshop.Settlement.Town)?.Type ?? workshop.WorkshopType;
        ChangeOwnerOfWorkshopAction.ApplyByPlayerSelling(workshop, buyer, type);
        _ledgers.Remove(Key(workshop));

        Notify.Success(L.T("{=PAI_WORKSHOP_SOLD}Sold the workshop in {TOWN} to {BUYER} for {PRICE} gold.")
            .SetTextVariable("TOWN", workshop.Settlement.Name)
            .SetTextVariable("BUYER", buyer.Name)
            .SetTextVariable("PRICE", price));
        return true;
    }

    // ---- Purchases -----------------------------------------------------------------------------

    /// <summary>A purchase only goes ahead when it uses at most this share of the gold above the reserve.</summary>
    private const float PurchaseSpendableShare = 0.5f;

    public static int MaxWorkshops
        => Campaign.Current.Models.WorkshopModel.GetMaxWorkshopCountForClanTier(Clan.PlayerClan.Tier);

    public static bool CanOwnMore => Hero.MainHero.OwnedWorkshops.Count < MaxWorkshops;

    /// <summary>Towns where the player could buy right now: own fiefs, plus the town the party is in.</summary>
    public static IEnumerable<Town> PurchaseTowns()
    {
        IFaction ours = Hero.MainHero.MapFaction;
        foreach (Town town in Town.AllTowns)
        {
            bool present = MobileParty.MainParty?.CurrentSettlement == town.Settlement;
            bool owned = town.Settlement.OwnerClan == Clan.PlayerClan;
            if ((present || owned)
                && !town.Settlement.IsUnderSiege
                && !FactionManager.IsAtWarAgainstFaction(ours, town.Settlement.MapFaction))
            {
                yield return town;
            }
        }
    }

    public static WorkshopAdvisor.Purchase? RecommendedPurchase()
        => CanOwnMore ? WorkshopAdvisor.BestPurchase(PurchaseTowns()) : null;

    public static bool CanAffordPurchase(int cost)
        => Treasury.CanSpend(cost) && cost <= Treasury.Spendable * PurchaseSpendableShare;

    public bool TryBuy(Workshop workshop)
    {
        if (!CanOwnMore || workshop.Owner == Hero.MainHero)
        {
            return false;
        }

        int cost = Campaign.Current.Models.WorkshopModel.GetCostForPlayer(workshop);
        if (!CanAffordPurchase(cost))
        {
            Notify.Warning(L.T("{=PAI_WORKSHOP_BUY_CANNOT_AFFORD}Buying the {TYPE} in {TOWN} ({COST} gold) would use too much of the gold above the reserve.")
                .SetTextVariable("TYPE", workshop.WorkshopType.Name)
                .SetTextVariable("TOWN", workshop.Settlement.Name)
                .SetTextVariable("COST", cost));
            return false;
        }

        ChangeOwnerOfWorkshopAction.ApplyByPlayerBuying(workshop);
        Ledger(workshop).Reset();
        Notify.Success(L.T("{=PAI_WORKSHOP_BOUGHT}Bought the {TYPE} in {TOWN} for {COST} gold.")
            .SetTextVariable("TYPE", workshop.WorkshopType.Name)
            .SetTextVariable("TOWN", workshop.Settlement.Name)
            .SetTextVariable("COST", cost));
        return true;
    }

    private void ConsiderPurchase()
    {
        WorkshopMode mode = PartyAi.Settings.WorkshopBuyMode;
        if (mode == WorkshopMode.Off || !CanOwnMore)
        {
            return;
        }

        WorkshopAdvisor.Purchase? best = RecommendedPurchase();
        if (best is null || !CanAffordPurchase(best.Value.Cost))
        {
            return;
        }

        Workshop workshop = best.Value.Workshop;
        if (mode == WorkshopMode.Auto)
        {
            TryBuy(workshop);
            return;
        }

        string key = Key(workshop);
        if (_lastPurchaseAdvice != key)
        {
            _lastPurchaseAdvice = key;
            Notify.Info(L.T("{=PAI_WORKSHOP_BUY_RECOMMEND}The {TYPE} in {TOWN} looks like a good buy ({COST} gold). See the Economy tab.")
                .SetTextVariable("TYPE", workshop.WorkshopType.Name)
                .SetTextVariable("TOWN", workshop.Settlement.Name)
                .SetTextVariable("COST", best.Value.Cost));
        }
    }

    private string? _lastPurchaseAdvice;

    // ---- Daily tick ----------------------------------------------------------------------------

    private void OnDailyTick()
    {
        ConsiderPurchase();

        var owned = PlayerWorkshops.ToList();
        var ownedKeys = new HashSet<string>(owned.Select(Key));
        foreach (string stale in _ledgers.Keys.Where(key => !ownedKeys.Contains(key)).ToList())
        {
            _ledgers.Remove(stale);
        }

        WorkshopMode mode = PartyAi.Settings.WorkshopMode;
        foreach (Workshop workshop in owned)
        {
            WorkshopLedger ledger = Ledger(workshop);
            ledger.Record(workshop.Capital);

            if (mode == WorkshopMode.Off || ledger.ChangedRecently(ChangeCooldownDays) || !IsUnderperforming(workshop))
            {
                continue;
            }

            WorkshopAdvisor.Candidate? better = Recommendation(workshop);
            if (better is null)
            {
                continue;
            }

            if (mode == WorkshopMode.Auto)
            {
                if (TrySwitchProduction(workshop, better.Value.Type))
                {
                    ledger.MarkProductionChanged();
                    ledger.Reset();
                }

                continue;
            }

            Notify.Info(L.T("{=PAI_WORKSHOP_RECOMMEND}{WORKSHOP} in {TOWN} has lost money for {DAYS} days. A {TYPE} would suit the town better ({COST} gold to convert).")
                .SetTextVariable("WORKSHOP", workshop.Name)
                .SetTextVariable("TOWN", workshop.Settlement.Name)
                .SetTextVariable("DAYS", PartyAi.Settings.WorkshopReviewDays)
                .SetTextVariable("TYPE", better.Value.Type.Name)
                .SetTextVariable("COST", ConversionCost(better.Value.Type)));
        }
    }

    private void OnWorkshopTypeChanged(Workshop workshop)
    {
        if (workshop.Owner == Hero.MainHero)
        {
            WorkshopLedger ledger = Ledger(workshop);
            ledger.MarkProductionChanged();
            ledger.Reset();
        }
    }

    private static string Key(Workshop workshop) => $"{workshop.Settlement.StringId}:{workshop.Tag}";
}
