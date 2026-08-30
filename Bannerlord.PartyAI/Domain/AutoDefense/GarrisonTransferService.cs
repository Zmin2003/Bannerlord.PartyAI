using Bannerlord.PartyAI.CampaignBehaviors;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Domain.AutoDefense;

internal static class GarrisonTransferService
{
    private static readonly MethodInfo? LeaveTroopsToGarrisonMethod = AccessTools.Method(
        typeof(GarrisonTroopsCampaignBehavior),
        "LeaveTroopsToGarrison",
        new[]
        {
            typeof(MobileParty),
            typeof(Settlement),
            typeof(int),
            typeof(bool)
        });

    internal static bool IsAutomatedTransferInProgress { get; private set; }

    internal static bool ShouldSuppressVanillaDonation(
        MobileParty mobileParty,
        Settlement settlement)
    {
        AutoDefenseBehavior? behavior = Campaign.Current?
            .GetCampaignBehavior<AutoDefenseBehavior>();
        return behavior?.OwnsGarrisonDonation(mobileParty, settlement) == true;
    }

    internal static int TryDonate(
        AutoDefenseAssignment assignment,
        bool enabled,
        int targetGarrisonTroops,
        float maximumPartyFraction,
        int minimumPartyTroops)
    {
        Hero hero = assignment.Hero;
        MobileParty? party = hero?.PartyBelongedTo;
        Settlement settlement = assignment.TargetSettlement;
        PartyAiOrder? activeOrder = hero is null
            ? null
            : SubModule.PartySettingsManager.Settings(hero).Order;

        if (!enabled
            || assignment.DonationCompleted
            || assignment.AutomationToken <= 0
            || party is null
            || party.CurrentSettlement != settlement
            || settlement?.Town is null
            || settlement.IsUnderSiege
            || FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction)
            || activeOrder?.AutomationToken != assignment.AutomationToken
            || activeOrder.Behavior != PartyAiOrderType.DefendSettlement
            || activeOrder.Target != settlement
            || !SubModule.PartySettingsManager.Settings(hero).AllowDonateTroops)
        {
            return 0;
        }

        if (settlement.Town.GarrisonParty is null)
        {
            settlement.AddGarrisonParty();
        }

        MobileParty? garrison = settlement.Town.GarrisonParty;
        if (garrison?.MemberRoster is null)
        {
            return 0;
        }

        int currentGarrisonTroops = garrison.MemberRoster.TotalManCount;
        int needed = Math.Max(0, targetGarrisonTroops - currentGarrisonTroops);
        int requested = Math.Min(
            needed,
            GetMaximumDonation(
                party,
                maximumPartyFraction,
                minimumPartyTroops));

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
