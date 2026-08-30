using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Models;
using Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.CampaignBehaviors;

public class PartyAutoCreationBehavior : CampaignBehaviorBase
{
    private bool _autoCreateClanParties;
    private int _autoCreateClanPartiesMax;
    private List<Hero> _autoCreateClanPartiesRoster;
    private bool _autoCreateClanCaravans;
    private int _autoCreateClanCaravansMax;
    private int _autoCreateClanCaravansGoldReserve;
    private bool _autoCreateEliteCaravans;

    public PartyAutoCreationBehavior()
    {
        _autoCreateClanParties = false;
        _autoCreateClanPartiesMax = 0;
        _autoCreateClanPartiesRoster = new List<Hero>();
        _autoCreateClanCaravans = false;
        _autoCreateClanCaravansMax = 1;
        _autoCreateClanCaravansGoldReserve = 30000;
        _autoCreateEliteCaravans = false;
    }

    public bool AutoCreateClanParties => _autoCreateClanParties;

    public int AutoCreateClanPartiesMax => _autoCreateClanPartiesMax;

    public ReadOnlyCollection<Hero> AutoCreateClanPartiesRoster => _autoCreateClanPartiesRoster.AsReadOnly();
    public bool AutoCreateClanCaravans => _autoCreateClanCaravans;
    public int AutoCreateClanCaravansMax => _autoCreateClanCaravansMax;
    public int AutoCreateClanCaravansGoldReserve => _autoCreateClanCaravansGoldReserve;
    public bool AutoCreateEliteCaravans => _autoCreateEliteCaravans;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("AutoCreateClanParties", ref _autoCreateClanParties);
        dataStore.SyncData("AutoCreateClanPartiesMax", ref _autoCreateClanPartiesMax);
        dataStore.SyncData("AutoCreateClanPartiesRoster", ref _autoCreateClanPartiesRoster);
        _autoCreateClanPartiesRoster ??= new List<Hero>();
        if (!dataStore.SyncData("AutoCreateClanCaravans", ref _autoCreateClanCaravans) && dataStore.IsLoading)
        {
            _autoCreateClanCaravans = false;
        }
        if (!dataStore.SyncData("AutoCreateClanCaravansMax", ref _autoCreateClanCaravansMax) && dataStore.IsLoading)
        {
            _autoCreateClanCaravansMax = 1;
        }
        if (!dataStore.SyncData("AutoCreateClanCaravansGoldReserve", ref _autoCreateClanCaravansGoldReserve) && dataStore.IsLoading)
        {
            _autoCreateClanCaravansGoldReserve = 30000;
        }
        if (!dataStore.SyncData("AutoCreateEliteCaravans", ref _autoCreateEliteCaravans) && dataStore.IsLoading)
        {
            _autoCreateEliteCaravans = false;
        }
        _autoCreateClanCaravansMax = Math.Max(0, _autoCreateClanCaravansMax);
        _autoCreateClanCaravansGoldReserve = Math.Max(0, _autoCreateClanCaravansGoldReserve);
    }

    public void UpdateSettings(
        bool autoCreateClanParties,
        int autoCreateClanPartiesMax,
        List<Hero> autoCreateClanPartiesRoster,
        bool autoCreateClanCaravans,
        int autoCreateClanCaravansMax,
        int autoCreateClanCaravansGoldReserve,
        bool autoCreateEliteCaravans)
    {
        _autoCreateClanParties = autoCreateClanParties;
        _autoCreateClanPartiesMax = autoCreateClanPartiesMax;
        _autoCreateClanPartiesRoster = autoCreateClanPartiesRoster;
        _autoCreateClanCaravans = autoCreateClanCaravans;
        _autoCreateClanCaravansMax = Math.Max(0, autoCreateClanCaravansMax);
        _autoCreateClanCaravansGoldReserve = Math.Max(0, autoCreateClanCaravansGoldReserve);
        _autoCreateEliteCaravans = autoCreateEliteCaravans;
    }

    private void OnDailyTick()
    {
        RemoveUnsuitableHeroesFromRoster();
        ImplementAutoCreateClanParties();
        ImplementAutoCreateClanCaravan();
    }

    private void RemoveUnsuitableHeroesFromRoster()
    {
        for (int i = _autoCreateClanPartiesRoster.Count - 1; i >= 0; i--)
        {
            var hero = _autoCreateClanPartiesRoster[i];
            
            if (hero.IsDead || hero.IsDisabled)
            {
                _autoCreateClanPartiesRoster.RemoveAt(i);
            }
        }
    }

    private void ImplementAutoCreateClanParties()
    {
        if (!_autoCreateClanParties)
        {
            return;
        }

        if (_autoCreateClanPartiesMax > 0
            && ActiveClanParties(Clan.PlayerClan).Count() >= _autoCreateClanPartiesMax)
        {
            return;
        }

        while (Clan.PlayerClan.WarPartyComponents.Count < Clan.PlayerClan.WarPartyLimit)
        {
            ClanPartiesVM stockVM = new(() => { }, null, () => { }, (i) => { });

            if (!stockVM.CanCreateNewParty)
            {
                return;
            }

            IEnumerable<Hero> eligibleLeadersQuery = Clan.PlayerClan.Heroes
                .Where((Hero h) => !h.IsDisabled)
                .Union(Clan.PlayerClan.Companions)
                .Where(h => h.IsActive
                    && !h.IsReleased
                    && !h.IsFugitive
                    && !h.IsPrisoner
                    && !h.IsChild
                    && h != Hero.MainHero
                    && h.CanLeadParty()
                    && !h.IsPartyLeader
                    && h.GovernorOf == null
                    && h.PartyBelongedTo == null
                    && (!h.CurrentSettlement?.IsUnderSiege ?? true));

            if (_autoCreateClanPartiesRoster.Count > 0)
            {
                eligibleLeadersQuery = eligibleLeadersQuery.Where(_autoCreateClanPartiesRoster.Contains);
            }

            if (eligibleLeadersQuery.Count() == 0)
            {
                return;
            }

            var eligibleLeaders = eligibleLeadersQuery.ToArray();

            Hero leader = eligibleLeaders.GetRandomElement();
            Settlement? settlement = Navigation.FindNearestSettlement(leader.GetMapPoint());
            MobileParty newParty = MobilePartyHelper.CreateNewClanMobileParty(leader, Clan.PlayerClan);

            InformationManager.DisplayMessage(
                new InformationMessage(
                    new TextObject("{=PAIJPxU5978}{HERO} has created a new party near {SETTLEMENT}")
                        .SetTextVariable("HERO", leader.Name)
                        .SetTextVariable("SETTLEMENT", settlement?.Name)
                        .ToString(), Colors.Gray));

            if (_autoCreateClanPartiesMax > 0
                && ActiveClanParties(Clan.PlayerClan).Count() >= _autoCreateClanPartiesMax)
            {
                break;
            }
        }
    }

    private IEnumerable<WarPartyComponent> ActiveClanParties(Clan c)
        => c.WarPartyComponents.Where(p => p.MobileParty != MobileParty.MainParty);

    private void ImplementAutoCreateClanCaravan()
    {
        if (_autoCreateClanCaravans)
        {
            SubModule.PartySettingsManager.ManageCaravans = true;
        }

        if (!_autoCreateClanCaravans
            || MobileParty.MainParty?.MapEvent is not null
            || MobileParty.MainParty?.CurrentSettlement is not Settlement settlement
            || !settlement.IsTown
            || settlement.IsUnderSiege
            || FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, settlement.MapFaction))
        {
            return;
        }

        int activeCaravans = Hero.MainHero.OwnedCaravans.Count(component =>
            component.MobileParty?.IsActive == true);
        if (_autoCreateClanCaravansMax > 0
            && activeCaravans >= _autoCreateClanCaravansMax)
        {
            return;
        }

        Hero? leader = MobileParty.MainParty.MemberRoster
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

        bool naval = settlement.HasPort;
        int cost = Campaign.Current.Models.CaravanModel.GetCaravanFormingCost(
            _autoCreateEliteCaravans,
            naval);
        if (Hero.MainHero.Gold - cost < _autoCreateClanCaravansGoldReserve)
        {
            return;
        }

        PartyTemplateObject template = CaravanHelper.GetRandomCaravanTemplate(
            settlement.Culture,
            _autoCreateEliteCaravans,
            !naval);

        LeaveSettlementAction.ApplyForCharacterOnly(leader);
        _ = CaravanPartyComponent.CreateCaravanParty(
            Hero.MainHero,
            settlement,
            template,
            isInitialSpawn: false,
            leader,
            caravanItems: null,
            _autoCreateEliteCaravans);
        GiveGoldAction.ApplyForCharacterToSettlement(
            Hero.MainHero,
            settlement,
            cost,
            disableNotification: true);

        SubModule.PartySettingsManager.ManageCaravans = true;
        SubModule.PartySettingsManager.AllowTroopConversionForCaravans = true;
        PartyAiEntitySettings settings = SubModule.PartySettingsManager.Settings(leader);
        settings.SettlementAutomation = SettlementAutomationLevel.Full;
        if (settings.PartyTemplate is null)
        {
            PAICustomTemplate? cultureTemplate = SubModule.PartySettingsManager.AllTemplates
                .FirstOrDefault(candidate => candidate.SourceId
                    == $"builtin:v1.5.2:culture:{settlement.Culture.StringId}");
            if (cultureTemplate is not null)
            {
                settings.SetPartyTemplate(cultureTemplate);
            }
        }

        TextObject message = new(
            "{=PAI_AUTO_CARAVAN_CREATED}{HERO} formed an automatically managed caravan in {SETTLEMENT} for {COST}{GOLD_ICON}.");
        message.SetTextVariable("HERO", leader.Name);
        message.SetTextVariable("SETTLEMENT", settlement.Name);
        message.SetTextVariable("COST", cost);
        InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Gray));
    }

    private static int CaravanLeaderScore(Hero hero)
    {
        return hero.GetSkillValue(DefaultSkills.Trade) * 3
            + hero.GetSkillValue(DefaultSkills.Scouting) * 2
            + hero.GetSkillValue(DefaultSkills.Tactics);
    }
}
