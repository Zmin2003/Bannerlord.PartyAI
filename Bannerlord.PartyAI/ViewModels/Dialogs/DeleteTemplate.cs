using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class DeleteTemplate
{

    private static Action _onDeleteCallback = null!;

    public static void Delete(Action callback)
    {
        _onDeleteCallback = callback;

        string title = new TextObject("{=PAI0HrENRV2}Select which template to delete").ToString();

        List<InquiryElement> list = SubModule.PartySettingsManager.AllTemplates
            .Where(template => !template.IsBuiltIn)
            .OrderBy(t => t.Name)
            .ToList()
            .ConvertAll(t =>
          new InquiryElement(t, t.Name, new CharacterImageIdentifier(CampaignUIHelper.GetCharacterCode(t.Troops.First())))
        );
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, string.Empty, list, isExitShown: true, 1, 1, new TextObject("{=PAIFofWXL3f}DELETE").ToString(), GameTexts.FindText("str_cancel").ToString(), DeletePartyTemplateCallback, null, "", true));
    }

    private static void DeletePartyTemplateCallback(List<InquiryElement> list)
    {
        if (list.Count == 0)
        {
            return;
        }

        PAICustomTemplate template = (PAICustomTemplate)list.First().Identifier;

        TextObject text = new TextObject("{=PAGIUuSgSnB}Are you sure you want to delete the template {TEMPLATE}?").SetTextVariable("TEMPLATE", template.Name);
        InformationManager.ShowInquiry(new InquiryData(new TextObject("{=PAR1D0VvXKZ}Delete Template").ToString(), text.ToString(), true, true, new TextObject("{=Y94H6XnK}Accept").ToString(), GameTexts.FindText("str_cancel").ToString(), () =>
        {
            SubModule.PartySettingsManager.DeletePartyTemplate(template);
            _onDeleteCallback.Invoke();
        }, () => { }), true);
    }
}
