using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.Patches;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.CampaignBehaviors;

internal sealed class SettlementAutomationBehavior : CampaignBehaviorBase
{
    private static readonly MethodInfo? UpgradeReadyTroopsMethod = AccessTools.Method(
        typeof(PartyUpgraderCampaignBehavior),
        "UpgradeReadyTroops");

    private readonly PartyAITroopRecruiter _troopRecruiter;

    internal SettlementAutomationBehavior(PartyAITroopRecruiter troopRecruiter)
    {
        _troopRecruiter = troopRecruiter;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.OnLootDistributedToPartyEvent.AddNonSerializedListener(this, OnLootDistributedToParty);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        Hero leader = party?.LeaderHero ?? hero;
        if (party?.Party is null
            || leader is null
            || settlement is null
            || (!settlement.IsTown && !settlement.IsVillage)
            || settlement.IsUnderSiege
            || settlement.IsRaided
            || settlement.IsUnderRaid
            || !SubModule.PartySettingsManager.IsSettlementAutomationEligible(leader))
        {
            return;
        }

        PartyAiEntitySettings settings = SubModule.PartySettingsManager.Settings(leader);
        if (settings.SettlementAutomation == SettlementAutomationLevel.Off)
        {
            return;
        }

        int recruited = Recruit(party, settlement, settings);
        int upgraded = 0;
        int balanced = 0;
        int equipped = 0;

        if (settings.SettlementAutomation >= SettlementAutomationLevel.RecruitAndUpgrade)
        {
            upgraded = UpgradeReadyTroops(party);
        }

        if (settings.SettlementAutomation == SettlementAutomationLevel.Full)
        {
            balanced = _troopRecruiter.BalancePartyNow(party, settings);
            equipped = HeroEquipmentOptimizer.OptimizeParty(party);
        }

        if (party == MobileParty.MainParty
            && (recruited > 0 || upgraded > 0 || balanced > 0 || equipped > 0))
        {
            TextObject message = new("{=PAI_AUTOMATION_RESULT}Party AI: recruited {RECRUITED}, upgraded {UPGRADED}, balanced {BALANCED}, equipped {EQUIPPED}.");
            message.SetTextVariable("RECRUITED", recruited);
            message.SetTextVariable("UPGRADED", upgraded);
            message.SetTextVariable("BALANCED", balanced);
            message.SetTextVariable("EQUIPPED", equipped);
            TaleWorlds.Library.InformationManager.DisplayMessage(new(message.ToString()));
        }
    }

    private static int Recruit(
        MobileParty party,
        Settlement settlement,
        PartyAiEntitySettings settings)
    {
        if (!settings.RecruitFromEnemySettlements
            && FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
        {
            return 0;
        }

        RecruitmentCampaignBehavior? behavior = Campaign.Current
            .GetCampaignBehavior<RecruitmentCampaignBehavior>();

        return RecruitmentCampaignBehaviorPatches.RecruitEligibleVolunteers(
            behavior,
            party,
            settlement,
            settings);
    }

    private static int UpgradeReadyTroops(MobileParty party)
    {
        if (party == MobileParty.MainParty)
        {
            return PlayerTroopUpgrader.UpgradeReadyTroops();
        }

        if (UpgradeReadyTroopsMethod is null)
        {
            return 0;
        }

        PartyUpgraderCampaignBehavior? behavior = Campaign.Current
            .GetCampaignBehavior<PartyUpgraderCampaignBehavior>();
        if (behavior is null)
        {
            return 0;
        }

        int powerBefore = party.MemberRoster
            .GetTroopRoster()
            .Where(element => !element.Character.IsHero)
            .Sum(element => element.Character.Tier * element.Number);

        try
        {
            UpgradeReadyTroopsMethod.Invoke(behavior, [party.Party]);
        }
        catch (TargetInvocationException)
        {
            return 0;
        }

        int powerAfter = party.MemberRoster
            .GetTroopRoster()
            .Where(element => !element.Character.IsHero)
            .Sum(element => element.Character.Tier * element.Number);

        return Math.Max(0, powerAfter - powerBefore);
    }

    private void OnLootDistributedToParty(PartyBase winnerParty, PartyBase defeatedParty, ItemRoster lootedItems)
    {
        MobileParty? party = winnerParty?.MobileParty;
        Hero? leader = party?.LeaderHero;
        if (party is null
            || leader is null
            || !SubModule.PartySettingsManager.IsSettlementAutomationEligible(leader))
        {
            return;
        }

        PartyAiEntitySettings settings = SubModule.PartySettingsManager.Settings(leader);
        if (settings.SettlementAutomation == SettlementAutomationLevel.Full)
        {
            HeroEquipmentOptimizer.OptimizeParty(party);
        }
    }
}
