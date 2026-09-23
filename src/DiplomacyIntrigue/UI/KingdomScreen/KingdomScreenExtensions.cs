using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// Our two tab buttons - Realm, then Court (Phase 2.7) - appended after Diplomacy in the
    /// header's tab strip.
    ///
    /// **One patch inserts both, on purpose.** Court must come after Realm, because the last
    /// tab wears the rounded end cap and Realm now wears the centre art. Two separate Append
    /// patches on the same anchor would leave that order to whatever sequence UIExtenderEx
    /// happens to apply them in. A single document with a throwaway root, loaded with
    /// removeRootNode, inserts its children in document order.
    ///
    /// The strip is a compiled <c>KingdomTabControlListPanel</c> that only knows the five
    /// vanilla button/panel pairs, so each button manages itself: its click calls
    /// <c>ExecuteShow</c> on our own view model, which hides every other panel, and its
    /// selected state binds to the same <c>Show</c> flag its panel binds its visibility to.
    /// Markup lives in <c>module/DiplomacyIntrigue/GUI/Prefabs/KingdomManagement/</c>. If the
    /// XPath misses, the screen renders with five tabs exactly as vanilla.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("KingdomManagement",
        "descendant::KingdomTabControlListPanel/Children/ButtonWidget[@Id='DiplomacyTabButton']")]
    internal sealed class RealmTabButtonExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        private readonly XmlDocument _document = new XmlDocument();

        public RealmTabButtonExtension()
        {
            _document.LoadXml("<DiTabs><DiRealmTabButton /><DiCourtTabButton /></DiTabs>");
        }

        [PrefabExtensionXmlDocument(true)]
        public XmlDocument GetPrefabExtension() => _document;
    }

    /// <summary>
    /// Moves the Diplomacy tab's end-cap art onto ours. The last button in the strip
    /// wears <c>Header.Tab.Right</c> (the rounded right end) and the ones before it
    /// wear <c>Header.Tab.Center</c>; with a sixth tab appended, Diplomacy is no
    /// longer the last, so it takes the centre brush instead.
    /// </summary>
    [PrefabExtension("KingdomManagement",
        "descendant::KingdomTabControlListPanel/Children/ButtonWidget[@Id='DiplomacyTabButton']")]
    internal sealed class DiplomacyTabBrushPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            // The centre art is narrower than the end cap and sits 2px lower, so the
            // size constants and the offset move with the brush, the way Fiefs,
            // Policies and Armies already declare them.
            new PrefabExtensionSetAttributePatch.Attribute("Brush", "Header.Tab.Center"),
            new PrefabExtensionSetAttributePatch.Attribute("SuggestedWidth", "!Header.Tab.Center.Width.Scaled"),
            new PrefabExtensionSetAttributePatch.Attribute("SuggestedHeight", "!Header.Tab.Center.Height.Scaled"),
            new PrefabExtensionSetAttributePatch.Attribute("PositionYOffset", "2"),
        };
    }

    /// <summary>
    /// The label's -10 offset compensates for the end cap's art; on a centre tab it
    /// would just sit the word ten pixels left, so it goes back to zero.
    /// </summary>
    [PrefabExtension("KingdomManagement",
        "descendant::KingdomTabControlListPanel/Children/ButtonWidget[@Id='DiplomacyTabButton']/Children/TextWidget")]
    internal sealed class DiplomacyTabTextPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            new PrefabExtensionSetAttributePatch.Attribute("PositionXOffset", "0"),
        };
    }

    /// <summary>
    /// The Court panel (Phase 2.7), a seventh sibling in the same slot as the five vanilla
    /// panels and the Realm panel.
    ///
    /// A separate patch from the Realm panel's, unlike the buttons: panels overlap in one
    /// slot and only one is ever visible, so the order they are inserted in changes nothing.
    /// Margins match the vanilla panels exactly. Data context is our <c>DiCourt</c> view
    /// model; visibility is its <c>Show</c> flag, traded with every other panel by
    /// <see cref="KingdomManagementVMMixin"/>. Markup lives in
    /// <c>module/DiplomacyIntrigue/GUI/Prefabs/KingdomManagement/DiCourtPanel.xml</c>.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("KingdomManagement", "descendant::DiplomacyPanel")]
    internal sealed class CourtPanelExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        private readonly XmlDocument _document = new XmlDocument();

        public CourtPanelExtension()
        {
            _document.LoadXml("<DiCourtPanel Id=\"DiCourtPanel\" DataSource=\"{DiCourt}\""
                              + " MarginTop=\"188\" MarginBottom=\"75\" />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }

    /// <summary>
    /// Narrows vanilla's five tabs so seven fit. Vanilla scales every tab's art to 0.90; at
    /// that size six tabs already reached the leader portrait's caption, and seven would run
    /// into it. 0.70 is the same scale our own two buttons declare in their prefabs, so all
    /// seven stay the same width. The Diplomacy mod does the same thing (0.60 for six tabs).
    /// Height is left at 0.90: the art only needs to be narrower, not shorter.
    /// </summary>
    [PrefabExtension("KingdomManagement", "descendant::Constant[@Name='Header.Tab.Left.Width.Scaled']")]
    internal sealed class TabLeftWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            new PrefabExtensionSetAttributePatch.Attribute("MultiplyResult", "0.70"),
        };
    }

    /// <summary>See <see cref="TabLeftWidthPatch"/>.</summary>
    [PrefabExtension("KingdomManagement", "descendant::Constant[@Name='Header.Tab.Center.Width.Scaled']")]
    internal sealed class TabCenterWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            new PrefabExtensionSetAttributePatch.Attribute("MultiplyResult", "0.70"),
        };
    }

    /// <summary>See <see cref="TabLeftWidthPatch"/>.</summary>
    [PrefabExtension("KingdomManagement", "descendant::Constant[@Name='Header.Tab.Right.Width.Scaled']")]
    internal sealed class TabRightWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            new PrefabExtensionSetAttributePatch.Attribute("MultiplyResult", "0.70"),
        };
    }

    /// <summary>
    /// The Realm panel itself, a sixth sibling beside the five vanilla panels.
    ///
    /// Inserted directly after <c>DiplomacyPanel</c> so it occupies the same slot
    /// (the margins come from the instance attributes here, matching the vanilla
    /// panels exactly). Its data context is our <c>DiRealm</c> view model rather
    /// than a vanilla category VM, and visibility is our <c>Show</c> flag - see
    /// <see cref="KingdomManagementVMMixin"/> for how it trades the area with the
    /// five vanilla categories. Markup lives in
    /// <c>module/DiplomacyIntrigue/GUI/Prefabs/KingdomManagement/DiRealmPanel.xml</c>.
    /// If the XPath misses, the screen renders exactly as vanilla.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("KingdomManagement", "descendant::DiplomacyPanel")]
    internal sealed class RealmPanelExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        private readonly XmlDocument _document = new XmlDocument();

        public RealmPanelExtension()
        {
            _document.LoadXml("<DiRealmPanel Id=\"DiRealmPanel\" DataSource=\"{DiRealm}\""
                              + " MarginTop=\"188\" MarginBottom=\"75\" />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }
}
