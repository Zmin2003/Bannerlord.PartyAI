using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;

namespace Bannerlord.PartyAI.Mixins;

[ViewModelMixin(nameof(ClanPartiesVM.RefreshValues))]
internal class ClanPartiesVMMixin : BaseViewModelMixin<ClanPartiesVM>
{
    internal ClanPartiesVM _vm;
    internal static ClanPartiesVMMixin Instance = null!;

    public ClanPartiesVMMixin(ClanPartiesVM vm) : base(vm)
    {
        _vm = vm;
        Instance = this;
    }
}
