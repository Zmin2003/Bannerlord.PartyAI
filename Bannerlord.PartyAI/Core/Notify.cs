using Bannerlord.PartyAI.Parties.Orders;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Core;

/// <summary>Player-facing log messages.</summary>
internal static class Notify
{
    private static readonly Color OrderColor = Colors.Magenta;

    public static void Info(TextObject text) => Show(text, Colors.Gray);

    public static void Success(TextObject text) => Show(text, Colors.Green);

    public static void Warning(TextObject text) => Show(text, Colors.Yellow);

    public static void Error(TextObject text) => Show(text, Colors.Red);

    public static void OrderStoppedTargetUnreachable(MobileParty party, PartyOrder order)
        => OrderStopped("{=PAI_order_stopped_unreachable}{PARTY} is no longer {ORDER} because their target is not reachable.", party, order);

    public static void OrderStoppedTargetInvalid(MobileParty party, PartyOrder order)
        => OrderStopped("{=PAI_order_stopped_invalid_target}{PARTY} is no longer {ORDER} because their target is invalid.", party, order);

    public static void OrderStoppedNoValidTargets(MobileParty party, PartyOrder order)
        => OrderStopped("{=PAI_order_stopped_no_valid_targets}{PARTY} is no longer {ORDER} because no suitable target could be found.", party, order);

    public static void OrderStoppedTargetEnemy(MobileParty party, PartyOrder order)
        => OrderStopped("{=PAI_order_stopped_war}{PARTY} is no longer {ORDER} because the target's faction became an enemy.", party, order);

    public static void OrderStoppedTargetFriendly(MobileParty party, PartyOrder order)
        => OrderStopped("{=PAI_order_stopped_peace}{PARTY} is no longer {ORDER} because the target's faction is no longer an enemy.", party, order);

    public static void OrderStoppedTargetSieged(MobileParty party, PartyOrder order)
        => OrderStopped("{=PAI_order_stopped_siege}{PARTY} is no longer {ORDER} because the target is under siege.", party, order);

    public static void OrderStoppedCalledToArmy(MobileParty party, PartyOrder order, TextObject armyName)
        => Show(new TextObject("{=PAIOEWao2aI}{PARTY} is no longer {ORDER} because they were called to {ARMY}")
            .SetTextVariable("PARTY", party.Name)
            .SetTextVariable("ORDER", OrderText.Status(order))
            .SetTextVariable("ARMY", armyName), OrderColor);

    private static void OrderStopped(string template, MobileParty party, PartyOrder order)
        => Show(new TextObject(template)
            .SetTextVariable("PARTY", party.LeaderHero?.Name ?? party.Name)
            .SetTextVariable("ORDER", OrderText.Status(order)), OrderColor);

    private static void Show(TextObject text, Color color)
        => InformationManager.DisplayMessage(new InformationMessage(text.ToString(), color));
}
