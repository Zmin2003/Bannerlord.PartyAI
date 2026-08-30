using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Bannerlord.PartyAI.PrefabExtensions;

internal class InventoryPrefabExtension
{
    [PrefabExtension("Inventory", "descendant::ListPanel[@IsVisible='@OtherSideHasCapacity']/Children")]
    internal class Popups : PrefabExtensionInsertPatch
    {
        private IEnumerable<XmlNode>? _nodes;
        public override InsertType Type => InsertType.Child;
        public override int Index => 2;

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> GetNodes()
        {
            if (_nodes is null)
            {
                XmlDocument document = new();
                document.LoadXml("<DiscardedRoot><HintWidget DataSource=\"{OtherSideEquipmentMaxCountHint}\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Command.HoverBegin=\"ExecuteBeginHint\" Command.HoverEnd=\"ExecuteEndHint\" /></DiscardedRoot>");
                _nodes = document.DocumentElement.ChildNodes.Cast<XmlNode>();
            }
            return _nodes;
        }
    }
}
