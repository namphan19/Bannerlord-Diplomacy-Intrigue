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
            // to clear our block as well: one button row of about 120 (a short explanation,
            // the button, the influence cost) plus the strip's own margins. Measured
            // against the war case on screen - at peace it leaves a gap, which is the
            // right way round, since a gap reads as breathing space and an overlap reads
            // as a bug.
            new PrefabExtensionSetAttributePatch.Attribute("MarginBottom", "160"),
        };
    }

    /// <summary>
    /// Hides the game's own proposal row while the mod runs diplomacy.
    ///
    /// See <see cref="KingdomDiplomacyVMMixin"/> for why: every vanilla proposal is
    /// refused by our models and renders as a dead button on top of our own row. The
    /// value is a binding, not a constant - Gauntlet reads "@..." against the panel VM,
    /// where the mixin lives - so flipping the mod off in MCM brings vanilla's row back
    /// on the next screen open.
    /// </summary>
    [PrefabExtension("DiplomacyPanel", "descendant::ListPanel[@DataSource='{Actions}']")]
    internal sealed class DiplomacyVanillaActionsVisibilityPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            new PrefabExtensionSetAttributePatch.Attribute("IsVisible", "@DiShowVanillaProposals"),
        };
    }
}
