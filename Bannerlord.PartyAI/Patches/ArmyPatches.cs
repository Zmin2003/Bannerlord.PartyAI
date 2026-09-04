using HarmonyLib;
using HarmonyLib.PatchBuilder;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace Bannerlord.PartyAI.Patches;

internal static class ArmyPatches
{
    private static FieldInfo HourlyTickEvent = default!;
    private static MethodInfo AddEventHandlers = default!;

    public static void Apply(Harmony harmony)
    {
        HourlyTickEvent = AccessTools.Field(typeof(Army), "_hourlyTickEvent");
        AddEventHandlers = AccessTools.Method(typeof(Army), "AddEventHandlers");

        harmony.Patch<Army>()
            .Method("DisperseInternal")
                .Prefix(DisperseInternalPrefix);
    }

    private static void DisperseInternalPrefix(Army __instance)
    {
        // this patches an issue where Army._hourlyTickEvent will be null for some reason
        // and cause a crash on army dispersion
        if (HourlyTickEvent.GetValue(__instance) == null)
        {
            AddEventHandlers.Invoke(__instance, new object[] { });
        }
    }
}
