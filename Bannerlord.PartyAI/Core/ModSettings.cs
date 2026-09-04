using Bannerlord.PartyAI.Finance;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.InputSystem;

namespace Bannerlord.PartyAI.Core;

/// <summary>
/// Every campaign-wide option of the mod in one place.
/// <para>
/// Persistence is split across two campaign behaviors purely to stay compatible with saves
/// created by earlier versions: the core options live in <see cref="Parties.PartyRegistry"/>'s
/// data store and the clan-automation options live in
/// <see cref="Parties.ClanAutomationBehavior"/>'s data store, both under their historical keys.
/// </para>
/// </summary>
public sealed class ModSettings
{
    // ---- Management scope -------------------------------------------------------------

    /// <summary>Manage player-clan caravans (troops, trade-town filter).</summary>
    public bool ManageCaravans { get; set; }

    /// <summary>Manage garrisons of player-clan fiefs.</summary>
    public bool ManageClanGarrisons { get; set; }

    /// <summary>When the player rules a kingdom, also manage every vassal lord party.</summary>
    public bool ManageKingdomParties { get; set; }

    /// <summary>When the player rules a kingdom, also manage every kingdom garrison.</summary>
    public bool ManageKingdomGarrisons { get; set; }

    // ---- Troop conversion -------------------------------------------------------------

    /// <summary>Convert lord-party troops that do not fit the assigned template.</summary>
    public bool AllowTroopConversion { get; set; }

    public bool AllowTroopConversionForCaravans { get; set; } = true;

    public bool AllowTroopConversionForGarrisons { get; set; } = true;

    /// <summary>Per-party daily conversion budget. Zero means unlimited.</summary>
    public int TroopsConvertedPerDay { get; set; } = 4;

    // ---- Campaign AI tweaks -----------------------------------------------------------

    /// <summary>Patrolling AI parties chase catchable hostile parties instead of wandering.</summary>
    public bool AggressivePatrols { get; set; }

    // ---- Battle -----------------------------------------------------------------------

    public bool AutoDelegateBattleCommand { get; set; } = true;

    public bool EnhancedBattleAi { get; set; } = true;

    public bool AvoidSiegeArtillery { get; set; } = true;

    // ---- Hotkeys ----------------------------------------------------------------------

    public Hotkey OpenControlPanel { get; set; } = new(InputKey.LeftControl, InputKey.P);

    public Hotkey SelectCommandedParties { get; set; } = new(InputKey.LeftAlt, InputKey.X);

    /// <summary>Held while clicking the map to redirect the selected parties. Modifier-less.</summary>
    public Hotkey CommandParties { get; set; } = new(InputKey.LeftAlt);

    public Hotkey ToggleBattleCommander { get; set; } = new(InputKey.LeftControl, InputKey.M);

    // ---- Treasury ---------------------------------------------------------------------

    /// <summary>Automation never spends the player's gold below this amount.</summary>
    public int GoldReserve { get; set; } = 20000;

    /// <summary>
    /// Automation that adds a recurring cost (new parties, garrison reinforcements) must keep the
    /// projected daily balance at or above this value.
    /// </summary>
    public int MinimumDailyBalance { get; set; }

    public WorkshopMode WorkshopMode { get; set; } = WorkshopMode.Recommend;

    /// <summary>Days of falling capital before a workshop counts as unprofitable.</summary>
    public int WorkshopReviewDays { get; set; } = 10;

    // ---- Clan automation --------------------------------------------------------------

    public bool AutoCreateClanParties { get; set; }

    /// <summary>Maximum number of auto-created clan parties. Zero means the clan limit.</summary>
    public int AutoCreateClanPartiesMax { get; set; }

    /// <summary>Heroes allowed to lead auto-created parties. Empty means anyone eligible.</summary>
    public List<Hero> AutoCreateClanPartiesRoster { get; set; } = new();

    public bool AutoCreateClanCaravans { get; set; }

    /// <summary>Maximum number of player caravans. Zero means unlimited.</summary>
    public int AutoCreateClanCaravansMax { get; set; } = 1;

    public bool AutoCreateEliteCaravans { get; set; }

    // ---- Main party autopilot ---------------------------------------------------------

    /// <summary>Lets the order system drive the player's own party while the player is not steering it.</summary>
    public bool MainPartyAutopilot { get; set; }

    /// <summary>Real seconds the party must stand idle before the autopilot takes over again.</summary>
    public int AutopilotResumeSeconds { get; set; } = 5;

    /// <summary>Whether autopilot orders may bring the player into settlements (and leave again on their own).</summary>
    public bool AutopilotEntersSettlements { get; set; } = true;

    // ---- Offense ----------------------------------------------------------------------

    public bool AutoOffense { get; set; }

    /// <summary>Required ratio of our gathered strength to the target's defense before a siege is launched.</summary>
    public float OffenseStrengthRatio { get; set; } = 2f;

    /// <summary>Only enemy fortifications within this map distance of our fiefs or parties are considered.</summary>
    public float OffenseRadius { get; set; } = 120f;

    public int OffenseMaxParties { get; set; } = 4;

    /// <summary>Give up on a siege that has not succeeded after this many days.</summary>
    public int OffenseMaxDays { get; set; } = 15;

    /// <summary>Merge the attackers into one army when the clan belongs to a kingdom.</summary>
    public bool OffenseFormArmy { get; set; } = true;

    // ---- Workshop purchases -----------------------------------------------------------

    public WorkshopMode WorkshopBuyMode { get; set; } = WorkshopMode.Recommend;

    // ---- Town visits ------------------------------------------------------------------

    public bool AutoSellPrisoners { get; set; }

    /// <summary>Keep prisoners that fit the main party's template so they can be recruited later.</summary>
    public bool SellPrisonersKeepTemplate { get; set; } = true;

    public bool AutoSellLoot { get; set; }

    public bool AutoSellTradeGoods { get; set; }

    // ---- Persistence ------------------------------------------------------------------

    /// <summary>Saved through <see cref="Parties.PartyRegistry"/>. Keys are historical.</summary>
    internal void SyncCore(IDataStore store)
    {
        Sync(store, "GoldReserve", () => GoldReserve, v => GoldReserve = Math.Max(0, v), 20000);
        Sync(store, "MinimumDailyBalance", () => MinimumDailyBalance, v => MinimumDailyBalance = v, 0);
        Sync(store, "WorkshopMode", () => WorkshopMode, v => WorkshopMode = v, WorkshopMode.Recommend);
        Sync(store, "WorkshopReviewDays", () => WorkshopReviewDays, v => WorkshopReviewDays = Math.Max(1, v), 10);
        Sync(store, "WorkshopBuyMode", () => WorkshopBuyMode, v => WorkshopBuyMode = v, WorkshopMode.Recommend);

        Sync(store, "MainPartyAutopilot", () => MainPartyAutopilot, v => MainPartyAutopilot = v, false);
        Sync(store, "AutopilotResumeSeconds", () => AutopilotResumeSeconds, v => AutopilotResumeSeconds = Math.Max(0, v), 5);
        Sync(store, "AutopilotEntersSettlements", () => AutopilotEntersSettlements, v => AutopilotEntersSettlements = v, true);

        Sync(store, "AutoOffense", () => AutoOffense, v => AutoOffense = v, false);
        Sync(store, "OffenseStrengthRatio", () => OffenseStrengthRatio, v => OffenseStrengthRatio = Math.Max(1f, v), 2f);
        Sync(store, "OffenseRadius", () => OffenseRadius, v => OffenseRadius = Math.Max(10f, v), 120f);
        Sync(store, "OffenseMaxParties", () => OffenseMaxParties, v => OffenseMaxParties = Math.Max(1, v), 4);
        Sync(store, "OffenseMaxDays", () => OffenseMaxDays, v => OffenseMaxDays = Math.Max(1, v), 15);
        Sync(store, "OffenseFormArmy", () => OffenseFormArmy, v => OffenseFormArmy = v, true);

        Sync(store, "AutoSellPrisoners", () => AutoSellPrisoners, v => AutoSellPrisoners = v, false);
        Sync(store, "SellPrisonersKeepTemplate", () => SellPrisonersKeepTemplate, v => SellPrisonersKeepTemplate = v, true);
        Sync(store, "AutoSellLoot", () => AutoSellLoot, v => AutoSellLoot = v, false);
        Sync(store, "AutoSellTradeGoods", () => AutoSellTradeGoods, v => AutoSellTradeGoods = v, false);

        Sync(store, "ManageCaravans", () => ManageCaravans, v => ManageCaravans = v, false);
        Sync(store, "ManageClanGarrisons", () => ManageClanGarrisons, v => ManageClanGarrisons = v, false);
        Sync(store, "ManageKingdomParties", () => ManageKingdomParties, v => ManageKingdomParties = v, false);
        Sync(store, "ManageKingdomGarrisons", () => ManageKingdomGarrisons, v => ManageKingdomGarrisons = v, false);

        Sync(store, "AllowTroopConversion", () => AllowTroopConversion, v => AllowTroopConversion = v, false);
        Sync(store, "AllowTroopConversionForCaravans", () => AllowTroopConversionForCaravans, v => AllowTroopConversionForCaravans = v, true);
        Sync(store, "AllowTroopConversionForGarrisons", () => AllowTroopConversionForGarrisons, v => AllowTroopConversionForGarrisons = v, true);
        Sync(store, "TroopsConvertedPerDay", () => TroopsConvertedPerDay, v => TroopsConvertedPerDay = v, 4);

        Sync(store, "AggressivePatrols", () => AggressivePatrols, v => AggressivePatrols = v, false);

        Sync(store, "AutoDelegateBattleCommand", () => AutoDelegateBattleCommand, v => AutoDelegateBattleCommand = v, true);
        Sync(store, "EnhancedBattleAi", () => EnhancedBattleAi, v => EnhancedBattleAi = v, true);
        Sync(store, "AvoidSiegeArtillery", () => AvoidSiegeArtillery, v => AvoidSiegeArtillery = v, true);

        OpenControlPanel = SyncHotkey(store, "ControlPanelModiferKey", "ControlPanelKey", OpenControlPanel, new(InputKey.LeftControl, InputKey.P));
        SelectCommandedParties = SyncHotkey(store, "CommandedPartiesModiferKey", "CommandedPartiesKey", SelectCommandedParties, new(InputKey.LeftAlt, InputKey.X));
        CommandParties = SyncHotkey(store, null, "CommandPartiesKey", CommandParties, new(InputKey.LeftAlt));
        ToggleBattleCommander = SyncHotkey(store, "BattleCommanderModifierKey", "BattleCommanderKey", ToggleBattleCommander, new(InputKey.LeftControl, InputKey.M));
    }

    /// <summary>Saved through <see cref="Parties.ClanAutomationBehavior"/>. Keys are historical.</summary>
    internal void SyncAutomation(IDataStore store)
    {
        Sync(store, "AutoCreateClanParties", () => AutoCreateClanParties, v => AutoCreateClanParties = v, false);
        Sync(store, "AutoCreateClanPartiesMax", () => AutoCreateClanPartiesMax, v => AutoCreateClanPartiesMax = v, 0);
        Sync(store, "AutoCreateClanPartiesRoster", () => AutoCreateClanPartiesRoster, v => AutoCreateClanPartiesRoster = v ?? new(), new());
        Sync(store, "AutoCreateClanCaravans", () => AutoCreateClanCaravans, v => AutoCreateClanCaravans = v, false);
        Sync(store, "AutoCreateClanCaravansMax", () => AutoCreateClanCaravansMax, v => AutoCreateClanCaravansMax = Math.Max(0, v), 1);
        Sync(store, "AutoCreateEliteCaravans", () => AutoCreateEliteCaravans, v => AutoCreateEliteCaravans = v, false);
    }

    private static void Sync<T>(IDataStore store, string key, Func<T> get, Action<T> set, T fallback)
    {
        T value = get();
        if (!store.SyncData(key, ref value) && store.IsLoading)
        {
            value = fallback;
        }

        set(value);
    }

    private static Hotkey SyncHotkey(IDataStore store, string? modifierKey, string key, Hotkey current, Hotkey fallback)
    {
        InputKey modifier = current.Modifier;
        if (modifierKey is not null
            && !store.SyncData(modifierKey, ref modifier)
            && store.IsLoading)
        {
            modifier = fallback.Modifier;
        }

        InputKey main = current.Key;
        if (!store.SyncData(key, ref main) && store.IsLoading)
        {
            main = fallback.Key;
        }

        return modifierKey is null ? new Hotkey(main) : new Hotkey(modifier, main);
    }
}
