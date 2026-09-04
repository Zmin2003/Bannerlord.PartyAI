using Helpers;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Parties;

internal static class Navigation
{
    /// <summary>
    /// Nearest settlement matching <paramref name="condition"/>, with a little randomness among
    /// the closest few so parties do not all converge on the same place.
    /// </summary>
    public static Settlement? FindNearestSettlement(MobileParty party, Func<Settlement, bool> condition)
        => FindNearestSettlement(party, condition, party.NavigationCapability);

    public static Settlement? FindNearestSettlement(IMapPoint point)
        => FindNearestSettlement(point, null, MobileParty.NavigationType.Default);

    private static Settlement? FindNearestSettlement(
        IMapPoint point,
        Func<Settlement, bool>? condition,
        MobileParty.NavigationType navigation)
    {
        if (navigation == MobileParty.NavigationType.None
            || !Enum.IsDefined(typeof(MobileParty.NavigationType), navigation))
        {
            navigation = MobileParty.NavigationType.Default;
        }

        var candidates = condition is null ? Settlement.All.AsEnumerable() : Settlement.All.Where(condition);

        return candidates
            .OrderBy(settlement => DistanceHelper.FindClosestDistanceFromMapPointToSettlement(point, settlement, navigation, out _))
            .Skip(MBRandom.RandomInt(5))
            .FirstOrDefault();
    }
}
