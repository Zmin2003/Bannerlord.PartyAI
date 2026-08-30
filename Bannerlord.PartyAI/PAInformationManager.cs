using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels;
using Bannerlord.PartyAI.ViewModels.MenuOptionVMs;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Bannerlord.PartyAI;

internal class PAInformationManager : GlobalLayer
{
    private class PAInfoLayer : GlobalLayer
    {
        private readonly GauntletLayer _layer;
        private readonly List<SpriteCategory> _spriteCategories = new();
        private GauntletMovieIdentifier? _movie;


        internal PAInfoLayer(int order)
        {
            _layer = new GauntletLayer("PAInfoLayer", order, false);
            _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
            Layer = _layer;
            ScreenManager.AddGlobalLayer(this, isFocusable: true);
            ScreenManager.SetSuspendLayer(Layer, isSuspended: true);
        }

        internal GauntletMovieIdentifier LoadMovie(string movie, ViewModel datasource)
    => _movie = _layer.LoadMovie(movie, datasource);

        internal void LoadSpriteCategories(string[] categories)
        {
            foreach (string cat in categories)
            {
                SpriteCategory category = UIResourceManager.SpriteData.SpriteCategories[cat];
                category.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
                _spriteCategories.Add(category);
            }
        }

        internal void UnloadSpriteCategories()
        {
            foreach (SpriteCategory category in _spriteCategories)
            {
                category.Unload();
            }
            _spriteCategories.Clear();
        }
        internal void RegisterState()
        {
            InformationManager.HideTooltip();
            SetLayerFocus(isFocused: true);

            GameStateManager.Current.RegisterActiveStateDisableRequest(this);
            MBCommon.PauseGameEngine();
        }

        internal void CloseQuery()
        {
            SetLayerFocus(isFocused: false);
            if (_movie != null)
            {
                _layer.ReleaseMovie(_movie);
                _movie = null;
            }

            if (_movie == null)
            {
                UnloadSpriteCategories();
                GameStateManager.Current.UnregisterActiveStateDisableRequest(this);
                MBCommon.UnPauseGameEngine();
            }
            UnloadSpriteCategories();
        }

        private void SetLayerFocus(bool isFocused)
        {
            if (isFocused)
            {
                ScreenManager.SetSuspendLayer(Layer, isSuspended: false);
                Layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(Layer);
                Layer.InputRestrictions.SetInputRestrictions();
            }
            else
            {
                Layer.InputRestrictions.ResetInputRestrictions();
                ScreenManager.SetSuspendLayer(Layer, isSuspended: true);
                Layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(Layer);
            }
        }

        protected override void OnEarlyTick(float dt)
        {
            base.OnEarlyTick(dt);
            if (_movie != null)
            {
                if (ScreenManager.FocusedLayer != Layer)
                {
                    SetLayerFocus(isFocused: true);
                }
                if (_layer.Input.IsHotKeyReleased("Confirm"))
                {
                    UISoundsHelper.PlayUISound("event:/ui/panels/next");
                    CloseQuery();
                }
                else if (_layer.Input.IsHotKeyReleased("Exit"))
                {
                    UISoundsHelper.PlayUISound("event:/ui/panels/next");
                    CloseQuery();
                }
            }
        }
    }

    private readonly PAInfoLayer _layer1;
    private readonly PAInfoLayer _layer2;
    private readonly PAInfoLayer _layer3;

    public PAInformationManager()
    {
        _layer1 = new(4497);
        _layer2 = new(4498);
        _layer3 = new(4499);
    }

    public void ShowPartyCompositionInquiry(PartyAiEntitySettings settings, Action<PartyComposition> callback)
    {
        if (!CheckContext()) { return; }
        _layer3.LoadMovie("PartyAICompositionSliders", new PartyAICompositionSlidersVM(settings, (PartyComposition comp) =>
        {
            _layer3.CloseQuery();
            callback?.Invoke(comp);
        }));
        _layer3.RegisterState();
    }

    public void ShowNumberPickerInquiry(int initialValue, int minValue, int maxValue, string title, string description, Action<int> callback, bool isPercentage = true)
    {
        if (!CheckContext()) { return; }
        _layer3.LoadMovie("PartyAINumberPicker", new PartyAINumberPickerVM(initialValue, minValue, maxValue, title, description, (int value) =>
        {
            _layer3.CloseQuery();
            callback?.Invoke(value);
        }, isPercentage));
        _layer3.RegisterState();
    }

    public void ShowModOptionsInquiry(Action callback)
    {
        if (!CheckContext()) { return; }
        _layer1.LoadMovie("PartyAIModOptions", new PartyAIModOptionsVM(() =>
        {
            _layer1.CloseQuery();
            callback?.Invoke();
        }));
        _layer1.RegisterState();
    }

    public void ShowTownManagementOptionsInquiry(Action callback)
    {
        if (!CheckContext()) { return; }
        _layer1.LoadMovie("PartyAITownManagementOptions", new TownManagementOptionsVM(() =>
        {
            _layer1.CloseQuery();
            callback?.Invoke();
        }));
        _layer1.RegisterState();
    }

    public void ShowPartyOptionsInquiry(PartyAiEntitySettings settings, Action callback)
    {
        if (!CheckContext()) { return; }
        _layer2.LoadMovie("PartyAIPartyOptions", new PartyAIPartyOptionsVM(settings, () =>
        {
            _layer2.CloseQuery();
            callback?.Invoke();
        }));
        _layer2.RegisterState();
    }

    public void ShowCaravanOptionsInquiry(PartyAiEntitySettings settings, Action callback)
    {
        if (!CheckContext()) { return; }
        _layer2.LoadMovie("PartyAICaravanOptions", new PartyAICaravanOptionsVM(settings, () =>
        {
            _layer2.CloseQuery();
            callback?.Invoke();
        }));
        _layer2.RegisterState();
    }

    public void ShowGarrisonOptionsInquiry(PartyAiEntitySettings settings, Action callback)
    {
        if (!CheckContext()) { return; }
        _layer2.LoadMovie("PartyAIGarrisonOptions", new PartyAIGarrisonOptionsVM(settings, () =>
        {
            _layer2.CloseQuery();
            callback?.Invoke();
        }));
        _layer2.RegisterState();
    }

    public void ShowTownOptionsInquiry(
        Settlement settlement,
        PartyAiEntitySettings garrisonSettings,
        Action callback)
    {
        if (!CheckContext()) { return; }
        TownManagementSettlementSettings townSettings = SubModule.TownManagementBehavior.SettingsSnapshot(settlement);
        _layer2.LoadMovie("PartyAITownOptions", new PartyAITownOptionsVM(settlement, garrisonSettings, townSettings, () =>
        {
            _layer2.CloseQuery();
            callback?.Invoke();
        }));
        _layer2.RegisterState();
    }

    public void ShowOrderQueueInquiry(PartyAiEntitySettings settings, Action callback)
    {
        if (!CheckContext()) { return; }
        _layer1.LoadSpriteCategories(new[] { "ui_partyscreen" });
        _layer1.LoadMovie("PartyAIOrderQueue", new PartyAIOrderQueueVM(settings, () =>
        {
            _layer1.CloseQuery();
            callback?.Invoke();
            _layer1.UnloadSpriteCategories();
        }));
        _layer1.RegisterState();
    }

    public void ShowDefaultSettingsInquiry()
    {
        if (!CheckContext()) { return; }
        _layer1.LoadMovie("PartyAIDefaultSettings", new PartyAIDefaultSettingsVM(() =>
        {
            _layer1.CloseQuery();
        }));
        _layer1.RegisterState();
    }

    private bool CheckContext()
    {
        if (Campaign.Current == null)
        {
            InformationManager.ShowInquiry(new InquiryData(
              new TextObject("{=oZrVNUOk}Error").ToString(),
              new TextObject("{=PAI0m3MyqBm}You must load a save game to use this menu.").ToString(),
              true, false, new TextObject("{=Y94H6XnK}Accept").ToString(), string.Empty,
              () => { }, null)
            );
            return false;
        }
        if (Mission.Current != null) { return false; }
        return true;
    }
}
