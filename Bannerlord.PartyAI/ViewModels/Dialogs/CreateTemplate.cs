using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.ViewModels.Misc;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class CreateTemplate
{
    private static string _newTemplateName = string.Empty;

    public static void Create()
    {
        string titleText = new TextObject("{=PAZ8yCnPyxh}Create New Troop Template").ToString();
        string innerText = new TextObject("{=PAO0HGBsJZd}Enter a name for your new party troop template.").ToString();
        InformationManager.ShowTextInquiry(new TextInquiryData(titleText, innerText, isAffirmativeOptionShown: true, isNegativeOptionShown: true, GameTexts.FindText("str_next").ToString(), GameTexts.FindText("str_cancel").ToString(), OnTemplateNamed, null, shouldInputBeObfuscated: false, IsTemplateNameValid));
    }

    private static Tuple<bool, string> IsTemplateNameValid(string name)
    {
        int minimum = 2;
        if (name.Length < minimum)
        {
            return new Tuple<bool, string>(false, new TextObject("{=PAHxbYi6Awy}Minimum {MIN} characters").SetTextVariable("MIN", minimum).ToString());
        }

        int maximum = 20;
        if (name.Length > maximum)
        {
            return new Tuple<bool, string>(false, new TextObject("{=PAbGk5vWaqM}Maximum {MAX} characters").SetTextVariable("MAX", maximum).ToString());
        }

        if (!SubModule.PartySettingsManager.IsUniqueTemplateName(name))
        {
            return new Tuple<bool, string>(false, new TextObject("{=PAuu16DcbWX}There is already a template with that name.").ToString());
        }

        return new Tuple<bool, string>(true, String.Empty);
    }

    private static void OnTemplateNamed(string name)
    {
        _newTemplateName = name;
        TextObject leftPartyName = new("{=PAirAdxXSc5}Eligible Troops");
        TextObject rightPartyName = new("{=PA3a9D3vJpb}Chosen Troops");
        TextObject header = new("{=PAH9JlPJqJC}Create New Template");
        VMUtilities.OpenPartyScreen(
            Recruitment.GetAllTopTierTroops(),
            null,
            leftPartyName,
            rightPartyName,
            header,
            TemplateCreateDoneHandler);
    }

    private static bool TemplateCreateDoneHandler(
        TroopRoster leftMemberRoster,
        TroopRoster leftPrisonRoster,
        TroopRoster rightMemberRoster,
        TroopRoster rightPrisonRoster,
        FlattenedTroopRoster takenPrisonerRoster,
        FlattenedTroopRoster releasedPrisonerRoster,
        bool isForced,
        PartyBase? leftParty = null,
        PartyBase? rightParty = null)
    {
        PAICustomTemplate template = new(
          _newTemplateName,
          rightMemberRoster
        );

        return true;
    }
}
