using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Models;

internal class PAITroopUpgradeModel : PartyTroopUpgradeModel
{
    public override bool CanPartyUpgradeTroopToTarget(PartyBase party, CharacterObject character, CharacterObject target)
    {
        return BaseModel.CanPartyUpgradeTroopToTarget(party, character, target);
    }

    public override bool DoesPartyHaveRequiredItemsForUpgrade(PartyBase party, CharacterObject upgradeTarget)
    {
        if (party.Owner?.Equals(Hero.MainHero) ?? false)
        {
            return BaseModel.DoesPartyHaveRequiredItemsForUpgrade(party, upgradeTarget);
        }

        // let AI always upgrade regardless of items
        return true;
    }

    public override bool DoesPartyHaveRequiredPerksForUpgrade(PartyBase party, CharacterObject character, CharacterObject upgradeTarget, out PerkObject requiredPerk)
    {
        return BaseModel.DoesPartyHaveRequiredPerksForUpgrade(party, character, upgradeTarget, out requiredPerk);
    }

    public override ExplainedNumber GetGoldCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
    {
        return BaseModel.GetGoldCostForUpgrade(party, characterObject, upgradeTarget);
    }

    public override int GetSkillXpFromUpgradingTroops(PartyBase party, CharacterObject troop, int numberOfTroops)
    {
        return BaseModel.GetSkillXpFromUpgradingTroops(party, troop, numberOfTroops);
    }

    public override float GetUpgradeChanceForTroopUpgrade(PartyBase party, CharacterObject troop, int upgradeTargetIndex)
    {
        if (!IsPartyManaged(party))
        {
            return BaseModel.GetUpgradeChanceForTroopUpgrade(party, troop, upgradeTargetIndex);
        }

        if (upgradeTargetIndex < 0 || upgradeTargetIndex >= troop.UpgradeTargets.Length || troop.UpgradeTargets.Length == 0)
        {
            return 0.00001f;
        }

        PartyAiEntitySettings heroSettings;
        if (party.MobileParty.IsGarrison)
        {
            heroSettings = SubModule.PartySettingsManager.Settings(party.MobileParty.CurrentSettlement);
        }
        else
        {
            heroSettings = SubModule.PartySettingsManager.Settings(party.LeaderHero);
        }

        PartyComposition comp = Recruitment.GetPartyComposition(party, heroSettings, troop);

        if (heroSettings.MaxTroopTier > 0 && troop.Tier >= heroSettings.MaxTroopTier) { return 0f; }

        if (Recruitment.ShouldRecruit(comp, heroSettings, troop.UpgradeTargets[upgradeTargetIndex], party))
        {
            return 1f;
        }

        for (int i = 0; i < troop.UpgradeTargets.Length; i++)
        {
            if (i == upgradeTargetIndex)
            {
                continue;
            }

            if (Recruitment.ShouldRecruit(comp, heroSettings, troop.UpgradeTargets[i], party))
            {
                return 0f;
            }
        }

        if (heroSettings.PartyTemplate?.Troops.Contains(troop.UpgradeTargets[upgradeTargetIndex]) ?? true)
        {
            IEnumerable<FormationClass> newTargets = Recruitment.UpgradeTargets(troop.UpgradeTargets[upgradeTargetIndex], true, heroSettings.PartyTemplate).ConvertAll(c => FormationClassExtensions.FallbackClass(c.DefaultFormationClass)).Distinct();
            IEnumerable<FormationClass> currentTargets = Recruitment.UpgradeTargets(troop, true, heroSettings.PartyTemplate).ConvertAll(c => FormationClassExtensions.FallbackClass(c.DefaultFormationClass)).Distinct();
            if (Enumerable.SequenceEqual(newTargets, currentTargets))
            {
                return 1f;
            }
        }

        return 0f;
    }

    public override int GetXpCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
    {
        return BaseModel.GetXpCostForUpgrade(party, characterObject, upgradeTarget);
    }

    public override bool IsTroopUpgradeable(PartyBase party, CharacterObject character)
    {
        bool result = BaseModel.IsTroopUpgradeable(party, character);

        if (!result || !IsPartyManaged(party))
        {
            return result;
        }

        for (int i = 0; i < character.UpgradeTargets.Length; i++)
        {
            if (GetUpgradeChanceForTroopUpgrade(party, character, i) > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartyManaged(PartyBase party)
    {
        MobileParty? mobileParty = party.MobileParty;
        if (mobileParty is null)
        {
            return false;
        }

        if (mobileParty.IsGarrison)
        {
            return SubModule.PartySettingsManager.IsGarrisonManageable(mobileParty.CurrentSettlement);
        }

        if (mobileParty == MobileParty.MainParty)
        {
            PartyAiEntitySettings settings = SubModule.PartySettingsManager.Settings(Hero.MainHero);
            return settings.SettlementAutomation >= SettlementAutomationLevel.RecruitAndUpgrade;
        }

        return SubModule.PartySettingsManager.IsManageable(party.LeaderHero);
    }
}
