using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// The Realm tab button, appended after Diplomacy in the header's tab strip.
    ///
    /// The strip is a compiled <c>KingdomTabControlListPanel</c> that only knows the
    /// five vanilla button/panel pairs, so the button manages itself: its click calls
    /// <see cref="DiRealmVM.ExecuteShow"/> on our own view model, which hides the five
    /// vanilla categories, and its selected state binds to the same <c>Show</c> flag
    /// the vanilla panels bind their visibility to. Markup lives in
    /// <c>module/DiplomacyIntrigue/GUI/Prefabs/KingdomManagement/DiRealmTabButton.xml</c>.
    /// If the XPath misses, the screen renders with five tabs exactly as vanilla.
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
            _document.LoadXml("<DiRealmTabButton />");
        }

        [PrefabExtensionXmlDocument]
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
