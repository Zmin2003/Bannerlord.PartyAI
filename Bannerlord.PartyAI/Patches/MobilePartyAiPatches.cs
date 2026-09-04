using HarmonyLib;
using HarmonyLib.PatchBuilder;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Patches;

internal static class MobilePartyAiPatches
{
    private const float AggroRadius = 15f;
    private const float MaxDistanceFromPatrolPoint = 100f;

    public static void Apply(Harmony harmony)
    {
        harmony.Patch<MobilePartyAi>()
            .Method("GetBehaviors")
                .Postfix(GetBehaviorsPostfix);
    }

    /// <summary>
    /// Postfix on GetBehaviors:
    /// If the AI decided “PatrolAroundPoint” AND AggressivePatrols is enabled,
    /// then scan for nearby enemy parties and override the output to EngageParty.
    /// </summary>
    private static void GetBehaviorsPostfix(
        ref AiBehavior bestAiBehavior,
        ref IInteractablePoint behaviorObject,
        ref CampaignVec2 bestTargetPoint,
        MobileParty ____mobileParty)
    {
        // Feature disabled in settings
        if (!PartyAi.Settings.AggressivePatrols)
            return;

        if (____mobileParty?.LeaderHero == null || ____mobileParty.MapFaction == null)
            return;

        // Only kingdom lords and the player clan's parties
        if (!____mobileParty.MapFaction.IsKingdomFaction &&
            ____mobileParty.ActualClan != Clan.PlayerClan)
            return;

        // We only touch the behaviour if vanilla already picked PatrolAroundPoint
        if (bestAiBehavior != AiBehavior.PatrolAroundPoint)
            return;

        // Patrol centre is current position
        // (vanilla also uses a point, but this is close enough for our aggro logic)
        Vec2 center;
        try
        {
            center = ____mobileParty.Position.ToVec2();
        }
        catch (KeyNotFoundException)
        {
            return;
        }

        try
        {
            LocatableSearchData<MobileParty> scan =
                MobileParty.StartFindingLocatablesAroundPosition(center, AggroRadius);

            for (MobileParty target = MobileParty.FindNextLocatable(ref scan);
                 target != null;
                 target = MobileParty.FindNextLocatable(ref scan))
            {
                if (target == ____mobileParty)
                    continue;

                if (!target.IsActive || target.IsMainParty || target.IsDisbanding)
                    continue;

                if (target.CurrentSettlement != null)
                    continue;

                IFaction targetFaction = target.MapFaction;
                if (targetFaction == null || !targetFaction.IsAtWarWith(____mobileParty.MapFaction))
                    continue;

                // Ignore attached parties unless they are the army leader
                if (target.Army != null && target != target.Army.LeaderParty)
                    continue;

                // Strength ratio using EstimatedStrength (matches vanilla EngageParty logic)
                float myStrength = ____mobileParty.Army?.EstimatedStrength
                                   ?? ____mobileParty.Party.EstimatedStrength;

                if (myStrength <= 0f)
                    myStrength = 1f;

                float theirStrength = target.Army?.EstimatedStrength
                                      ?? target.Party.EstimatedStrength;

                float ratio = theirStrength / myStrength;

                // Don’t chase things way bigger or tiny trash
                if (ratio > 0.8f || ratio < 0.05f)
                    continue;

                // Don’t chase faster parties
                if (target.Speed > ____mobileParty.Speed)
                    continue;

                CampaignVec2 targetPos;
                try
                {
                    targetPos = target.Position;
                }
                catch (KeyNotFoundException)
                {
                    continue;
                }

                // Must be reasonably close to the original patrol point
                float distSq = targetPos.ToVec2()
                               .DistanceSquared(bestTargetPoint.ToVec2());

                if (distSq > MaxDistanceFromPatrolPoint * MaxDistanceFromPatrolPoint)
                    continue;

                // Override: tell the AI we want to engage this party
                bestAiBehavior = AiBehavior.EngageParty;
                behaviorObject = target.Party;
                bestTargetPoint = targetPos;

                return;
            }
        }
        catch (KeyNotFoundException)
        {
            // LocatorGrid internal state can desync after load/mod interactions.
            // Skip aggressive patrol override rather than crashing the campaign tick.
            return;
        }
    }
}