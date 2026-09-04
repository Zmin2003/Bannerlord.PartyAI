using Bannerlord.PartyAI.Finance;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Orders;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Towns;

/// <summary>Moves troops from an assigned defender into a garrison through the vanilla transfer logic.</summary>
internal static class GarrisonTransfer
{
    private static readonly MethodInfo? LeaveTroopsToGarrisonMethod = AccessTools.Method(
        typeof(GarrisonTroopsCampaignBehavior),
        "LeaveTroopsToGarrison",
        [typeof(MobileParty), typeof(Settlement), typeof(int), typeof(bool)]);

    /// <summary>True while this class is invoking the vanilla transfer, so patches let it through.</summary>
    internal static bool IsAutomatedTransferInProgress { get; private set; }

    /// <summary>Vanilla donation is suppressed for parties whose donation the mod schedules itself.</summary>
    internal static bool ShouldSuppressVanillaDonation(MobileParty party, Settlement settlement)
        => PartyAi.IsActive && PartyAi.Defense.OwnsGarrisonDonation(party, settlement);

    internal static int TryDonate(
        DefenseAssignment assignment,
        bool enabled,
        int targetGarrisonTroops,
        float maximumPartyFraction,
        int minimumPartyTroops)
    {
        Hero hero = assignment.Hero;
        MobileParty? party = hero?.PartyBelongedTo;
        Settlement settlement = assignment.TargetSettlement;
        PartyOrder? activeOrder = hero is null
            ? null
            : PartyAi.Parties.Profile(hero).Order;

        if (!enabled
            || assignment.DonationCompleted
            || assignment.AutomationToken <= 0
            || party is null
            || party.CurrentSettlement != settlement
            || settlement?.Town is null
            || settlement.IsUnderSiege
            || FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction)
            || activeOrder?.AutomationToken != assignment.AutomationToken
            || activeOrder.Behavior != PartyOrderType.DefendSettlement
            || activeOrder.Target != settlement
            || !PartyAi.Parties.Profile(hero).AllowDonateTroops)
        {
            return 0;
        }

        MobileParty? garrison = settlement.Town.GarrisonParty;
        int currentGarrisonTroops = garrison?.MemberRoster?.TotalManCount ?? 0;
        int needed = Math.Max(0, targetGarrisonTroops - currentGarrisonTroops);
        int requested = Math.Min(
            needed,
            GetMaximumDonation(
                party,
                maximumPartyFraction,
                minimumPartyTroops));

        // Garrison troops are paid by the clan; only hand over as many as the daily balance can carry.
        requested = AffordableDonation(party, requested);

        if (requested <= 0)
        {
            assignment.MarkDonationCompleted(0);
            return 0;
        }

        GarrisonTroopsCampaignBehavior? behavior = Campaign.Current
            .GetCampaignBehavior<GarrisonTroopsCampaignBehavior>();
        if (behavior is null || LeaveTroopsToGarrisonMethod is null)
        {
            return 0;
        }

        if (garrison is null)
        {
            settlement.AddGarrisonParty();
            garrison = settlement.Town.GarrisonParty;
        }

        if (garrison?.MemberRoster is null)
        {
            return 0;
        }

        int before = party.MemberRoster.TotalRegulars;
        bool previousTransferState = IsAutomatedTransferInProgress;
        bool invocationCompleted = false;
        try
        {
            IsAutomatedTransferInProgress = true;
            LeaveTroopsToGarrisonMethod.Invoke(
                behavior,
                new object[] { party, settlement, requested, false });
            invocationCompleted = true;
        }
        catch (TargetInvocationException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (TargetParameterCountException)
        {
        }
        catch (MethodAccessException)
        {
        }
        finally
        {
            IsAutomatedTransferInProgress = previousTransferState;
        }

        int donated = Math.Max(0, before - party.MemberRoster.TotalRegulars);
        if (invocationCompleted || donated > 0)
        {
            assignment.MarkDonationCompleted(donated);
        }
        return donated;
    }

    /// <summary>Largest donation whose added garrison wages keep the projected daily balance acceptable.</summary>
    internal static int AffordableDonation(MobileParty party, int requested)
    {
        while (requested > 0 && !Treasury.CanAffordRecurring(Treasury.EstimatedWage(party, requested)))
        {
            requested = requested > 10 ? requested * 3 / 4 : requested - 1;
        }

        return Math.Max(0, requested);
    }

    internal static int GetMaximumDonation(
        MobileParty party,
        float maximumPartyFraction,
        int minimumPartyTroops)
    {
        if (party?.MemberRoster is null)
        {
            return 0;
        }

        int regularTroops = party.MemberRoster.TotalRegulars;
        float fraction = Math.Max(0f, Math.Min(1f, maximumPartyFraction));
        int fractionCap = (int)Math.Floor(regularTroops * fraction);
        int reserveCap = Math.Max(
            0,
            regularTroops - Math.Max(0, minimumPartyTroops));
        return Math.Min(fractionCap, reserveCap);
    }
}
