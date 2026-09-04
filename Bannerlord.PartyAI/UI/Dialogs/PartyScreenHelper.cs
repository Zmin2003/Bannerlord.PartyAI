using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Dialogs;

/// <summary>Opens the vanilla party screen as a generic "pick troops from left to right" dialog.</summary>
internal static class PartyScreenHelper
{
    public static void Open(
        TroopRoster? left,
        TroopRoster? right,
        TextObject leftName,
        TextObject rightName,
        TextObject header,
        PartyPresentationDoneButtonDelegate? onDone,
        PartyPresentationDoneButtonConditionDelegate? doneCondition = null,
        IsTroopTransferableDelegate? transferable = null)
    {
        left ??= TroopRoster.CreateDummyTroopRoster();
        right ??= TroopRoster.CreateDummyTroopRoster();
        PartyBase rightOwner = PartyBase.MainParty;

        var logic = new PartyScreenLogic();
        logic.Initialize(new PartyScreenLogicInitializationData
        {
            LeftOwnerParty = null,
            RightOwnerParty = rightOwner,
            LeftMemberRoster = left,
            LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
            RightMemberRoster = right,
            RightPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
            LeftLeaderHero = null,
            RightLeaderHero = null,
            LeftPartyMembersSizeLimit = 0,
            LeftPartyPrisonersSizeLimit = 0,
            RightPartyMembersSizeLimit = rightOwner.PartySizeLimit,
            RightPartyPrisonersSizeLimit = 0,
            LeftPartyName = leftName,
            RightPartyName = rightName,
            TroopTransferableDelegate = transferable ?? DefaultTransferable,
            PartyPresentationDoneButtonDelegate = onDone,
            PartyPresentationDoneButtonConditionDelegate = doneCondition ?? RequireAnyTroop,
            PartyPresentationCancelButtonActivateDelegate = null,
            PartyPresentationCancelButtonDelegate = null,
            IsDismissMode = false,
            IsTroopUpgradesDisabled = true,
            Header = header,
            PartyScreenClosedDelegate = null,
            TransferHealthiesGetWoundedsFirst = false,
            ShowProgressBar = false,
            MemberTransferState = PartyScreenLogic.TransferState.Transferable,
            PrisonerTransferState = PartyScreenLogic.TransferState.Transferable,
            AccompanyingTransferState = PartyScreenLogic.TransferState.NotTransferable
        });

        PartyState state = Game.Current.GameStateManager.CreateState<PartyState>();
        state.IsDonating = false;
        state.PartyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.Normal;
        state.PartyScreenLogic = logic;
        Game.Current.GameStateManager.PushState(state);
    }

    private static bool DefaultTransferable(CharacterObject character, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side, PartyBase leftOwner)
        => !character.IsHero && !character.IsNotTransferableInPartyScreen && type != PartyScreenLogic.TroopType.Prisoner;

    private static Tuple<bool, TextObject> RequireAnyTroop(TroopRoster left, TroopRoster leftPrisoners, TroopRoster right, TroopRoster rightPrisoners, int leftLimit, int rightLimit)
        => right.TotalManCount > 0
            ? new Tuple<bool, TextObject>(true, null!)
            : new Tuple<bool, TextObject>(false, new TextObject("{=PAIAAm1PQy1}Not enough troops in template."));
}
