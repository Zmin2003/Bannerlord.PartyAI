using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Recruitment;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.GameModels;

/// <summary>
/// Steers AI upgrades of managed parties along the template: only upgrade paths that lead to
/// wanted formation classes get a chance, and AI parties never lack the required items.
/// </summary>
internal sealed class TroopUpgradeModel : PartyTroopUpgradeModel
{
    public override bool CanPartyUpgradeTroopToTarget(PartyBase party, CharacterObject character, CharacterObject target)
        => BaseModel.CanPartyUpgradeTroopToTarget(party, character, target);

    public override bool DoesPartyHaveRequiredItemsForUpgrade(PartyBase party, CharacterObject upgradeTarget)
        => party.Owner == Hero.MainHero
            ? BaseModel.DoesPartyHaveRequiredItemsForUpgrade(party, upgradeTarget)
            : true;

    public override bool DoesPartyHaveRequiredPerksForUpgrade(PartyBase party, CharacterObject character, CharacterObject upgradeTarget, out PerkObject requiredPerk)
        => BaseModel.DoesPartyHaveRequiredPerksForUpgrade(party, character, upgradeTarget, out requiredPerk);

    public override ExplainedNumber GetGoldCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
        => BaseModel.GetGoldCostForUpgrade(party, characterObject, upgradeTarget);

    public override int GetSkillXpFromUpgradingTroops(PartyBase party, CharacterObject troop, int numberOfTroops)
        => BaseModel.GetSkillXpFromUpgradingTroops(party, troop, numberOfTroops);

    public override int GetXpCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
        => BaseModel.GetXpCostForUpgrade(party, characterObject, upgradeTarget);

    public override float GetUpgradeChanceForTroopUpgrade(PartyBase party, CharacterObject troop, int upgradeTargetIndex)
    {
        if (!TryGetManagedProfile(party, out PartyProfile? profile))
        {
            return BaseModel.GetUpgradeChanceForTroopUpgrade(party, troop, upgradeTargetIndex);
        }

        if (upgradeTargetIndex < 0 || upgradeTargetIndex >= troop.UpgradeTargets.Length)
        {
            return 0.00001f;
        }

        if (profile.MaxTroopTier > 0 && troop.Tier >= profile.MaxTroopTier)
        {
            return 0f;
        }

        CharacterObject target = troop.UpgradeTargets[upgradeTargetIndex];
        PartyComposition composition = RecruitmentRules.GetPartyComposition(party, profile, troop);

        if (RecruitmentRules.ShouldRecruit(composition, profile, target, party))
        {
            return 1f;
        }

        // Some other branch of this troop is wanted: never take this one.
        for (int index = 0; index < troop.UpgradeTargets.Length; index++)
        {
            if (index != upgradeTargetIndex && RecruitmentRules.ShouldRecruit(composition, profile, troop.UpgradeTargets[index], party))
            {
                return 0f;
            }
        }

        // Nothing is specifically wanted: allow the upgrade if it keeps the troop on the same final formations.
        bool inTemplate = profile.Template?.Troops.Contains(target) ?? true;
        if (inTemplate
            && RecruitmentRules.FinalFormations(target, profile.Template)
                .SequenceEqual(RecruitmentRules.FinalFormations(troop, profile.Template)))
        {
            return 1f;
        }

        return 0f;
    }

    public override bool IsTroopUpgradeable(PartyBase party, CharacterObject character)
    {
        bool result = BaseModel.IsTroopUpgradeable(party, character);
        if (!result || !TryGetManagedProfile(party, out _))
        {
            return result;
        }

        for (int index = 0; index < character.UpgradeTargets.Length; index++)
        {
            if (GetUpgradeChanceForTroopUpgrade(party, character, index) > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetManagedProfile(PartyBase party, out PartyProfile profile)
    {
        profile = null!;
        MobileParty? mobile = party.MobileParty;
        if (mobile is null || !PartyAi.IsActive)
        {
            return false;
        }

        if (mobile.IsGarrison)
        {
            if (!PartyAi.Parties.IsGarrisonManageable(mobile.CurrentSettlement))
            {
                return false;
            }

            profile = PartyAi.Parties.Profile(mobile.CurrentSettlement);
            return true;
        }

        if (!PartyAi.Parties.IsManageable(party.LeaderHero))
        {
            return false;
        }

        profile = PartyAi.Parties.Profile(party.LeaderHero);
        return true;
    }
}
