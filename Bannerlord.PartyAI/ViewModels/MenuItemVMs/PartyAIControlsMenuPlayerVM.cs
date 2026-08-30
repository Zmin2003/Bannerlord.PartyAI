using Bannerlord.PartyAI.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.MenuItemVMs;

public sealed class PartyAIControlsMenuPlayerVM : PartyAIControlsMenuPartyVM
{
    public PartyAIControlsMenuPlayerVM(Hero leader, PartyAIControlsMenuVM menu)
        : base(leader, menu)
    {
    }

    public override bool CanEditOrders => false;
    public override bool CanShowLocationOfHero => false;

    public override string ActiveOrder
    {
        get
        {
            SettlementAutomationLevel level = Settings.SettlementAutomation;
            return level switch
            {
                SettlementAutomationLevel.Off => new TextObject("{=PAI_AUTOMATION_OFF}Off").ToString(),
                SettlementAutomationLevel.Recruit => new TextObject("{=PAI_AUTOMATION_RECRUIT}Recruit").ToString(),
                SettlementAutomationLevel.RecruitAndUpgrade => new TextObject("{=PAI_AUTOMATION_UPGRADE}Recruit + Upgrade").ToString(),
                _ => new TextObject("{=PAI_AUTOMATION_FULL}Full Auto").ToString()
            };
        }
    }
}
