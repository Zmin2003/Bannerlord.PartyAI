using HarmonyLib;

namespace Bannerlord.PartyAI.Patches;

/// <summary>Applies every Harmony patch of the mod in one place.</summary>
internal static class PatchSet
{
    /// <summary>Patches that are safe to apply as soon as the module loads.</summary>
    public static void ApplyOnLoad(Harmony harmony)
    {
        PartyAiSaveCompatibilityPatches.Apply(harmony);
        ControlPanelScreenPatches.Apply(harmony);

        AiMilitaryBehaviorPatches.Apply(harmony);
        AiVisitSettlementBehaviorPatches.Apply(harmony);
        RecruitmentCampaignBehaviorPatches.Apply(harmony);
        GarrisonTroopsPatches.Apply(harmony);
        CaravansCampaignBehaviorPatches.Apply(harmony);
        PartiesBuyHorsePatches.Apply(harmony);
        TakePrisonerActionPatches.Apply(harmony);
        DisbandArmyActionPatches.Apply(harmony);
        MobilePartyAiPatches.Apply(harmony);
        MobilePartyPatches.Apply(harmony);

        ArmyPatches.Apply(harmony);
        InventoryLogicPatches.Apply(harmony);
        PartyVMPatches.Apply(harmony);
    }

    /// <summary>Patches that depend on which other mods are loaded; applied once all modules are up.</summary>
    public static void ApplyAfterModulesLoaded(Harmony harmony)
    {
        bool bannerKingsLoaded = AccessTools.TypeByName("BannerKings.Main") is not null;
        MapBarVMPatches.Apply(harmony, bannerKingsLoaded);
        if (!bannerKingsLoaded)
        {
            ArmyManagementVMPatches.Apply(harmony);
        }
    }
}
