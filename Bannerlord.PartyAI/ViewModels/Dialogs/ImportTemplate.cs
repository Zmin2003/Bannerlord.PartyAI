using Bannerlord.PartyAI.Domain;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class ImportTemplate
{
    internal static void Show(Action? onImported = null)
    {
        if (TemplateImportService.IsPending)
        {
            Display(false, new TextObject("{=PAI_TEMPLATE_IMPORT_PENDING}A template download is already running.").ToString());
            return;
        }

        string title = new TextObject("{=PAI_TEMPLATE_IMPORT_TITLE}Import Online Template").ToString();
        string description = new TextObject("{=PAI_TEMPLATE_IMPORT_PROMPT}Paste the HTTPS URL of a Party AI template JSON file.").ToString();

        InformationManager.ShowTextInquiry(new TextInquiryData(
            title,
            description,
            true,
            true,
            GameTexts.FindText("str_done").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            url => BeginImport(url, onImported),
            null,
            false,
            ValidateUrl));
    }

    private static Tuple<bool, string> ValidateUrl(string url)
    {
        bool valid = TemplateImportService.TryValidateUrl(url, out _, out string error);
        return new Tuple<bool, string>(valid, error);
    }

    private static void BeginImport(string url, Action? onImported)
    {
        bool started = TemplateImportService.TryBegin(
            url,
            result =>
            {
                if (result.Success)
                {
                    TextObject message = new("{=PAI_TEMPLATE_IMPORT_SUCCESS}Imported template: {NAME}");
                    message.SetTextVariable("NAME", result.Template!.Name);
                    Display(true, message.ToString());
                    onImported?.Invoke();
                }
                else
                {
                    Display(false, result.Error);
                }
            },
            out string error);

        if (!started)
        {
            Display(false, error);
        }
    }

    private static void Display(bool success, string message)
    {
        InformationManager.DisplayMessage(new InformationMessage(
            message,
            success ? Colors.Green : Colors.Red));
    }
}
