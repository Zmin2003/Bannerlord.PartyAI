using Bannerlord.PartyAI.Core;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Parties.Recruitment;

/// <summary>
/// Runs the per-party <see cref="SettlementAutomationLevel"/> actions the moment a managed party
/// (or the player) enters a town or village: recruit, upgrade, convert, re-equip heroes.
/// </summary>
public sealed class SettlementAutomationBehavior : CampaignBehaviorBase
{
    private static readonly MethodInfo? UpgradeReadyTroopsMethod = AccessTools.Method(
        typeof(PartyUpgraderCampaignBehavior),
        "UpgradeReadyTroops");

    private readonly TroopConverter _converter;

    public SettlementAutomationBehavior(TroopConverter converter)
    {
        _converter = converter;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.OnLootDistributedToPartyEvent.AddNonSerializedListener(this, OnLootDistributed);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        Hero? leader = party?.LeaderHero ?? hero;
        if (party?.Party is null
            || leader is null
            || settlement is null
            || (!settlement.IsTown && !settlement.IsVillage)
            || settlement.IsUnderSiege
            || settlement.IsRaided
            || settlement.IsUnderRaid
            || !PartyAi.Parties.IsAutomationEligible(leader))
        {
            return;
        }

        PartyProfile profile = PartyAi.Parties.Profile(leader);
        SettlementAutomationLevel level = profile.SettlementAutomation;
        if (level == SettlementAutomationLevel.Off)
        {
            return;
        }

        int recruited = Recruit(party, settlement, profile);
        int upgraded = level >= SettlementAutomationLevel.RecruitAndUpgrade ? UpgradeReadyTroops(party) : 0;
        int converted = 0;
        int equipped = 0;
        if (level == SettlementAutomationLevel.Full)
        {
            converted = _converter.BalancePartyNow(party, profile);
            equipped = HeroEquipmentOptimizer.OptimizeParty(party);
        }

        if (party == MobileParty.MainParty && recruited + upgraded + converted + equipped > 0)
        {
            Notify.Info(L.T("{=PAI_AUTOMATION_RESULT}Party AI: recruited {RECRUITED}, upgraded {UPGRADED}, balanced {BALANCED}, equipped {EQUIPPED}.")
                .SetTextVariable("RECRUITED", recruited)
                .SetTextVariable("UPGRADED", upgraded)
                .SetTextVariable("BALANCED", converted)
                .SetTextVariable("EQUIPPED", equipped));
        }
    }

    private void OnLootDistributed(PartyBase winner, PartyBase defeated, ItemRoster loot)
    {
        MobileParty? party = winner?.MobileParty;
        Hero? leader = party?.LeaderHero;
        if (party is not null
            && PartyAi.Parties.IsAutomationEligible(leader)
            && PartyAi.Parties.Profile(leader).SettlementAutomation == SettlementAutomationLevel.Full)
        {
            HeroEquipmentOptimizer.OptimizeParty(party);
        }
    }

    private static int Recruit(MobileParty party, Settlement settlement, PartyProfile profile)
    {
        if (!profile.RecruitFromEnemySettlements
            && FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
        {
            return 0;
        }

        return VolunteerRecruiter.Recruit(party, settlement, profile);
    }

    /// <summary>Returns a rough "tier points gained" figure so the summary message has something to show.</summary>
    private static int UpgradeReadyTroops(MobileParty party)
    {
        if (party == MobileParty.MainParty)
        {
            return PlayerTroopUpgrader.UpgradeReadyTroops();
        }

        PartyUpgraderCampaignBehavior? behavior = Campaign.Current.GetCampaignBehavior<PartyUpgraderCampaignBehavior>();
        if (UpgradeReadyTroopsMethod is null || behavior is null)
        {
            return 0;
        }

        int before = TierPoints(party);
        try
        {
            UpgradeReadyTroopsMethod.Invoke(behavior, [party.Party]);
        }
        catch (TargetInvocationException)
        {
            return 0;
        }

        return Math.Max(0, TierPoints(party) - before);
    }

    private static int TierPoints(MobileParty party)
        => party.MemberRoster.GetTroopRoster()
            .Where(element => !element.Character.IsHero)
            .Sum(element => element.Character.Tier * element.Number);
}
