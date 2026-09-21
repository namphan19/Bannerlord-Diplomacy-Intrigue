using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// Adds the mod's relation summary to each row of the Diplomacy tab's peace list -
    /// "our vassal · hold 52", "tributary · 500 to us", "answers to Northern Empire" -
    /// the truce counterpart of the war row's exhaustion-and-score strip.
    ///
    /// Markup lives in <c>module/DiplomacyIntrigue/GUI/Prefabs/KingdomManagement/Diplomacy/
    /// DiTruceTupleSummary.xml</c>: one self-contained text widget appended to the row's
    /// horizontal list at a <b>fixed</b> width, for the same reason the war summary is
    /// fixed - the name beside it stretches, and a content-sized widget steals its space.
    ///
    /// Target, as of game v1.4.8: <c>TruceTuple.xml</c> holds one horizontal
    /// <c>ListPanel</c> whose children are banner, tribute icons, name, alliance/trade
    /// icons, truce icon, in that order. Appending after the truce icon keeps the summary
    /// last however the row is later extended.
    ///
    /// <b>This patch is allowed to miss.</b> If TaleWorlds rearranges the prefab the XPath
    /// finds nothing, UIExtenderEx logs it, and the row renders exactly as vanilla.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("TruceTuple",
        "descendant::ListPanel/Children/Widget[@Sprite='SPKingdom\\Diplomacy\\diplomacy_peace_icon']")]
    internal sealed class TruceTupleExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        private readonly XmlDocument _document = new XmlDocument();

        public TruceTupleExtension()
        {
            _document.LoadXml("<DiTruceTupleSummary />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }
}
