using System;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using DiplomacyIntrigue.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.EncyclopediaPages
{
    /// <summary>
    /// Gives a kingdom's Encyclopedia page its court section (<see cref="DiEncyclopediaCourtVM"/>).
    ///
    /// Hooked on <c>RefreshValues</c>, the way UI-INTEGRATION.md §5.3 recommends. The page
    /// calls RefreshValues from its own constructor, before this mixin exists, so the
    /// section is composed once here in the constructor and again on every later refresh.
    /// A page VM is rebuilt on every visit, so a court never goes stale while you read it.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [ViewModelMixin(nameof(EncyclopediaFactionPageVM.RefreshValues))]
    internal sealed class EncyclopediaFactionPageVMMixin : BaseViewModelMixin<EncyclopediaFactionPageVM>
    {
        private DiEncyclopediaCourtVM _court;

        public EncyclopediaFactionPageVMMixin(EncyclopediaFactionPageVM vm) : base(vm)
        {
            try
            {
                DiEncyclopediaCourt = new DiEncyclopediaCourtVM(vm?.Obj as Kingdom);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "The Encyclopedia court section could not be created.", ex);
            }
        }

        public override void OnRefresh()
        {
            try
            {
                _court?.Rebuild();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "The Encyclopedia court section could not be refreshed.", ex);
            }
        }

        [DataSourceProperty]
        public DiEncyclopediaCourtVM DiEncyclopediaCourt
        {
            get => _court;
            private set
            {
                if (value == _court) return;
                _court = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiEncyclopediaCourt));
            }
        }
    }

    /// <summary>
    /// The court section, inserted right after the ruler on a kingdom's Encyclopedia page and
    /// before its clans: the ruler, then the court, then the houses' banners.
    ///
    /// Anchored on the leader element rather than on a later divider so the section stays
    /// beside the ruler whatever other mods append to the bottom of the page. If the XPath
    /// misses, the page renders exactly as vanilla. Markup lives in
    /// <c>module/DiplomacyIntrigue/GUI/Prefabs/Encyclopedia/DiEncyclopediaCourt.xml</c>.
    /// Verified against game v1.4.8, UIExtenderEx v2.13.2.
    /// </summary>
    [PrefabExtension("EncyclopediaFactionPage", "descendant::EncyclopediaSubPageElement[@Id='Leader']")]
    internal sealed class EncyclopediaCourtSectionExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        private readonly XmlDocument _document = new XmlDocument();

        public EncyclopediaCourtSectionExtension()
        {
            _document.LoadXml("<DiEncyclopediaCourt />");
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }
}
