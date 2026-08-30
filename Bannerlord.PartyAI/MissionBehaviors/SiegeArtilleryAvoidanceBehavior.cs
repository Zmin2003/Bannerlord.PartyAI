using Bannerlord.PartyAI.CampaignBehaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.PartyAI.MissionBehaviors;

internal sealed class SiegeArtilleryAvoidanceBehavior : MissionLogic
{
    private const float ScanInterval = 0.1f;
    private const float Gravity = 9.81f;
    private const float MaximumReactionTime = 2.6f;
    private const float EvasionDuration = 2.2f;

    private readonly PartyAIClanPartySettingsManager? _settings;
    private readonly Dictionary<Agent, float> _evadingAgents = new();
    private readonly Dictionary<int, float> _processedMissiles = new();
    private float _nextScanTime;

    internal SiegeArtilleryAvoidanceBehavior(PartyAIClanPartySettingsManager? settings)
    {
        _settings = settings;
    }

    public override void OnMissionTick(float dt)
    {
        if (!AvoidSiegeArtillery
            || !Mission.IsSiegeBattle
            || Mission.Mode != TaleWorlds.Core.MissionMode.Battle
            || !Mission.IsDeploymentFinished)
        {
            return;
        }

        ReleaseExpiredAgents();
        if (Mission.CurrentTime < _nextScanTime)
        {
            return;
        }

        _nextScanTime = Mission.CurrentTime + ScanInterval;
        RemoveExpiredMissileRecords();

        Team playerTeam = Mission.PlayerTeam;
        if (playerTeam is null)
        {
            return;
        }

        foreach (Mission.Missile missile in Mission.MissilesList)
        {
            if (_processedMissiles.ContainsKey(missile.Index)
                || !TryGetDangerRadius(missile, playerTeam, out float dangerRadius)
                || !TryPredictImpact(missile, out Vec3 impact, out float timeToImpact)
                || timeToImpact < 0.12f
                || timeToImpact > MaximumReactionTime)
            {
                continue;
            }

            _processedMissiles[missile.Index] = Mission.CurrentTime + 6f;
            EvadeAgents(playerTeam, missile, impact, dangerRadius);
        }
    }

    public override void OnRemoveBehavior()
    {
        ReleaseAllAgents();
        base.OnRemoveBehavior();
    }

    protected override void OnEndMission()
    {
        ReleaseAllAgents();
        base.OnEndMission();
    }

    private void EvadeAgents(
        Team playerTeam,
        Mission.Missile missile,
        Vec3 impact,
        float dangerRadius)
    {
        float dangerRadiusSquared = dangerRadius * dangerRadius;
        Vec2 impactPosition = impact.AsVec2;
        Vec2 missileDirection = missile.GetVelocity().AsVec2;

        foreach (Agent agent in playerTeam.TeamAgents)
        {
            if (!CanEvade(agent)
                || agent.Position.AsVec2.DistanceSquared(impactPosition) > dangerRadiusSquared)
            {
                continue;
            }

            Vec2 away = agent.Position.AsVec2 - impactPosition;
            if (away.LengthSquared < 0.25f)
            {
                away = new Vec2(-missileDirection.y, missileDirection.x);
            }
            if (away.LengthSquared < 0.25f)
            {
                away = Vec2.Side;
            }

            away.Normalize();
            Vec2 target = impactPosition + away * (dangerRadius + 3f);
            if (!Mission.IsPositionInsideBoundaries(target))
            {
                target = Mission.GetClosestBoundaryPosition(target);
            }

            if (!agent.CanMoveDirectlyToPosition(in target))
            {
                target = agent.FindLongestDirectMoveToPosition(
                    target,
                    checkBoundaries: true,
                    checkFriendlyAgents: false,
                    out _);
            }

            if (target.DistanceSquared(agent.Position.AsVec2) < 2.25f)
            {
                continue;
            }

            WorldPosition safePosition = agent.GetWorldPosition();
            safePosition.SetVec2(target);
            agent.SetScriptedPosition(
                ref safePosition,
                addHumanLikeDelay: false,
                Agent.AIScriptedFrameFlags.GoToPosition
                    | Agent.AIScriptedFrameFlags.NeverSlowDown
                    | Agent.AIScriptedFrameFlags.NoAttack);
            _evadingAgents[agent] = Mission.CurrentTime + EvasionDuration;
        }
    }

    private static bool CanEvade(Agent agent)
    {
        if (agent is null
            || !agent.IsActive()
            || agent == Mission.Current.MainAgent
            || agent.IsMount
            || agent.IsUsingGameObject
            || agent.IsDetachedFromFormation
            || agent.Formation is null
            || !agent.Formation.IsAIControlled)
        {
            return false;
        }

        Agent enemy = agent.ImmediateEnemy;
        return enemy is null
            || enemy.Position.DistanceSquared(agent.Position) > 36f;
    }

    private static bool TryGetDangerRadius(
        Mission.Missile missile,
        Team playerTeam,
        out float dangerRadius)
    {
        dangerRadius = 0f;
        Agent shooter = missile.ShooterAgent;
        if (shooter?.Team is not null && !shooter.Team.IsEnemyOf(playerTeam))
        {
            return false;
        }

        RangedSiegeWeapon? siegeWeapon = missile.MissionObjectToIgnore as RangedSiegeWeapon;
        if (siegeWeapon is not null && siegeWeapon.Side == playerTeam.Side)
        {
            return false;
        }

        string itemId = missile.Weapon.Item?.StringId?.ToLowerInvariant() ?? string.Empty;
        bool isHeavyWeapon = siegeWeapon is Mangonel
            || siegeWeapon is Trebuchet
            || siegeWeapon?.GetType().Name.IndexOf("mangonel", StringComparison.OrdinalIgnoreCase) >= 0
            || siegeWeapon?.GetType().Name.IndexOf("trebuchet", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isAreaProjectile = itemId.Contains("pot")
            || itemId.Contains("grapeshot")
            || itemId.Contains("boulder")
            || itemId.Contains("mangonel")
            || itemId.Contains("trebuchet");

        if (!isHeavyWeapon && !isAreaProjectile)
        {
            return false;
        }

        dangerRadius = itemId.Contains("grapeshot") ? 7f : 10f;
        return true;
    }

    private bool TryPredictImpact(
        Mission.Missile missile,
        out Vec3 impact,
        out float timeToImpact)
    {
        Vec3 position = missile.GetPosition();
        Vec3 velocity = missile.GetVelocity();
        impact = position;
        timeToImpact = 0f;

        if (!position.IsValid || !velocity.IsValid)
        {
            return false;
        }

        float groundHeight = Mission.Scene.GetGroundHeightAtPosition(position, BodyFlags.None);
        for (int iteration = 0; iteration < 2; iteration++)
        {
            float height = MathF.Max(0f, position.z - groundHeight);
            float discriminant = velocity.z * velocity.z + 2f * Gravity * height;
            if (discriminant < 0f)
            {
                return false;
            }

            timeToImpact = (velocity.z + MathF.Sqrt(discriminant)) / Gravity;
            if (timeToImpact <= 0f || timeToImpact > 6f)
            {
                return false;
            }

            impact = position + velocity * timeToImpact;
            groundHeight = Mission.Scene.GetGroundHeightAtPosition(impact, BodyFlags.None);
        }

        impact.z = groundHeight;
        return true;
    }

    private void ReleaseExpiredAgents()
    {
        foreach (Agent agent in _evadingAgents
            .Where(pair => pair.Value <= Mission.CurrentTime || !pair.Key.IsActive())
            .Select(pair => pair.Key)
            .ToList())
        {
            if (agent.IsActive())
            {
                agent.DisableScriptedMovement();
                agent.ForceAiBehaviorSelection();
            }
            _evadingAgents.Remove(agent);
        }
    }

    private void ReleaseAllAgents()
    {
        foreach (Agent agent in _evadingAgents.Keys.ToList())
        {
            if (agent.IsActive())
            {
                agent.DisableScriptedMovement();
                agent.ForceAiBehaviorSelection();
            }
        }
        _evadingAgents.Clear();
    }

    private void RemoveExpiredMissileRecords()
    {
        foreach (int index in _processedMissiles
            .Where(pair => pair.Value <= Mission.CurrentTime)
            .Select(pair => pair.Key)
            .ToList())
        {
            _processedMissiles.Remove(index);
        }
    }

    private bool AvoidSiegeArtillery => _settings?.AvoidSiegeArtillery ?? true;
}
