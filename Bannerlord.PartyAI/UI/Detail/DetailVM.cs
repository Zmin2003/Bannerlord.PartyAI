using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Recruitment;
using Bannerlord.PartyAI.Towns;
using Bannerlord.PartyAI.UI.Components;
using Bannerlord.PartyAI.UI.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Detail;

/// <summary>
/// The right-hand editor for whatever is selected in the Parties or Fiefs list. Every control
/// writes straight to the model; there is no Save/Cancel.
/// </summary>
public sealed class DetailVM : ViewModel
{
    private readonly EntryVM _entry;
    private readonly Action _onChanged;
    private readonly PartyProfile _profile;
    private CompositionEditorVM? _composition;
    private MBBindingList<SettingRowVM> _rows = new();
    private string _templateName = string.Empty;

    public DetailVM(EntryVM entry, Action onChanged)
    {
        _entry = entry;
        _onChanged = onChanged;
        _profile = entry.Profile;

        Title = entry.Name;
        Subtitle = entry.Subtitle;
        Visual = entry.Visual;
        Banner = entry.Banner;

        HasOrders = entry.Kind is EntryKind.LordParty or EntryKind.PlayerParty && entry.Hero?.PartyBelongedTo is not null;
        Orders = HasOrders ? new OrderQueueVM(_profile, NotifyChanged) : null;

        HasTroops = entry.Kind switch
        {
            EntryKind.GlobalTown => false,
            EntryKind.Fief => PartyAi.Parties.IsGarrisonManageable(entry.Settlement),
            _ => true
        };

        Build();
    }

    // ---- Header ----------------------------------------------------------------------------------

    [DataSourceProperty] public string Title { get; }
    [DataSourceProperty] public string Subtitle { get; }
    [DataSourceProperty] public ImageIdentifierVM? Visual { get; }
    [DataSourceProperty] public ImageIdentifierVM? Banner { get; }
    [DataSourceProperty] public bool ShowPortrait => Visual is not null;
    [DataSourceProperty] public bool ShowBanner => Banner is not null;
    [DataSourceProperty] public string StatusText => _entry.Status;
    [DataSourceProperty] public bool StatusNeedsAttention => _entry.StatusNeedsAttention;

    // ---- Orders ----------------------------------------------------------------------------------

    [DataSourceProperty] public bool HasOrders { get; }
    [DataSourceProperty] public OrderQueueVM? Orders { get; }

    // ---- Troops ----------------------------------------------------------------------------------

    [DataSourceProperty] public bool HasTroops { get; }
    [DataSourceProperty] public string TroopsHeader => L.S("{=PAI_TROOPS_HEADER}Troops");
    [DataSourceProperty] public string TemplateLabel => L.S("{=PAIrkbpwijb}Template");
    [DataSourceProperty] public HintViewModel TemplateHint => new(L.T("{=PAI_TEMPLATE_HINT}End-of-line troops this party works towards. Recruitment, upgrades and conversion prefer troops on a path to one of them."));
    [DataSourceProperty] public HintViewModel ChangeHint => new(L.T("{=PAIXIv9UgAt}Change"));
    [DataSourceProperty] public HintViewModel ViewHint => new(L.T("{=PAkCYmU0Qtl}View"));
    [DataSourceProperty] public bool HasTemplate => _profile.Template is not null;
    [DataSourceProperty] public ImageIdentifierVM? TemplatePortrait { get; private set; }

    [DataSourceProperty]
    public string TemplateName
    {
        get => _templateName;
        private set
        {
            if (value != _templateName)
            {
                _templateName = value;
                OnPropertyChangedWithValue(value, nameof(TemplateName));
            }
        }
    }

    [DataSourceProperty]
    public CompositionEditorVM? Composition
    {
        get => _composition;
        private set
        {
            if (value != _composition)
            {
                _composition = value;
                OnPropertyChangedWithValue(value, nameof(Composition));
            }
        }
    }

    // ---- Rows ------------------------------------------------------------------------------------

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

    // ---- Commands --------------------------------------------------------------------------------

    public void ExecuteChangeTemplate()
        => TemplateDialogs.PickForProfile(_profile, () =>
        {
            RefreshTemplate();
            Composition = new CompositionEditorVM(_profile, NotifyChanged);
            NotifyChanged();
        });

    public void ExecuteViewTemplate()
    {
        if (_profile.Template is not null)
        {
            TemplateDialogs.View(_profile.Template);
        }
    }

    // ---- Building --------------------------------------------------------------------------------

    private void Build()
    {
        if (HasTroops)
        {
            RefreshTemplate();
            Composition = new CompositionEditorVM(_profile, NotifyChanged);
        }

        var rows = new List<SettingRowVM>();
        switch (_entry.Kind)
        {
            case EntryKind.PlayerParty:
                AddPlayerTroopRows(rows);
                break;
            case EntryKind.LordParty:
                AddLordTroopRows(rows);
                AddBehaviorRows(rows);
                AddLogisticsRows(rows);
                break;
            case EntryKind.Caravan:
                AddCaravanRows(rows);
                break;
            case EntryKind.Garrison:
                AddGarrisonRows(rows);
                break;
            case EntryKind.Fief:
                AddFiefRows(rows);
                if (HasTroops)
                {
                    AddGarrisonRows(rows);
                }
                break;
            case EntryKind.GlobalTown:
                AddGlobalTownRows(rows);
                break;
            case EntryKind.Defaults:
                AddDefaultsRows(rows);
                break;
        }

        var list = new MBBindingList<SettingRowVM>();
        foreach (SettingRowVM row in rows)
        {
            row.Changed += OnRowChanged;
            list.Add(row);
        }

        Rows = list;
    }

    private void RefreshTemplate()
    {
        TemplateName = _profile.Template?.Name ?? L.S("{=PATZD6SvrZr}No Template");
        TemplatePortrait = _profile.Template?.Portrait is null
            ? null
            : new CharacterImageIdentifierVM(CharacterCode.CreateFrom(_profile.Template.Portrait));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(TemplatePortrait));
    }

    private void OnRowChanged()
    {
        foreach (SettingRowVM row in _rows)
        {
            row.RefreshValues();
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        _entry.RefreshValues();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusNeedsAttention));
        _onChanged();
    }

    // ---- Row groups ------------------------------------------------------------------------------

    private static int MaxTier => Campaign.Current.Models.CharacterStatsModel.MaxCharacterTier;

    private SettingRowVM AutomationRow()
        => SettingRowVM.Enum<SettlementAutomationLevel>(
            "{=PAI_AUTOMATION_LEVEL}Settlement automation",
            "{=PAI_AUTOMATION_LEVEL_HINT}What happens when the party enters a town or village. Recruit fills the party according to the template and composition; Recruit + Upgrade also spends XP, gold and required items on upgrades; Full Auto additionally converts off-template troops and re-equips heroes from the party inventory.",
            EntryVM.AutomationText,
            () => _profile.SettlementAutomation,
            value => _profile.SettlementAutomation = value);

    private SettingRowVM MaxTierRow()
        => SettingRowVM.Limit(
            "{=PAIn4UJJg3a}Max Troop Tier",
            "{=PAIKeTFa2PX}Maximum troop tier to upgrade troops to. If you lower this setting while there are higher tier troops in the party, they will be downgraded.",
            MaxTier,
            L.S("{=PAIIqVpFFAi}Max"),
            () => _profile.MaxTroopTier,
            value => _profile.MaxTroopTier = value);

    private void AddPlayerTroopRows(List<SettingRowVM> rows)
    {
        ModSettings settings = PartyAi.Settings;
        Func<bool> autopilot = () => settings.MainPartyAutopilot;

        rows.Add(AutomationRow());
        rows.Add(MaxTierRow());
        rows.Add(SettingRowVM.Toggle(
            "{=PAIY2oX1c1Y}Recruit From Enemy Settlements",
            "{=PAIu0g5Z5Yk}Allow this party to recruit troops from enemy settlements. This can help parties operating behind enemy lines, but is risky.",
            () => _profile.RecruitFromEnemySettlements,
            value => _profile.RecruitFromEnemySettlements = value));

        rows.Add(SettingRowVM.Header("{=PAI_AUTOPILOT_HEADER}Autopilot"));
        rows.Add(SettingRowVM.Toggle("{=PAI_AUTOPILOT}Drive my party when I am not steering", "{=PAI_AUTOPILOT_HINT}Your party follows its own order queue and fallback order like a clan party does, but only while you are not steering it. Any click on the map takes control back; the autopilot resumes once the party has stood idle for the configured time. Pausing the game stops it as well.",
            () => settings.MainPartyAutopilot, value => settings.MainPartyAutopilot = value));
        rows.Add(SettingRowVM.Info("{=PAI_STATUS}Status", () => PartyAi.Autopilot.Status.ToString()));
        rows.Add(SettingRowVM.Number("{=PAI_AUTOPILOT_RESUME}Resume after idling for", "{=PAI_AUTOPILOT_RESUME_HINT}Real seconds your party must stand still after you last steered it before the autopilot takes over.",
            0, 60, () => settings.AutopilotResumeSeconds, value => settings.AutopilotResumeSeconds = value, value => L.T("{=PAI_SECONDS}{SECONDS} s", "SECONDS", value).ToString(), autopilot));
        rows.Add(SettingRowVM.Toggle("{=PAI_AUTOPILOT_ENTER}May enter settlements", "{=PAI_AUTOPILOT_ENTER_HINT}Allow recruit and visit orders to take your party into towns and villages. Settlement automation runs on entry and the autopilot leaves again by itself. A 'stay in settlement' order always hands the visit over to you.",
            () => settings.AutopilotEntersSettlements, value => settings.AutopilotEntersSettlements = value, autopilot));
        rows.Add(SettingRowVM.Toggle(
            "{=PAI_AUTO_RECRUIT}Recruit automatically when understrength",
            "{=PAI_AUTOPILOT_AUTO_RECRUIT_HINT}With the autopilot on, queue a recruiting trip whenever the party drops below the threshold.",
            () => _profile.AutoRecruitment,
            value => _profile.AutoRecruitment = value,
            autopilot));
        rows.Add(SettingRowVM.Percent(
            "{=PAI_AUTO_RECRUIT_THRESHOLD}Recruit below (% of party size)",
            "{=PAIdpT9SuVN}Party will go on a recruitment run when under this percentage of its maximum troops.",
            1, 99,
            () => _profile.AutoRecruitmentPercentage,
            value => _profile.AutoRecruitmentPercentage = value,
            () => settings.MainPartyAutopilot && _profile.AutoRecruitment));

        rows.Add(SettingRowVM.Header("{=PAI_TOWN_VISITS_HEADER}Town Visits"));
        rows.Add(SettingRowVM.Toggle("{=PAI_SELL_PRISONERS}Sell prisoners on entering a town", "{=PAI_SELL_PRISONERS_HINT}Sell every non-hero prisoner to the town when you enter it. Prisoners locked in the party screen are kept.",
            () => settings.AutoSellPrisoners, value => settings.AutoSellPrisoners = value));
        rows.Add(SettingRowVM.Toggle("{=PAI_SELL_PRISONERS_KEEP}Keep prisoners that fit the template", "{=PAI_SELL_PRISONERS_KEEP_HINT}Prisoners on an upgrade path of your party template are kept so they can be recruited later.",
            () => settings.SellPrisonersKeepTemplate, value => settings.SellPrisonersKeepTemplate = value, () => settings.AutoSellPrisoners));
        rows.Add(SettingRowVM.Toggle("{=PAI_SELL_LOOT}Sell weapons and armour on entering a town", "{=PAI_SELL_LOOT_HINT}Sell all weapons, armour and horse harness in the party inventory (mounts, food, books and banners are never sold). Lock an item in the inventory to keep it. Equip your companions first: with full settlement automation this happens automatically.",
            () => settings.AutoSellLoot, value => settings.AutoSellLoot = value));
        rows.Add(SettingRowVM.Toggle("{=PAI_SELL_GOODS}Sell trade goods too", "{=PAI_SELL_GOODS_HINT}Also sell trade goods (excluding food). Leave this off if you trade by hand.",
            () => settings.AutoSellTradeGoods, value => settings.AutoSellTradeGoods = value));
    }

    private void AddLordTroopRows(List<SettingRowVM> rows)
    {
        rows.Add(AutomationRow());
        rows.Add(MaxTierRow());
        rows.Add(SettingRowVM.Toggle(
            "{=PAIBMzCXm1l}May Recruit Troops",
            "{=PAIhSoz6d1X}Allow this party to recruit troops. If you disable this setting, you will have to supply the party with troops manually. If you do not, you can expect the AI to behave stupidly.",
            () => _profile.AllowRecruitment,
            value => _profile.AllowRecruitment = value));
        rows.Add(SettingRowVM.Toggle(
            "{=PAIY2oX1c1Y}Recruit From Enemy Settlements",
            "{=PAIu0g5Z5Yk}Allow this party to recruit troops from enemy settlements. This can help parties operating behind enemy lines, but is risky.",
            () => _profile.RecruitFromEnemySettlements,
            value => _profile.RecruitFromEnemySettlements = value,
            () => _profile.AllowRecruitment));
        rows.Add(SettingRowVM.Toggle(
            "{=PAI_AUTO_RECRUIT}Recruit automatically when understrength",
            "{=PAIMah8wd6z}Automatically set an order to go recruiting when party is below X% of it's max party size. The order will only be added if there is not an existing order to recruit troops in the queue and you are not directly commanding the party.",
            () => _profile.AutoRecruitment,
            value => _profile.AutoRecruitment = value,
            () => _profile.AllowRecruitment));
        rows.Add(SettingRowVM.Percent(
            "{=PAI_AUTO_RECRUIT_THRESHOLD}Recruit below (% of party size)",
            "{=PAIdpT9SuVN}Party will go on a recruitment run when under this percentage of its maximum troops.",
            1, 99,
            () => _profile.AutoRecruitmentPercentage,
            value => _profile.AutoRecruitmentPercentage = value,
            () => _profile.AllowRecruitment && _profile.AutoRecruitment));
        rows.Add(SettingRowVM.Toggle(
            "{=PAI_DISMISS_UNWANTED}Dismiss troops that do not fit",
            "{=PAIrFBBz1kW}Dismiss troops that do not fit the party template or the chosen composition percentages. This only happens above the specified party-size percentage so the party does not leave itself vulnerable.",
            () => _profile.DismissUnwantedTroops,
            value => _profile.DismissUnwantedTroops = value,
            () => !PartyAi.Settings.AllowTroopConversion));
        rows.Add(SettingRowVM.Percent(
            "{=PAI_DISMISS_THRESHOLD}Dismiss above (% of party size)",
            "{=PAItU2CGhot}The party starts dismissing troops that do not fit its template once it exceeds this percentage of its maximum size.",
            1, 99,
            () => _profile.DismissUnwantedTroopsPercentage,
            value => _profile.DismissUnwantedTroopsPercentage = value,
            () => _profile.DismissUnwantedTroops && !PartyAi.Settings.AllowTroopConversion));
    }

    private void AddBehaviorRows(List<SettingRowVM> rows)
    {
        rows.Add(SettingRowVM.Header("{=PAI_BEHAVIOR_HEADER}Behaviour"));
        rows.Add(SettingRowVM.Toggle("{=PAClaZBMEprx}May Join Armies", "{=PAD5Oih6uaW}Allow this party to join kingdom armies. Even with this setting disabled the party will be allowed to join your army.",
            () => _profile.AllowJoinArmies, value => _profile.AllowJoinArmies = value));
        rows.Add(SettingRowVM.Toggle("{=PAIJB8EuhJN}May Besiege", "{=PAIHANhrppA}Allow this party to besiege towns and castles. If disabled, parties will leave armies that are sieging, refunding the influence to the army leader.",
            () => _profile.AllowSieging, value => _profile.AllowSieging = value));
        rows.Add(SettingRowVM.Toggle("{=PArB6kGmInk}May Raid Villages", "{=PAIG8Ela5BJ}Allow this party to raid hostile villages. If disabled, parties will leave armies that are raiding, refunding the influence to the army leader.",
            () => _profile.AllowRaidVillages, value => _profile.AllowRaidVillages = value));
        rows.Add(SettingRowVM.Toggle("{=PAIv3zQDvLn}May Take Lords Prisoner", "{=PAIgE8T3Qxh}Allow this party to take enemy lords prisoner after battle. If disabled, parties will release captured lords.",
            () => _profile.AllowLordPrisoners, value => _profile.AllowLordPrisoners = value));
        rows.Add(SettingRowVM.Toggle("{=PAhInSCxPlc}May Donate Troops To Garrisons", "{=PAIYcYomRnV}Allow this party to donate troops to friendly garrisons.",
            () => _profile.AllowDonateTroops, value => _profile.AllowDonateTroops = value));
        rows.Add(SettingRowVM.Toggle("{=PAhQoukaUbN}May Take Troops From Settlements", "{=PAIRCSZxGNl}Allow this party to take troops from your garrisons.",
            () => _profile.AllowTakeTroopsFromSettlement, value => _profile.AllowTakeTroopsFromSettlement = value));
    }

    private void AddLogisticsRows(List<SettingRowVM> rows)
    {
        rows.Add(SettingRowVM.Header("{=PAI_LOGISTICS_HEADER}Logistics"));
        rows.Add(SettingRowVM.Toggle("{=PAIWXzJxqgi}Buy Horses", "{=PAIK3xNilPb}Buy enough horses to mount your troops on foot in order to increase movement speed. If you set the budget to zero, this will still prevent unncessary selling of horses. Please note that some horse types do not count towards the speed bonus, like Sumpter Horses in native. This feature won't treat a horse as providing a speed bonus unless the game treats it that way in native or whatever overhaul you play.",
            () => _profile.BuyHorses, value => _profile.BuyHorses = value));
        rows.Add(SettingRowVM.Number("{=PAI_HORSE_BUDGET}Daily horse budget", "{=PAIcyDMxg8t}Horse purchase budget per day.",
            0, 20000, () => _profile.BuyHorsesBudget, value => _profile.BuyHorsesBudget = value, null, () => _profile.BuyHorses));
        rows.Add(SettingRowVM.Number("{=PAIGHyxwrgx}Patrol Radius", "{=PAIMaf6ECHe}Change radius for patrol orders. Adjust it until it produces the result you want. The percentage is relative to the default radius.",
            10, 500, () => (int)Math.Round(_profile.PatrolRadius * 100f), value => _profile.PatrolRadius = value / 100f, value => value + "%"));
    }

    private void AddCaravanRows(List<SettingRowVM> rows)
    {
        if (!PartyAi.Settings.AllowTroopConversionForCaravans)
        {
            rows.Add(SettingRowVM.Info("{=PAI_NOTE}Note", () => L.S("{=PAIIAZIrPw0}These values are not useful for caravans unless you enable troop conversion in the mod options.")));
        }

        rows.Add(AutomationRow());
        rows.Add(MaxTierRow());
        rows.Add(SettingRowVM.Header("{=PAI_TRADE_HEADER}Trade"));
        rows.Add(SettingRowVM.Toggle("{=PAI7L3x9T3p}Filter Trading Settlements", "{=PAIRrqrDxYm}The caravan will only visit settlements in this list.",
            () => _profile.FilterSettlements, value => _profile.FilterSettlements = value));
        rows.Add(SettingRowVM.Info("{=PAI_TRADE_TOWNS}Trading towns", () => _profile.FilteredSettlements.Count == 0
            ? L.S("{=PAI_ALL_TOWNS}All towns")
            : string.Join(", ", _profile.FilteredSettlements.Select(settlement => settlement.Name.ToString()))));
        rows.Add(SettingRowVM.Action("{=PAI_EDIT_TRADE_TOWNS}Choose trading towns", null, "{=PAIQNUqwt4C}Edit", EditTradeTowns, () => _profile.FilterSettlements));
    }

    private void EditTradeTowns()
    {
        MobileParty main = MobileParty.MainParty;
        List<InquiryElement> elements = Settlement.All
            .Where(settlement => settlement.IsTown)
            .OrderBy(settlement => settlement.Name.ToString())
            .Select(settlement => new InquiryElement(
                settlement,
                settlement.Name.ToString(),
                settlement.MapFaction?.Banner is null ? null : new BannerImageIdentifier(settlement.MapFaction.Banner, false),
                true,
                DirectionHint(settlement, main)))
            .ToList();

        var selected = new HashSet<Settlement>(_profile.FilteredSettlements);
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            L.S("{=PAIr1WS36dp}Settlements to Visit"),
            string.Empty,
            elements,
            true,
            1,
            elements.Count,
            L.Game("str_done"),
            L.Game("str_cancel"),
            results =>
            {
                _profile.FilteredSettlements = results.Select(result => result.Identifier).OfType<Settlement>().ToList();
                OnRowChanged();
            },
            null,
            string.Empty,
            true));
    }

    private static string DirectionHint(Settlement settlement, MobileParty main)
    {
        Vec2 delta = settlement.GetPosition2D - main.GetPosition2D;
        string vertical = delta.y > 0 ? L.S("{=PAImajjVs8d}north") : L.S("{=PAISzVDwcWu}south");
        string horizontal = delta.x > 0 ? L.S("{=PAIHQQPyo2M}east") : L.S("{=PAIWGp1Ti1N}west");
        return L.T("{=PAI7v81Hher}Currently {DIRECTION} of you.", "DIRECTION", vertical + horizontal).ToString();
    }

    private void AddGarrisonRows(List<SettingRowVM> rows)
    {
        rows.Add(SettingRowVM.Header("{=PAI_TOWN_GARRISON_HEADER}Garrison"));
        rows.Add(MaxTierRow());
    }

    private void AddDefaultsRows(List<SettingRowVM> rows)
    {
        PartyRegistry registry = PartyAi.Parties;
        if (_profile == registry.DefaultClanCaravan)
        {
            rows.Add(AutomationRow());
            rows.Add(MaxTierRow());
        }
        else if (_profile == registry.DefaultClanGarrison || _profile == registry.DefaultKingdomGarrison)
        {
            rows.Add(MaxTierRow());
        }
        else
        {
            AddLordTroopRows(rows);
            AddBehaviorRows(rows);
            AddLogisticsRows(rows);
        }
    }

    // ---- Town management -------------------------------------------------------------------------

    private void AddFiefRows(List<SettingRowVM> rows)
    {
        Settlement settlement = _entry.Settlement!;
        FiefSettings fief = PartyAi.Towns.Fief(settlement);
        Func<bool> custom = () => !fief.UseGlobalDefaults;

        rows.Add(SettingRowVM.Header("{=PAI_TOWN_OPTIONS_TITLE}Town Management"));
        rows.Add(SettingRowVM.Info("{=PAI_STATUS}Status", () => ManagementStatus(fief), "{=PAI_TOWN_MANAGEMENT_STATUS_HINT}Global enable is the master switch. A fief may either follow the global defaults or keep explicit local settings."));
        rows.Add(SettingRowVM.Info("{=PAI_GOVERNOR}Governor", () => GovernorStatus(settlement, fief), "{=PAI_TOWN_GOVERNOR_STATUS_HINT}Automatic mode assigns an eligible unassigned player-clan hero on the next daily town update. Recommend mode only reports the best candidate."));
        rows.Add(SettingRowVM.Info("{=PAI_ECONOMY}Economy", () => EconomyStatus(settlement, fief), "{=PAI_TOWN_ECONOMY_STATUS_HINT}Food or loyalty emergencies temporarily outrank profit. Economy strategy otherwise favors taxes, tariffs, prosperity, workshops, villages and lower garrison costs. Listed net income is an operational estimate, not the clan finance total."));

        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_SETTLEMENT_ENABLED}Enable Town AI for This Fief", "{=PAI_TOWN_SETTLEMENT_ENABLED_HINT}Allow Town AI to manage this settlement while global town management is enabled.",
            () => fief.Enabled, value => fief.Enabled = value));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_USE_GLOBAL_DEFAULTS}Follow Global Town Defaults", "{=PAI_TOWN_USE_GLOBAL_DEFAULTS_HINT}Keep this fief synchronized with global construction, governor and defense defaults. The fief enable switch remains independent.",
            () => fief.UseGlobalDefaults,
            value =>
            {
                fief.UseGlobalDefaults = value;
                if (value)
                {
                    fief.ApplyDefaults(PartyAi.Towns.Settings);
                    fief.Normalize();
                }
            }));

        rows.Add(SettingRowVM.Enum<TownStrategy>("{=PAI_TOWN_STRATEGY}Management Strategy", "{=PAI_TOWN_STRATEGY_HINT}Balanced covers food, security, prosperity and defense; Stability protects loyalty, security and food; Economy prioritizes prosperity and revenue from taxes, tariffs, workshops and villages; Military prioritizes walls, militia and garrisons. Loyalty and food emergencies override the selected focus.",
            TownText.Strategy, () => fief.Strategy, value => fief.Strategy = value, custom));

        rows.Add(SettingRowVM.Header("{=PAI_TOWN_CONSTRUCTION_HEADER}Construction"));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_BUILD_QUEUE}Manage Building Queue", "{=PAI_TOWN_BUILD_QUEUE_HINT}Choose and reorder construction projects according to the selected strategy and current emergencies.",
            () => fief.ManageBuildingQueue, value => fief.ManageBuildingQueue = value, custom));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_DAILY_PROJECTS}Manage Daily Projects", "{=PAI_TOWN_DAILY_PROJECTS_HINT}Select a continuous project when no building should be constructed or an emergency needs attention.",
            () => fief.ManageDailyProjects, value => fief.ManageDailyProjects = value, custom));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_AUTO_FUND}Fund Construction Automatically", "{=PAI_TOWN_AUTO_FUND_SETTLEMENT_HINT}Move player gold into this settlement's construction reserve within the global gold reserve.",
            () => fief.AutoFundConstruction, value => fief.AutoFundConstruction = value, custom));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_RESERVE_TARGET}Construction Reserve Target", "{=PAI_TOWN_SETTLEMENT_RESERVE_TARGET_HINT}Stop adding construction funds after this settlement's reserve reaches this amount.",
            0, 100000, () => fief.ConstructionReserveTarget, value => fief.ConstructionReserveTarget = value, null, () => custom() && fief.AutoFundConstruction));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_DAILY_DEPOSIT_LIMIT}Daily Funding Limit", "{=PAI_TOWN_DAILY_DEPOSIT_LIMIT_HINT}Maximum construction funds that Town AI may add to one settlement per day.",
            0, 20000, () => fief.DailyConstructionDepositLimit, value => fief.DailyConstructionDepositLimit = value, null, () => custom() && fief.AutoFundConstruction));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_LOYALTY_THRESHOLD}Loyalty Emergency Threshold", "{=PAI_TOWN_LOYALTY_THRESHOLD_HINT}Below this loyalty value, stability projects take precedence over the selected strategy.",
            0, 100, () => (int)fief.LoyaltyEmergencyThreshold, value => fief.LoyaltyEmergencyThreshold = value, null, custom));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_FOOD_SHORTAGE_DAYS}Food Emergency Days", "{=PAI_TOWN_FOOD_SHORTAGE_DAYS_HINT}Treat a projected food shortage within this many days as an emergency.",
            1, 30, () => fief.FoodShortageDays, value => fief.FoodShortageDays = value, Days, custom));

        rows.Add(SettingRowVM.Header("{=PAI_TOWN_GOVERNOR_HEADER}Governors"));
        rows.Add(SettingRowVM.Enum<GovernorMode>("{=PAI_TOWN_GOVERNOR_MODE}Governor Mode", "{=PAI_TOWN_GOVERNOR_MODE_HINT}Recommend suitable governors or assign them automatically. Automatic assignment only uses eligible player-clan heroes.",
            TownText.Governor, () => fief.GovernorMode,
            value =>
            {
                fief.GovernorMode = value;
                PartyAi.Towns.TryAssignMissingGovernor(settlement);
            }, custom));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_GOVERNOR_REASSIGN}Allow Governor Reassignment", "{=PAI_TOWN_GOVERNOR_REASSIGN_HINT}Allow automatic governor mode to replace an existing governor when a meaningfully better candidate is available.",
            () => fief.AllowGovernorReassignment, value => fief.AllowGovernorReassignment = value, custom));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_GOVERNOR_COOLDOWN}Governor Reassignment Cooldown", "{=PAI_TOWN_GOVERNOR_COOLDOWN_HINT}Minimum days before Town AI may replace a governor it assigned.",
            0, 365, () => fief.GovernorAssignmentCooldownDays, value => fief.GovernorAssignmentCooldownDays = value, Days, custom));

        rows.Add(SettingRowVM.Header("{=PAI_TOWN_DEFENSE_HEADER}Defense Dispatch"));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_SETTLEMENT_AUTO_DEFENSE}Defend This Fief Automatically", "{=PAI_TOWN_SETTLEMENT_AUTO_DEFENSE_HINT}Allow Town AI to assign suitable player-clan parties when this settlement is threatened.",
            () => fief.AutoDefenseEnabled, value => fief.AutoDefenseEnabled = value, custom));
        rows.Add(SettingRowVM.Enum<DefensePriority>("{=PAI_TOWN_DEFENSE_PRIORITY}Defense Priority", "{=PAI_TOWN_DEFENSE_PRIORITY_HINT}Higher-priority settlements receive available defenders before lower-priority settlements with similar threats.",
            TownText.Priority, () => fief.DefensePriority, value => fief.DefensePriority = value, custom));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_TARGET_DEFENSE_STRENGTH}Target Defense Strength", "{=PAI_TOWN_TARGET_DEFENSE_STRENGTH_HINT}Desired combined local defense strength. Zero lets Town AI calculate a target.",
            0, 5000, () => (int)fief.TargetDefenseStrength, value => fief.TargetDefenseStrength = value, null, custom));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_TARGET_GARRISON_TROOPS}Target Garrison Troops", "{=PAI_TOWN_TARGET_GARRISON_TROOPS_HINT}Desired garrison troop count for automatic reinforcement. Zero disables the fixed target.",
            0, 1000, () => fief.TargetGarrisonTroops, value => fief.TargetGarrisonTroops = value, null, custom));
    }

    private void AddGlobalTownRows(List<SettingRowVM> rows)
    {
        TownSettings town = PartyAi.Towns.Settings;
        Func<bool> enabled = () => town.Enabled;

        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_ENABLED}Enable Town Management", "{=PAI_TOWN_ENABLED_HINT}Allow Party AI to manage player-clan towns and castles using the settings below.",
            () => town.Enabled, value => town.Enabled = value));
        rows.Add(SettingRowVM.Info("{=PAI_NOTE}Note", () => L.S("{=PAI_GLOBAL_TOWN_NOTE}These values apply to every fief that follows global defaults. Fiefs with custom settings keep their own values.")));
        rows.Add(SettingRowVM.Action("{=PAI_TOWN_APPLY_DEFAULTS_EXISTING}Apply defaults to every fief", "{=PAI_TOWN_APPLY_DEFAULTS_EXISTING_HINT}Make all current player-clan towns and castles follow these defaults. Each fief's enabled/disabled state is preserved.",
            "{=PAI_APPLY}Apply", () =>
            {
                PartyAi.Towns.ApplyGlobalDefaultsToAllFiefs();
                Notify.Success(L.T("{=PAI_TOWN_DEFAULTS_APPLIED}All fiefs now follow the global town defaults."));
                _onChanged();
            }, enabled));

        rows.Add(SettingRowVM.Header("{=PAI_TOWN_CONSTRUCTION_HEADER}Construction"));
        rows.Add(SettingRowVM.Enum<TownStrategy>("{=PAI_TOWN_DEFAULT_STRATEGY}Default Strategy", "{=PAI_TOWN_DEFAULT_STRATEGY_HINT}Strategy used by new fiefs and existing fiefs that follow global defaults. Economy prioritizes prosperity and revenue from taxes, tariffs, workshops and villages.",
            TownText.Strategy, () => town.DefaultStrategy, value => town.DefaultStrategy = value, enabled));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_BUILD_QUEUE}Manage Building Queue", "{=PAI_TOWN_DEFAULT_BUILD_QUEUE_HINT}Enable building-queue management for fiefs that follow global defaults.",
            () => town.ManageBuildingQueue, value => town.ManageBuildingQueue = value, enabled));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_DAILY_PROJECTS}Manage Daily Projects", "{=PAI_TOWN_DEFAULT_DAILY_PROJECTS_HINT}Enable daily-project management for fiefs that follow global defaults.",
            () => town.ManageDailyProjects, value => town.ManageDailyProjects = value, enabled));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_AUTO_FUND}Fund Construction Automatically", "{=PAI_TOWN_DEFAULT_AUTO_FUND_HINT}Enable automatic construction funding for fiefs that follow global defaults. Funding never spends below the treasury gold reserve.",
            () => town.AutoFundConstruction, value => town.AutoFundConstruction = value, enabled));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_RESERVE_TARGET}Construction Reserve Target", "{=PAI_TOWN_DEFAULT_RESERVE_TARGET_HINT}Construction reserve target for fiefs that follow global defaults.",
            0, 100000, () => town.TownConstructionReserveTarget, value => town.TownConstructionReserveTarget = value, null, () => enabled() && town.AutoFundConstruction));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_DAILY_DEPOSIT_LIMIT}Daily Funding Limit", "{=PAI_TOWN_DEFAULT_DAILY_DEPOSIT_LIMIT_HINT}Daily construction funding limit for fiefs that follow global defaults.",
            0, 20000, () => town.DailyConstructionDepositLimit, value => town.DailyConstructionDepositLimit = value, null, () => enabled() && town.AutoFundConstruction));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_LOYALTY_THRESHOLD}Loyalty Emergency Threshold", "{=PAI_TOWN_DEFAULT_LOYALTY_THRESHOLD_HINT}Loyalty emergency threshold for fiefs that follow global defaults.",
            0, 100, () => (int)town.LoyaltyEmergencyThreshold, value => town.LoyaltyEmergencyThreshold = value, null, enabled));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_FOOD_SHORTAGE_DAYS}Food Emergency Days", "{=PAI_TOWN_DEFAULT_FOOD_SHORTAGE_DAYS_HINT}Food-emergency horizon for fiefs that follow global defaults.",
            1, 30, () => town.FoodShortageDays, value => town.FoodShortageDays = value, Days, enabled));

        rows.Add(SettingRowVM.Header("{=PAI_TOWN_GOVERNOR_HEADER}Governors"));
        rows.Add(SettingRowVM.Enum<GovernorMode>("{=PAI_TOWN_GOVERNOR_MODE}Governor Mode", "{=PAI_TOWN_DEFAULT_GOVERNOR_MODE_HINT}Governor mode used by new fiefs and existing fiefs that follow global defaults. Assign appoints eligible unassigned clan heroes automatically.",
            TownText.Governor, () => town.GovernorMode,
            value =>
            {
                town.GovernorMode = value;
                PartyAi.Towns.TryAssignMissingGovernorsFollowingGlobalDefaults();
            }, enabled));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_GOVERNOR_REASSIGN}Allow Governor Reassignment", "{=PAI_TOWN_DEFAULT_GOVERNOR_REASSIGN_HINT}Allow governor replacement for fiefs that follow global defaults. Empty governor slots use unassigned heroes first.",
            () => town.AllowGovernorReassignment, value => town.AllowGovernorReassignment = value, enabled));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_GOVERNOR_COOLDOWN}Governor Reassignment Cooldown", "{=PAI_TOWN_DEFAULT_GOVERNOR_COOLDOWN_HINT}Governor reassignment cooldown for fiefs that follow global defaults. Empty governor slots ignore reassignment cooldown.",
            0, 365, () => town.GovernorAssignmentCooldownDays, value => town.GovernorAssignmentCooldownDays = value, Days, enabled));

        rows.Add(SettingRowVM.Header("{=PAI_TOWN_DEFENSE_HEADER}Defense Dispatch"));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_AUTO_DEFENSE}Dispatch Defenders Automatically", "{=PAI_TOWN_AUTO_DEFENSE_HINT}Temporarily redirect suitable player-clan parties to threatened settlements and restore their previous orders afterwards.",
            () => town.AutoDefenseEnabled, value => town.AutoDefenseEnabled = value, enabled));
        Func<bool> defense = () => enabled() && town.AutoDefenseEnabled;
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_DEFAULT_DEFENSE}Defend fiefs by default", "{=PAI_TOWN_DEFAULT_DEFENSE_HINT}Use automatic defense for fiefs that follow global defaults.",
            () => town.DefaultTownDefenseEnabled, value => town.DefaultTownDefenseEnabled = value, defense));
        rows.Add(SettingRowVM.Enum<DefensePriority>("{=PAI_TOWN_DEFAULT_DEFENSE_PRIORITY}Default Defense Priority", "{=PAI_TOWN_DEFAULT_DEFENSE_PRIORITY_HINT}Defense priority used by new fiefs and existing fiefs that follow global defaults.",
            TownText.Priority, () => town.DefaultDefensePriority, value => town.DefaultDefensePriority = value, defense));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_DEFAULT_DEFENSE_STRENGTH}Default Target Defense Strength", "{=PAI_TOWN_DEFAULT_DEFENSE_STRENGTH_HINT}Desired combined defense strength for fiefs following global defaults. Zero calculates a target from fief type, prosperity and priority.",
            0, 5000, () => (int)town.DefaultTargetDefenseStrength, value => town.DefaultTargetDefenseStrength = value, null, defense));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_THREAT_RADIUS}Threat Radius", "{=PAI_TOWN_THREAT_RADIUS_HINT}Map radius used to count hostile parties near a settlement.",
            1, 500, () => (int)town.ThreatRadius, value => town.ThreatRadius = value, null, defense));
        rows.Add(SettingRowVM.Percent("{=PAI_TOWN_DISPATCH_THRESHOLD}Dispatch Threat Ratio", "{=PAI_TOWN_DISPATCH_THRESHOLD_HINT}Dispatch defenders when nearby hostile strength reaches this share of local defense strength.",
            1, 500, () => town.DispatchThreatThreshold, value => town.DispatchThreatThreshold = value, defense));
        rows.Add(SettingRowVM.Percent("{=PAI_TOWN_RELEASE_THRESHOLD}Release Threat Ratio", "{=PAI_TOWN_RELEASE_THRESHOLD_HINT}Restore defenders' previous orders after threat falls below this ratio. Keep it lower than the dispatch ratio.",
            0, 500, () => town.ReleaseThreatThreshold, value => town.ReleaseThreatThreshold = value, defense));
        rows.Add(SettingRowVM.Percent("{=PAI_TOWN_MIN_PARTY_RATIO}Minimum Party Strength", "{=PAI_TOWN_MIN_PARTY_RATIO_HINT}Only dispatch parties at or above this share of their party-size limit.",
            0, 100, () => town.MinimumPartyStrengthRatio, value => town.MinimumPartyStrengthRatio = value, defense));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_RESERVE_PARTIES}Reserve Mobile Parties", "{=PAI_TOWN_RESERVE_PARTIES_HINT}Number of eligible clan parties Town AI must leave unassigned as a mobile reserve.",
            0, 20, () => town.ReserveMobileParties, value => town.ReserveMobileParties = value, null, defense));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_MAX_DEFENDERS}Maximum Defenders per Fief", "{=PAI_TOWN_MAX_DEFENDERS_HINT}Maximum clan parties Town AI may assign to one settlement at the same time.",
            0, 20, () => town.MaxDefendingPartiesPerTown, value => town.MaxDefendingPartiesPerTown = value, null, defense));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_MIN_ASSIGNMENT_DAYS}Minimum Assignment Days", "{=PAI_TOWN_MIN_ASSIGNMENT_DAYS_HINT}Keep a defender assigned for at least this many days unless its order becomes invalid.",
            0, 365, () => town.MinimumGarrisonDays, value => town.MinimumGarrisonDays = value, Days, defense));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_REASSIGNMENT_COOLDOWN}Party Reassignment Cooldown", "{=PAI_TOWN_REASSIGNMENT_COOLDOWN_HINT}Wait this many days after release before automatically assigning the same party again.",
            0, 365, () => town.ReassignmentCooldownDays, value => town.ReassignmentCooldownDays = value, Days, defense));

        rows.Add(SettingRowVM.Header("{=PAI_TOWN_DONATION_HEADER}Garrison Reinforcement"));
        rows.Add(SettingRowVM.Toggle("{=PAI_TOWN_AUTO_DONATE}Donate Garrison Troops Automatically", "{=PAI_TOWN_AUTO_DONATE_HINT}Allow assigned defenders whose party options permit donations to transfer troops once on arrival while preserving their minimum force. Donations stop when the added garrison wages would push the daily balance below the treasury minimum.",
            () => town.AutoDonateTroops, value => town.AutoDonateTroops = value, defense));
        Func<bool> donate = () => defense() && town.AutoDonateTroops;
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_DEFAULT_GARRISON_TARGET}Default Target Garrison Troops", "{=PAI_TOWN_DEFAULT_GARRISON_TARGET_HINT}Target garrison troop count for fiefs following global defaults. Zero disables the fixed target.",
            0, 1000, () => town.DonationTargetTroops, value => town.DonationTargetTroops = value, null, donate));
        rows.Add(SettingRowVM.Percent("{=PAI_TOWN_MAX_DONATION_RATIO}Maximum Donation Share", "{=PAI_TOWN_MAX_DONATION_RATIO_HINT}Maximum share of an assigned party's troops that may be donated during one assignment.",
            0, 100, () => town.MaxDonationRatio, value => town.MaxDonationRatio = value, donate));
        rows.Add(SettingRowVM.Number("{=PAI_TOWN_MIN_REMAINING_TROOPS}Minimum Remaining Party Troops", "{=PAI_TOWN_MIN_REMAINING_TROOPS_HINT}Never reduce an assigned party below this troop count when donating to a garrison.",
            0, 1000, () => town.MinimumTroopsAfterDonation, value => town.MinimumTroopsAfterDonation = value, null, donate));
    }

    private static string Days(int value) => L.T("{=PAI_TOWN_DAYS}{DAYS} days", "DAYS", value).ToString();

    // ---- Town status lines -----------------------------------------------------------------------

    private static string ManagementStatus(FiefSettings fief)
    {
        if (!PartyAi.Towns.Settings.Enabled)
        {
            return L.S("{=PAI_TOWN_STATUS_GLOBAL_PAUSED}Paused: global town management is off.");
        }

        if (!fief.Enabled)
        {
            return L.S("{=PAI_TOWN_STATUS_LOCAL_PAUSED}Paused for this fief.");
        }

        return fief.UseGlobalDefaults
            ? L.S("{=PAI_TOWN_STATUS_GLOBAL_ACTIVE}Active: following global defaults.")
            : L.S("{=PAI_TOWN_STATUS_CUSTOM_ACTIVE}Active: using custom fief settings.");
    }

    private static string GovernorStatus(Settlement settlement, FiefSettings fief)
    {
        Town town = settlement.Town;
        if (town.Governor is Hero governor)
        {
            return L.T("{=PAI_TOWN_STATUS_GOVERNOR_ASSIGNED}Governor: {HERO}.", "HERO", governor.Name).ToString();
        }

        if (!PartyAi.Towns.Settings.Enabled || !fief.Enabled)
        {
            return L.S("{=PAI_TOWN_STATUS_NO_GOVERNOR_PAUSED}No governor; Town AI is paused.");
        }

        FiefSettings effective = fief.Resolve(PartyAi.Towns.Settings);
        if (effective.GovernorMode == GovernorMode.Off)
        {
            return L.S("{=PAI_TOWN_STATUS_NO_GOVERNOR_OFF}No governor; governor management is off.");
        }

        Hero? candidate = PartyAi.Towns.GovernorCandidate(town, fief);
        if (candidate is null)
        {
            return L.S("{=PAI_TOWN_STATUS_NO_GOVERNOR_CANDIDATE}No governor; no eligible unassigned clan hero is available.");
        }

        return effective.GovernorMode == GovernorMode.Recommend
            ? L.T("{=PAI_TOWN_STATUS_GOVERNOR_RECOMMENDED}No governor; recommended: {HERO}. Switch to Assign to appoint automatically.", "HERO", candidate.Name).ToString()
            : L.T("{=PAI_TOWN_STATUS_GOVERNOR_PENDING}No governor; {HERO} will be assigned on the next daily update.", "HERO", candidate.Name).ToString();
    }

    private static string EconomyStatus(Settlement settlement, FiefSettings fief)
    {
        Town town = settlement.Town;
        FiefSettings effective = fief.Resolve(PartyAi.Towns.Settings);

        if (TownManagementBehavior.IsFoodEmergency(town, effective.FoodShortageDays))
        {
            float days = TownManagementBehavior.FoodDaysRemaining(town);
            return L.T("{=PAI_TOWN_STATUS_FOOD_EMERGENCY}Food emergency: {FOOD} stock, {CHANGE}/day; estimated days remaining: {DAYS}. Stabilization has priority.")
                .SetTextVariable("FOOD", town.FoodStocks.ToString("0"))
                .SetTextVariable("CHANGE", Signed(town.FoodChange))
                .SetTextVariable("DAYS", float.IsPositiveInfinity(days) ? L.S("{=PAI_TOWN_FOOD_STABLE}infinite") : Math.Ceiling(days).ToString("0"))
                .ToString();
        }

        if (TownManagementBehavior.IsLoyaltyEmergency(town, effective))
        {
            return L.T("{=PAI_TOWN_STATUS_LOYALTY_EMERGENCY}Loyalty emergency: {LOYALTY}. Stability has priority over profit.", "LOYALTY", town.Loyalty.ToString("0")).ToString();
        }

        var finance = Campaign.Current.Models.ClanFinanceModel;
        int tariffs = (int)Math.Round(finance.CalculateTownIncomeFromTariffs(Clan.PlayerClan, town).ResultNumber);
        int projects = finance.CalculateTownIncomeFromProjects(town);
        int villages = settlement.BoundVillages.Sum(village => finance.CalculateVillageIncome(Clan.PlayerClan, village));
        int wage = town.GarrisonParty?.TotalWage ?? 0;

        return L.T("{=PAI_TOWN_STATUS_ECONOMY}Prosperity {PROSPERITY}; food change {FOOD}; tariffs {TARIFFS}, projects {PROJECTS}, villages {VILLAGES}, garrison wage {WAGE}; listed net {NET}.")
            .SetTextVariable("PROSPERITY", town.Prosperity.ToString("0"))
            .SetTextVariable("FOOD", Signed(town.FoodChange))
            .SetTextVariable("TARIFFS", tariffs)
            .SetTextVariable("PROJECTS", projects)
            .SetTextVariable("VILLAGES", villages)
            .SetTextVariable("WAGE", wage)
            .SetTextVariable("NET", tariffs + projects + villages - wage)
            .ToString();
    }

    private static string Signed(float value) => value > 0f ? "+" + value.ToString("0.0") : value.ToString("0.0");
}
