using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Mixins;

[ViewModelMixin("RefreshInformationValues")]
internal class SPInventoryVMMixin : BaseViewModelMixin<SPInventoryVM>
{
    private readonly SPInventoryVM _vm;
    private readonly InventoryLogic? _inventoryLogic;
    private BasicTooltipViewModel _otherSideEquipmentMaxCountHint = new();
    private static readonly FieldInfo? _inventoryLogicField = AccessTools.Field(typeof(SPInventoryVM), "_inventoryLogic");

    public SPInventoryVMMixin(SPInventoryVM vm) : base(vm)
    {
        _vm = vm;
        _inventoryLogic = _inventoryLogicField?.GetValue(_vm) as InventoryLogic;
    }

    public override void OnRefresh()
    {
        base.OnRefresh();

        if (_vm.OtherSideHasCapacity
            && _inventoryLogic?.OtherSideCapacityData is { } capacityData
            && !_vm.IsTrading)
        {
            int weight;
            if (_inventoryLogic.OtherParty?.MobileParty is MobileParty otherParty)
            {
                OtherSideEquipmentMaxCountHint = new BasicTooltipViewModel(
                    () => CampaignUIHelper.GetPartyInventoryCapacityTooltip(otherParty));
                weight = MathF.Ceiling(_vm.LeftItemListVM.Where(i => !i.ItemRosterElement.EquipmentElement.Item.IsMountable && !i.ItemRosterElement.EquipmentElement.Item.IsAnimal).Sum((SPItemVM x) => x.ItemRosterElement.GetRosterElementWeight()));
            }
            else
            {
                weight = MathF.Ceiling(_vm.LeftItemListVM.Sum((SPItemVM x) => x.ItemRosterElement.GetRosterElementWeight()));
            }

            TextObject textObject = GameTexts.FindText("str_LEFT_over_RIGHT");
            int capacity = capacityData.GetCapacity();
            textObject.SetTextVariable("LEFT", weight);
            textObject.SetTextVariable("RIGHT", capacity);
            _vm.OtherEquipmentCountText = textObject.ToString();
            _vm.OtherEquipmentCountWarned = weight > capacity;
            OtherSideEquipmentMaxCountHint ??= new BasicTooltipViewModel();

            _vm.IsDoneDisabled = weight > capacity;
        }
    }

    [DataSourceProperty]
    public BasicTooltipViewModel OtherSideEquipmentMaxCountHint
    {
        get
        {
            return _otherSideEquipmentMaxCountHint;
        }
        set
        {
            if (value != _otherSideEquipmentMaxCountHint)
            {
                _otherSideEquipmentMaxCountHint = value;
                OnPropertyChangedWithValue(value, "OtherSideEquipmentMaxCountHint");
            }
        }
    }
}
