using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Towns;
using Bannerlord.PartyAI.UI.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Dialogs;

/// <summary>Copies selected parts of one entry's settings onto other compatible entries.</summary>
internal sealed class SettingsClipboard
{
    private enum Part
    {
        Composition,
        Template,
        Orders,
        Options,
        TownManagement
    }

    private PartyProfile? _sourceProfile;
    private FiefSettings? _sourceFief;
    private HashSet<Part> _parts = new();

    public bool HasContent => _sourceProfile is not null && _parts.Count > 0;

    public string SourceName { get; private set; } = string.Empty;

    public bool CopiesTownManagement => _parts.Contains(Part.TownManagement);

    public void Clear()
    {
        _sourceProfile = null;
        _sourceFief = null;
        _parts.Clear();
        SourceName = string.Empty;
    }

    /// <summary>Asks which parts to copy, then stores them. <paramref name="onCopied"/> runs only when something was chosen.</summary>
    public void Copy(EntryVM source, Action onCopied)
    {
        TextObject owner = source.Hero?.Name ?? source.Settlement?.Name ?? new TextObject(source.Name);
        var elements = new List<InquiryElement>
        {
            Element(Part.Composition, L.S("{=PAI42PrfM04}Party Composition"), owner),
            Element(Part.Template, L.S("{=PAIrkbpwijb}Template"), owner)
        };

        if (source.Kind == EntryKind.LordParty)
        {
            elements.Add(Element(Part.Orders, L.S("{=PAI6XKZojTt}Order"), owner));
        }

        if (source.IsParty)
        {
            elements.Add(Element(Part.Options, L.S("{=PAIQnwbXcqc}Options"), owner));
        }

        if (source.Kind == EntryKind.Fief)
        {
            elements.Add(Element(Part.TownManagement, L.S("{=PAI_TOWN_OPTIONS_TITLE}Town Management"), owner));
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            L.S("{=PAIEv0gLuYi}Select which settings you would like to copy."),
            L.S("{=PAIZZEi6e9F}You may select more than one option."),
            elements,
            true,
            1,
            elements.Count,
            L.Game("str_next"),
            L.Game("str_cancel"),
            results =>
            {
                _parts = new HashSet<Part>(results.Select(result => (Part)result.Identifier));
                if (_parts.Count == 0)
                {
                    return;
                }

                _sourceProfile = source.Profile;
                _sourceFief = source.Settlement is not null && _parts.Contains(Part.TownManagement)
                    ? PartyAi.Towns.Fief(source.Settlement).DeepCopy()
                    : null;
                SourceName = source.Name;
                onCopied();
            },
            null));
    }

    public void PasteTo(EntryVM target)
    {
        if (_sourceProfile is null)
        {
            return;
        }

        PartyProfile profile = target.Profile;

        if (_parts.Contains(Part.Composition))
        {
            profile.Composition = new PartyComposition(_sourceProfile.Composition);
        }

        if (_parts.Contains(Part.Template))
        {
            profile.SetTemplate(_sourceProfile.Template);
        }

        if (_parts.Contains(Part.Options) && target.IsParty)
        {
            profile.CopyOptionsFrom(_sourceProfile);
        }

        if (_parts.Contains(Part.Orders) && target.Kind == EntryKind.LordParty)
        {
            profile.CopyOrdersFrom(_sourceProfile);
        }

        if (_parts.Contains(Part.TownManagement) && _sourceFief is not null && target.Settlement is not null
            && PartyAi.Towns.IsTownManageable(target.Settlement))
        {
            FiefSettings fief = PartyAi.Towns.Fief(target.Settlement);
            bool enabled = fief.Enabled;
            fief.CopyFrom(_sourceFief);
            fief.Enabled = enabled;
        }
    }

    private static InquiryElement Element(Part part, string text, TextObject owner)
        => new(part, text, null, true,
            L.T("{=PAI_COPY_PART_HINT}{HERO}'s {OPTION}").SetTextVariable("HERO", owner).SetTextVariable("OPTION", text).ToString());
}
