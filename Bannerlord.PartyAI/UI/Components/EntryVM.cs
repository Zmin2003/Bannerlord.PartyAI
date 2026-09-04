using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Orders;
using Bannerlord.PartyAI.Parties.Recruitment;
using Bannerlord.PartyAI.Towns;
using SandBox.View.Map;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View;

namespace Bannerlord.PartyAI.UI.Components;

public enum EntryKind
{
    /// <summary>A set of default settings applied to newly managed parties/garrisons.</summary>
    Defaults,
    /// <summary>Global town-management settings.</summary>
    GlobalTown,
    PlayerParty,
    LordParty,
    Caravan,
    /// <summary>Player-owned town or castle (town management + garrison).</summary>
    Fief,
    /// <summary>Kingdom garrison the player rules but does not own.</summary>
    Garrison
}

/// <summary>One selectable row in the Parties or Fiefs list.</summary>
public sealed class EntryVM : ViewModel
{
    private readonly Action<EntryVM> _onSelect;
    private readonly Action<EntryVM, bool>? _onChecked;
    private bool _isSelected;
    private bool _isChecked;
    private bool _canCheck = true;
    private bool _isInspected;

    private EntryVM(EntryKind kind, PartyProfile profile, Action<EntryVM> onSelect, Action<EntryVM, bool>? onChecked)
    {
        Kind = kind;
        Profile = profile;
        _onSelect = onSelect;
        _onChecked = onChecked;
    }

    // ---- Factories -------------------------------------------------------------------------------

    public static EntryVM ForDefaults(PartyProfile profile, string name, string subtitle, Action<EntryVM> onSelect)
        => new EntryVM(EntryKind.Defaults, profile, onSelect, null)
        {
            Name = name,
            Subtitle = subtitle,
            _canCheck = false
        }.Refreshed();

    public static EntryVM ForGlobalTown(Action<EntryVM> onSelect)
        => new EntryVM(EntryKind.GlobalTown, new PartyProfile(), onSelect, null)
        {
            Name = L.S("{=PAI_TOWN_OPTIONS_TITLE}Town Management"),
            Subtitle = L.S("{=PAI_GLOBAL_DEFAULTS}Global defaults"),
            _canCheck = false
        }.Refreshed();

    public static EntryVM ForHero(Hero hero, Action<EntryVM> onSelect, Action<EntryVM, bool> onChecked)
    {
        EntryKind kind = hero == Hero.MainHero ? EntryKind.PlayerParty
            : PartyRegistry.IsLeadingCaravan(hero) ? EntryKind.Caravan
            : EntryKind.LordParty;

        return new EntryVM(kind, PartyAi.Parties.Profile(hero), onSelect, onChecked)
        {
            Hero = hero,
            Visual = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(hero.CharacterObject)),
            Banner = hero.ClanBanner is null ? null : new BannerImageIdentifierVM(hero.ClanBanner, true)
        }.Refreshed();
    }

    public static EntryVM ForSettlement(Settlement settlement, Action<EntryVM> onSelect, Action<EntryVM, bool> onChecked)
    {
        EntryKind kind = PartyAi.Towns.IsTownManageable(settlement) ? EntryKind.Fief : EntryKind.Garrison;
        return new EntryVM(kind, PartyAi.Parties.Profile(settlement), onSelect, onChecked)
        {
            Settlement = settlement,
            Banner = settlement.OwnerClan?.Banner is null ? null : new BannerImageIdentifierVM(settlement.OwnerClan.Banner, true)
        }.Refreshed();
    }

    private EntryVM Refreshed()
    {
        RefreshValues();
        return this;
    }

    // ---- Identity --------------------------------------------------------------------------------

    public EntryKind Kind { get; }
    public PartyProfile Profile { get; }
    public Hero? Hero { get; private init; }
    public Settlement? Settlement { get; private init; }
    public MobileParty? Party => Hero?.PartyBelongedTo ?? Settlement?.Town?.GarrisonParty;

    public bool IsParty => Kind is EntryKind.PlayerParty or EntryKind.LordParty or EntryKind.Caravan;
    public bool IsSettlement => Kind is EntryKind.Fief or EntryKind.Garrison;
    public bool IsDefaults => Kind is EntryKind.Defaults or EntryKind.GlobalTown;

    /// <summary>Entries whose settings can be pasted onto each other.</summary>
    public bool IsCompatibleWith(EntryVM other)
    {
        if (IsDefaults || other.IsDefaults)
        {
            return false;
        }

        if (Kind == EntryKind.Caravan || other.Kind == EntryKind.Caravan)
        {
            return Kind == other.Kind;
        }

        return IsParty == other.IsParty && IsSettlement == other.IsSettlement;
    }

    // ---- Bound state -----------------------------------------------------------------------------

    [DataSourceProperty] public string Name { get; private set; } = string.Empty;
    [DataSourceProperty] public string Subtitle { get; private set; } = string.Empty;
    [DataSourceProperty] public string Status { get; private set; } = string.Empty;
    [DataSourceProperty] public bool StatusNeedsAttention { get; private set; }
    [DataSourceProperty] public HintViewModel StatusHint { get; private set; } = new();
    [DataSourceProperty] public string Strength { get; private set; } = string.Empty;
    [DataSourceProperty] public ImageIdentifierVM? Visual { get; private init; }
    [DataSourceProperty] public ImageIdentifierVM? Banner { get; private init; }
    [DataSourceProperty] public bool ShowPortrait => Visual is not null;
    [DataSourceProperty] public bool ShowBanner => Banner is not null;
    [DataSourceProperty] public bool ShowWalls => IsSettlement;
    [DataSourceProperty] public int WallsLevel => Settlement?.Town?.GetWallLevel() ?? 1;
    [DataSourceProperty] public bool ShowDefaultsIcon => IsDefaults;
    [DataSourceProperty] public bool IsInArmy => Party?.Army is not null && Party.Army.LeaderParty != Party;
    [DataSourceProperty] public bool IsArmyLeader => Party?.Army?.LeaderParty == Party && Party is not null;
    [DataSourceProperty] public bool IsCaravan => Kind == EntryKind.Caravan;
    [DataSourceProperty] public bool CanShowOnMap => Settlement is not null || (Hero is not null && Hero != Hero.MainHero && (Hero.IsActive || Hero.IsPrisoner));
    [DataSourceProperty] public HintViewModel ShowOnMapHint => new(L.T("{=aGJYQOef}Show hero's location on map."));
    [DataSourceProperty] public HintViewModel InArmyHint => new(Party?.Army?.Name ?? TextObject.GetEmpty());
    [DataSourceProperty] public HintViewModel ArmyLeaderHint => new(L.T("{=PAI8qha4sZa}This hero is an army leader. Orders given to their party will apply to their entire army."));

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value != _isSelected)
            {
                _isSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsSelected));
            }
        }
    }

    [DataSourceProperty]
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (value != _isChecked)
            {
                _isChecked = value;
                OnPropertyChangedWithValue(value, nameof(IsChecked));
                _onChecked?.Invoke(this, value);
            }
        }
    }

    [DataSourceProperty]
    public bool CanCheck
    {
        get => _canCheck;
        set
        {
            if (value != _canCheck)
            {
                _canCheck = value;
                OnPropertyChangedWithValue(value, nameof(CanCheck));
            }
        }
    }

    /// <summary>Sets the checkbox without notifying the page (used when the page drives selection).</summary>
    internal void SetCheckedSilently(bool value)
    {
        if (value != _isChecked)
        {
            _isChecked = value;
            OnPropertyChangedWithValue(value, nameof(IsChecked));
        }
    }

    // ---- Commands --------------------------------------------------------------------------------

    public void ExecuteSelect() => _onSelect(this);

    public void ExecuteOpenEncyclopedia()
    {
        var encyclopedia = Campaign.Current.EncyclopediaManager;
        if (Hero is not null && encyclopedia.GetPageOf(typeof(Hero)).IsValidEncyclopediaItem(Hero))
        {
            encyclopedia.GoToLink(Hero.EncyclopediaLink);
        }
        else if (Settlement is not null && encyclopedia.GetPageOf(typeof(Settlement)).IsValidEncyclopediaItem(Settlement))
        {
            encyclopedia.GoToLink(Settlement.EncyclopediaLink);
        }
    }

    public void ExecuteShowOnMap()
    {
        CampaignVec2? position = Settlement?.Position ?? Hero?.GetCampaignPosition();
        if (position is null)
        {
            return;
        }

        Game.Current.GameStateManager.PopState();
        UISoundsHelper.PlayUISound("event:/ui/default");
        MapScreen.Instance.FastMoveCameraToPosition(position.Value);
    }

    public void ExecuteBeginStrengthHint()
    {
        if (Settlement is not null)
        {
            _isInspected = Settlement.IsInspected;
            Settlement.IsInspected = true;
            InformationManager.ShowTooltip(typeof(Settlement), Settlement, true);
        }
        else if (Party is not null)
        {
            _isInspected = Party.IsInspected;
            Party.IsInspected = true;
            InformationManager.ShowTooltip(typeof(MobileParty), Party, false, true);
        }
    }

    public void ExecuteEndStrengthHint()
    {
        InformationManager.HideTooltip();
        if (Settlement is not null)
        {
            Settlement.IsInspected = _isInspected;
        }
        else if (Party is not null)
        {
            Party.IsInspected = _isInspected;
        }
    }

    // ---- Refresh ---------------------------------------------------------------------------------

    public override void RefreshValues()
    {
        base.RefreshValues();

        switch (Kind)
        {
            case EntryKind.Defaults:
            case EntryKind.GlobalTown:
                Status = string.Empty;
                StatusNeedsAttention = false;
                Strength = string.Empty;
                break;

            case EntryKind.PlayerParty:
                Name = Hero!.Name.ToString();
                Subtitle = L.S("{=PAI_YOUR_PARTY}Your party");
                Status = PartyAi.Settings.MainPartyAutopilot
                    ? PartyAi.Autopilot.Status.ToString()
                    : AutomationText(Profile.SettlementAutomation);
                Strength = PartySize(Party);
                StatusHint = new HintViewModel(L.T("{=PAI_PLAYER_STATUS_HINT}Autopilot state, or the settlement automation level while the autopilot is off."));
                break;

            case EntryKind.Caravan:
                Name = Party?.Name.ToString() ?? Hero!.Name.ToString();
                Subtitle = L.T("{=PAI_CARAVAN_OF}Caravan of {HERO}", "HERO", Hero!.Name).ToString();
                Status = L.T("{=PAI_CARAVAN_GOLD}{GOLD} trade gold", "GOLD", Party?.PartyTradeGold ?? 0).ToString();
                Strength = PartySize(Party);
                StatusHint = new HintViewModel();
                break;

            case EntryKind.LordParty:
                Name = Hero!.Name.ToString();
                Subtitle = Hero.Clan?.Name.ToString() ?? string.Empty;
                Status = OrderText.Status(Profile.Order).ToString();
                StatusNeedsAttention = Party is null;
                Strength = Party is null ? L.S("{=PAI_NO_PARTY}No party") : PartySize(Party);
                StatusHint = new HintViewModel(Profile.OrderQueue.Count > 0
                    ? L.T("{=PAI_QUEUED_ORDERS}{COUNT} more order(s) queued.", "COUNT", Profile.OrderQueue.Count)
                    : TextObject.GetEmpty());
                break;

            case EntryKind.Fief:
            case EntryKind.Garrison:
                Name = Settlement!.Name.ToString();
                Subtitle = Settlement.OwnerClan?.Name.ToString() ?? string.Empty;
                Strength = Party is null ? L.S("{=PAI_TOWN_NO_GARRISON}No garrison") : PartySize(Party);
                RefreshSettlementStatus();
                break;
        }

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusNeedsAttention));
        OnPropertyChanged(nameof(StatusHint));
        OnPropertyChanged(nameof(Strength));
        OnPropertyChanged(nameof(IsInArmy));
        OnPropertyChanged(nameof(IsArmyLeader));
    }

    private void RefreshSettlementStatus()
    {
        Town? town = Settlement?.Town;
        if (town is null)
        {
            Status = string.Empty;
            StatusNeedsAttention = false;
            return;
        }

        if (Kind == EntryKind.Garrison)
        {
            Status = L.S("{=PAI_TOWN_GARRISON_ONLY}Garrison management only");
            StatusNeedsAttention = false;
            StatusHint = new HintViewModel(L.T("{=PAI_TOWN_GARRISON_ONLY_HINT}Only garrison troops are managed here because this fief does not belong to the player clan."));
            return;
        }

        FiefSettings fief = PartyAi.Towns.Effective(Settlement!);
        bool active = PartyAi.Towns.Settings.Enabled && fief.Enabled;
        string management = active ? TownText.Strategy(fief.Strategy) : L.S("{=PAI_TOWN_STATUS_DISABLED}Town AI off");
        string governor = town.Governor?.Name.ToString() ?? L.S("{=PAI_TOWN_NO_GOVERNOR}No governor");

        Status = L.T("{=PAI_TOWN_ROW_SUMMARY}{MANAGEMENT} | {GOVERNOR} | Prosperity {PROSPERITY}")
            .SetTextVariable("MANAGEMENT", management)
            .SetTextVariable("GOVERNOR", governor)
            .SetTextVariable("PROSPERITY", (int)town.Prosperity)
            .ToString();

        StatusNeedsAttention = town.Governor is null
            || town.Loyalty <= fief.LoyaltyEmergencyThreshold
            || TownManagementBehavior.IsFoodEmergency(town, fief.FoodShortageDays);

        string food = town.FoodChange < 0f
            ? L.T("{=PAI_TOWN_FOOD_FALLING}{FOOD} ({CHANGE}/day)")
                .SetTextVariable("FOOD", (int)town.FoodStocks)
                .SetTextVariable("CHANGE", town.FoodChange.ToString("0.0"))
                .ToString()
            : ((int)town.FoodStocks).ToString();

        StatusHint = new HintViewModel(L.T("{=PAI_TOWN_ROW_HINT}{SETTLEMENT}\nManagement: {MANAGEMENT}\nGovernor: {GOVERNOR}\nProsperity: {PROSPERITY}\nLoyalty: {LOYALTY}\nFood: {FOOD}\nGarrison wage: {WAGE}")
            .SetTextVariable("SETTLEMENT", Settlement!.Name)
            .SetTextVariable("MANAGEMENT", management)
            .SetTextVariable("GOVERNOR", governor)
            .SetTextVariable("PROSPERITY", (int)town.Prosperity)
            .SetTextVariable("LOYALTY", town.Loyalty.ToString("0.0"))
            .SetTextVariable("FOOD", food)
            .SetTextVariable("WAGE", town.GarrisonParty?.TotalWage ?? 0));
    }

    private static string PartySize(MobileParty? party)
        => party is null
            ? string.Empty
            : GameTexts.FindText("str_LEFT_over_RIGHT")
                .SetTextVariable("LEFT", party.MemberRoster.TotalManCount)
                .SetTextVariable("RIGHT", party.Party.PartySizeLimit)
                .ToString();

    public static string AutomationText(SettlementAutomationLevel level) => level switch
    {
        SettlementAutomationLevel.Off => L.S("{=PAI_AUTOMATION_OFF}Off"),
        SettlementAutomationLevel.Recruit => L.S("{=PAI_AUTOMATION_RECRUIT}Recruit"),
        SettlementAutomationLevel.RecruitAndUpgrade => L.S("{=PAI_AUTOMATION_UPGRADE}Recruit + Upgrade"),
        _ => L.S("{=PAI_AUTOMATION_FULL}Full Auto")
    };
}
