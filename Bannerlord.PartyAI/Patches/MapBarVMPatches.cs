using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Patches;

internal static class MapBarVMPatches
{
    private static bool _bannerKingsLoaded;

    public static void Apply(Harmony harmony, bool bannerKingsLoaded)
    {
        _bannerKingsLoaded = bannerKingsLoaded;

        harmony.Patch<MapBarVM>()
            .Method("UpdateCanGatherArmyAndReason")
                .Postfix(UpdateCanGatherArmyAndReasonPostfix);
    }

    private static void UpdateCanGatherArmyAndReasonPostfix(MapBarVM __instance)
    {
        if (_bannerKingsLoaded)
        {
            if (Clan.PlayerClan.Kingdom == null || Clan.PlayerClan.IsUnderMercenaryService)
            {
                __instance.GatherArmyHint = new(new("{=PAIoLtvzpKU}PartyAIControls: Feature to enable armies without being a Vassal is not compatible with BannerKings."));
            }
            return;
        }

        IFaction mapFaction = Hero.MainHero.MapFaction;

        if (mapFaction != null && !mapFaction.IsKingdomFaction)
        {
            __instance.CanGatherArmy = true;
            __instance.GatherArmyHint.HintText = new TextObject("");
        }
        else if (Clan.PlayerClan.IsUnderMercenaryService)
        {
            __instance.CanGatherArmy = true;
            __instance.GatherArmyHint.HintText = new TextObject("");
        }
    }
}