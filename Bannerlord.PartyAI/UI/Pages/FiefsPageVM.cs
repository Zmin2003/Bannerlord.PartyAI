using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.UI.Components;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.UI.Pages;

/// <summary>Global town settings, garrison defaults, the player's fiefs and managed kingdom garrisons.</summary>
public sealed class FiefsPageVM : ListPageVM
{
    public FiefsPageVM() : base("{=PAI_TAB_FIEFS}Fiefs")
    {
    }

    public override string EmptyText => L.S("{=PAI_UI_EMPTY_FIEFS}No fiefs to manage yet. Acquire a town or castle, or enable garrison management in Settings.");

    protected override IEnumerable<EntryVM> BuildEntries()
    {
        PartyRegistry registry = PartyAi.Parties;

        yield return EntryForGlobalTown();

        if (registry.Settings.ManageClanGarrisons)
        {
            yield return EntryForDefaults(registry.DefaultClanGarrison, "{=PAIKf5y8Z4K}Clan Garrisons", "{=PAI_DEFAULTS_SUBTITLE}Defaults for new entries");
        }

        if (registry.Settings.ManageKingdomGarrisons)
        {
            yield return EntryForDefaults(registry.DefaultKingdomGarrison, "{=PAIJkUlgNUw}Kingdom Garrisons", "{=PAI_DEFAULTS_SUBTITLE}Defaults for new entries");
        }

        IEnumerable<Settlement> fiefs = Settlement.All
            .Where(settlement => PartyAi.Towns.IsTownManageable(settlement)
                || (registry.IsGarrisonManageable(settlement) && settlement.Town?.GarrisonParty is not null))
            .OrderByDescending(settlement => settlement.OwnerClan == TaleWorlds.CampaignSystem.Clan.PlayerClan)
            .ThenByDescending(settlement => settlement.IsTown)
            .ThenBy(settlement => settlement.Name.ToString());

        foreach (Settlement settlement in fiefs)
        {
            yield return EntryForSettlement(settlement);
        }
    }
}
