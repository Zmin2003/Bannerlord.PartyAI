using Bannerlord.PartyAI.Models;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Domain;

public static class CallingToArmy
{
    public static void RemoveForbiddenPartiesFromArmyCall(MobileParty thinkingParty, MBList<MobileParty>? partiesToCallToArmy)
    {
        var thinkingHero = thinkingParty?.LeaderHero;
        if (partiesToCallToArmy is null || thinkingHero is null)
        {
            return;
        }

        for (int index = partiesToCallToArmy.Count - 1; index >= 0; index--)
        {
            MobileParty candidate = partiesToCallToArmy[index];

            var candidateHero = candidate?.LeaderHero;
            if (candidateHero is null || candidateHero == thinkingHero
                || !SubModule.PartySettingsManager.IsHeroManageable(candidateHero))
            {
                continue;
            }

            if (SubModule.AutoDefenseBehavior?.IsAutomaticallyDefending(candidateHero) == true)
            {
                partiesToCallToArmy.RemoveAt(index);
                continue;
            }

            PartyAiEntitySettings settings = SubModule.PartySettingsManager.Settings(candidateHero);

            if (!settings.AllowJoinArmies)
            {
                partiesToCallToArmy.RemoveAt(index);
                continue;
            }
        }
    }
}
