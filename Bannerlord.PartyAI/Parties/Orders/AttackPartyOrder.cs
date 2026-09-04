using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>Chase and engage a hostile party. Issued through direct map commands.</summary>
public sealed class AttackPartyOrder : OrderBehaviorBase
{
    protected override PartyOrderType OrderType => PartyOrderType.AttackParty;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order)
            || !RequirePartyTarget(party, profile, order, hostile: true, out MobileParty? target))
        {
            return;
        }

        HoldInitiative(party, avoid: 0.33f);

        if (!TryNavigateToParty(party, target, AiBehavior.EngageParty, thinkParams))
        {
            Stop(party, profile, order, StopReason.TargetUnreachable);
        }
    }
}
