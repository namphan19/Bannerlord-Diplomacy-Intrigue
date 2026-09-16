using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// One line under the two banners saying what this relationship is: the casus belli of
    /// a war and what the enemy's condition means, or the agreements standing between the
    /// two and who answers to whom.
    ///
    /// It uses the brush and size the panel uses for "For 1 day" a few pixels above it, so
    /// it reads as part of the header rather than as something bolted on.
    ///
    /// Target, as of game v1.4.8: the right pane's vertical stack, inserted after the title
    /// container that holds the two leaders. If TaleWorlds moves it the XPath misses,
    /// UIExtenderEx logs that, and the tab renders without the line rather than not at all.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    // See WarTupleExtension for why the obsolete Prefabs namespace is the one that works
    // with UIExtenderEx v2.13.2.
#pragma warning disable CS0618
    [PrefabExtension("DiplomacyPanel",
        "descendant::ListPanel[@IsVisible='@IsAcceptableItemSelected']/Children")]
    internal sealed class DiplomacyHeadlineExtension : PrefabExtensionInsertPatch
#pragma warning restore CS0618
    {
        public override string Id => "DiplomacyIntrigue.DiplomacyPanel.Headline";

        /// <summary>Second in the stack: directly under the title container.</summary>
        public override int Position => 1;

        public override XmlDocument GetPrefabExtension()
        {
            var document = new XmlDocument();
            document.LoadXml(
                "<TextWidget DataSource=\"{CurrentSelectedDiplomacyItem}\" DoNotAcceptEvents=\"true\" "
                + "WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" "
                + "HorizontalAlignment=\"Center\" MarginLeft=\"60\" MarginRight=\"60\" "
                + "MarginTop=\"6\" MarginBottom=\"4\" "
                + "Brush=\"ArmyManagement.Army.Tuple.Name\" Brush.FontSize=\"20\" "
                + "Brush.TextHorizontalAlignment=\"Center\" IsEnabled=\"false\" "
                + "IsVisible=\"@DiHasHeadline\" Text=\"@DiHeadline\" />");
            return document;
        }
    }

    /// <summary>
    /// The mod's comparison rows and its action buttons, in the strip between the game's
    /// own bars and its own proposals.
    ///
    /// **The markup is TaleWorlds', the data is ours.** Every widget below is copied from
    /// <c>DiplomacyPanel.xml</c>'s own stat template and proposal-button template - the same
    /// <c>FillBarHorizontalWidget</c> pair with a separator, the same <c>ButtonBrush2</c>
    /// button with its influence icon - and only the bindings are changed to our lists. That
    /// is the answer to "use the original design": not a description of it, the thing
    /// itself. See <see cref="DiplomacyItemMixinBase{T}"/> for why our own lists are needed
    /// rather than the panel's.
    ///
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
#pragma warning disable CS0618
    [PrefabExtension("DiplomacyPanel", "descendant::TextWidget[@Text='@NoItemSelectedText']/..")]
    internal sealed class DiplomacyBlockExtension : PrefabExtensionInsertPatch
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
                // IsAcceptableItemSelected lives on the panel's view model, and this block
                // binds to the selected item - asking for it here resolves against the wrong
                // object and hides everything, which is exactly what the first run did.
                + "VerticalAlignment=\"Bottom\" MarginBottom=\"128\" MarginRight=\"42\" "
                + "IsVisible=\"@DiHasHeadline\">"
                + "  <Children>"

                + "    <ListPanel DataSource=\"{DiStats}\" WidthSizePolicy=\"StretchToParent\" "
                + "       HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\" "
                + "       StackLayout.LayoutMethod=\"VerticalTopToBottom\">"
                + "      <ItemTemplate>"
                + "        <ListPanel WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" "
                + "           StackLayout.LayoutMethod=\"VerticalTopToBottom\" MarginBottom=\"6\">"
                + "          <Children>"
                + "            <TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" "
                + "               Brush=\"Kingdom.Wars.Stat.Name.Text\" Text=\"@Name\" />"
                + "            <ListPanel WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"Fixed\" "
                + "               SuggestedHeight=\"35\" HorizontalAlignment=\"Center\" "
                + "               MarginLeft=\"10\" MarginRight=\"10\">"
                + "              <Children>"
                + "                <FillBarHorizontalWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" "
                + "                   SuggestedWidth=\"350\" SuggestedHeight=\"35\" HorizontalAlignment=\"Center\" "
                + "                   Sprite=\"BlankWhiteSquare_9\" Color=\"#00000040\" "
                + "                   FillWidget=\"OurValueParent\\FillWidget\" InitialAmount=\"@OurPercentage\" "
                + "                   IsDirectionUpward=\"false\" MaxAmount=\"100\">"
                + "                  <Children>"
                + "                    <ListPanel Id=\"OurValueParent\" WidthSizePolicy=\"StretchToParent\" "
                + "                       HeightSizePolicy=\"StretchToParent\" "
                + "                       StackLayout.LayoutMethod=\"HorizontalRightToLeft\">"
                + "                      <Children>"
                + "                        <Widget Id=\"FillWidget\" WidthSizePolicy=\"Fixed\" "
                + "                           HeightSizePolicy=\"StretchToParent\" Sprite=\"BlankWhiteSquare_9\" "
                + "                           AlphaFactor=\"1\" Color=\"@OurColor\" />"
                + "                        <TextWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" "
                + "                           HorizontalAlignment=\"Right\" VerticalAlignment=\"Center\" "
                + "                           MarginRight=\"5\" MarginTop=\"5\" "
                + "                           Brush=\"Kingdom.Wars.Stat.Value.Text.Left\" "
                + "                           Brush.TextHorizontalAlignment=\"Right\" ClipContents=\"false\" "
                + "                           IntText=\"@OurValue\" />"
                + "                      </Children>"
                + "                    </ListPanel>"
                + "                    <HintWidget DataSource=\"{OurHint}\" WidthSizePolicy=\"StretchToParent\" "
                + "                       HeightSizePolicy=\"StretchToParent\" Command.HoverBegin=\"ExecuteBeginHint\" "
                + "                       Command.HoverEnd=\"ExecuteEndHint\" />"
                + "                  </Children>"
                + "                </FillBarHorizontalWidget>"
                + "                <Widget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"2\" "
                + "                   SuggestedHeight=\"35\" Sprite=\"SPKingdom\\Diplomacy\\bar_seperator\" />"
                + "                <FillBarHorizontalWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" "
                + "                   SuggestedWidth=\"350\" SuggestedHeight=\"35\" HorizontalAlignment=\"Center\" "
                + "                   Sprite=\"BlankWhiteSquare_9\" Color=\"#00000040\" "
                + "                   FillWidget=\"TheirValueParent\\FillWidget\" InitialAmount=\"@TheirPercentage\" "
                + "                   IsDirectionRightward=\"true\" MaxAmount=\"100\">"
                + "                  <Children>"
                + "                    <ListPanel Id=\"TheirValueParent\" WidthSizePolicy=\"StretchToParent\" "
                + "                       HeightSizePolicy=\"StretchToParent\" "
                + "                       StackLayout.LayoutMethod=\"HorizontalLeftToRight\">"
                + "                      <Children>"
                + "                        <Widget Id=\"FillWidget\" WidthSizePolicy=\"Fixed\" "
                + "                           HeightSizePolicy=\"StretchToParent\" Sprite=\"BlankWhiteSquare_9\" "
                + "                           AlphaFactor=\"1\" Color=\"@TheirColor\" />"
                + "                        <TextWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" "
                + "                           HorizontalAlignment=\"Left\" VerticalAlignment=\"Center\" "
                + "                           MarginLeft=\"5\" MarginTop=\"5\" "
                + "                           Brush=\"Kingdom.Wars.Stat.Value.Text.Right\" ClipContents=\"false\" "
                + "                           IntText=\"@TheirValue\" />"
                + "                      </Children>"
                + "                    </ListPanel>"
                + "                    <HintWidget DataSource=\"{TheirHint}\" WidthSizePolicy=\"StretchToParent\" "
                + "                       HeightSizePolicy=\"StretchToParent\" Command.HoverBegin=\"ExecuteBeginHint\" "
                + "                       Command.HoverEnd=\"ExecuteEndHint\" />"
                + "                  </Children>"
                + "                </FillBarHorizontalWidget>"
                + "              </Children>"
                + "            </ListPanel>"
                + "          </Children>"
                + "        </ListPanel>"
                + "      </ItemTemplate>"
                + "    </ListPanel>"

                // ---- action buttons, the panel's own proposal template ----------
                + "    <ListPanel DataSource=\"{DiActions}\" WidthSizePolicy=\"CoverChildren\" "
                + "       HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\" MarginTop=\"12\">"
                + "      <ItemTemplate>"
                + "        <ListPanel WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" "
                + "           StackLayout.LayoutMethod=\"VerticalTopToBottom\" MarginLeft=\"5\" MarginRight=\"5\" "
                + "           VerticalAlignment=\"Bottom\">"
                + "          <Children>"
                + "            <TextWidget WidthSizePolicy=\"Fixed\" SuggestedWidth=\"290\" "
                + "               HeightSizePolicy=\"CoverChildren\" Brush=\"Kingdom.ParagraphSmall.Text\" "
                + "               MarginBottom=\"8\" Text=\"@Explanation\" IsEnabled=\"@IsEnabled\" "
                + "               DoNotAcceptEvents=\"true\" />"
                + "            <Widget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"227\" "
                + "               SuggestedHeight=\"30\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Bottom\" "
                + "               MarginBottom=\"2\">"
                + "              <Children>"
                + "                <HintWidget DataSource=\"{Hint}\" WidthSizePolicy=\"StretchToParent\" "
                + "                   HeightSizePolicy=\"StretchToParent\" Command.HoverBegin=\"ExecuteBeginHint\" "
                + "                   Command.HoverEnd=\"ExecuteEndHint\" IsEnabled=\"false\" />"
                + "                <ButtonWidget DoNotPassEventsToChildren=\"true\" WidthSizePolicy=\"StretchToParent\" "
                + "                   HeightSizePolicy=\"StretchToParent\" Brush=\"ButtonBrush2\" "
                + "                   UpdateChildrenStates=\"true\" Command.Click=\"ExecuteAction\" "
                + "                   IsEnabled=\"@IsEnabled\">"
                + "                  <Children>"
                + "                    <TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" "
                + "                       Brush=\"Kingdom.GeneralButtons.Text\" Text=\"@Name\" />"
                + "                  </Children>"
                + "                </ButtonWidget>"
                + "              </Children>"
                + "            </Widget>"
                + "            <ListPanel DoNotAcceptEvents=\"true\" DoNotPassEventsToChildren=\"true\" "
                + "               WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" "
                + "               HorizontalAlignment=\"Center\" IsVisible=\"@HasInfluenceCost\">"
                + "              <Children>"
                + "                <TextWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" "
                + "                   HorizontalAlignment=\"Center\" VerticalAlignment=\"Bottom\" "
                + "                   Brush=\"Kingdom.GeneralButtons.Text\" IntText=\"@InfluenceCost\" "
                + "                   IsEnabled=\"@IsEnabled\" />"
                + "                <Widget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"17\" "
                + "                   SuggestedHeight=\"27\" HorizontalAlignment=\"Right\" VerticalAlignment=\"Bottom\" "
                + "                   Sprite=\"SPKingdom\\influence_icon_small\" />"
                + "              </Children>"
                + "            </ListPanel>"
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
    /// Shortens the game's comparison bars so the mod's block has a strip to live in.
    ///
    /// One attribute on one vanilla widget, and the smallest change that makes the layout
    /// work: the bars keep their own scroll area and simply stop higher up. If the XPath
    /// ever misses, the bars stay full height and the block overlaps them - ugly, and not
    /// fatal, which is the trade every patch in this folder is written around.
    /// </summary>
#pragma warning disable CS0618
    [PrefabExtension("DiplomacyPanel", "descendant::Widget[@IsHidden='@IsDisplayingWarLogs']")]
    internal sealed class DiplomacyBarsMarginPatch : PrefabExtensionSetAttributePatch
#pragma warning restore CS0618
    {
        public override string Id => "DiplomacyIntrigue.DiplomacyPanel.BarsMargin";

        public override string Attribute => "MarginBottom";

        /// <summary>
        /// Vanilla is 110, which clears the proposal buttons and nothing else. This has to
        /// clear our block as well, and our block is at its tallest during a war: four
        /// comparison rows and two buttons. Measured against that case on screen - at peace
        /// it leaves a gap, which is the right way round, since a gap reads as breathing
        /// space and an overlap reads as a bug.
        /// </summary>
        public override string Value => "470";
    }
}
