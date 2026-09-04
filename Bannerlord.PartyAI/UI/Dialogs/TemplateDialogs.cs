using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Recruitment;
using Bannerlord.PartyAI.Parties.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Dialogs;

/// <summary>Create / edit / view / pick troop templates.</summary>
internal static class TemplateDialogs
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 20;

    /// <summary>Name prompt, then the party screen to choose end-of-line troops.</summary>
    public static void Create(Action<TroopTemplate> onCreated)
    {
        InformationManager.ShowTextInquiry(new TextInquiryData(
            L.S("{=PAZ8yCnPyxh}Create New Troop Template"),
            L.S("{=PAO0HGBsJZd}Enter a name for your new party troop template."),
            true,
            true,
            L.Game("str_next"),
            L.Game("str_cancel"),
            name => PickTargets(name.Trim(), onCreated),
            null,
            false,
            ValidateName));
    }

    private static Tuple<bool, string> ValidateName(string name)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length < MinNameLength)
        {
            return new(false, L.T("{=PAHxbYi6Awy}Minimum {MIN} characters", "MIN", MinNameLength).ToString());
        }

        if (name.Length > MaxNameLength)
        {
            return new(false, L.T("{=PAbGk5vWaqM}Maximum {MAX} characters", "MAX", MaxNameLength).ToString());
        }

        if (!PartyAi.Parties.IsUniqueTemplateName(name))
        {
            return new(false, L.S("{=PAuu16DcbWX}There is already a template with that name."));
        }

        return new(true, string.Empty);
    }

    private static void PickTargets(string name, Action<TroopTemplate> onCreated)
    {
        PartyScreenHelper.Open(
            RecruitmentRules.AllTopTierTroops(),
            null,
            L.T("{=PAirAdxXSc5}Eligible Troops"),
            L.T("{=PA3a9D3vJpb}Chosen Troops"),
            L.T("{=PAH9JlPJqJC}Create New Template"),
            (left, leftPrisoners, right, rightPrisoners, taken, released, forced, leftParty, rightParty) =>
            {
                onCreated(PartyAi.Parties.CreateTemplate(name, right));
                return true;
            });
    }

    /// <summary>Choose exactly which troops along the upgrade paths belong to the template.</summary>
    public static void FineTune(TroopTemplate template, Action onChanged)
    {
        TroopRoster selected = TroopRoster.CreateDummyTroopRoster();
        foreach (CharacterObject troop in template.Troops)
        {
            selected.AddToCounts(troop, 1);
        }

        TroopRoster available = TroopRoster.CreateDummyTroopRoster();
        foreach (CharacterObject troop in template.ResolveTroops().Except(template.Troops))
        {
            available.AddToCounts(troop, 1);
        }

        PartyScreenHelper.Open(
            available,
            selected,
            L.T("{=PAirAdxXSc5}Eligible Troops"),
            L.T("{=PABrUnTTy9r}Troops in Template '{TEMPLATE}'", "TEMPLATE", template.Name),
            L.T("{=PAIxE5LIta2}Fine Tune Template"),
            (left, leftPrisoners, right, rightPrisoners, taken, released, forced, leftParty, rightParty) =>
            {
                template.SetTroops(right.GetTroopRoster().Select(element => element.Character));
                onChanged();
                return true;
            },
            ValidateFineTune);
    }

    /// <summary>Every kept troop must still be reachable from another kept troop or be a root of the tree.</summary>
    private static Tuple<bool, TextObject> ValidateFineTune(TroopRoster left, TroopRoster leftPrisoners, TroopRoster right, TroopRoster rightPrisoners, int leftLimit, int rightLimit)
    {
        if (right.TotalManCount == 0)
        {
            return new(false, L.T("{=PAIAAm1PQy1}Not enough troops in template."));
        }

        List<CharacterObject> selected = right.GetTroopRoster().Select(element => element.Character).ToList();
        List<CharacterObject> unselected = left.GetTroopRoster().Select(element => element.Character).ToList();
        var errors = new List<string>();

        foreach (CharacterObject troop in selected)
        {
            foreach (CharacterObject target in selected.Where(target => target != troop && TroopTemplate.UpgradesTo(troop, target)))
            {
                bool bridged = selected.Any(other => other.UpgradeTargets.Contains(target))
                    || troop.UpgradeTargets.Any(next => selected.Contains(next) && TroopTemplate.UpgradesTo(next, target));
                if (!bridged)
                {
                    errors.Add(L.T("{=PAI5D5Ofcoo}No upgrade path between {CHARACTER} and {TARGET}")
                        .SetTextVariable("CHARACTER", troop.Name)
                        .SetTextVariable("TARGET", target.Name)
                        .ToString());
                }
            }

            bool onlyReachableFromUnselected = unselected.Any(other => other.UpgradeTargets.Contains(troop))
                && !selected.Any(other => other.UpgradeTargets.Contains(troop))
                && !selected.Any(other => other != troop && TroopTemplate.UpgradesTo(other, troop));
            if (onlyReachableFromUnselected)
            {
                errors.Add(L.T("{=PAI91kcf3yB}Must select at least one troop that upgrades to {CHARACTER}", "CHARACTER", troop.Name).ToString());
            }
        }

        return errors.Count == 0
            ? new(true, null!)
            : new(false, new TextObject(string.Join(Environment.NewLine, errors)));
    }

    public static void View(TroopTemplate template)
    {
        TroopRoster selected = TroopRoster.CreateDummyTroopRoster();
        foreach (CharacterObject troop in template.Troops)
        {
            selected.AddToCounts(troop, 1);
        }

        PartyScreenHelper.Open(
            null,
            selected,
            TextObject.GetEmpty(),
            L.T("{=PABrUnTTy9r}Troops in Template '{TEMPLATE}'", "TEMPLATE", template.Name),
            L.T("{=PAoNJx6fAYq}View Template"),
            null,
            transferable: (character, type, side, leftOwner) => false);
    }

    /// <summary>Assign a template (or none) to a profile.</summary>
    public static void PickForProfile(PartyProfile profile, Action onPicked)
    {
        TextObject title = profile.Hero is not null
            ? L.T("{=PAI6hwc2LSt}Select a new template for {LEADER}'s Party", "LEADER", profile.Hero.Name)
            : profile.Settlement is not null
                ? L.T("{=PAI1HObVpcg}Select a new template for {SETTLEMENT}'s Garrison", "SETTLEMENT", profile.Settlement.Name)
                : L.T("{=PAI9HyjJ7ss}Select a new template");

        List<InquiryElement> elements = PartyAi.Parties.Templates
            .OrderBy(template => template.IsBuiltIn)
            .ThenBy(template => template.Name)
            .Select(template => new InquiryElement(template, template.Name, Portrait(template)))
            .ToList();
        elements.Add(new InquiryElement(null, L.S("{=koX9okuG}None"), null));

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            title.ToString(),
            string.Empty,
            elements,
            true,
            1,
            1,
            L.Game("str_done"),
            L.Game("str_cancel"),
            results =>
            {
                if (results.Count > 0)
                {
                    profile.SetTemplate(results[0].Identifier as TroopTemplate);
                    onPicked();
                }
            },
            null,
            string.Empty,
            true));
    }

    public static void ConfirmDelete(TroopTemplate template, Action onDeleted)
    {
        InformationManager.ShowInquiry(new InquiryData(
            L.S("{=PAR1D0VvXKZ}Delete Template"),
            L.T("{=PAGIUuSgSnB}Are you sure you want to delete the template {TEMPLATE}?", "TEMPLATE", template.Name).ToString(),
            true,
            true,
            L.S("{=Y94H6XnK}Accept"),
            L.Game("str_cancel"),
            () =>
            {
                PartyAi.Parties.DeleteTemplate(template);
                onDeleted();
            },
            null), true);
    }

    public static void Import(Action onImported)
    {
        if (PartyAi.TemplateImport.IsPending)
        {
            Notify.Warning(L.T("{=PAI_TEMPLATE_IMPORT_PENDING}A template download is already running."));
            return;
        }

        InformationManager.ShowTextInquiry(new TextInquiryData(
            L.S("{=PAI_TEMPLATE_IMPORT_TITLE}Import Online Template"),
            L.S("{=PAI_TEMPLATE_IMPORT_PROMPT}Paste the HTTPS URL of a Party AI template JSON file."),
            true,
            true,
            L.Game("str_done"),
            L.Game("str_cancel"),
            url =>
            {
                bool started = PartyAi.TemplateImport.TryBegin(url, result =>
                {
                    if (result.Success)
                    {
                        Notify.Success(L.T("{=PAI_TEMPLATE_IMPORT_SUCCESS}Imported template: {NAME}", "NAME", result.Template!.Name));
                        onImported();
                    }
                    else
                    {
                        Notify.Error(new TextObject(result.Error));
                    }
                }, out string error);

                if (!started)
                {
                    Notify.Error(new TextObject(error));
                }
            },
            null,
            false,
            url => new Tuple<bool, string>(TemplateImportService.TryValidateUrl(url, out _, out string error), error)));
    }

    public static ImageIdentifier? Portrait(TroopTemplate template)
        => template.Portrait is null ? null : new CharacterImageIdentifier(CampaignUIHelper.GetCharacterCode(template.Portrait));
}
