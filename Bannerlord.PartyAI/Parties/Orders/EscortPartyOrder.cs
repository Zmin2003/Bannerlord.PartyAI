using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PartyAI.Parties.Orders;

/// <summary>Follow and protect a friendly party.</summary>
public sealed class EscortPartyOrder : OrderBehaviorBase
{
    protected override PartyOrderType OrderType => PartyOrderType.EscortParty;

    public override void RegisterEvents()
    {
        CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
    }

    private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
    {
        if (!TryGetRelevantOrder(party, out PartyProfile? profile, out PartyOrder? order)
            || !RequirePartyTarget(party, profile, order, hostile: false, out MobileParty? target))
        {
            return;
        }

        if (!TryNavigateToParty(party, target, AiBehavior.EscortParty, thinkParams))
        {
            Stop(party, profile, order, StopReason.TargetUnreachable);
            return;
        }

        HoldInitiative(party, avoid: 0.33f);
    }
}
