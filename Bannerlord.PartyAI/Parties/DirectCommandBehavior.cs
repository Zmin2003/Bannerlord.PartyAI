using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Parties;

/// <summary>
/// Lets the player pick nearby managed parties and redirect them by clicking on the map while
/// the command key is held. Every click becomes a regular <see cref="PartyOrder"/> so the AI
/// keeps executing it after the key is released.
/// </summary>
public sealed class DirectCommandBehavior : CampaignBehaviorBase
{
    private const string LegacyStringId = "ControlAssumptionBehavior";

    private List<MobileParty> _commandedParties = new();
    private bool _isPickerOpen;

    public DirectCommandBehavior() : base(LegacyStringId)
    {
    }

    public IReadOnlyList<MobileParty> CommandedParties => _commandedParties;

    public override void RegisterEvents()
    {
        CampaignEvents.OnPartyJoinedArmyEvent.AddNonSerializedListener(this, party => _commandedParties.Remove(party));
        CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, party => _commandedParties.Remove(party));
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, (party, _) => _commandedParties.Remove(party));
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_assumingDirectControl", ref _commandedParties);
        _commandedParties ??= new();
    }

    public bool IsCommanded(MobileParty? party) => party is not null && _commandedParties.Contains(party);

    // ---- Selection ------------------------------------------------------------------------

    public void OpenPicker()
    {
        if (_isPickerOpen)
        {
            return;
        }

        CampaignTimeControlMode previousMode = Campaign.Current.TimeControlMode;
        Campaign.Current.TimeControlMode = CampaignTimeControlMode.FastForwardStop;

        List<InquiryElement> elements = MobileParty.AllLordParties
            .Where(party => PartyAi.Parties.IsHeroManageable(party.LeaderHero)
                && IsWithinSeeingRange(party)
                && !IsInSomeoneElsesArmy(party))
            .OrderByDescending(party => party.ActualClan == Clan.PlayerClan)
            .ThenBy(party => party.Name?.ToString())
            .Select(party => new InquiryElement(
                party,
                party.Name.ToString(),
                new CharacterImageIdentifier(CharacterCode.CreateFrom(party.LeaderHero.CharacterObject))))
            .ToList();

        void Close()
        {
            _isPickerOpen = false;
            Campaign.Current.TimeControlMode = previousMode;
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            L.S("{=PAIFHytp3D7}Choose which parties to directly command"),
            L.S("{=PAIRzSgh49H}Parties must be manageable and in visual range to appear here."),
            elements,
            isExitShown: true,
            minSelectableOptionCount: 0,
            maxSelectableOptionCount: elements.Count,
            L.Game("str_done"),
            L.Game("str_cancel"),
            affirmativeAction: results =>
            {
                _commandedParties = results.Select(element => element.Identifier).OfType<MobileParty>().ToList();
                Close();
            },
            negativeAction: _ => Close(),
            isSeachAvailable: true));

        _isPickerOpen = true;
    }

    // ---- Map click handlers (called from MobileParty.SetMove* patches) ----------------------

    public void OnMainPartyMovesToPoint(MobileParty mover)
        => ForEachCommandedParty(mover, party =>
        {
            SetPartyAiAction.GetActionForEscortingParty(party, MobileParty.MainParty, party.DesiredAiNavigationType, false, false);
            IssueOrder(party, PartyOrderType.EscortParty, MobileParty.MainParty);
        });

    public void OnMainPartyTargetsParty(MobileParty mover, MobileParty target)
        => ForEachCommandedParty(mover, party =>
        {
            if (FactionManager.IsAtWarAgainstFaction(target.MapFaction, party.MapFaction))
            {
                SetPartyAiAction.GetActionForEngagingParty(party, target, party.DesiredAiNavigationType, false);
                IssueOrder(party, PartyOrderType.AttackParty, target);
            }
            else
            {
                SetPartyAiAction.GetActionForEscortingParty(party, target, party.DesiredAiNavigationType, false, false);
                IssueOrder(party, PartyOrderType.EscortParty, target);
            }
        });

    public void OnMainPartyTargetsSettlement(MobileParty mover, Settlement settlement)
        => ForEachCommandedParty(mover, party =>
        {
            if (FactionManager.IsAtWarAgainstFaction(settlement.MapFaction, party.MapFaction))
            {
                SetPartyAiAction.GetActionForBesiegingSettlement(party, settlement, party.DesiredAiNavigationType, false);
                IssueOrder(party, PartyOrderType.BesiegeSettlement, settlement);
            }
            else if (settlement.IsUnderSiege)
            {
                SetPartyAiAction.GetActionForDefendingSettlement(party, settlement, party.DesiredAiNavigationType, false, false);
                IssueOrder(party, PartyOrderType.DefendSettlement, settlement);
            }
            else
            {
                SetPartyAiAction.GetActionForVisitingSettlement(party, settlement, party.DesiredAiNavigationType, false, false);
                IssueOrder(party, PartyOrderType.VisitSettlement, settlement);
            }
        });

    private void ForEachCommandedParty(MobileParty mover, Action<MobileParty> command)
    {
        if (mover != MobileParty.MainParty || !PartyAi.Settings.CommandParties.IsDown())
        {
            return;
        }

        foreach (MobileParty party in _commandedParties.ToList())
        {
            if (party?.LeaderHero is null
                || !PartyAi.Parties.IsHeroManageable(party.LeaderHero)
                || party.MapEvent is not null)
            {
                continue;
            }

            if (!IsWithinSeeingRange(party))
            {
                Notify.Warning(L.T("{=PAIc1pTxSOA}{NAME} is out of range to be commanded directly", "NAME", party.Name));
                continue;
            }

            party.Ai?.SetDoNotMakeNewDecisions(true);
            command(party);
        }
    }

    private static void IssueOrder(MobileParty party, PartyOrderType type, TaleWorlds.CampaignSystem.Map.IMapPoint target)
    {
        PartyProfile profile = PartyAi.Parties.Profile(party.LeaderHero);
        profile.ClearAllOrders();
        profile.SetOrder(type, target);
    }

    private static bool IsWithinSeeingRange(MobileParty party)
        => party.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D) <= MobileParty.MainParty.SeeingRange;

    private static bool IsInSomeoneElsesArmy(MobileParty party)
        => party.Army is not null && party.Army.LeaderParty != party;
}
