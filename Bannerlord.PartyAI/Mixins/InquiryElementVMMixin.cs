using Bannerlord.PartyAI.Models;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Mixins;

[ViewModelMixin]
internal class InquiryElementVMMixin : BaseViewModelMixin<InquiryElementVM>
{
    private readonly InquiryElementVM _vm;

    public InquiryElementVMMixin(InquiryElementVM vm) : base(vm)
    {
        _vm = vm;

        // Only add banners when the popup mixin wants them
        if (!MultiSelectionQueryPopupVMMixin.AddClanBanners)
            return;

        // Identifier is your PartyAI settings object
        if (_vm.InquiryElement?.Identifier is PartyAiEntitySettings settings &&
            settings.Hero?.ClanBanner != null)
        {
            Banner_9 = new BannerImageIdentifierVM(settings.Hero.ClanBanner, true);
        }
        // Identifier is a Hero directly
        else if (_vm.InquiryElement?.Identifier is Hero hero &&
                 hero.ClanBanner != null)
        {
            Banner_9 = new BannerImageIdentifierVM(hero.ClanBanner, true);
        }
    }

    [DataSourceProperty]
    public BannerImageIdentifierVM? Banner_9 { get; private set; }
}
