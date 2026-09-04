using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Finance;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Autopilot;
using Bannerlord.PartyAI.Parties.Orders;
using Bannerlord.PartyAI.Parties.Recruitment;
using Bannerlord.PartyAI.Parties.Templates;
using Bannerlord.PartyAI.Towns;
using Bannerlord.PartyAI.War;
using TaleWorlds.CampaignSystem;

namespace Bannerlord.PartyAI;

/// <summary>
/// Single access point for the mod's campaign services. Populated when a campaign starts and
/// cleared when it ends, so anything holding a reference must re-read it per use.
/// </summary>
internal static class PartyAi
{
    public static ModSettings Settings { get; private set; } = new();
    public static PartyRegistry Parties { get; private set; } = null!;
    public static TownManagementBehavior Towns { get; private set; } = null!;
    public static AutoDefenseBehavior Defense { get; private set; } = null!;
    public static DirectCommandBehavior DirectCommand { get; private set; } = null!;
    public static WorkshopManagementBehavior Workshops { get; private set; } = null!;
    public static TownSalesBehavior Sales { get; private set; } = null!;
    public static RecruitOrder Recruiting { get; private set; } = null!;
    public static AutopilotBehavior Autopilot { get; private set; } = null!;
    public static OffenseBehavior Offense { get; private set; } = null!;
    public static TemplateImportService TemplateImport { get; } = new();

    /// <summary>True once a campaign has been bound and its services exist.</summary>
    public static bool IsActive => Campaign.Current is not null && Parties is not null;

    /// <summary>Creates every campaign behavior and registers it with the starter.</summary>
    internal static void Bind(CampaignGameStarter starter)
    {
        Settings = new ModSettings();
        Parties = new PartyRegistry(Settings);
        DirectCommand = new DirectCommandBehavior();
        Towns = new TownManagementBehavior();
        Defense = new AutoDefenseBehavior();
        Workshops = new WorkshopManagementBehavior();
        Sales = new TownSalesBehavior();
        Recruiting = new RecruitOrder();
        Autopilot = new AutopilotBehavior();
        Offense = new OffenseBehavior();

        starter.AddBehavior(Parties);
        starter.AddBehavior(DirectCommand);
        starter.AddBehavior(Towns);
        starter.AddBehavior(Defense);
        starter.AddBehavior(Workshops);
        starter.AddBehavior(Sales);
        starter.AddBehavior(Autopilot);
        starter.AddBehavior(Offense);

        var troopConverter = new TroopConverter();
        starter.AddBehavior(troopConverter);
        starter.AddBehavior(new SettlementAutomationBehavior(troopConverter));
        starter.AddBehavior(new ClanAutomationBehavior(Settings));
        starter.AddBehavior(new OrderLifecycleBehavior());

        starter.AddBehavior(Recruiting);
        starter.AddBehavior(new EscortPartyOrder());
        starter.AddBehavior(new AttackPartyOrder());
        starter.AddBehavior(new VisitSettlementOrder());
        starter.AddBehavior(new StayInSettlementOrder());
        starter.AddBehavior(new DefendSettlementOrder());
        starter.AddBehavior(new BesiegeSettlementOrder());
        starter.AddBehavior(new PatrolAroundSettlementOrder());
        starter.AddBehavior(new PatrolClanLandsOrder());
    }

    internal static void Unbind()
    {
        Parties = null!;
        Towns = null!;
        Defense = null!;
        DirectCommand = null!;
        Workshops = null!;
        Sales = null!;
        Recruiting = null!;
        Autopilot = null!;
        Offense = null!;
        Settings = new ModSettings();
    }
}
