using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// Adds the mod's war summary to each row of the Diplomacy tab's war list.
    ///
    /// Markup lives in <c>module/DiplomacyIntrigue/GUI/Prefabs/KingdomManagement/Diplomacy/
    /// DiWarTupleSummary.xml</c>: one self-contained text widget appended to the row's
    /// horizontal list at a <b>fixed</b> width. The name beside it stretches, so a widget
    /// that sizes to its own content steals the name's space - the first attempt squeezed
    /// "Khuzait" into a column one letter wide. Everything longer than a bar and a score
    /// belongs in the detail pane, which has room for it.
    ///
    /// Target, as of game v1.4.8: <c>WarTuple.xml</c> holds one horizontal
    /// <c>ListPanel</c> whose children are banner, padding, name, padding, crossed-swords
    /// icon, in that order. Appending after the icon keeps the summary last however the
    /// row is later extended: a <c>Child</c> insert lands at <c>Index</c>, which defaults
    /// to 0 - first, not last.
    ///
    /// <b>This patch is allowed to miss.</b> If TaleWorlds rearranges the prefab the XPath
    /// finds nothing, UIExtenderEx logs it, and the row renders exactly as vanilla.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("WarTuple",
        "descendant::ListPanel/Children/Widget[@Sprite='SPKingdom\\Diplomacy\\diplomacy_war_icon']")]
    internal sealed class WarTupleExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        private readonly XmlDocument _document = new XmlDocument();

        public WarTupleExtension()
        {
            _document.LoadXml("<DiWarTupleSummary />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }
}
