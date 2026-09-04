using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Bannerlord.PartyAI.UI;

/// <summary>Game state for the full-screen control panel.</summary>
public sealed class ControlPanelState : GameState
{
    /// <summary>Opens the panel if the player is on the campaign map.</summary>
    public static bool Open()
    {
        GameState? active = Game.Current?.GameStateManager?.ActiveState;
        if (active is not MapState || active.IsMenuState)
        {
            return false;
        }

        GameStateManager.Current.PushState(GameStateManager.Current.CreateState<ControlPanelState>());
        return true;
    }

    /// <summary>Console fallback: <c>partyai.open</c>.</summary>
    [CommandLineFunctionality.CommandLineArgumentFunction("open", "partyai")]
    public static string OpenFromConsole(List<string> args)
    {
        if (Campaign.Current is null)
        {
            return "No campaign found, are you in the right game mode?";
        }

        return Open() ? "Success" : "You must be on the map screen to use this command.";
    }
}

[GameStateScreen(typeof(ControlPanelState))]
public sealed class ControlPanelScreen : ScreenBase, IGameStateListener
{
    private static readonly string[] SpriteCategories =
    [
        "ui_clan", "ui_kingdom", "ui_mplobby", "ui_characterdeveloper", "ui_partyscreen", "ui_inventory"
    ];

    private readonly List<SpriteCategory> _loadedSprites = new();
    private GauntletLayer? _layer;
    private ControlPanelVM? _dataSource;

    public ControlPanelScreen(ControlPanelState state)
    {
        SpriteData spriteData = UIResourceManager.SpriteData;
        foreach (string name in SpriteCategories)
        {
            SpriteCategory category = spriteData.SpriteCategories[name];
            category.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
            _loadedSprites.Add(category);
        }

        state.RegisterListener(this);
    }

    void IGameStateListener.OnActivate()
    {
        _dataSource = new ControlPanelVM(Close);
        _layer = new GauntletLayer("PartyAiControlPanel", 1, true);
        _layer.LoadMovie("PartyAiControlPanel", _dataSource);
        _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
        _layer.IsFocusLayer = true;
        AddLayer(_layer);
        ScreenManager.TrySetFocus(_layer);
    }

    void IGameStateListener.OnDeactivate()
    {
        if (_layer is not null)
        {
            _layer.InputRestrictions.ResetInputRestrictions();
            _layer.IsFocusLayer = false;
            RemoveLayer(_layer);
            _layer = null;
        }

        _dataSource?.OnFinalize();
        _dataSource = null;
    }

    void IGameStateListener.OnInitialize()
    {
    }

    void IGameStateListener.OnFinalize()
    {
        foreach (SpriteCategory category in _loadedSprites)
        {
            category.Unload();
        }

        _loadedSprites.Clear();
    }

    protected override void OnFrameTick(float dt)
    {
        base.OnFrameTick(dt);
        if (_layer is null || _dataSource is null)
        {
            return;
        }

        if (_layer.Input.IsKeyReleased(InputKey.Escape))
        {
            Close();
        }
        else if (_layer.Input.IsKeyDown(InputKey.LeftControl) && _layer.Input.IsKeyReleased(InputKey.C))
        {
            _dataSource.CopySelected();
        }
        else if (_layer.Input.IsKeyDown(InputKey.LeftControl) && _layer.Input.IsKeyReleased(InputKey.V))
        {
            _dataSource.PasteToSelected();
        }
    }

    private static void Close() => GameStateManager.Current.PopState();
}
