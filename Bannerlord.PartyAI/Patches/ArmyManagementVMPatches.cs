using HarmonyLib;
using HarmonyLib.PatchBuilder;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;
using TaleWorlds.Core;

namespace Bannerlord.PartyAI.Patches;

internal static class ArmyManagementVMPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch<ArmyManagementVM>()
            .Method(x => x.RefreshValues())
                .Postfix(RefreshValuesPostfix)
            .Method(x => x.ExecuteDone())
                .Postfix(ExecuteDonePostfix);
    }

    private static void RefreshValuesPostfix(ArmyManagementVM __instance)
    {
        if (Clan.PlayerClan is null
            || !Clan.PlayerClan.IsUnderMercenaryService
            || __instance.PartyList is null)
        {
            return;
        }

        List<ArmyManagementItemVM> parties = __instance.PartyList.ToList();
        __instance.PartyList.Clear();

        foreach (ArmyManagementItemVM party in parties)
        {
            if (party.Clan == Clan.PlayerClan)
            {
                __instance.PartyList.Add(party);
            }
        }

        __instance.OnPropertyChanged("PartyList");
    }

    private static void ExecuteDonePostfix(ArmyManagementVM __instance)
    {
        if (__instance.PartiesInCart.Count > 1)
        {
            // If player is not already in an army, create one
            if (MobileParty.MainParty.Army == null)
            {
                Hero armyLeader = Hero.MainHero;

                if (armyLeader?.PartyBelongedTo?.LeaderHero != null)
                {
                    // Kingdom can be null – independent/merc armies are fine
                    Army army = new Army(null, armyLeader.PartyBelongedTo, Army.ArmyTypes.Patrolling);

                    // Use home settlement if available, otherwise current settlement (or null is also acceptable)
                    Settlement gatherTarget =
                        Hero.MainHero.HomeSettlement ??
                        MobileParty.MainParty.CurrentSettlement;

                    army.Gather(gatherTarget);
                    CampaignEventDispatcher.Instance.OnArmyCreated(army);
                }

                if (armyLeader == Hero.MainHero)
                {
                    (Game.Current.GameStateManager.GameStates
                        .Single(S => S is MapState) as MapState)
                        ?.OnArmyCreated(MobileParty.MainParty);
                }
            }

            // Attach all selected parties to the newly created (or existing) army
            foreach (ArmyManagementItemVM item in __instance.PartiesInCart)
            {
                if (item.Party != MobileParty.MainParty)
                {
                    item.Party.Army = MobileParty.MainParty.Army;

                    SetPartyAiAction.GetActionForEscortingParty(
                        item.Party,
                        MobileParty.MainParty,
                        item.Party.DesiredAiNavigationType,
                        false,  // isFromPort
                        false   // isTargetingPort
                    );
                }
            }
        }
    }
}
