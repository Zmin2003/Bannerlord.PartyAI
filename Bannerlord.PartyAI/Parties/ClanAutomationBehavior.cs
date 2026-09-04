using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Finance;
using Bannerlord.PartyAI.Parties.Recruitment;
using Bannerlord.PartyAI.Parties.Templates;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Parties;

/// <summary>
/// Daily automation of the player clan: forming new war parties and caravans.
/// </summary>
public sealed class ClanAutomationBehavior : CampaignBehaviorBase
{
    private const string LegacyStringId = "PartyAutoCreationBehavior";

    private readonly ModSettings _settings;

    public ClanAutomationBehavior(ModSettings settings) : base(LegacyStringId)
    {
        _settings = settings;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore) => _settings.SyncAutomation(dataStore);

    private void OnDailyTick()
    {
        _settings.AutoCreateClanPartiesRoster.RemoveAll(hero => hero is null || hero.IsDead || hero.IsDisabled);
        CreateClanParties();
        CreateClanCaravan();
    }

    // ---- Parties --------------------------------------------------------------------------

    public static bool CanCreateNewParty()
    {
        var stockVm = new ClanPartiesVM(() => { }, _ => { }, () => { }, _ => { });
        return stockVm.CanCreateNewParty;
    }

    public static TextObject CreateNewPartyHint()
    {
        var stockVm = new ClanPartiesVM(() => { }, _ => { }, () => { }, _ => { });
        return stockVm.CreateNewPartyActionHint?.HintText ?? TextObject.GetEmpty();
    }

    private void CreateClanParties()
    {
        if (!_settings.AutoCreateClanParties || IsPartyLimitReached())
        {
            return;
        }

        while (Clan.PlayerClan.WarPartyComponents.Count < Clan.PlayerClan.WarPartyLimit)
        {
            if (!CanCreateNewParty())
            {
                return;
            }

            // A new party is a permanent wage bill: only raise one if the clan can carry it.
            int wage = Treasury.EstimatedNewPartyWage();
            if (Treasury.Gold < Treasury.Reserve || !Treasury.CanAffordRecurring(wage))
            {
                return;
            }

            Hero[] candidates = EligiblePartyLeaders().ToArray();
            if (candidates.Length == 0)
            {
                return;
            }

            Hero leader = candidates.GetRandomElement();
            Settlement? near = Navigation.FindNearestSettlement(leader.GetMapPoint());
            MobilePartyHelper.CreateNewClanMobileParty(leader, Clan.PlayerClan);

            Notify.Info(L.T("{=PAIJPxU5978}{HERO} has created a new party near {SETTLEMENT}")
                .SetTextVariable("HERO", leader.Name)
                .SetTextVariable("SETTLEMENT", near?.Name ?? TextObject.GetEmpty()));

            if (IsPartyLimitReached())
            {
                return;
            }
        }
    }

    private bool IsPartyLimitReached()
        => _settings.AutoCreateClanPartiesMax > 0
            && Clan.PlayerClan.WarPartyComponents.Count(component => component.MobileParty != MobileParty.MainParty)
                >= _settings.AutoCreateClanPartiesMax;

    private IEnumerable<Hero> EligiblePartyLeaders()
    {
        IEnumerable<Hero> heroes = Clan.PlayerClan.Heroes
            .Where(hero => !hero.IsDisabled)
            .Union(Clan.PlayerClan.Companions)
            .Where(hero => hero.IsActive
                && !hero.IsReleased
                && !hero.IsFugitive
                && !hero.IsPrisoner
                && !hero.IsChild
                && hero != Hero.MainHero
                && hero.CanLeadParty()
                && !hero.IsPartyLeader
                && hero.GovernorOf is null
                && hero.PartyBelongedTo is null
                && hero.CurrentSettlement?.IsUnderSiege != true);

        return _settings.AutoCreateClanPartiesRoster.Count > 0
            ? heroes.Where(_settings.AutoCreateClanPartiesRoster.Contains)
            : heroes;
    }

    /// <summary>Heroes the player may pick for the auto-creation roster.</summary>
    public static IEnumerable<Hero> RosterCandidates()
        => Clan.PlayerClan.Heroes
            .Where(hero => !hero.IsDisabled && hero.IsAlive)
            .Union(Clan.PlayerClan.Companions)
            .Where(hero => hero != Hero.MainHero && hero.CanLeadParty())
            .OrderBy(hero => hero.Name.ToString());

    // ---- Caravans -------------------------------------------------------------------------

    private void CreateClanCaravan()
    {
        if (!_settings.AutoCreateClanCaravans)
        {
            return;
        }

        // Caravans created here must be managed so they pick up their template.
        _settings.ManageCaravans = true;

        MobileParty main = MobileParty.MainParty;
        if (main?.MapEvent is not null
            || main?.CurrentSettlement is not Settlement settlement
            || !settlement.IsTown
            || settlement.IsUnderSiege
            || FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, settlement.MapFaction))
        {
            return;
        }

        int activeCaravans = Hero.MainHero.OwnedCaravans.Count(component => component.MobileParty?.IsActive == true);
        if (_settings.AutoCreateClanCaravansMax > 0 && activeCaravans >= _settings.AutoCreateClanCaravansMax)
        {
            return;
        }

        Hero? leader = main.MemberRoster
            .GetTroopRoster()
            .Select(element => element.Character.HeroObject)
            .Where(hero => hero is not null
                && hero != Hero.MainHero
                && hero.Clan == Clan.PlayerClan
                && hero.GovernorOf is null
                && !hero.IsPrisoner
                && !hero.IsDisabled
                && hero.CanLeadParty())
            .OrderByDescending(CaravanLeaderScore)
            .FirstOrDefault();
        if (leader is null)
        {
            return;
        }

        bool elite = _settings.AutoCreateEliteCaravans;
        bool naval = settlement.HasPort;
        int cost = Campaign.Current.Models.CaravanModel.GetCaravanFormingCost(elite, naval);
        if (!Treasury.CanSpend(cost))
        {
            return;
        }

        PartyTemplateObject partyTemplate = CaravanHelper.GetRandomCaravanTemplate(settlement.Culture, elite, !naval);

        LeaveSettlementAction.ApplyForCharacterOnly(leader);
        CaravanPartyComponent.CreateCaravanParty(
            Hero.MainHero,
            settlement,
            partyTemplate,
            isInitialSpawn: false,
            leader,
            caravanItems: null,
            elite);
        GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, cost, disableNotification: true);

        _settings.AllowTroopConversionForCaravans = true;
        PartyProfile profile = PartyAi.Parties.Profile(leader);
        profile.SettlementAutomation = SettlementAutomationLevel.Full;
        if (profile.Template is null)
        {
            TroopTemplate? cultureTemplate = PartyAi.Parties.FindTemplateBySource(
                BuiltinTemplateCatalog.CultureSourceId(settlement.Culture));
            if (cultureTemplate is not null)
            {
                profile.SetTemplate(cultureTemplate);
            }
        }

        Notify.Info(L.T("{=PAI_AUTO_CARAVAN_CREATED}{HERO} formed an automatically managed caravan in {SETTLEMENT} for {COST}{GOLD_ICON}.")
            .SetTextVariable("HERO", leader.Name)
            .SetTextVariable("SETTLEMENT", settlement.Name)
            .SetTextVariable("COST", cost));
    }

    private static int CaravanLeaderScore(Hero hero)
        => hero.GetSkillValue(DefaultSkills.Trade) * 3
            + hero.GetSkillValue(DefaultSkills.Scouting) * 2
            + hero.GetSkillValue(DefaultSkills.Tactics);
}
