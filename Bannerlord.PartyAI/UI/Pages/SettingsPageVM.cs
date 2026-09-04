using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.UI.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.UI.Pages;

/// <summary>Campaign-wide options: what is managed, troop conversion, clan automation, AI tweaks, battle, hotkeys.</summary>
public sealed class SettingsPageVM : ViewModel
{
    private MBBindingList<SettingRowVM> _rows = new();
    private bool _isVisible;

    public SettingsPageVM()
    {
        ModSettings settings = PartyAi.Settings;
        var rows = new List<SettingRowVM>();

        rows.Add(SettingRowVM.Header("{=PAI2Kzocn92}Management"));
        rows.Add(SettingRowVM.Info("{=PAI_NOTE}Note", () => L.S("{=PAIdJOXgi68}Enable or disable management of specific types of parties. You can change these at any time. If you disable a category that was previously managed, your settings for those parties will be ignored until you enable management again.")));
        rows.Add(SettingRowVM.Toggle("{=PAI68ZMWYZS}Caravans", "{=PAIlZXTnEd8}Manage caravans for your clan. Caravan settings are saved separately from regular party settings, so you can have heroes with both. In order to manage their troops properly, the mod needs to have troop conversion enabled for caravans.",
            () => settings.ManageCaravans, value => settings.ManageCaravans = value));
        rows.Add(SettingRowVM.Toggle("{=PAIFQeuWjXA}Clan Garrisons", "{=PAIYkuWNX8E}Manage garrison parties for your clan.",
            () => settings.ManageClanGarrisons, value => settings.ManageClanGarrisons = value, () => settings.AllowTroopConversionForGarrisons));
        rows.Add(SettingRowVM.Toggle("{=PAIeJ3goSqx}Kingdom Parties", "{=PAI8l5Lt9g3}If you are the ruler of your kingdom, manage parties for the entire kingdom instead of just your clan.",
            () => settings.ManageKingdomParties, value => settings.ManageKingdomParties = value));
        rows.Add(SettingRowVM.Toggle("{=PAIGJEcA4MB}Kingdom Garrisons", "{=PAIy0nVMLXY}If you are the ruler of your kingdom, manage garrisons for the entire kingdom instead of just your clan.",
            () => settings.ManageKingdomGarrisons, value => settings.ManageKingdomGarrisons = value, () => settings.AllowTroopConversionForGarrisons));

        rows.Add(SettingRowVM.Header("{=PAIrJoL4fjj}Troop Conversion"));
        rows.Add(SettingRowVM.Info("{=PAI_NOTE}Note", () => L.S("{=PAIn0vtnqMG}Allow troops to be automatically converted to the ones in your assigned party template. Cost is adjusted if needed.")));
        rows.Add(SettingRowVM.Toggle("{=PAIhmt3dhI6}For Lords", "{=PAIlflf9B0n}Allows troop conversion for lord parties. This setting is no longer required to manage lord party troop composition. Use the recruitment order to make sure your parties recruit the troops you want.",
            () => settings.AllowTroopConversion,
            value =>
            {
                settings.AllowTroopConversion = value;
                if (value)
                {
                    foreach (PartyProfile profile in PartyAi.Parties.AllPartyProfiles)
                    {
                        profile.DismissUnwantedTroops = false;
                    }
                }
            }));
        rows.Add(SettingRowVM.Toggle("{=PAIIraDmoi5}For Caravans", "{=PAIr7ucbc6X}Allows troop conversion for caravans. At present this setting is necessary to manage caravan troop composition.",
            () => settings.AllowTroopConversionForCaravans, value => settings.AllowTroopConversionForCaravans = value));
        rows.Add(SettingRowVM.Toggle("{=PAIxcNer6Hm}For Garrisons", "{=PAIc99C3VmP}Allows troop conversion for garrisons. At present this setting is necessary to manage garrison troop composition.",
            () => settings.AllowTroopConversionForGarrisons,
            value =>
            {
                settings.AllowTroopConversionForGarrisons = value;
                if (!value)
                {
                    settings.ManageClanGarrisons = false;
                    settings.ManageKingdomGarrisons = false;
                }
            }));
        rows.Add(SettingRowVM.Number("{=PAIcd2NB574}Troops Converted Per Day", "{=PAImiSXBh3N}Amount of troops to convert to a party template per day. This value is per-party and applies to all managed parties, caravans, and garrisons. Helps protect against large spikes in cost from changing templates, and just makes it feel a little less awkward than magically converting 300 troops at once.",
            0, 100, () => settings.TroopsConvertedPerDay, value => settings.TroopsConvertedPerDay = value,
            value => value == 0 ? L.S("{=PAILiYi3RTj}All") : value.ToString()));

        rows.Add(SettingRowVM.Header("{=PAI_CLAN_AUTOMATION}Clan Automation"));
        rows.Add(SettingRowVM.Toggle("{=PAIsUcGJNnV}Auto Create Clan Parties", "{=PAI_AUTO_PARTIES_HINT}Automatically create clan parties for heroes that are available. A party is only raised when the treasury is above its reserve and the projected daily balance can carry another party's wages.",
            () => settings.AutoCreateClanParties, value => settings.AutoCreateClanParties = value));
        rows.Add(SettingRowVM.Limit("{=PAIt2WLwtca}Party Limit", "{=PAIi4vuS6na}Limits the maximum amount of clan parties that will be auto created.",
            Clan.PlayerClan.WarPartyLimit, L.S("{=PAIIqVpFFAi}Max"),
            () => settings.AutoCreateClanPartiesMax, value => settings.AutoCreateClanPartiesMax = value));
        rows.Add(SettingRowVM.Info("{=PAIsBxGgiYZ}Leader Roster", () => settings.AutoCreateClanPartiesRoster.Count == 0
            ? L.S("{=PAI_ROSTER_ANYONE}Any eligible hero")
            : string.Join(", ", settings.AutoCreateClanPartiesRoster.Select(hero => hero.Name.ToString())), "{=PAIBKfwhLn2}Heroes that we are allowed to create parties for. If blank, all available heroes will be considered."));
        rows.Add(SettingRowVM.Action("{=PAI_EDIT_ROSTER}Choose party leaders", null, "{=PAIQNUqwt4C}Edit", EditRoster, () => settings.AutoCreateClanParties));
        rows.Add(SettingRowVM.Toggle("{=PAI_AUTO_CREATE_CARAVANS}Auto Create Clan Caravans", "{=PAI_AUTO_CARAVANS_HINT}While the main party is in a safe town, automatically pay for at most one caravan per day until the configured limit is reached, as long as the cost stays above the gold reserve. The best available companion is selected by trade, scouting and tactics skill.",
            () => settings.AutoCreateClanCaravans, value => settings.AutoCreateClanCaravans = value));
        rows.Add(SettingRowVM.Toggle("{=PAI_AUTO_ELITE_CARAVANS}Use Elite Caravans", "{=PAI_AUTO_ELITE_CARAVANS_HINT}Create the more expensive caravan variant with stronger starting guards.",
            () => settings.AutoCreateEliteCaravans, value => settings.AutoCreateEliteCaravans = value, () => settings.AutoCreateClanCaravans));
        rows.Add(SettingRowVM.Limit("{=PAI_AUTO_CARAVAN_LIMIT}Caravan Limit", "{=PAI_AUTO_CARAVAN_LIMIT_HINT}Maximum number of active player-clan caravans. Max removes the limit.",
            10, L.S("{=PAIIqVpFFAi}Max"), () => settings.AutoCreateClanCaravansMax, value => settings.AutoCreateClanCaravansMax = value));

        rows.Add(SettingRowVM.Header("{=PAI_WAR_HEADER}Offense"));
        rows.Add(SettingRowVM.Toggle("{=PAI_AUTO_OFFENSE}Launch sieges automatically", "{=PAI_AUTO_OFFENSE_HINT}Once a day, when your free clan parties clearly outmatch a nearby enemy castle or town, send them to besiege it. Parties keep the reserve set under town defense, never leave a besieged fief of yours, and get their previous orders back when the siege ends, is relieved, or drags on too long. Your own party is never sent.",
            () => settings.AutoOffense, value => settings.AutoOffense = value));
        rows.Add(SettingRowVM.Info("{=PAI_STATUS}Status", () => PartyAi.Offense.Status.ToString()));
        rows.Add(SettingRowVM.Action("{=PAI_OFFENSE_CANCEL}Call off the current offensive", null, "{=PAI_OFFENSE_CANCEL_BUTTON}Call off", () =>
        {
            PartyAi.Offense.Cancel();
            RefreshRows();
        }, () => PartyAi.Offense.Current is not null));
        Func<bool> offense = () => settings.AutoOffense;
        rows.Add(SettingRowVM.Number("{=PAI_OFFENSE_RATIO}Required strength advantage", "{=PAI_OFFENSE_RATIO_HINT}Our gathered strength must be at least this multiple of the target's defense (garrison, militia, lords inside and half of the enemy lords nearby).",
            110, 500, () => (int)Math.Round(settings.OffenseStrengthRatio * 100f), value => settings.OffenseStrengthRatio = value / 100f, value => (value / 100f).ToString("0.0") + "x", offense));
        rows.Add(SettingRowVM.Number("{=PAI_OFFENSE_RADIUS}Search radius", "{=PAI_OFFENSE_RADIUS_HINT}Only enemy fortifications within this map distance of one of your fiefs or free parties are considered.",
            30, 400, () => (int)settings.OffenseRadius, value => settings.OffenseRadius = value, null, offense));
        rows.Add(SettingRowVM.Number("{=PAI_OFFENSE_MAX_PARTIES}Parties per offensive", "{=PAI_OFFENSE_MAX_PARTIES_HINT}Maximum clan parties committed to one siege.",
            1, 10, () => settings.OffenseMaxParties, value => settings.OffenseMaxParties = value, null, offense));
        rows.Add(SettingRowVM.Number("{=PAI_OFFENSE_MAX_DAYS}Give up after", "{=PAI_OFFENSE_MAX_DAYS_HINT}A siege that has not succeeded after this many days is abandoned and the parties resume their orders.",
            3, 60, () => settings.OffenseMaxDays, value => settings.OffenseMaxDays = value, value => L.T("{=PAI_TOWN_DAYS}{DAYS} days", "DAYS", value).ToString(), offense));
        rows.Add(SettingRowVM.Toggle("{=PAI_OFFENSE_ARMY}Form an army", "{=PAI_OFFENSE_ARMY_HINT}When your clan belongs to a kingdom, the attackers merge into one army led by the strongest party so they fight together. Calling your own clan's parties costs no influence, but keeping the army's cohesion up may. Without a kingdom the parties besiege side by side.",
            () => settings.OffenseFormArmy, value => settings.OffenseFormArmy = value, offense));

        rows.Add(SettingRowVM.Header("{=PAIwNqKC63z}AI Tweaks"));
        rows.Add(SettingRowVM.Toggle("{=PAI9BPfqnUx}Aggressive Patrols", "{=PAIFxvrVYlD}If enabled, all AI patrols will attack any parties that come in range if they can catch them. Amends the 'Patrolling around X' AI behavior to include searching for targets--normally they wander aimlessly and don't attack anything. This is applied across the board, so you may not want to enable it until you're in the vassal/kingdom stage so there'll be more bandits.",
            () => settings.AggressivePatrols, value => settings.AggressivePatrols = value));

        rows.Add(SettingRowVM.Header("{=PAI_BATTLE_HEADER}Battle"));
        rows.Add(SettingRowVM.Toggle("{=PAI_AUTO_BATTLE_COMMANDER}Automatic Battle Commander", "{=PAI_AUTO_BATTLE_COMMANDER_HINT}Automatically hand all player formations to the native tactical AI after deployment. Your character remains under your control, and the battle commander key toggles command at any time.",
            () => settings.AutoDelegateBattleCommand, value => settings.AutoDelegateBattleCommand = value));
        rows.Add(SettingRowVM.Toggle("{=PAI_ENHANCED_BATTLE_AI}Adaptive Field Battle AI", "{=PAI_ENHANCED_BATTLE_AI_HINT}Adds a power-aware combined-arms tactic that competes normally with native tactics instead of overriding them. Realistic Battle AI still takes priority.",
            () => settings.EnhancedBattleAi, value => settings.EnhancedBattleAi = value));
        rows.Add(SettingRowVM.Toggle("{=PAI_AVOID_SIEGE_ARTILLERY}Avoid Siege Artillery", "{=PAI_AVOID_SIEGE_ARTILLERY_HINT}AI troops near the predicted impact point of heavy siege projectiles briefly scatter, then return to native siege behavior. Troops in melee or using siege objects are not interrupted.",
            () => settings.AvoidSiegeArtillery, value => settings.AvoidSiegeArtillery = value));

        rows.Add(SettingRowVM.Header("{=PAIKbIc509P}Keybinds"));
        rows.Add(SettingRowVM.HotkeyRow("{=PAI_KEY_CONTROL_PANEL}Control Panel", "{=PAIQNbMherW}Keybind to open this control panel. If you lock yourself out with a broken key combo, use partyai.open in the console to get back here and fix it.",
            () => settings.OpenControlPanel, value => settings.OpenControlPanel = value));
        rows.Add(SettingRowVM.HotkeyRow("{=PAI_KEY_CHOOSE_PARTIES}Choose Parties to Command", "{=PAIdjKjbD9Y}Keybind to choose which parties to directly command. Press ALT+X (default) to choose nearby parties, then hold ALT (default) to order them around.",
            () => settings.SelectCommandedParties, value => settings.SelectCommandedParties = value));
        rows.Add(SettingRowVM.HotkeyRow("{=PAI_KEY_COMMAND_PARTIES}Command Parties (hold)", "{=PAIY9zrtsqV}Keybind to command nearby parties. Press ALT+X (default) to choose nearby parties, then hold ALT (default) to order them around.",
            () => settings.CommandParties, value => settings.CommandParties = value, hasModifier: false));
        rows.Add(SettingRowVM.HotkeyRow("{=PAI_KEY_BATTLE_COMMANDER}Battle Commander", "{=PAI_BATTLE_COMMANDER_KEY_HINT}Toggle between native AI command and your previous formation control state during battle.",
            () => settings.ToggleBattleCommander, value => settings.ToggleBattleCommander = value));

        var list = new MBBindingList<SettingRowVM>();
        foreach (SettingRowVM row in rows)
        {
            row.Changed += RefreshRows;
            list.Add(row);
        }

        Rows = list;
    }

    [DataSourceProperty] public string Title => L.S("{=PAI_TAB_SETTINGS}Settings");

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (value != _isVisible)
            {
                _isVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsVisible));
                if (value)
                {
                    RefreshRows();
                }
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<SettingRowVM> Rows
    {
        get => _rows;
        private set
        {
            if (value != _rows)
            {
                _rows = value;
                OnPropertyChangedWithValue(value, nameof(Rows));
            }
        }
    }

    private void RefreshRows()
    {
        foreach (SettingRowVM row in _rows)
        {
            row.RefreshValues();
        }
    }

    private void EditRoster()
    {
        ModSettings settings = PartyAi.Settings;
        List<InquiryElement> elements = ClanAutomationBehavior.RosterCandidates()
            .Select(hero => new InquiryElement(hero, hero.Name.ToString(), new CharacterImageIdentifier(CharacterCode.CreateFrom(hero.CharacterObject))))
            .ToList();

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            L.S("{=PAIAbEyy75G}Select which heroes to automatically create parties for"),
            string.Empty,
            elements,
            true,
            0,
            elements.Count,
            L.Game("str_done"),
            L.Game("str_cancel"),
            results =>
            {
                settings.AutoCreateClanPartiesRoster = results.Select(result => result.Identifier).OfType<Hero>().ToList();
                RefreshRows();
            },
            null));
    }
}
