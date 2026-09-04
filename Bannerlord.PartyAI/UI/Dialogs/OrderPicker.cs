using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.UI.Dialogs;

/// <summary>Two-step picker: order kind, then (if needed) its settlement or party target.</summary>
internal sealed class OrderPicker
{
    private readonly PartyProfile _profile;
    private readonly bool _forFallback;
    private readonly Action<PartyOrderType, IMapPoint?> _onPicked;

    private OrderPicker(PartyProfile profile, bool forFallback, Action<PartyOrderType, IMapPoint?> onPicked)
    {
        _profile = profile;
        _forFallback = forFallback;
        _onPicked = onPicked;
    }

    public static void Show(PartyProfile profile, bool forFallback, Action<PartyOrderType, IMapPoint?> onPicked)
        => new OrderPicker(profile, forFallback, onPicked).PickKind();

    private void PickKind()
    {
        List<InquiryElement> elements = PartyOrderTypeExtensions.PlayerSelectable
            .Where(type => !_forFallback || type.CanBeFallback())
            .Select(type => new InquiryElement(type, OrderText.Kind(type).ToString(), null, true, OrderText.KindHint(type).ToString()))
            .ToList();

        if (_forFallback)
        {
            elements.Add(new InquiryElement(PartyOrderType.None, L.S("{=koX9okuG}None"), null));
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            L.S("{=PAIUq8Q1n8k}Choose which type of order to add"),
            string.Empty,
            elements,
            true,
            1,
            1,
            L.Game("str_next"),
            L.Game("str_cancel"),
            results =>
            {
                if (results.FirstOrDefault()?.Identifier is PartyOrderType type)
                {
                    OnKindPicked(type);
                }
            },
            null,
            string.Empty,
            true));
    }

    private void OnKindPicked(PartyOrderType type)
    {
        if (!type.NeedsTarget())
        {
            _onPicked(type, null);
            return;
        }

        List<InquiryElement> elements = type.TargetsParty() ? PartyTargets() : SettlementTargets(type);

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            L.S("{=PAIZScpdz8d}Select a target"),
            string.Empty,
            elements,
            true,
            1,
            1,
            L.Game("str_done"),
            L.Game("str_cancel"),
            results =>
            {
                if (results.FirstOrDefault()?.Identifier is IMapPoint target)
                {
                    _onPicked(type, target);
                }
            },
            null,
            string.Empty,
            true));
    }

    private List<InquiryElement> PartyTargets()
    {
        Hero hero = _profile.Hero ?? Hero.MainHero;
        MobileParty? own = hero.PartyBelongedTo;

        IEnumerable<MobileParty?> faction = hero.MapFaction?.WarPartyComponents.Select(component => component?.MobileParty) ?? [];
        IEnumerable<MobileParty?> kingdom = hero.Clan?.Kingdom?.WarPartyComponents.Select(component => component?.MobileParty) ?? [];
        IEnumerable<MobileParty> nearby = own is null
            ? []
            : MobileParty.All.Where(party => party?.MapFaction is not null
                && party.GetPosition2D.Distance(own.GetPosition2D) <= own.SeeingRange * 2f
                && !party.IsGarrison
                && !party.IsMilitia
                && !FactionManager.IsAtWarAgainstFaction(party.MapFaction, hero.MapFaction));

        return faction.Concat(kingdom).Concat(nearby)
            .Where(party => party is not null && party != own)
            .Select(party => party!)
            .Distinct()
            .OrderByDescending(party => party.ActualClan == hero.Clan)
            .ThenBy(party => party.Name?.ToString())
            .Select(party =>
            {
                CharacterObject? leader = ConversationHelper.GetConversationCharacterPartyLeader(party.Party);
                ImageIdentifier? image = leader is null ? null : new CharacterImageIdentifier(CharacterCode.CreateFrom(leader));
                return new InquiryElement(party, party.Name?.ToString() ?? string.Empty, image);
            })
            .ToList();
    }

    private List<InquiryElement> SettlementTargets(PartyOrderType type)
    {
        Clan? clan = _profile.Hero?.Clan ?? Clan.PlayerClan;
        IFaction playerFaction = Hero.MainHero.MapFaction;

        IEnumerable<Settlement> settlements = type switch
        {
            PartyOrderType.BesiegeSettlement => Settlement.All
                .Where(settlement => settlement.IsFortification && FactionManager.IsAtWarAgainstFaction(settlement.MapFaction, playerFaction)),
            PartyOrderType.PatrolAroundPoint => Settlement.All
                .Where(settlement => settlement.IsFortification || settlement.IsVillage),
            _ => Settlement.All
                .Where(settlement => (settlement.IsFortification || settlement.IsVillage)
                    && !FactionManager.IsAtWarAgainstFaction(playerFaction, settlement.MapFaction))
        };

        return settlements
            .OrderByDescending(settlement => settlement.OwnerClan == clan)
            .ThenByDescending(settlement => settlement.IsTown)
            .ThenByDescending(settlement => settlement.IsCastle)
            .ThenBy(settlement => settlement.Name.ToString())
            .Select(settlement => new InquiryElement(
                settlement,
                settlement.Name.ToString(),
                settlement.OwnerClan?.Banner is null ? null : new BannerImageIdentifier(settlement.OwnerClan.Banner, false)))
            .ToList();
    }
}
