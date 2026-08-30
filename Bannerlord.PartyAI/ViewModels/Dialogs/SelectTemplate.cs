using Bannerlord.PartyAI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class SelectTemplate
{
    private static Action? _onSelectCallback;
    internal static void Select(PartyAiEntitySettings settings, Action? callback = null)
    {
        _onSelectCallback = callback;

        string title;
        if (settings.Hero != null)
        {
            title = new TextObject("{=PAI6hwc2LSt}Select a new template for {LEADER}'s Party").SetTextVariable("LEADER", settings.Hero.Name).ToString();
        }
        else if (settings.Settlement != null)
        {
            title = new TextObject("{=PAI1HObVpcg}Select a new template for {SETTLEMENT}'s Garrison").SetTextVariable("SETTLEMENT", settings.Settlement.Name).ToString();
        }
        else
        {
            title = new TextObject("{=PAI9HyjJ7ss}Select a new template").ToString();
        }

        List<InquiryElement> list = SubModule.PartySettingsManager.AllTemplates.OrderBy(t => t.Name).ToList().ConvertAll(t =>
          new InquiryElement(t, t.Name, new CharacterImageIdentifier(CampaignUIHelper.GetCharacterCode(t.Troops.First())))
        );

        list.Add(new InquiryElement(null, "None", null));
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, string.Empty, list, isExitShown: true, 1, 1, GameTexts.FindText("str_done").ToString(), GameTexts.FindText("str_cancel").ToString(), delegate (List<InquiryElement> list)
        {
            if (list.Count == 0) { return; }

            settings.SetPartyTemplate((PAICustomTemplate?)list.FirstOrDefault()?.Identifier);

            _onSelectCallback?.Invoke();
        }, null, "", true));
    }
}
