using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// One line under the two banners saying what this relationship is: the casus belli of
    /// a war and what the enemy's condition means, or the agreements standing between the
    /// two and who answers to whom.
    ///
    /// Markup lives in <c>module/DiplomacyIntrigue/GUI/Prefabs/KingdomManagement/Diplomacy/
    /// DiDiplomacyHeadline.xml</c> and uses the brush and size the panel uses for
    /// "For 1 day" a few pixels above it, so it reads as part of the header rather than
    /// as something bolted on.
    ///
    /// Target, as of game v1.4.8: the right pane's vertical stack, second in the stack,
    /// directly under the title container that holds the two leaders. If TaleWorlds moves
    /// it the XPath misses, UIExtenderEx logs that, and the tab renders without the line
    /// rather than not at all.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("DiplomacyPanel",
        "descendant::ListPanel[@IsVisible='@IsAcceptableItemSelected']/Children")]
    internal sealed class DiplomacyHeadlineExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;

        /// <summary>Second in the stack: directly under the title container.</summary>
        public override int Index => 1;

        private readonly XmlDocument _document = new XmlDocument();

        public DiplomacyHeadlineExtension()
        {
            _document.LoadXml("<DiDiplomacyHeadline />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }

    /// <summary>
    /// The mod's action buttons, after the game's own proposals.
    ///
    /// Markup lives in <c>module/DiplomacyIntrigue/GUI/Prefabs/KingdomManagement/Diplomacy/
    /// DiDiplomacyActions.xml</c> and copies <c>DiplomacyPanel.xml</c>'s own proposal-button
    /// template - the same <c>ButtonBrush2</c> button with its influence icon - with only
    /// the bindings changed to our <c>DiActions</c> list. Our comparison rows need no
    /// markup of their own: they go into the panel's <c>Stats</c> list as vanilla rows
    /// (see <see cref="DiplomacyItemMixinBase{T}"/>) and TaleWorlds' own template draws
    /// them.
    ///
    /// Target, as of game v1.4.8: the game's proposal list. An Append after it keeps our
    /// buttons below theirs inside the same bottom strip. If the XPath misses, the tab
    /// renders without our buttons rather than not at all.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("DiplomacyPanel", "descendant::ListPanel[@DataSource='{Actions}']")]
    internal sealed class DiplomacyActionsExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        private readonly XmlDocument _document = new XmlDocument();

        public DiplomacyActionsExtension()
        {
            _document.LoadXml("<DiDiplomacyActions />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }

    /// <summary>
    /// The "what their court would sign" chooser: one number - their court's own
    /// valuation of a pact with us - weighed against the three rungs it could buy,
    /// each with a propose button. Visible only at peace, only to the ruler.
    ///
    /// Markup lives in <c>module/DiplomacyIntrigue/GUI/Prefabs/KingdomManagement/Diplomacy/
    /// DiPactChooser.xml</c>. Third in the right pane's stack, under our headline (index 1)
    /// and the vanilla trade/alliance icons row (index 2 is before it). If the XPath
    /// misses the chooser simply does not render; the Ctrl+D diplomacy menu still
    /// proposes pacts, so the miss is loud in the log rather than silent in the UI.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("DiplomacyPanel",
        "descendant::ListPanel[@IsVisible='@IsAcceptableItemSelected']/Children")]
    internal sealed class DiplomacyPactChooserExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;

        /// <summary>After the title container (0) and our headline (1).</summary>
        public override int Index => 2;

        private readonly XmlDocument _document = new XmlDocument();

        public DiplomacyPactChooserExtension()
        {
            _document.LoadXml("<DiPactChooser />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }

    /// <summary>
    /// Shortens the game's comparison bars so the mod's block has a strip to live in.
    ///
    /// One attribute on one vanilla widget, and the smallest change that makes the layout
    /// work: the bars keep their own scroll area and simply stop higher up. If the XPath
    /// ever misses, the bars stay full height and the block overlaps them - ugly, and not
    /// fatal, which is the trade every patch in this folder is written around.
    /// </summary>
    [PrefabExtension("DiplomacyPanel", "descendant::Widget[@IsHidden='@IsDisplayingWarLogs']")]
    internal sealed class DiplomacyBarsMarginPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            // Vanilla is 110, which clears the proposal buttons and nothing else. This has
            // to clear our grid as well: two rows of about 110 each (explanation, button,
            // influence cost) plus the strip's bottom margin - the worst case is a peace
            // row, which can carry six or seven actions once pacts moved to the chooser.
            // The war case gets the same margin and shows a gap; a gap reads as breathing
            // space and an overlap reads as a bug, so the margin is sized for peace.
            new PrefabExtensionSetAttributePatch.Attribute("MarginBottom", "250"),
        };
    }

    /// <summary>
    /// Empties the game's own proposal row while the mod runs diplomacy.
    ///
    /// See <see cref="KingdomDiplomacyVMMixin"/> for why this is a DataSource swap and
    /// not an IsVisible flag: a binding on the <c>{Actions}</c> ListPanel resolves
    /// against the Actions list, not the panel VM, so the flag was never found and the
    /// row rendered anyway - on top of ours. Pointing its DataSource at
    /// <c>DiVanillaActions</c> feeds it the real list when the mod is off and an empty
    /// one when it runs, so flipping the mod off in MCM brings vanilla's row back.
    /// </summary>
    [PrefabExtension("DiplomacyPanel", "descendant::ListPanel[@DataSource='{Actions}']")]
    internal sealed class DiplomacyVanillaActionsVisibilityPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            new PrefabExtensionSetAttributePatch.Attribute("DataSource", "{DiVanillaActions}"),
        };
    }
}
