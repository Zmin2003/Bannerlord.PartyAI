using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Mixins;
using Bannerlord.PartyAI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class CopyPaste
{
    private static PartyAiEntitySettings _source = null!;
    private static List<InquiryElement> _copySources = new();
    private static Action? _callback;
    public static void CopyTo(Hero? hero, Action? callback = null)
    {
        _callback = callback;

        if (hero == null) { return; }

        CopyCallback(SubModule.PartySettingsManager.Settings(hero));
    }

    public static void CopyGarrisonTo(Settlement? settlement, Action? callback = null)
    {
        _callback = callback;

        if (settlement == null) { return; }

        CopyCallback(SubModule.PartySettingsManager.Settings(settlement));
    }

    internal static void CopyCallback(PartyAiEntitySettings? source, Action<List<InquiryElement>>? callback = null)
    {
        if (source == null) { return; }
        callback ??= ChooseCopyTypeCallback;

        _source = source;

        string title = new TextObject("{=PAIEv0gLuYi}Select which settings you would like to copy.").ToString();
        string description = new TextObject("{=PAIZZEi6e9F}You may select more than one option.").ToString();

        List<InquiryElement> newList = new();
        string CompositionText = new TextObject("{=PAI42PrfM04}Party Composition").ToString();
        string TemplateText = new TextObject("{=PAIrkbpwijb}Template").ToString();
        string OrderText = new TextObject("{=PAI6XKZojTt}Order").ToString();
        string OptionsText = new TextObject("{=PAIQnwbXcqc}Options").ToString();
        TextObject hint = new TextObject("{=!}{HERO}'s {OPTION}")
            .SetTextVariable(
                "HERO",
                _source.Hero?.Name ?? _source.Settlement?.Name ?? TextObject.GetEmpty());

        newList.Add(new InquiryElement(source.Composition, CompositionText, null, true, hint.SetTextVariable("OPTION", CompositionText).ToString()));
        newList.Add(new InquiryElement(source.PartyTemplate, TemplateText, null, true, hint.SetTextVariable("OPTION", TemplateText).ToString()));
        if (_source.Settlement == null)
        {
            if (!SubModule.PartySettingsManager.IsCaravanManageable(_source.Hero))
            {
                newList.Add(new InquiryElement(source.Order ?? new PartyAiOrder(PartyAiOrderType.None), OrderText, null, true, hint.SetTextVariable("OPTION", OrderText).ToString()));
            }
            newList.Add(new InquiryElement(source, OptionsText, null, true, hint.SetTextVariable("OPTION", OptionsText).ToString()));
        }

        MultiSelectionQueryPopupVMMixin.AddClanBanners = true;
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, description, newList, isExitShown: true, 1, newList.Count, GameTexts.FindText("str_next").ToString(), GameTexts.FindText("str_cancel").ToString(), callback, null));
    }

    private static void ChooseCopyTypeCallback(List<InquiryElement> list)
    {
        if (list.Count == 0) { return; }

        _copySources = list;

        string title = new TextObject("{=PAIdpg5Dset}Select which parties to copy settings to").ToString();

        List<Hero> heroList = new();
        List<InquiryElement> newList;

        if (SubModule.PartySettingsManager.IsCaravanManageable(_source.Hero))
        {
            heroList = Clan.PlayerClan.Heroes.Where(h => SubModule.PartySettingsManager.IsCaravanManageable(h) && h != _source.Hero).ToList();
        }
        else if (_source.Settlement != null)
        {
            newList = Clan.PlayerClan.Settlements
                        .Where(s => SubModule.PartySettingsManager.IsGarrisonManageable(s) && s != _source.Settlement)
                        .ToList()
                        .ConvertAll(s =>
              new InquiryElement(
                  s,
                  s.Name.ToString(),
                  new BannerImageIdentifier(s.OwnerClan.Banner, false))
            );
            goto done;
        }
        else
        {
            PartyAIControlsMenuVM.GetManageableHeroes(heroList, clanOnly: true, showAll: false);
            heroList = heroList.Where(h => h != _source.Hero && !(h.PartyBelongedTo?.IsCaravan ?? false)).ToList();
        }

        newList = heroList.ConvertAll(p =>
          new InquiryElement(
              p,
              p.Name.ToString(),
              new CharacterImageIdentifier(CharacterCode.CreateFrom(p.CharacterObject)),
              true,
              p.Clan.Name.ToString())
        );

    done:
        MultiSelectionQueryPopupVMMixin.AddClanBanners = true;
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, string.Empty, newList, isExitShown: true, 1, 5000, GameTexts.FindText("str_done").ToString(), GameTexts.FindText("str_cancel").ToString(), ChooseDestinationPartiesCallback, null, "", true));
    }

    private static void ChooseDestinationPartiesCallback(List<InquiryElement> list)
    {
        foreach (InquiryElement p in list)
        {
            PartyAiEntitySettings settings = p.Identifier is Settlement ? SubModule.PartySettingsManager.Settings((Settlement)p.Identifier) : SubModule.PartySettingsManager.Settings((Hero)p.Identifier);
            foreach (InquiryElement source in _copySources)
            {
                CopySettings(settings, source);
            }
        }

        _callback?.Invoke();
    }

    internal static void CopySettings(PartyAiEntitySettings settings, InquiryElement source)
    {
        if (source.Identifier is PartyComposition composition)
        {
            settings.Composition = new PartyComposition(composition);
        }

        if (source.Identifier is PAICustomTemplate)
        {
            PAICustomTemplate template = (PAICustomTemplate)source.Identifier;
            settings.SetPartyTemplate(template);
        }

        if (source.Identifier is PartyAiOrder)
        {
            PartyAiOrder order = (PartyAiOrder)source.Identifier;

            // Explicitly wipe the target's existing order state
            settings.ClearAllOrders();

            if (order.Behavior != PartyAiOrderType.None)
            {
                // Reconstruct the order state using Clones to avoid reference sharing
                if (_source.Order != null)
                {
                    settings.SetOrder(_source.Order.Behavior, _source.Order.Target);
                }

                foreach (PartyAiOrder queuedOrder in _source.OrderQueue)
                {
                    settings.OrderQueue.Add(new(queuedOrder));
                }
            }
        }

        if (source.Identifier is PartyAiEntitySettings)
        {
            settings.CopyOptionsFrom((PartyAiEntitySettings)source.Identifier);
        }

        if (source.Identifier == null)
        {
            settings.SetPartyTemplate(null);
        }
    }
}
