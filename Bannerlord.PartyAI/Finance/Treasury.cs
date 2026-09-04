using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PartyAI.Finance;

/// <summary>
/// The single place every automated expense asks before spending the player's gold. Enforces
/// the gold reserve for one-off costs and the minimum daily balance for recurring costs.
/// </summary>
public static class Treasury
{
    public static int Gold => Hero.MainHero?.Gold ?? 0;

    public static int Reserve => PartyAi.Settings.GoldReserve;

    /// <summary>Gold above the reserve that automation may spend.</summary>
    public static int Spendable => Math.Max(0, Gold - Reserve);

    /// <summary>Whether a one-off purchase keeps the treasury above the reserve.</summary>
    public static bool CanSpend(int amount) => amount <= 0 || amount <= Spendable;

    /// <summary>Projected daily gold change of the player clan, as shown in the clan finance screen.</summary>
    public static float ProjectedDailyChange
        => Clan.PlayerClan is null
            ? 0f
            : Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(Clan.PlayerClan).ResultNumber;

    public static ExplainedNumber DailyChangeBreakdown
        => Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(Clan.PlayerClan, includeDescriptions: true);

    public static float DailyIncome
        => Campaign.Current.Models.ClanFinanceModel.CalculateClanIncome(Clan.PlayerClan).ResultNumber;

    public static float DailyExpenses
        => Campaign.Current.Models.ClanFinanceModel.CalculateClanExpenses(Clan.PlayerClan).ResultNumber;

    /// <summary>
    /// Whether taking on <paramref name="dailyCost"/> more per day keeps the projected balance at or
    /// above the configured minimum. A zero or negative cost is always affordable.
    /// </summary>
    public static bool CanAffordRecurring(float dailyCost)
        => dailyCost <= 0f || ProjectedDailyChange - dailyCost >= PartyAi.Settings.MinimumDailyBalance;

    /// <summary>Average daily wage of the clan's existing war parties (excluding the player's), used to estimate a new party's cost.</summary>
    public static int EstimatedNewPartyWage()
    {
        var wages = Clan.PlayerClan.WarPartyComponents
            .Select(component => component.MobileParty)
            .Where(party => party is not null && party != MobileParty.MainParty)
            .Select(party => party.TotalWage)
            .ToList();

        return wages.Count > 0 ? (int)wages.Average() : Math.Max(150, MobileParty.MainParty?.TotalWage ?? 300);
    }

    /// <summary>Approximate daily wage of <paramref name="troopCount"/> average troops of <paramref name="party"/>.</summary>
    public static int EstimatedWage(MobileParty party, int troopCount)
    {
        int men = party.MemberRoster.TotalManCount;
        if (men <= 0 || troopCount <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(party.TotalWage / (float)men * troopCount);
    }
}
