using Bannerlord.PartyAI.Parties.Orders;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.War;

/// <summary>One clan party taking part in an offensive, with the orders it had before. Save id 12.</summary>
public sealed class OffenseParticipant
{
    [SaveableProperty(1)] public Hero Hero { get; private set; }
    [SaveableProperty(2)] public PartyOrder? SuspendedOrder { get; private set; }
    [SaveableProperty(3)] public List<PartyOrder> SuspendedQueue { get; private set; }
    [SaveableProperty(4)] public int Token { get; private set; }

    public OffenseParticipant(Hero hero, PartyOrder? suspendedOrder, List<PartyOrder> suspendedQueue, int token)
    {
        Hero = hero;
        SuspendedOrder = suspendedOrder;
        SuspendedQueue = suspendedQueue;
        Token = token;
    }
}

/// <summary>A siege the mod launched on its own. Save id 11.</summary>
public sealed class OffenseOperation
{
    [SaveableProperty(1)] public Settlement Target { get; private set; }
    [SaveableProperty(2)] public CampaignTime Started { get; private set; }
    [SaveableProperty(3)] public List<OffenseParticipant> Participants { get; private set; }
    [SaveableProperty(4)] public Hero? ArmyLeader { get; set; }
    [SaveableProperty(5)] public float DefenseAtStart { get; private set; }

    public OffenseOperation(Settlement target, List<OffenseParticipant> participants, Hero? armyLeader, float defenseAtStart)
    {
        Target = target;
        Started = CampaignTime.Now;
        Participants = participants;
        ArmyLeader = armyLeader;
        DefenseAtStart = defenseAtStart;
    }

    public float DaysRunning => Started.ElapsedDaysUntilNow;
}
