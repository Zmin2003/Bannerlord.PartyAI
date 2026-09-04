using Bannerlord.PartyAI.Parties.Orders;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Towns;

public class DefenseAssignment
{
    [SaveableProperty(1)] public Hero Hero { get; private set; }
    [SaveableProperty(2)] public Settlement TargetSettlement { get; private set; }
    [SaveableProperty(3)] public CampaignTime AssignedAt { get; private set; }
    [SaveableProperty(4)] public CampaignTime LastThreatSeenAt { get; private set; }
    [SaveableProperty(5)] public bool DonationCompleted { get; private set; }
    [SaveableProperty(6)] public int DonatedTroops { get; private set; }
    [SaveableProperty(7)] public PartyOrder? SuspendedOrder { get; private set; }
    [SaveableProperty(8)] public List<PartyOrder> SuspendedQueue { get; private set; }
    [SaveableProperty(9)] public int AutomationToken { get; private set; }
    [SaveableProperty(10)] public bool PendingRestore { get; private set; }
    [SaveableProperty(11)] public bool HasReachedTarget { get; private set; }
    [SaveableProperty(12)] public CampaignTime ReachedTargetAt { get; private set; } = CampaignTime.Never;

    public DefenseAssignment(
        Hero hero,
        Settlement targetSettlement,
        CampaignTime assignedAt,
        PartyOrder? suspendedOrder,
        IEnumerable<PartyOrder> suspendedQueue,
        int automationToken)
    {
        Hero = hero;
        TargetSettlement = targetSettlement;
        AssignedAt = assignedAt;
        LastThreatSeenAt = assignedAt;
        SuspendedOrder = suspendedOrder is null ? null : new PartyOrder(suspendedOrder);
        SuspendedQueue = suspendedQueue.Select(order => new PartyOrder(order)).ToList();
        AutomationToken = automationToken;
    }

    internal void MarkThreatSeen() => LastThreatSeenAt = CampaignTime.Now;

    internal void MarkReachedTarget()
    {
        if (HasReachedTarget
            && (ReachedTargetAt.IsNow || ReachedTargetAt.IsPast))
        {
            return;
        }

        HasReachedTarget = true;
        ReachedTargetAt = CampaignTime.Now;
    }

    internal void MarkDonationCompleted(int donatedTroops)
    {
        DonationCompleted = true;
        DonatedTroops += donatedTroops;
    }

    internal void DeferRestore() => PendingRestore = true;
}
