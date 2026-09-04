using Bannerlord.PartyAI.UI;
using HarmonyLib;
using HarmonyLib.PatchBuilder;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace Bannerlord.PartyAI.Patches;

/// <summary>
/// The game's screen manager only scans its own assemblies for [GameStateScreen] classes, so a
/// modded GameState needs help to find its screen. Also hardens RegisterListener against nulls.
/// </summary>
internal static class ControlPanelScreenPatches
{
    public static void Apply(Harmony harmony)
        => harmony.Patch()
            .Method<GameStateScreenManager>(x => x.CreateScreen(null))
                .Prefix(CreateScreenPrefix)
            .Method<ControlPanelState>(x => x.RegisterListener(null))
                .Prefix(RegisterListenerPrefix);

    private static bool CreateScreenPrefix(ref ScreenBase __result, GameState state)
    {
        if (state is not ControlPanelState panelState)
        {
            return true;
        }

        __result = new ControlPanelScreen(panelState);
        return false;
    }

    private static bool RegisterListenerPrefix(ref bool __result, IGameStateListener listener)
    {
        if (listener is null)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
