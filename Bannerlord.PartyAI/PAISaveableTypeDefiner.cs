using Bannerlord.PartyAI.CampaignBehaviors.AiBehaviors;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.InputSystem;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI;

internal class PAISaveableTypeDefiner : SaveableTypeDefiner
{
    public PAISaveableTypeDefiner() : base(548730888) { }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(PartyAiEntitySettings), 1);
        AddClassDefinition(typeof(PAICustomTemplate), 2);
        AddClassDefinition(typeof(PartyComposition), 3);
        AddClassDefinition(typeof(PartyAiOrder), 4);
        AddClassDefinition(typeof(RecruitmentBehavior.PAISettlementVisitLog), 5);
        AddClassDefinition(typeof(TownManagementOptions), 6);
        AddClassDefinition(typeof(TownManagementSettlementSettings), 7);
        AddClassDefinition(typeof(AutoDefenseAssignment), 8);
        AddClassDefinition(typeof(TownGovernorAssignmentState), 9);
    }

    protected override void DefineEnumTypes()
    {
        AddEnumDefinition(typeof(PartyAiOrderType), 1001);
        AddEnumDefinition(typeof(InputKey), 1002);
        AddEnumDefinition(typeof(SettlementAutomationLevel), 1003);
        AddEnumDefinition(typeof(TownManagementStrategy), 1004);
        AddEnumDefinition(typeof(AutoGovernorMode), 1005);
        AddEnumDefinition(typeof(TownDefensePriority), 1006);
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<Hero, PartyAiEntitySettings>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, PartyAiEntitySettings>));
        ConstructContainerDefinition(typeof(List<PAICustomTemplate>));
        ConstructContainerDefinition(typeof(List<CharacterObject>));
        ConstructContainerDefinition(typeof(List<Hero>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, CampaignTime>));
        ConstructContainerDefinition(typeof(List<RecruitmentBehavior.PAISettlementVisitLog>));
        ConstructContainerDefinition(typeof(List<PartyAiOrder>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, TownManagementSettlementSettings>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, TownGovernorAssignmentState>));
        ConstructContainerDefinition(typeof(Dictionary<Hero, AutoDefenseAssignment>));
        ConstructContainerDefinition(typeof(Dictionary<Hero, CampaignTime>));
    }
}
