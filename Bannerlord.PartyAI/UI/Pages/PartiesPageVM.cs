using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.UI.Components;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Pages;

/// <summary>The player's party, clan parties, caravans, managed kingdom parties and their defaults.</summary>
public sealed class PartiesPageVM : ListPageVM
{
    private enum SortMode
    {
        Clan,
        Name,
        Type,
        Troops,
        Army
    }

    private static SortMode _sort = SortMode.Clan;
    private static bool _showAllHeroes;

    public PartiesPageVM() : base("{=PAI_TAB_PARTIES}Parties")
    {
        SortSelector = new SelectorVM<SelectorItemVM>((int)_sort, selector =>
        {
            _sort = (SortMode)selector.SelectedIndex;
            Rebuild();
        });
        SortSelector.AddItem(new SelectorItemVM(L.T("{=str_clan}Clan")));
        SortSelector.AddItem(new SelectorItemVM(L.T("{=str_sort_by_name_label}Name")));
        SortSelector.AddItem(new SelectorItemVM(L.T("{=zMMqgxb1}Type")));
        SortSelector.AddItem(new SelectorItemVM(L.T("{=5k4dxUEJ}Troops")));
        SortSelector.AddItem(new SelectorItemVM(L.T("{=j12VrGKz}Army")));
        SortSelector.SelectedIndex = (int)_sort;
    }

    public override string EmptyText => L.S("{=PAI_UI_EMPTY_PARTIES}No manageable parties. Create a clan party or enable caravan management in Settings.");

    [DataSourceProperty] public SelectorVM<SelectorItemVM> SortSelector { get; }
    [DataSourceProperty] public string SortText => L.S("{=PAIuPlFS64X}Sort");
    [DataSourceProperty] public string ShowAllHeroesText => L.S("{=PAIlKT8heH9}Show All Heroes");
    [DataSourceProperty] public HintViewModel ShowAllHeroesHint => new(L.T("{=PAIqJ0819Nl}Show all heroes that can lead parties. Useful for assigning settings for any potential hero that might be a leader."));
    [DataSourceProperty] public string CreatePartyText => L.Game("str_clan_create_new_party");
    [DataSourceProperty] public bool CanCreateParty => ClanAutomationBehavior.CanCreateNewParty();
    [DataSourceProperty] public HintViewModel CreatePartyHint => new(ClanAutomationBehavior.CreateNewPartyHint());

    [DataSourceProperty]
    public bool ShowAllHeroes
    {
        get => _showAllHeroes;
        set
        {
            if (value != _showAllHeroes)
            {
                _showAllHeroes = value;
                OnPropertyChangedWithValue(value, nameof(ShowAllHeroes));
                Rebuild();
            }
        }
    }

    public void ExecuteCreateParty()
    {
        new ClanPartiesVM(() => { }, hero => PartyScreenHelper.OpenScreenAsCreateClanPartyForHero(hero), Rebuild, _ => { })
            .ExecuteCreateNewParty();
    }

    protected override IEnumerable<EntryVM> BuildEntries()
    {
        PartyRegistry registry = PartyAi.Parties;

        yield return EntryForDefaults(registry.DefaultClanParty, "{=PAIOMxOAsTY}Clan Parties", "{=PAI_DEFAULTS_SUBTITLE}Defaults for new entries");
        if (registry.Settings.ManageCaravans)
        {
            yield return EntryForDefaults(registry.DefaultClanCaravan, "{=PAId8ZsX3ID}Clan Caravans", "{=PAI_DEFAULTS_SUBTITLE}Defaults for new entries");
        }

        if (registry.Settings.ManageKingdomParties)
        {
            yield return EntryForDefaults(registry.DefaultKingdomParty, "{=PAIObdiWWBa}Kingdom Parties", "{=PAI_DEFAULTS_SUBTITLE}Defaults for new entries");
        }

        if (Hero.MainHero.PartyBelongedTo is not null && Hero.MainHero.IsPartyLeader)
        {
            yield return EntryForHero(Hero.MainHero);
        }

        IEnumerable<Hero> heroes = Hero.AllAliveHeroes
            .Where(hero => hero != Hero.MainHero
                && hero.CanLeadParty()
                && registry.IsManageable(hero)
                && (_showAllHeroes || (hero.PartyBelongedTo is not null && hero.IsPartyLeader)));

        foreach (EntryVM entry in Sort(heroes.Select(EntryForHero).ToList()))
        {
            yield return entry;
        }
    }

    private static IEnumerable<EntryVM> Sort(List<EntryVM> entries) => _sort switch
    {
        SortMode.Name => entries.OrderBy(entry => entry.Name),
        SortMode.Type => entries.OrderBy(entry => entry.Kind).ThenBy(entry => entry.Name),
        SortMode.Troops => entries.OrderByDescending(entry => entry.Party?.MemberRoster.TotalManCount ?? -1),
        SortMode.Army => entries.OrderByDescending(entry => entry.Party?.Army is not null)
            .ThenBy(entry => entry.Party?.Army?.Name?.ToString() ?? string.Empty)
            .ThenByDescending(entry => entry.IsArmyLeader),
        _ => entries.OrderByDescending(entry => entry.Hero?.Clan == Clan.PlayerClan)
            .ThenByDescending(entry => entry.Hero?.Clan?.Tier ?? 0)
            .ThenByDescending(entry => entry.Hero?.IsClanLeader ?? false)
            .ThenBy(entry => entry.Name)
    };
}
