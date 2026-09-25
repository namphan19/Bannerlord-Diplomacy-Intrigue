using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace DiplomacyIntrigue.UI.ClanScreen
{
    /// <summary>
    /// The Intelligence tab button (Phase 3.7), appended after vanilla's fourth tab ("Other",
    /// the income category) in the Clan screen's header.
    ///
    /// Vanilla gives its tab buttons no Id, so the anchor is the command parameter that makes the
    /// fourth one the fourth: <c>SetSelectedCategory</c> with 3. The button manages itself - its
    /// click calls <c>ExecuteShow</c> on our view model and its selected state binds to the same
    /// <c>Show</c> flag the panel binds its visibility to (see <see cref="ClanManagementVMMixin"/>).
    /// If the XPath misses, the screen renders with its four tabs exactly as vanilla.
    /// Verified against game v1.4.8 (Sandbox/GUI/Prefabs/Clan/ClanScreen.xml), UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("ClanScreen", "descendant::ButtonWidget[@CommandParameter.Click='3']")]
    internal sealed class IntelligenceTabButtonExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        private readonly XmlDocument _document = new XmlDocument();

        public IntelligenceTabButtonExtension()
        {
            _document.LoadXml("<DiIntelTabButton />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }

    /// <summary>
    /// The fourth tab is no longer the last, so it gives the rounded end cap to ours and takes the
    /// centre art, with the size constants and the 6px drop the Parties and Fiefs tabs declare.
    /// </summary>
    [PrefabExtension("ClanScreen", "descendant::ButtonWidget[@CommandParameter.Click='3']")]
    internal sealed class IncomeTabBrushPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            new PrefabExtensionSetAttributePatch.Attribute("Brush", "Header.Tab.Center"),
            new PrefabExtensionSetAttributePatch.Attribute("SuggestedWidth", "!Header.Tab.Center.Width.Scaled"),
            new PrefabExtensionSetAttributePatch.Attribute("SuggestedHeight", "!Header.Tab.Center.Height.Scaled"),
            new PrefabExtensionSetAttributePatch.Attribute("PositionYOffset", "6"),
        };
    }

    /// <summary>
    /// The label's -10 offset and 3px top margin belong to the end cap's art; on a centre tab they
    /// would push the word off centre, so they go back to what the centre tabs use.
    /// </summary>
    [PrefabExtension("ClanScreen", "descendant::ButtonWidget[@CommandParameter.Click='3']/Children/TextWidget")]
    internal sealed class IncomeTabTextPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PrefabExtensionSetAttributePatch.Attribute> Attributes => new List<PrefabExtensionSetAttributePatch.Attribute>
        {
            new PrefabExtensionSetAttributePatch.Attribute("PositionXOffset", "0"),
            new PrefabExtensionSetAttributePatch.Attribute("MarginTop", "0"),
        };
    }

    /// <summary>
    /// The Intelligence panel, a fifth sibling after the four vanilla panels in the screen's lower
    /// half. That parent already carries the vanilla panels' margins, so the panel needs none of its
    /// own. Data context is our <c>DiIntelligence</c> view model; visibility is its <c>Show</c> flag.
    /// Markup lives in <c>module/DiplomacyIntrigue/GUI/Prefabs/ClanScreen/DiIntelPanel.xml</c>.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("ClanScreen", "descendant::ClanIncome")]
    internal sealed class IntelligencePanelExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        private readonly XmlDocument _document = new XmlDocument();

        public IntelligencePanelExtension()
        {
            _document.LoadXml("<DiIntelPanel Id=\"DiIntelPanel\" DataSource=\"{DiIntelligence}\" />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }
}
