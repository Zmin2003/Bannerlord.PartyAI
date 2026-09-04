using Bannerlord.PartyAI.Finance;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Orders;
using Bannerlord.PartyAI.Parties.Recruitment;
using Bannerlord.PartyAI.Parties.Templates;
using Bannerlord.PartyAI.Towns;
using Bannerlord.PartyAI.War;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.InputSystem;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Core;

/// <summary>
/// Save-file type registry. The numeric ids are part of the save format and must never change;
/// the C# type names are free to change.
/// </summary>
internal sealed class SaveDefiner : SaveableTypeDefiner
{
    public SaveDefiner() : base(548730888) { }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(PartyProfile), 1);
        AddClassDefinition(typeof(TroopTemplate), 2);
        AddClassDefinition(typeof(PartyComposition), 3);
        AddClassDefinition(typeof(PartyOrder), 4);
        AddClassDefinition(typeof(RecruitOrder.SettlementVisitLog), 5);
        AddClassDefinition(typeof(TownSettings), 6);
        AddClassDefinition(typeof(FiefSettings), 7);
        AddClassDefinition(typeof(DefenseAssignment), 8);
        AddClassDefinition(typeof(GovernorState), 9);
        AddClassDefinition(typeof(WorkshopLedger), 10);
        AddClassDefinition(typeof(OffenseOperation), 11);
        AddClassDefinition(typeof(OffenseParticipant), 12);
    }

    protected override void DefineEnumTypes()
    {
        AddEnumDefinition(typeof(PartyOrderType), 1001);
        AddEnumDefinition(typeof(InputKey), 1002);
        AddEnumDefinition(typeof(SettlementAutomationLevel), 1003);
        AddEnumDefinition(typeof(TownStrategy), 1004);
        AddEnumDefinition(typeof(GovernorMode), 1005);
        AddEnumDefinition(typeof(DefensePriority), 1006);
        AddEnumDefinition(typeof(WorkshopMode), 1007);
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<Hero, PartyProfile>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, PartyProfile>));
        ConstructContainerDefinition(typeof(List<TroopTemplate>));
        ConstructContainerDefinition(typeof(List<CharacterObject>));
        ConstructContainerDefinition(typeof(List<Hero>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, CampaignTime>));
        ConstructContainerDefinition(typeof(List<RecruitOrder.SettlementVisitLog>));
        ConstructContainerDefinition(typeof(List<PartyOrder>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, FiefSettings>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, GovernorState>));
        ConstructContainerDefinition(typeof(Dictionary<Hero, DefenseAssignment>));
        ConstructContainerDefinition(typeof(Dictionary<Hero, CampaignTime>));
        ConstructContainerDefinition(typeof(Dictionary<string, WorkshopLedger>));
        ConstructContainerDefinition(typeof(List<OffenseParticipant>));
    }
}
