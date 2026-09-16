using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// The mod's block in the right-hand detail pane of the Diplomacy tab, in the strip
    /// between the game's strength-comparison bars and its proposal buttons.
    ///
    /// **Why a strip and not a sibling of the bars.** The first attempt appended the block
    /// next to <c>ListPanel Id="StatBars"</c>, which is the obvious place and the wrong one:
    /// its parent is a plain <c>Widget</c>, which lays children on top of one another rather
    /// than stacking them, so the block rendered straight over "Total Strength". The pane's
    /// own bottom-aligned proposal buttons show the idiom that works here - claim a strip by
    /// alignment - so <see cref="DiplomacyBarsMarginPatch"/> shortens the bars to make room
    /// and this puts the block in it.
    ///
    /// The block binds to <c>CurrentSelectedDiplomacyItem</c>, where
    /// <see cref="DiplomacyItemMixin"/> lives, and draws one row per line that mixin
    /// produces. That is the point of a bound list: how many rows there are and what they
    /// say is decided in C#, so a game update can cost us the block but never the
    /// correctness of what is in it.
    ///
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    // See WarTupleExtension for why the obsolete Prefabs namespace is the one that works
    // with UIExtenderEx v2.13.2.
#pragma warning disable CS0618
    [PrefabExtension("DiplomacyPanel", "descendant::TextWidget[@Text='@NoItemSelectedText']/..")]
    internal sealed class DiplomacyPanelExtension : PrefabExtensionInsertPatch
#pragma warning restore CS0618
    {
        public override string Id => "DiplomacyIntrigue.DiplomacyPanel.Block";

        public override int Position => PositionLast;

        public override XmlDocument GetPrefabExtension()
        {
            var document = new XmlDocument();
            document.LoadXml(
                "<ListPanel DataSource=\"{CurrentSelectedDiplomacyItem}\" "
                + "WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" "
                + "StackLayout.LayoutMethod=\"VerticalTopToBottom\" "
                + "VerticalAlignment=\"Bottom\" MarginBottom=\"150\" "
                + "MarginLeft=\"40\" MarginRight=\"60\" IsVisible=\"@DiHasDetail\">"
                + "  <Children>"
                + "    <TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" "
                + "       HorizontalAlignment=\"Center\" MarginBottom=\"8\" "
                + "       Brush=\"Kingdom.TitleMedium.Text\" Brush.FontSize=\"26\" Text=\"@DiDetailTitle\" />"
                + "    <ListPanel DataSource=\"{DiDetailLines}\" WidthSizePolicy=\"StretchToParent\" "
                + "       HeightSizePolicy=\"CoverChildren\" StackLayout.LayoutMethod=\"VerticalTopToBottom\">"
                + "      <ItemTemplate>"
                + "        <ListPanel WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" "
                + "           MarginBottom=\"4\">"
                + "          <Children>"
                + "            <TextWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"CoverChildren\" "
                + "               SuggestedWidth=\"200\" Brush=\"Kingdom.ParagraphSmall.Text\" "
                + "               Brush.FontSize=\"20\" Brush.TextHorizontalAlignment=\"Right\" "
                + "               MarginRight=\"20\" Text=\"@Label\" />"
                + "            <TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" "
                + "               Brush=\"Kingdom.ParagraphSmall.Text\" Brush.FontSize=\"20\" "
                + "               Brush.TextHorizontalAlignment=\"Left\" Text=\"@Value\" />"
                + "          </Children>"
                + "        </ListPanel>"
                + "      </ItemTemplate>"
                + "    </ListPanel>"
                + "  </Children>"
                + "</ListPanel>");
            return document;
        }
    }

    /// <summary>
    /// Shortens the strength-comparison bars so the mod's block has a strip to live in.
    ///
    /// One attribute on one vanilla widget, and the smallest change that makes the layout
    /// work: the bars keep their own scroll area and simply stop 250 pixels higher. If the
    /// XPath ever misses, the bars stay full height and the block overlaps them - ugly, and
    /// not fatal, which is the trade this whole file is written around.
    /// </summary>
#pragma warning disable CS0618
    [PrefabExtension("DiplomacyPanel", "descendant::Widget[@IsHidden='@IsDisplayingWarLogs']")]
    internal sealed class DiplomacyBarsMarginPatch : PrefabExtensionSetAttributePatch
#pragma warning restore CS0618
    {
        public override string Id => "DiplomacyIntrigue.DiplomacyPanel.BarsMargin";

        public override string Attribute => "MarginBottom";

        /// <summary>
        /// Vanilla is 110, which clears the proposal buttons and nothing else. 360 left the
        /// last bar label still under our header, measured on screen rather than guessed.
        /// </summary>
        public override string Value => "430";
    }
}
