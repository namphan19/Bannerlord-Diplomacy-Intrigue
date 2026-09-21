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
    /// the same strip our own buttons are appended to. The strip's parent is a plain
    /// Widget, not a stack, so the two rows sit on top of each other.
    ///
    /// Hiding that row cannot be done with <c>IsVisible</c>: the row is the
    /// <c>{Actions}</c> ListPanel, and a binding on that widget resolves against the
    /// Actions list itself - not the panel VM - so a flag on this mixin is never found
    /// and the row stays visible. What CAN be bound on it is <c>DataSource</c>, which
    /// resolves on the inherited panel context. So the patch points the row at this
    /// property: the real <c>Actions</c> while the mod is off or failed to start, an
    /// empty list while it runs - an empty list renders nothing, which hides the row
    /// without touching visibility at all.
    ///
    /// Deliberately selection-independent: it reads only the mod's health and setting,
    /// so it cannot go stale when the selected item changes.
    /// </summary>
    [ViewModelMixin]
    internal sealed class KingdomDiplomacyVMMixin : BaseViewModelMixin<KingdomDiplomacyVM>
    {
        private static readonly MBBindingList<KingdomDiplomacyProposalActionItemVM> NoActions =
            new MBBindingList<KingdomDiplomacyProposalActionItemVM>();

        private MBBindingList<KingdomDiplomacyProposalActionItemVM> _vanillaActions = NoActions;

        public KingdomDiplomacyVMMixin(KingdomDiplomacyVM vm) : base(vm)
        {
            OnRefresh();
        }

        [DataSourceProperty]
        public MBBindingList<KingdomDiplomacyProposalActionItemVM> DiVanillaActions
        {
            get => _vanillaActions;
            set => SetField(ref _vanillaActions, value, nameof(DiVanillaActions));
        }

        public override void OnRefresh()
        {
            try
            {
                var vanilla = !SubModule.Healthy || !Settings.Current.EnableDiplomacy;
                DiVanillaActions = vanilla && ViewModel?.Actions != null
                    ? ViewModel.Actions
                    : NoActions;
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Diplomacy panel switch failed; vanilla proposals stay.", ex);
                DiVanillaActions = ViewModel?.Actions ?? NoActions;
            }
        }
    }
}
