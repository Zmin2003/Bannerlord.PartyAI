using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.InputSystem;

namespace Bannerlord.PartyAI.CampaignBehaviors;

public class PartyAIClanPartySettingsManager : CampaignBehaviorBase
{
    private Dictionary<Hero, PartyAiEntitySettings> _partySettings = new();
    private Dictionary<Settlement, PartyAiEntitySettings> _garrisonSettings = new();
    private Dictionary<Hero, PartyAiEntitySettings> _caravanSettings = new();
    private List<PAICustomTemplate> _partyTemplates = new();
    private PartyAiEntitySettings? _playerPartySettings;
    private int _settlementAutomationVersion = 1;

    internal bool AllowTroopConversion = false;
    internal bool AllowTroopConversionForCaravans = true;
    internal bool AllowTroopConversionForGarrisons = true;
    internal bool ManageCaravans;
    internal bool ManageClanGarrisons;
    internal bool ManageKingdomParties;
    internal bool ManageKingdomGarrisons;
    internal int TroopsConvertedPerDay = 4;
    internal PartyAiEntitySettings _defaultClanPartySettings = new();
    internal PartyAiEntitySettings _defaultClanCaravanSettings = new();
    internal PartyAiEntitySettings _defaultClanGarrisonSettings = new();
    internal PartyAiEntitySettings _defaultKingdomPartySettings = new();
    internal PartyAiEntitySettings _defaultKingdomGarrisonSettings = new();
    internal bool AggressivePatrols = false;
    internal bool AutoDelegateBattleCommand = true;
    internal bool EnhancedBattleAi = true;
    internal bool AvoidSiegeArtillery = true;
    private int _battleAutomationVersion = 1;
    internal InputKey ControlPanelModiferKey = InputKey.LeftControl;
    internal InputKey ControlPanelKey = InputKey.P;
    internal InputKey CommandedPartiesModiferKey = InputKey.LeftAlt;
    internal InputKey CommandedPartiesKey = InputKey.X;
    internal InputKey CommandPartiesKey = InputKey.LeftAlt;
    internal InputKey BattleCommanderModifierKey = InputKey.LeftControl;
    internal InputKey BattleCommanderKey = InputKey.M;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(OnDailyTick));
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
    }

    private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
    {
        _playerPartySettings ??= new PartyAiEntitySettings(_defaultClanPartySettings, Hero.MainHero);

        if (_settlementAutomationVersion < 1)
        {
            foreach (PartyAiEntitySettings settings in DefaultSettings())
            {
                settings.SettlementAutomation = SettlementAutomationLevel.Full;
            }

            foreach (PartyAiEntitySettings settings in AllSettings())
            {
                settings.SettlementAutomation = SettlementAutomationLevel.Full;
            }
            _settlementAutomationVersion = 1;
        }

        if (_battleAutomationVersion < 1)
        {
            EnhancedBattleAi = true;
            AvoidSiegeArtillery = true;
            _battleAutomationVersion = 1;
        }

        foreach (PartyAiEntitySettings settings in AllSettings())
        {
            settings.FilteredSettlements ??= new();
            settings.OrderQueue ??= new();
            if (settings.PatrolRadius == 0f)
            {
                settings.PatrolRadius = 1f;
            }
        }

        BuiltinTemplateCatalog.EnsureBuiltInTemplates(this);
    }

    private void OnDailyTick()
    {
        // reset budgets
        foreach (PartyAiEntitySettings item in AllSettings())
        {
            item.ResetBudgets();
        }

        // cleanup dead heroes
        foreach (KeyValuePair<Hero, PartyAiEntitySettings> item in _partySettings.AsEnumerable().Reverse())
        {
            if (item.Value.Hero?.IsDead ?? true || item.Value.Hero.IsDisabled)
            {
                _partySettings.Remove(item.Key);
            }
        }

        foreach (KeyValuePair<Hero, PartyAiEntitySettings> item in _caravanSettings.AsEnumerable().Reverse())
        {
            if (item.Value.Hero?.IsDead ?? true || item.Value.Hero.IsDisabled)
            {
                _caravanSettings.Remove(item.Key);
            }
        }
    }

    internal List<PartyAiEntitySettings> HeroesWithOrders => _partySettings.Where(s => s.Value.HasActiveOrder).ToList().ConvertAll(s => s.Value);

    internal IEnumerable<PartyAiEntitySettings> AllPartySettings => _partySettings.Values;

    internal void AddPartyTemplate(PAICustomTemplate template)
    {
        _partyTemplates.Add(template);
    }

    internal void DeletePartyTemplate(PAICustomTemplate template)
    {
        _partyTemplates.Remove(template);

        foreach (PartyAiEntitySettings settings in AllSettings().Concat(DefaultSettings()))
        {
            if (settings.PartyTemplate == template)
            {
                settings.SetPartyTemplate(null);
            }
        }
    }

    internal List<PAICustomTemplate> AllTemplates => _partyTemplates.ToList();

    internal bool HasActiveOrder(Hero? h) => Settings(h).HasActiveOrder;
    internal bool IsUniqueTemplateName(string name)
    {
        foreach (PAICustomTemplate t in _partyTemplates)
        {
            if (t.Name == name)
            {
                return false;
            }
        }

        return true;
    }

    internal PartyAiEntitySettings Settings(Settlement? settlement)
    {
        if (settlement is null)
        {
            return new PartyAiEntitySettings();
        }

        if (!_garrisonSettings.ContainsKey(settlement))
        {
            PartyAiEntitySettings settings;
            if (settlement.OwnerClan == Clan.PlayerClan)
            {
                settings = new PartyAiEntitySettings(_defaultClanGarrisonSettings, settlement: settlement);
            }
            else if (settlement.MapFaction == Hero.MainHero.MapFaction)
            {
                settings = new PartyAiEntitySettings(_defaultKingdomGarrisonSettings, settlement: settlement);
            }
            else
            {
                settings = new PartyAiEntitySettings();
            }

            _garrisonSettings[settlement] = settings;
        }

        return _garrisonSettings[settlement];
    }

    internal PartyAiEntitySettings Settings(Hero? hero)
    {
        if (hero is null)
        {
            return new PartyAiEntitySettings();
        }

        if (hero == Hero.MainHero)
        {
            _playerPartySettings ??= new PartyAiEntitySettings(_defaultClanPartySettings, hero);
            return _playerPartySettings;
        }

        if (IsLeadingCaravan(hero))
        {
            if (!_caravanSettings.ContainsKey(hero))
            {
                _caravanSettings.Add(hero, new PartyAiEntitySettings(_defaultClanCaravanSettings, hero: hero));
            }
            return _caravanSettings[hero];
        }

        if (!_partySettings.ContainsKey(hero))
        {
            if (hero.Clan == Clan.PlayerClan)
            {
                _partySettings.Add(hero, new PartyAiEntitySettings(_defaultClanPartySettings, hero: hero));
            }
            else if (IsHeroManageable(hero))
            {
                _partySettings.Add(hero, new PartyAiEntitySettings(_defaultKingdomPartySettings, hero: hero));
            }
            else
            {
                return new PartyAiEntitySettings();
            }
        }

        return _partySettings[hero];
    }

    internal bool IsAIHeroManageable(Hero? hero) => !IsLeadingCaravan(hero) && hero?.Clan != null && hero.Clan != Clan.PlayerClan && !hero.Clan.IsBanditFaction && hero.Occupation == Occupation.Lord;

    internal bool IsManageable(Hero? hero) => IsHeroManageable(hero) || IsCaravanManageable(hero);

    internal bool IsSettlementAutomationEligible(Hero? hero)
        => hero is not null && (hero == Hero.MainHero || IsManageable(hero));

    internal bool IsHeroManageable([NotNullWhen(true)]Hero? hero)
    {
        if (hero == null
            || Hero.MainHero.Equals(hero))
        {
            return false;
        }

        if (IsLeadingCaravan(hero))
        {
            return false;
        }

        if (Clan.PlayerClan.Heroes.Contains(hero))
        {
            return true;
        }

        // if we're not managing kingdom parties, we can skip the rest
        if (!ManageKingdomParties)
        {
            return false;
        }

        if (Clan.PlayerClan.Kingdom == null
            || hero?.Clan?.Kingdom == null
            || !hero.Clan.Kingdom.Equals(Clan.PlayerClan.Kingdom))
        {
            return false;
        }

        if (!Clan.PlayerClan.Kingdom.Leader.Equals(Hero.MainHero))
        {
            return false;
        }

        return true;
    }

    internal bool AllowCaravanConversion(Hero? hero) => SubModule.PartySettingsManager.IsCaravanManageable(hero) && SubModule.PartySettingsManager.AllowTroopConversionForCaravans;

    internal bool IsCaravanManageable(Hero? hero)
    {
        if (!ManageCaravans) { return false; }

        if (hero == null || Hero.MainHero.Equals(hero)) { return false; }

        if (!Clan.PlayerClan.Heroes.Contains(hero)) { return false; }

        return IsLeadingCaravan(hero);
    }

    internal bool IsGarrisonManageable(Settlement? settlement)
    {
        if (settlement is null || !settlement.IsFortification)
        {
            return false;
        }

        if (ManageClanGarrisons && settlement.OwnerClan == Clan.PlayerClan)
        {
            return true;
        }

        if (ManageKingdomGarrisons
            && settlement.MapFaction == Hero.MainHero.MapFaction
            && Clan.PlayerClan.Kingdom?.RulingClan == Clan.PlayerClan
            && settlement.OwnerClan != Clan.PlayerClan)
        {
            return true;
        }

        return false;
    }

    internal bool IsLeadingCaravan(Hero? hero)
    {
        return hero?.PartyBelongedTo != null && hero.IsPartyLeader && hero.PartyBelongedTo.IsCaravan;
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_partySettings", ref _partySettings);
        dataStore.SyncData("_garrisonSettings", ref _garrisonSettings);
        dataStore.SyncData("_caravanSettings", ref _caravanSettings);
        dataStore.SyncData("_partyTemplates", ref _partyTemplates);
        dataStore.SyncData("_playerPartySettings", ref _playerPartySettings);
        _partySettings ??= new Dictionary<Hero, PartyAiEntitySettings>();
        _garrisonSettings ??= new Dictionary<Settlement, PartyAiEntitySettings>();
        _caravanSettings ??= new Dictionary<Hero, PartyAiEntitySettings>();
        _partyTemplates ??= new List<PAICustomTemplate>();

        // set default fallback values here
        if (!dataStore.SyncData("AllowTroopConversion", ref AllowTroopConversion) && dataStore.IsLoading)
        {
            AllowTroopConversion = false;
        }

        if (!dataStore.SyncData("AllowTroopConversionForCaravans", ref AllowTroopConversionForCaravans) && dataStore.IsLoading)
        {
            AllowTroopConversionForCaravans = true;
        }

        if (!dataStore.SyncData("AllowTroopConversionForGarrisons", ref AllowTroopConversionForGarrisons) && dataStore.IsLoading)
        {
            AllowTroopConversionForGarrisons = true;
        }

        if (!dataStore.SyncData("ManageCaravans", ref ManageCaravans) && dataStore.IsLoading)
        {
            ManageCaravans = false;
        }

        if (!dataStore.SyncData("ManageClanGarrisons", ref ManageClanGarrisons) && dataStore.IsLoading)
        {
            ManageClanGarrisons = false;
        }

        if (!dataStore.SyncData("ManageKingdomParties", ref ManageKingdomParties) && dataStore.IsLoading)
        {
            ManageKingdomParties = false;
        }

        if (!dataStore.SyncData("ManageKingdomGarrisons", ref ManageKingdomGarrisons) && dataStore.IsLoading)
        {
            ManageKingdomGarrisons = false;
        }

        if (!dataStore.SyncData("TroopsConvertedPerDay", ref TroopsConvertedPerDay) && dataStore.IsLoading)
        {
            TroopsConvertedPerDay = 4;
        }

        if (!dataStore.SyncData("_defaultClanPartySettings", ref _defaultClanPartySettings) && dataStore.IsLoading)
        {
            _defaultClanPartySettings = new();
        }

        if (!dataStore.SyncData("_defaultClanCaravanSettings", ref _defaultClanCaravanSettings) && dataStore.IsLoading)
        {
            _defaultClanCaravanSettings = new();
        }

        if (!dataStore.SyncData("_defaultClanGarrisonSettings", ref _defaultClanGarrisonSettings) && dataStore.IsLoading)
        {
            _defaultClanGarrisonSettings = new();
        }

        if (!dataStore.SyncData("_defaultKingdomPartySettings", ref _defaultKingdomPartySettings) && dataStore.IsLoading)
        {
            _defaultKingdomPartySettings = new();
        }

        if (!dataStore.SyncData("_defaultKingdomGarrisonSettings", ref _defaultKingdomGarrisonSettings) && dataStore.IsLoading)
        {
            _defaultKingdomGarrisonSettings = new();
        }

        if (!dataStore.SyncData("AggressivePatrols", ref AggressivePatrols) && dataStore.IsLoading)
        {
            AggressivePatrols = false;
        }

        if (!dataStore.SyncData("AutoDelegateBattleCommand", ref AutoDelegateBattleCommand) && dataStore.IsLoading)
        {
            AutoDelegateBattleCommand = true;
        }

        if (!dataStore.SyncData("EnhancedBattleAi", ref EnhancedBattleAi) && dataStore.IsLoading)
        {
            EnhancedBattleAi = true;
        }

        if (!dataStore.SyncData("AvoidSiegeArtillery", ref AvoidSiegeArtillery) && dataStore.IsLoading)
        {
            AvoidSiegeArtillery = true;
        }

        if (!dataStore.SyncData("ControlPanelModiferKey", ref ControlPanelModiferKey) && dataStore.IsLoading)
        {
            ControlPanelModiferKey = InputKey.LeftControl;
        }

        if (!dataStore.SyncData("ControlPanelKey", ref ControlPanelKey) && dataStore.IsLoading)
        {
            ControlPanelKey = InputKey.P;
        }

        if (!dataStore.SyncData("CommandedPartiesModiferKey", ref CommandedPartiesModiferKey) && dataStore.IsLoading)
        {
            CommandedPartiesModiferKey = InputKey.LeftAlt;
        }

        if (!dataStore.SyncData("CommandedPartiesKey", ref CommandedPartiesKey) && dataStore.IsLoading)
        {
            CommandedPartiesKey = InputKey.X;
        }

        if (!dataStore.SyncData("CommandPartiesKey", ref CommandPartiesKey) && dataStore.IsLoading)
        {
            CommandPartiesKey = InputKey.LeftAlt;
        }

        if (!dataStore.SyncData("BattleCommanderModifierKey", ref BattleCommanderModifierKey) && dataStore.IsLoading)
        {
            BattleCommanderModifierKey = InputKey.LeftControl;
        }

        if (!dataStore.SyncData("BattleCommanderKey", ref BattleCommanderKey) && dataStore.IsLoading)
        {
            BattleCommanderKey = InputKey.M;
        }

        if (!dataStore.SyncData("SettlementAutomationVersion", ref _settlementAutomationVersion) && dataStore.IsLoading)
        {
            _settlementAutomationVersion = 0;
        }

        if (!dataStore.SyncData("BattleAutomationVersion", ref _battleAutomationVersion) && dataStore.IsLoading)
        {
            _battleAutomationVersion = 0;
        }

        _playerPartySettings ??= new PartyAiEntitySettings(_defaultClanPartySettings, Hero.MainHero);

        foreach (PartyAiEntitySettings settings in AllSettings())
        {
            settings.Composition ??= new PartyComposition(0.35f, 0.30f, 0.20f, 0.15f);
            settings.Composition.ApplyTemplate(settings.PartyTemplate, out _);
        }
    }

    private IEnumerable<PartyAiEntitySettings> AllSettings()
    {
        if (_playerPartySettings is not null)
        {
            yield return _playerPartySettings;
        }

        foreach (PartyAiEntitySettings settings in _partySettings.Values)
        {
            yield return settings;
        }

        foreach (PartyAiEntitySettings settings in _caravanSettings.Values)
        {
            yield return settings;
        }

        foreach (PartyAiEntitySettings settings in _garrisonSettings.Values)
        {
            yield return settings;
        }
    }

    private IEnumerable<PartyAiEntitySettings> DefaultSettings()
    {
        yield return _defaultClanPartySettings;
        yield return _defaultClanCaravanSettings;
        yield return _defaultClanGarrisonSettings;
        yield return _defaultKingdomPartySettings;
        yield return _defaultKingdomGarrisonSettings;
    }
}
