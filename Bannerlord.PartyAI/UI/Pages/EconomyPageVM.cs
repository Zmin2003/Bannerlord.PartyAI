using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Finance;
using Bannerlord.PartyAI.UI.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Pages;

/// <summary>One line of the clan's daily balance.</summary>
public sealed class FinanceLineVM : ViewModel
{
    public FinanceLineVM(string name, float amount)
    {
        Name = name;
        Amount = (amount >= 0 ? "+" : string.Empty) + ((int)Math.Round(amount)).ToString();
        IsNegative = amount < 0;
    }

    [DataSourceProperty] public string Name { get; }
    [DataSourceProperty] public string Amount { get; }
    [DataSourceProperty] public bool IsNegative { get; }
}

/// <summary>One player workshop with its health and the advisor's recommendation.</summary>
public sealed class WorkshopItemVM : ViewModel
{
    private readonly Workshop _workshop;
    private readonly Action _refresh;
    private readonly WorkshopAdvisor.Candidate? _recommendation;

    public WorkshopItemVM(Workshop workshop, Action refresh)
    {
        _workshop = workshop;
        _refresh = refresh;

        WorkshopManagementBehavior manager = PartyAi.Workshops;
        WorkshopLedger ledger = manager.Ledger(workshop);
        int? trend = ledger.Trend(PartyAi.Settings.WorkshopReviewDays);
        _recommendation = manager.Recommendation(workshop);

        Town = workshop.Settlement.Name.ToString();
        TypeName = workshop.WorkshopType.Name.ToString();
        Capital = L.T("{=PAI_WORKSHOP_CAPITAL}Capital {CAPITAL}", "CAPITAL", workshop.Capital).ToString();
        Income = L.T("{=PAI_WORKSHOP_INCOME}{INCOME}/day", "INCOME", WorkshopManagementBehavior.DailyIncome(workshop)).ToString();
        Trend = trend is null
            ? L.S("{=PAI_WORKSHOP_TREND_UNKNOWN}Collecting data...")
            : L.T("{=PAI_WORKSHOP_TREND}{TREND} over {DAYS} days")
                .SetTextVariable("TREND", (trend.Value >= 0 ? "+" : string.Empty) + trend.Value)
                .SetTextVariable("DAYS", Math.Min(PartyAi.Settings.WorkshopReviewDays, ledger.DaysTracked - 1))
                .ToString();
        IsUnderperforming = manager.IsUnderperforming(workshop);
        IsNearBankruptcy = workshop.Capital <= Campaign.Current.Models.WorkshopModel.CapitalLowLimit;

        int daysIdle = workshop.LastRunCampaignTime == CampaignTime.Never ? -1 : (int)workshop.LastRunCampaignTime.ElapsedDaysUntilNow;
        Activity = daysIdle switch
        {
            < 0 => string.Empty,
            <= 1 => L.S("{=PAI_WORKSHOP_RUNNING}Producing"),
            _ => L.T("{=PAI_WORKSHOP_IDLE}Idle for {DAYS} days", "DAYS", daysIdle).ToString()
        };

        if (_recommendation is { } better)
        {
            Recommendation = L.T("{=PAI_WORKSHOP_BETTER}Better here: {TYPE} ({COST} gold to convert)")
                .SetTextVariable("TYPE", better.Type.Name)
                .SetTextVariable("COST", WorkshopManagementBehavior.ConversionCost(better.Type))
                .ToString();
            SwitchText = L.T("{=PAI_WORKSHOP_SWITCH}Switch to {TYPE}", "TYPE", better.Type.Name).ToString();
        }
        else
        {
            Recommendation = L.S("{=PAI_WORKSHOP_SUITED}Well suited to this town");
            SwitchText = string.Empty;
        }

        SellText = L.T("{=PAI_WORKSHOP_SELL}Sell ({PRICE})", "PRICE", WorkshopManagementBehavior.SalePrice(workshop)).ToString();
        CanSell = WorkshopManagementBehavior.CanSell(workshop, out _);
    }

    [DataSourceProperty] public string Town { get; }
    [DataSourceProperty] public string TypeName { get; }
    [DataSourceProperty] public string Capital { get; }
    [DataSourceProperty] public string Income { get; }
    [DataSourceProperty] public string Trend { get; }
    [DataSourceProperty] public string Activity { get; }
    [DataSourceProperty] public string Recommendation { get; }
    [DataSourceProperty] public string SwitchText { get; }
    [DataSourceProperty] public string SellText { get; }
    [DataSourceProperty] public bool IsUnderperforming { get; }
    [DataSourceProperty] public bool IsNearBankruptcy { get; }
    [DataSourceProperty] public bool HasRecommendation => _recommendation is not null;
    [DataSourceProperty] public bool CanSell { get; }
    [DataSourceProperty] public HintViewModel SellHint => new(L.T("{=PAI_WORKSHOP_SELL_HINT}Sell to a local notable at the notable price. Cannot be undone."));
    [DataSourceProperty] public HintViewModel SwitchHint => new(L.T("{=PAI_WORKSHOP_SWITCH_HINT}Convert production. The cost is paid from your gold and must not break the reserve."));

    public void ExecuteSwitch()
    {
        if (_recommendation is { } better && PartyAi.Workshops.TrySwitchProduction(_workshop, better.Type))
        {
            _refresh();
        }
    }

    public void ExecuteSell()
    {
        InformationManager.ShowInquiry(new InquiryData(
            L.S("{=PAI_WORKSHOP_SELL_TITLE}Sell Workshop"),
            L.T("{=PAI_WORKSHOP_SELL_PROMPT}Sell the {TYPE} in {TOWN} for {PRICE} gold?")
                .SetTextVariable("TYPE", _workshop.WorkshopType.Name)
                .SetTextVariable("TOWN", _workshop.Settlement.Name)
                .SetTextVariable("PRICE", WorkshopManagementBehavior.SalePrice(_workshop))
                .ToString(),
            true,
            true,
            L.Game("str_yes"),
            L.Game("str_cancel"),
            () =>
            {
                PartyAi.Workshops.TrySell(_workshop);
                _refresh();
            },
            null));
    }
}

/// <summary>Clan finances at a glance, the treasury policy that governs automation, and workshops.</summary>
public sealed class EconomyPageVM : ViewModel
{
    private MBBindingList<FinanceLineVM> _lines = new();
    private MBBindingList<WorkshopItemVM> _workshops = new();
    private MBBindingList<SettingRowVM> _policy = new();
    private bool _isVisible;

    public EconomyPageVM()
    {
        var rows = new List<SettingRowVM>
        {
            SettingRowVM.Number("{=PAI_GOLD_RESERVE}Gold reserve", "{=PAI_GOLD_RESERVE_HINT}Automation (construction funding, caravans, troop upgrades and conversions, workshop conversions) never spends your gold below this amount.",
                0, 500000, () => PartyAi.Settings.GoldReserve, value => PartyAi.Settings.GoldReserve = value),
            SettingRowVM.Number("{=PAI_MIN_DAILY_BALANCE}Minimum daily balance", "{=PAI_MIN_DAILY_BALANCE_HINT}Automation that adds recurring wages (new clan parties, garrison reinforcements) must keep the projected daily balance at or above this value. Set it above zero to keep growing your treasury; set it negative to allow a deficit.",
                -5000, 5000, () => PartyAi.Settings.MinimumDailyBalance, value => PartyAi.Settings.MinimumDailyBalance = value),
            SettingRowVM.Enum<WorkshopMode>("{=PAI_WORKSHOP_MODE}Workshop management", "{=PAI_WORKSHOP_MODE_HINT}Recommend reports a better production type for workshops that keep losing money. Auto also pays for the conversion when the reserve allows it.",
                WorkshopModeText, () => PartyAi.Settings.WorkshopMode, value => PartyAi.Settings.WorkshopMode = value),
            SettingRowVM.Number("{=PAI_WORKSHOP_REVIEW_DAYS}Workshop review period", "{=PAI_WORKSHOP_REVIEW_DAYS_HINT}Days of falling capital and zero income before a workshop counts as unprofitable.",
                3, 30, () => PartyAi.Settings.WorkshopReviewDays, value => PartyAi.Settings.WorkshopReviewDays = value,
                value => L.T("{=PAI_TOWN_DAYS}{DAYS} days", "DAYS", value).ToString()),
            SettingRowVM.Enum<WorkshopMode>("{=PAI_WORKSHOP_BUY_MODE}Buying workshops", "{=PAI_WORKSHOP_BUY_MODE_HINT}Looks at notable-owned workshops in your own towns and in the town you are visiting. A workshop qualifies when its production ranks near the top for that town. Recommend only reports it; Auto buys it when the price is at most half of the gold above the reserve and your clan tier allows another workshop.",
                WorkshopModeText, () => PartyAi.Settings.WorkshopBuyMode, value => PartyAi.Settings.WorkshopBuyMode = value),
            SettingRowVM.Info("{=PAI_WORKSHOP_BUY_ADVICE}Purchase advice", PurchaseAdvice),
            SettingRowVM.Action("{=PAI_WORKSHOP_BUY_NOW}Buy the recommended workshop", null, "{=PAI_WORKSHOP_BUY_BUTTON}Buy", () =>
            {
                if (WorkshopManagementBehavior.RecommendedPurchase() is { } purchase)
                {
                    PartyAi.Workshops.TryBuy(purchase.Workshop);
                    Refresh();
                }
            }, () => WorkshopManagementBehavior.RecommendedPurchase() is { } purchase && WorkshopManagementBehavior.CanAffordPurchase(purchase.Cost))
        };

        var list = new MBBindingList<SettingRowVM>();
        foreach (SettingRowVM row in rows)
        {
            row.Changed += Refresh;
            list.Add(row);
        }

        Policy = list;
        Refresh();
    }

    [DataSourceProperty] public string Title => L.S("{=PAI_TAB_ECONOMY}Economy");
    [DataSourceProperty] public string BalanceHeader => L.S("{=PAI_BALANCE_HEADER}Daily balance");
    [DataSourceProperty] public string PolicyHeader => L.S("{=PAI_POLICY_HEADER}Treasury policy");
    [DataSourceProperty] public string WorkshopsHeader => L.S("{=PAI_WORKSHOPS_HEADER}Workshops");
    [DataSourceProperty] public string NoWorkshopsText => L.S("{=PAI_NO_WORKSHOPS}You do not own any workshops.");

    [DataSourceProperty] public string GoldText { get; private set; } = string.Empty;
    [DataSourceProperty] public string DailyChangeText { get; private set; } = string.Empty;
    [DataSourceProperty] public string ReserveText { get; private set; } = string.Empty;
    [DataSourceProperty] public bool IsDeficit { get; private set; }
    [DataSourceProperty] public bool IsBelowReserve { get; private set; }
    [DataSourceProperty] public bool HasWorkshops => _workshops.Count > 0;

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (value != _isVisible)
            {
                _isVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsVisible));
                if (value)
                {
                    Refresh();
                }
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<FinanceLineVM> Lines
    {
        get => _lines;
        private set
        {
            if (value != _lines)
            {
                _lines = value;
                OnPropertyChangedWithValue(value, nameof(Lines));
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<WorkshopItemVM> Workshops
    {
        get => _workshops;
        private set
        {
            if (value != _workshops)
            {
                _workshops = value;
                OnPropertyChangedWithValue(value, nameof(Workshops));
                OnPropertyChanged(nameof(HasWorkshops));
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<SettingRowVM> Policy
    {
        get => _policy;
        private set
        {
            if (value != _policy)
            {
                _policy = value;
                OnPropertyChangedWithValue(value, nameof(Policy));
            }
        }
    }

    public void Refresh()
    {
        float change = Treasury.ProjectedDailyChange;
        GoldText = L.T("{=PAI_GOLD}Gold: {GOLD}", "GOLD", Treasury.Gold).ToString();
        DailyChangeText = L.T("{=PAI_DAILY_CHANGE}Projected daily change: {CHANGE}", "CHANGE", (change >= 0 ? "+" : string.Empty) + (int)Math.Round(change)).ToString();
        ReserveText = L.T("{=PAI_RESERVE_STATUS}Reserve {RESERVE}; {SPENDABLE} available to automation", "RESERVE", Treasury.Reserve)
            .SetTextVariable("SPENDABLE", Treasury.Spendable)
            .ToString();
        IsDeficit = change < PartyAi.Settings.MinimumDailyBalance;
        IsBelowReserve = Treasury.Gold < Treasury.Reserve;
        OnPropertyChanged(nameof(GoldText));
        OnPropertyChanged(nameof(DailyChangeText));
        OnPropertyChanged(nameof(ReserveText));
        OnPropertyChanged(nameof(IsDeficit));
        OnPropertyChanged(nameof(IsBelowReserve));

        var lines = new MBBindingList<FinanceLineVM>();
        foreach ((string name, float number) in Treasury.DailyChangeBreakdown.GetLines())
        {
            if (Math.Abs(number) >= 0.5f)
            {
                lines.Add(new FinanceLineVM(name, number));
            }
        }

        Lines = lines;

        var workshops = new MBBindingList<WorkshopItemVM>();
        foreach (Workshop workshop in WorkshopManagementBehavior.PlayerWorkshops.OrderBy(workshop => workshop.Settlement.Name.ToString()))
        {
            workshops.Add(new WorkshopItemVM(workshop, Refresh));
        }

        Workshops = workshops;

        foreach (SettingRowVM row in _policy)
        {
            row.RefreshValues();
        }
    }

    private static string PurchaseAdvice()
    {
        if (!WorkshopManagementBehavior.CanOwnMore)
        {
            return L.T("{=PAI_WORKSHOP_BUY_LIMIT}Your clan tier allows {MAX} workshops and you own {COUNT}.")
                .SetTextVariable("MAX", WorkshopManagementBehavior.MaxWorkshops)
                .SetTextVariable("COUNT", Hero.MainHero.OwnedWorkshops.Count)
                .ToString();
        }

        WorkshopAdvisor.Purchase? best = WorkshopManagementBehavior.RecommendedPurchase();
        if (best is null)
        {
            return L.S("{=PAI_WORKSHOP_BUY_NONE}Nothing worth buying in your towns or where you are. Visit a town to check its workshops.");
        }

        Workshop workshop = best.Value.Workshop;
        string affordability = WorkshopManagementBehavior.CanAffordPurchase(best.Value.Cost)
            ? L.S("{=PAI_WORKSHOP_BUY_AFFORDABLE}affordable within the treasury policy")
            : L.S("{=PAI_WORKSHOP_BUY_TOO_DEAR}too expensive for the treasury policy right now");
        return L.T("{=PAI_WORKSHOP_BUY_BEST}{TYPE} in {TOWN} for {COST} gold ({AFFORD}).")
            .SetTextVariable("TYPE", workshop.WorkshopType.Name)
            .SetTextVariable("TOWN", workshop.Settlement.Name)
            .SetTextVariable("COST", best.Value.Cost)
            .SetTextVariable("AFFORD", affordability)
            .ToString();
    }

    private static string WorkshopModeText(WorkshopMode mode) => mode switch
    {
        WorkshopMode.Recommend => L.S("{=PAI_GOVERNOR_RECOMMEND}Recommend"),
        WorkshopMode.Auto => L.S("{=PAI_WORKSHOP_AUTO}Automatic"),
        _ => L.S("{=PAI_GOVERNOR_OFF}Off")
    };
}
