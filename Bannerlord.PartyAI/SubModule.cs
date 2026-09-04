using Bannerlord.PartyAI.Battle;
using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.GameModels;
using Bannerlord.PartyAI.Patches;
using Bannerlord.PartyAI.UI;
using Bannerlord.UIExtenderEx;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.PartyAI;

public sealed class SubModule : MBSubModuleBase
{
    private static readonly string HarmonyId = typeof(SubModule).Namespace!;

    private readonly Harmony _harmony = new(HarmonyId);
    private bool _lateInitDone;

    protected override void OnSubModuleLoad()
    {
        PatchSet.ApplyOnLoad(_harmony);

        UIExtender extender = UIExtender.Create(HarmonyId);
        extender.Register(typeof(SubModule).Assembly);
        extender.Enable();

        base.OnSubModuleLoad();
    }

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        if (!_lateInitDone)
        {
            PatchSet.ApplyAfterModulesLoaded(_harmony);
            _lateInitDone = true;
        }

        base.OnBeforeInitialModuleScreenSetAsRoot();
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarter)
    {
        if (game.GameType is not Campaign || gameStarter is not CampaignGameStarter starter)
        {
            return;
        }

        PartyAi.Bind(starter);

        AddModel<PartyTroopUpgradeModel, TroopUpgradeModel>(starter);
        AddModel<ArmyManagementCalculationModel, ArmyManagementModel>(starter);
        AddModel<PrisonerRecruitmentCalculationModel, PrisonerRecruitmentModel>(starter);
        AddModel<PartyFoodBuyingModel, FoodBuyingModel>(starter);
    }

    public override void OnGameEnd(Game game)
    {
        PartyAi.Unbind();
        base.OnGameEnd(game);
    }

    public override void OnGameInitializationFinished(Game game)
    {
        if (game.GameType is not Campaign)
        {
            return;
        }

        WarnIfModelOverridden(Campaign.Current.Models.PartyTroopUpgradeModel);
        WarnIfModelOverridden(Campaign.Current.Models.ArmyManagementCalculationModel);
        WarnIfModelOverridden(Campaign.Current.Models.PrisonerRecruitmentCalculationModel);
        WarnIfModelOverridden(Campaign.Current.Models.PartyFoodBuyingModel);

        Notify.Success(L.T("{=PAIEUwVpMPm}Thank you for using Party AI Controls! To access the configuration panel, press {KEYBIND}!",
            "KEYBIND", PartyAi.Settings.OpenControlPanel));
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        if (!GameNetwork.IsSessionActive)
        {
            ModSettings? settings = Game.Current?.GameType is Campaign ? PartyAi.Settings : null;
            mission.AddMissionBehavior(new BattleCommanderBehavior(settings));
            mission.AddMissionBehavior(new SiegeArtilleryAvoidanceBehavior(settings));
        }

        base.OnMissionBehaviorInitialize(mission);
    }

    protected override void OnApplicationTick(float dt)
    {
        PartyAi.TemplateImport.Tick();

        if (!PartyAi.IsActive)
        {
            return;
        }

        GameState? state = Game.Current?.GameStateManager?.ActiveState;
        if (state is not MapState || Mission.Current is not null)
        {
            return;
        }

        // The autopilot has to see menu frames too: it leaves settlements it entered on its own.
        PartyAi.Autopilot.ApplicationTick(dt);

        if (state.IsMenuState)
        {
            return;
        }

        if (PartyAi.Settings.OpenControlPanel.IsDown())
        {
            ControlPanelState.Open();
        }
        else if (PartyAi.Settings.SelectCommandedParties.IsDown())
        {
            PartyAi.DirectCommand.OpenPicker();
        }
    }

    private static void AddModel<TModel, TOverride>(CampaignGameStarter starter)
        where TModel : GameModel
        where TOverride : MBGameModel<TModel>, new()
        // The generic overload is required: the non-generic AddModel leaves BaseModel uninitialized.
        => starter.AddModel<TModel>(new TOverride());

    private void WarnIfModelOverridden(GameModel model)
    {
        var modelAssembly = model.GetType().Assembly;
        var ownAssembly = GetType().Assembly;
        if (modelAssembly == ownAssembly || model.GetType().BaseType?.IsAbstract == true)
        {
            return;
        }

        Notify.Error(L.T("{=I2LlBDKr}Game Model Error: Please move {THIS} below {OTHER} in your load order to ensure mod compatibility")
            .SetTextVariable("THIS", ownAssembly.GetName().Name)
            .SetTextVariable("OTHER", modelAssembly.GetName().Name));
    }
}
