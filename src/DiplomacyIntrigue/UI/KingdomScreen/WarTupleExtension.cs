using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// Adds the mod's war summary to each row of the Diplomacy tab's war list.
    ///
    /// Target, as of game v1.4.8: <c>WarTuple.xml</c> holds one horizontal
    /// <c>ListPanel</c> whose children are banner, padding, name, padding, crossed-swords
    /// icon. The summary is appended to that list at a **fixed** width: the name beside it
    /// stretches, so a widget that sizes to its own content steals the name's space - the
    /// first attempt squeezed "Khuzait" into a column one letter wide. Everything longer
    /// than a bar and a score belongs in the detail pane, which has room for it.
    ///
    /// **This patch is allowed to miss.** If TaleWorlds rearranges the prefab the XPath
    /// finds nothing, UIExtenderEx logs it, and the row renders exactly as vanilla - which
    /// is why the text is one self-contained widget rather than a restructuring of the row.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    // Prefabs.PrefabExtensionInsertPatch is marked obsolete in favour of Prefabs2, and we
    // use it anyway: Prefabs2's patches take their XML from PrefabExtensionTextAttribute,
    // which is *internal* in UIExtenderEx v2.13.2 - the version this module depends on - so
    // the replacement cannot actually be written from outside the library. Checked against
    // the shipped assembly rather than the documentation. Revisit if the dependency moves.
#pragma warning disable CS0618
    [PrefabExtension("WarTuple", "descendant::ListPanel/Children")]
    internal sealed class WarTupleExtension : PrefabExtensionInsertPatch
#pragma warning restore CS0618
    {
        public override string Id => "DiplomacyIntrigue.WarTuple.Summary";

        public override int Position => PositionLast;

        public override XmlDocument GetPrefabExtension()
        {
            var document = new XmlDocument();
            document.LoadXml(
                "<TextWidget DoNotAcceptEvents=\"true\" WidthSizePolicy=\"Fixed\" "
                + "HeightSizePolicy=\"CoverChildren\" SuggestedWidth=\"150\" "
                + "VerticalAlignment=\"Center\" MarginRight=\"10\" "
                + "Brush=\"ArmyManagement.Army.Tuple.Name\" "
                + "IsVisible=\"@DiHasRowSummary\" Text=\"@DiRowSummary\" />");
            return document;
        }
    }
}
