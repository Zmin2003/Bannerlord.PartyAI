using Bannerlord.PartyAI.ViewModels;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Bannerlord.PartyAI;

public class PartyAIControlsMenuState : GameState
{
    public PartyAIControlsMenuState()
        : base()
    {
    }
}

[GameStateScreen(typeof(PartyAIControlsMenuState))]
public class PartyAIControlsMenuScreen : ScreenBase, IGameStateListener
{
    private GauntletLayer? _gauntletLayer;
    private readonly PartyAIControlsMenuState _partyAIControlsMenuState;
    private PartyAIControlsMenuVM? _dataSource;
    private readonly List<SpriteCategory> _spriteCategories = new();

    public PartyAIControlsMenuScreen(PartyAIControlsMenuState partyAIControlsMenuState)
    {
        SpriteData spriteData = UIResourceManager.SpriteData;
        TwoDimensionEngineResourceContext resourceContext = UIResourceManager.ResourceContext;
        ResourceDepot uIResourceDepot = UIResourceManager.ResourceDepot;

        foreach (string cat in new string[] { "ui_clan", "ui_kingdom", "ui_mplobby", "ui_characterdeveloper", "ui_partyscreen" })
        {
            SpriteCategory category = spriteData.SpriteCategories[cat];
            category.Load(resourceContext, uIResourceDepot);
            _spriteCategories.Add(category);
        }

        _partyAIControlsMenuState = partyAIControlsMenuState;
        _partyAIControlsMenuState.RegisterListener(this);
    }

    void IGameStateListener.OnActivate()
    {
        _gauntletLayer = new GauntletLayer("GauntletLayer", 1, true);
        AddLayer(_gauntletLayer);
        _dataSource = new PartyAIControlsMenuVM();
        _gauntletLayer.LoadMovie("PartyAIControlsMenu", _dataSource);
        _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
        _gauntletLayer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(_gauntletLayer);
    }

    void IGameStateListener.OnDeactivate()
    {
        if (_gauntletLayer is not null)
        {
            _gauntletLayer.InputRestrictions.ResetInputRestrictions();
            _gauntletLayer.IsFocusLayer = false;
            RemoveLayer(_gauntletLayer);
            _gauntletLayer = null;
        }
        _dataSource = null;
    }

    protected override void OnFrameTick(float dt)
    {
        base.OnFrameTick(dt);
        if (_gauntletLayer is not GauntletLayer layer
            || _dataSource is not PartyAIControlsMenuVM dataSource)
        {
            return;
        }

        if (layer.Input.IsKeyReleased(TaleWorlds.InputSystem.InputKey.Escape))
        {
            dataSource.ExecuteDone();
        }

        if (layer.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftControl)
            && layer.Input.IsKeyReleased(TaleWorlds.InputSystem.InputKey.C))
        {
            dataSource.Copy();
        }
        if (layer.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftControl)
            && layer.Input.IsKeyReleased(TaleWorlds.InputSystem.InputKey.V))
        {
            dataSource.Paste();
        }
    }

    void IGameStateListener.OnInitialize()
    {
    }

    void IGameStateListener.OnFinalize()
    {
        foreach (SpriteCategory category in _spriteCategories)
        {
            category.Unload();
        }
        _spriteCategories.Clear();
    }
}
