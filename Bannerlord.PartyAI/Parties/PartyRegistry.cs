using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties.Recruitment;
using Bannerlord.PartyAI.Parties.Templates;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PartyAI.Parties;

/// <summary>
/// Owns every <see cref="PartyProfile"/>, the troop templates and the global
/// <see cref="ModSettings"/>. Also answers "is this party managed by the mod?".
/// </summary>
public sealed class PartyRegistry : CampaignBehaviorBase
{
    // Historical behavior name; the save system keys this behavior's data by it.
    private const string LegacyStringId = "PartyAIClanPartySettingsManager";

    private Dictionary<Hero, PartyProfile> _partyProfiles = new();
    private Dictionary<Hero, PartyProfile> _caravanProfiles = new();
    private Dictionary<Settlement, PartyProfile> _garrisonProfiles = new();
    private PartyProfile? _playerProfile;
    private List<TroopTemplate> _templates = new();

    private PartyProfile _defaultClanParty = new();
    private PartyProfile _defaultClanCaravan = new();
    private PartyProfile _defaultClanGarrison = new();
    private PartyProfile _defaultKingdomParty = new();
    private PartyProfile _defaultKingdomGarrison = new();

    private int _settlementAutomationVersion = 1;
    private int _battleAutomationVersion = 1;
    private int _financeVersion = 1;

    public PartyRegistry(ModSettings settings) : base(LegacyStringId)
    {
        Settings = settings;
    }

    public ModSettings Settings { get; }

    // ---- Default profiles -----------------------------------------------------------------

    public PartyProfile DefaultClanParty => _defaultClanParty;
    public PartyProfile DefaultClanCaravan => _defaultClanCaravan;
    public PartyProfile DefaultClanGarrison => _defaultClanGarrison;
    public PartyProfile DefaultKingdomParty => _defaultKingdomParty;
    public PartyProfile DefaultKingdomGarrison => _defaultKingdomGarrison;

    public IEnumerable<PartyProfile> DefaultProfiles
    {
        get
        {
            yield return _defaultClanParty;
            yield return _defaultClanCaravan;
            yield return _defaultClanGarrison;
            yield return _defaultKingdomParty;
            yield return _defaultKingdomGarrison;
        }
    }

    // ---- Events ---------------------------------------------------------------------------

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        _playerProfile ??= new PartyProfile(_defaultClanParty, Hero.MainHero);

        if (_settlementAutomationVersion < 1)
        {
            foreach (PartyProfile profile in AllProfiles().Concat(DefaultProfiles))
            {
                profile.SettlementAutomation = SettlementAutomationLevel.Full;
            }

            _settlementAutomationVersion = 1;
        }

        if (_battleAutomationVersion < 1)
        {
            Settings.EnhancedBattleAi = true;
            Settings.AvoidSiegeArtillery = true;
            _battleAutomationVersion = 1;
        }

        // Older saves kept the gold reserve inside the town settings; it is now global.
        if (_financeVersion < 1)
        {
            Settings.GoldReserve = PartyAi.Towns.Settings.PlayerGoldReserve;
            _financeVersion = 1;
        }

        foreach (PartyProfile profile in AllProfiles())
        {
            profile.FilteredSettlements ??= new();
            profile.OrderQueue ??= new();
            if (profile.PatrolRadius <= 0f)
            {
                profile.PatrolRadius = 1f;
            }
        }

        BuiltinTemplateCatalog.EnsureBuiltInTemplates(this);
    }

    private void OnDailyTick()
    {
        foreach (PartyProfile profile in AllProfiles())
        {
            profile.ResetBudgets(Settings.TroopsConvertedPerDay);
        }

        RemoveDeadHeroes(_partyProfiles);
        RemoveDeadHeroes(_caravanProfiles);
    }

    private static void RemoveDeadHeroes(Dictionary<Hero, PartyProfile> profiles)
    {
        foreach (Hero hero in profiles.Keys.Where(hero => hero is null || hero.IsDead || hero.IsDisabled).ToList())
        {
            profiles.Remove(hero);
        }
    }

    // ---- Profile lookup -------------------------------------------------------------------

    public IEnumerable<PartyProfile> AllPartyProfiles => _partyProfiles.Values;

    public IEnumerable<PartyProfile> ProfilesWithOrders
        => _partyProfiles.Values.Where(profile => profile.HasActiveOrder).ToList();

    public IEnumerable<PartyProfile> AllProfiles()
    {
        if (_playerProfile is not null)
        {
            yield return _playerProfile;
        }

        foreach (PartyProfile profile in _partyProfiles.Values) yield return profile;
        foreach (PartyProfile profile in _caravanProfiles.Values) yield return profile;
        foreach (PartyProfile profile in _garrisonProfiles.Values) yield return profile;
    }

    /// <summary>
    /// The profile for a hero's party. Unmanaged heroes get a throw-away default so callers never
    /// have to null-check.
    /// </summary>
    public PartyProfile Profile(Hero? hero)
    {
        if (hero is null)
        {
            return new PartyProfile();
        }

        if (hero == Hero.MainHero)
        {
            return _playerProfile ??= new PartyProfile(_defaultClanParty, hero);
        }

        if (IsLeadingCaravan(hero))
        {
            if (!_caravanProfiles.TryGetValue(hero, out PartyProfile? caravan))
            {
                caravan = new PartyProfile(_defaultClanCaravan, hero);
                _caravanProfiles.Add(hero, caravan);
            }

            return caravan;
        }

        if (_partyProfiles.TryGetValue(hero, out PartyProfile? party))
        {
            return party;
        }

        if (hero.Clan == Clan.PlayerClan)
        {
            party = new PartyProfile(_defaultClanParty, hero);
        }
        else if (IsHeroManageable(hero))
        {
            party = new PartyProfile(_defaultKingdomParty, hero);
        }
        else
        {
            return new PartyProfile();
        }

        _partyProfiles.Add(hero, party);
        return party;
    }

    /// <summary>The garrison profile of a fortification.</summary>
    public PartyProfile Profile(Settlement? settlement)
    {
        if (settlement is null)
        {
            return new PartyProfile();
        }

        if (_garrisonProfiles.TryGetValue(settlement, out PartyProfile? profile))
        {
            return profile;
        }

        if (settlement.OwnerClan == Clan.PlayerClan)
        {
            profile = new PartyProfile(_defaultClanGarrison, settlement);
        }
        else if (settlement.MapFaction == Hero.MainHero.MapFaction)
        {
            profile = new PartyProfile(_defaultKingdomGarrison, settlement);
        }
        else
        {
            profile = new PartyProfile();
        }

        _garrisonProfiles[settlement] = profile;
        return profile;
    }

    public bool HasActiveOrder(Hero? hero) => hero is not null && Profile(hero).HasActiveOrder;

    // ---- Manageability --------------------------------------------------------------------

    /// <summary>A non-player, non-caravan lord party the mod controls.</summary>
    public bool IsHeroManageable([NotNullWhen(true)] Hero? hero)
    {
        if (hero is null || hero == Hero.MainHero || IsLeadingCaravan(hero))
        {
            return false;
        }

        if (hero.Clan == Clan.PlayerClan)
        {
            return true;
        }

        if (!Settings.ManageKingdomParties)
        {
            return false;
        }

        Kingdom? kingdom = Clan.PlayerClan.Kingdom;
        return kingdom is not null
            && hero.Clan?.Kingdom == kingdom
            && kingdom.Leader == Hero.MainHero;
    }

    public bool IsCaravanManageable([NotNullWhen(true)] Hero? hero)
        => Settings.ManageCaravans
            && hero is not null
            && hero != Hero.MainHero
            && hero.Clan == Clan.PlayerClan
            && IsLeadingCaravan(hero);

    /// <summary>Either a managed lord party or a managed caravan.</summary>
    public bool IsManageable([NotNullWhen(true)] Hero? hero)
        => IsHeroManageable(hero) || IsCaravanManageable(hero);

    /// <summary>Managed parties plus the player's own party.</summary>
    public bool IsAutomationEligible([NotNullWhen(true)] Hero? hero)
        => hero is not null && (hero == Hero.MainHero || IsManageable(hero));

    public bool IsGarrisonManageable([NotNullWhen(true)] Settlement? settlement)
    {
        if (settlement is null || !settlement.IsFortification)
        {
            return false;
        }

        if (Settings.ManageClanGarrisons && settlement.OwnerClan == Clan.PlayerClan)
        {
            return true;
        }

        return Settings.ManageKingdomGarrisons
            && settlement.MapFaction == Hero.MainHero.MapFaction
            && Clan.PlayerClan.Kingdom?.RulingClan == Clan.PlayerClan
            && settlement.OwnerClan != Clan.PlayerClan;
    }

    public bool AllowsCaravanConversion(Hero? hero)
        => IsCaravanManageable(hero) && Settings.AllowTroopConversionForCaravans;

    /// <summary>Whether template-based troop conversion applies to this profile.</summary>
    public bool AllowsConversion(PartyProfile profile)
    {
        if (profile.Template is null)
        {
            return false;
        }

        if (profile.IsGarrison)
        {
            return Settings.AllowTroopConversionForGarrisons && IsGarrisonManageable(profile.Settlement);
        }

        if (IsCaravanManageable(profile.Hero))
        {
            return Settings.AllowTroopConversionForCaravans;
        }

        return Settings.AllowTroopConversion || profile.SettlementAutomation == SettlementAutomationLevel.Full;
    }

    public static bool IsLeadingCaravan([NotNullWhen(true)] Hero? hero)
        => hero?.PartyBelongedTo is { IsCaravan: true } && hero.IsPartyLeader;

    // ---- Templates ------------------------------------------------------------------------

    public IReadOnlyList<TroopTemplate> Templates => _templates;

    public bool IsUniqueTemplateName(string name)
        => _templates.All(template => template.Name != name);

    public TroopTemplate? FindTemplateBySource(string sourceId)
        => _templates.FirstOrDefault(template => template.SourceId == sourceId);

    public TroopTemplate CreateTemplate(
        string name,
        TroopRoster upgradeTargets,
        PartyComposition? recommendedComposition = null,
        string? sourceId = null)
    {
        var template = new TroopTemplate(name, upgradeTargets, recommendedComposition, sourceId);
        _templates.Add(template);
        return template;
    }

    public void DeleteTemplate(TroopTemplate template)
    {
        _templates.Remove(template);
        foreach (PartyProfile profile in AllProfiles().Concat(DefaultProfiles))
        {
            if (profile.Template == template)
            {
                profile.SetTemplate(null);
            }
        }
    }

    // ---- Persistence ----------------------------------------------------------------------

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_partySettings", ref _partyProfiles);
        dataStore.SyncData("_garrisonSettings", ref _garrisonProfiles);
        dataStore.SyncData("_caravanSettings", ref _caravanProfiles);
        dataStore.SyncData("_partyTemplates", ref _templates);
        dataStore.SyncData("_playerPartySettings", ref _playerProfile);
        _partyProfiles ??= new();
        _garrisonProfiles ??= new();
        _caravanProfiles ??= new();
        _templates ??= new();

        SyncDefault(dataStore, "_defaultClanPartySettings", ref _defaultClanParty);
        SyncDefault(dataStore, "_defaultClanCaravanSettings", ref _defaultClanCaravan);
        SyncDefault(dataStore, "_defaultClanGarrisonSettings", ref _defaultClanGarrison);
        SyncDefault(dataStore, "_defaultKingdomPartySettings", ref _defaultKingdomParty);
        SyncDefault(dataStore, "_defaultKingdomGarrisonSettings", ref _defaultKingdomGarrison);

        Settings.SyncCore(dataStore);

        if (!dataStore.SyncData("SettlementAutomationVersion", ref _settlementAutomationVersion) && dataStore.IsLoading)
        {
            _settlementAutomationVersion = 0;
        }

        if (!dataStore.SyncData("BattleAutomationVersion", ref _battleAutomationVersion) && dataStore.IsLoading)
        {
            _battleAutomationVersion = 0;
        }

        if (!dataStore.SyncData("FinanceVersion", ref _financeVersion) && dataStore.IsLoading)
        {
            _financeVersion = 0;
        }

        _playerProfile ??= new PartyProfile(_defaultClanParty, Hero.MainHero);

        foreach (PartyProfile profile in AllProfiles())
        {
            profile.Composition ??= PartyComposition.Default;
            profile.Composition.ApplyTemplate(profile.Template);
        }
    }

    private static void SyncDefault(IDataStore store, string key, ref PartyProfile profile)
    {
        bool found = store.SyncData(key, ref profile);
        if ((!found && store.IsLoading) || profile is null)
        {
            profile = new PartyProfile();
        }
    }
}
