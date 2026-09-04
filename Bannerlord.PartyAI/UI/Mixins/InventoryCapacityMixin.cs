using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Mixins;

/// <summary>
/// Quality-of-life fix for the inventory screen: shows the other party's real carrying
/// capacity (mounts excluded) and blocks "Done" when it would be overloaded.
/// </summary>
[ViewModelMixin("RefreshInformationValues")]
internal sealed class InventoryCapacityMixin : BaseViewModelMixin<SPInventoryVM>
{
    private static readonly FieldInfo? InventoryLogicField = AccessTools.Field(typeof(SPInventoryVM), "_inventoryLogic");

    private readonly SPInventoryVM _vm;
    private readonly InventoryLogic? _logic;
    private BasicTooltipViewModel? _otherSideEquipmentMaxCountHint;

    public InventoryCapacityMixin(SPInventoryVM vm) : base(vm)
    {
        _vm = vm;
        _logic = InventoryLogicField?.GetValue(vm) as InventoryLogic;
    }

    [DataSourceProperty]
    public BasicTooltipViewModel? OtherSideEquipmentMaxCountHint
    {
        get => _otherSideEquipmentMaxCountHint;
        set
        {
            if (value != _otherSideEquipmentMaxCountHint)
            {
                _otherSideEquipmentMaxCountHint = value;
                OnPropertyChanged(nameof(OtherSideEquipmentMaxCountHint));
            }
        }
    }

    public override void OnRefresh()
    {
        base.OnRefresh();
        if (_logic?.OtherSideCapacityData is null || !_vm.OtherSideHasCapacity || _vm.IsTrading)
        {
            return;
        }

        int weight;
        if (_logic.OtherParty?.MobileParty is not null)
        {
            var party = _logic.OtherParty.MobileParty;
            OtherSideEquipmentMaxCountHint = new BasicTooltipViewModel(() => CampaignUIHelper.GetPartyInventoryCapacityTooltip(party));
            weight = MathF.Ceiling(_vm.LeftItemListVM
                .Where(item => !item.ItemRosterElement.EquipmentElement.Item.IsMountable && !item.ItemRosterElement.EquipmentElement.Item.IsAnimal)
                .Sum(item => item.ItemRosterElement.GetRosterElementWeight()));
        }
        else
        {
            weight = MathF.Ceiling(_vm.LeftItemListVM.Sum(item => item.ItemRosterElement.GetRosterElementWeight()));
        }

        int capacity = _logic.OtherSideCapacityData.GetCapacity();
        TextObject text = GameTexts.FindText("str_LEFT_over_RIGHT");
        text.SetTextVariable("LEFT", weight);
        text.SetTextVariable("RIGHT", capacity);
        _vm.OtherEquipmentCountText = text.ToString();
        _vm.OtherEquipmentCountWarned = weight > capacity;
        _vm.IsDoneDisabled = weight > capacity;
        OtherSideEquipmentMaxCountHint ??= new BasicTooltipViewModel();
    }
}

internal static class InventoryPrefabExtension
{
    [PrefabExtension("Inventory", "descendant::ListPanel[@IsVisible='@OtherSideHasCapacity']/Children")]
    internal sealed class CapacityHint : PrefabExtensionInsertPatch
    {
        private IEnumerable<XmlNode>? _nodes;

        public override InsertType Type => InsertType.Child;
        public override int Index => 2;

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> GetNodes()
        {
            if (_nodes is null)
            {
                var document = new XmlDocument();
                document.LoadXml("<Root><HintWidget DataSource=\"{OtherSideEquipmentMaxCountHint}\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Command.HoverBegin=\"ExecuteBeginHint\" Command.HoverEnd=\"ExecuteEndHint\" /></Root>");
                _nodes = document.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
            }

            return _nodes;
        }
    }
}
