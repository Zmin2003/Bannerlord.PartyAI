using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Patches;

internal static class PartyVMPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch<PartyVM>()
            .Method(x => x.TitleLbl)
                .Postfix(TitleLblPostfix)
            .Method(x => x.ExecuteDone())
                .Postfix(ExecuteDonePostfix);
    }

    private static void TitleLblPostfix(ref string __result, PartyVM __instance)
    {
        if (__result != __instance.HeaderLbl)
        {
            __result = __instance.HeaderLbl;
        }
    }

    private static void ExecuteDonePostfix(PartyVM __instance)
    {
        try
        {
            // refreshes text on done button tooltip if it didn't auto update itself
            if (!__instance.PartyScreenLogic.IsDoneActive())
            {
                __instance.IsDoneDisabled = !__instance.PartyScreenLogic.IsDoneActive();
                __instance.DoneHint.HintText = new TextObject("{=!}" + __instance.PartyScreenLogic.DoneReasonString);
                __instance.OnPropertyChanged("DoneHint");
                __instance.OnPropertyChanged("IsDoneDisabled");
            }
        }
        catch
        {

        }
    }
}
