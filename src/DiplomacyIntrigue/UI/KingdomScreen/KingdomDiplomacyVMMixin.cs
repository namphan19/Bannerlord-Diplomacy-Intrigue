using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using DiplomacyIntrigue.Core;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// Panel-level switch for the Diplomacy tab's proposal strip.
    ///
    /// When this mod runs inter-kingdom diplomacy, every vanilla proposal in the bottom
    /// strip is refused by our models (peace, alliances, trade agreements, call-to-war -
    /// see docs/design/05-vanilla-override.md) and renders as a dead disabled button in
    /// the same strip our own buttons are appended to. The pane root lays its children
    /// out by alignment rather than flow, so the two rows sit on top of each other.
    /// Hiding the dead row while the mod is on is honest; when the mod is off or failed
    /// to start, vanilla's row shows again and nothing is lost.
    ///
    /// Deliberately selection-independent: it reads only the mod's health and setting,
    /// so it cannot go stale when the selected item changes.
    /// </summary>
    [ViewModelMixin]
    internal sealed class KingdomDiplomacyVMMixin : BaseViewModelMixin<KingdomDiplomacyVM>
    {
        private bool _showVanillaProposals = true;

        public KingdomDiplomacyVMMixin(KingdomDiplomacyVM vm) : base(vm)
        {
            OnRefresh();
        }

        [DataSourceProperty]
        public bool DiShowVanillaProposals
        {
            get => _showVanillaProposals;
            set => SetField(ref _showVanillaProposals, value, nameof(DiShowVanillaProposals));
        }

        public override void OnRefresh()
        {
            try
            {
                DiShowVanillaProposals = !SubModule.Healthy || !Settings.Current.EnableDiplomacy;
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Diplomacy panel switch failed; vanilla proposals stay.", ex);
                DiShowVanillaProposals = true;
            }
        }
    }
}
