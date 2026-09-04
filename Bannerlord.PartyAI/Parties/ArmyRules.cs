using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Parties;

/// <summary>Army-related rules derived from a party's profile.</summary>
internal static class ArmyRules
{
    /// <summary>Strips parties that must not be summoned from an AI leader's call-to-arms list.</summary>
    public static void RemoveForbiddenParties(MobileParty? leader, MBList<MobileParty>? candidates)
    {
        if (candidates is null || leader?.LeaderHero is null)
        {
            return;
        }

        for (int index = candidates.Count - 1; index >= 0; index--)
        {
            Hero? hero = candidates[index]?.LeaderHero;
            if (hero is null || hero == leader.LeaderHero || !PartyAi.Parties.IsHeroManageable(hero))
            {
                continue;
            }

            if (PartyAi.Defense.IsAutomaticallyDefending(hero) || !PartyAi.Parties.Profile(hero).AllowJoinArmies)
            {
                candidates.RemoveAt(index);
            }
        }
    }

    /// <summary>Whether a managed party has to leave the army it is currently in.</summary>
    public static bool MustLeaveArmy(Army army, PartyProfile profile)
    {
        if (army.LeaderParty.LeaderHero == Hero.MainHero)
        {
            return false;
        }

        bool illegalRaid = !profile.AllowRaidVillages && army.ArmyType == Army.ArmyTypes.Raider;
        bool illegalSiege = !profile.AllowSieging && army.ArmyType == Army.ArmyTypes.Besieger;
        return !profile.AllowJoinArmies || illegalRaid || illegalSiege;
    }

    /// <summary>Leaves the army and gives the leader back the influence spent to summon us.</summary>
    public static void LeaveArmyWithRefund(MobileParty party)
    {
        Army? army = party.Army;
        if (army is null)
        {
            return;
        }

        int influence = Campaign.Current.Models.ArmyManagementCalculationModel
            .CalculatePartyInfluenceCost(army.LeaderParty, party);
        ChangeClanInfluenceAction.Apply(army.LeaderParty.LeaderHero.Clan, influence);
        party.Army = null;
    }
}
