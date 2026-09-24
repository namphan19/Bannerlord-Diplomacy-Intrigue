using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// Panel-level work on the Diplomacy tab: the proposal strip, and keeping a civil war's
    /// rising out of the list of wars.
    ///
    /// **The proposal strip.** When this mod runs inter-kingdom diplomacy, every vanilla
    /// proposal in the bottom strip is refused by our models (peace, alliances, trade agreements,
    /// call-to-war - see docs/design/05-vanilla-override.md) and renders as a dead disabled button
    /// in the same strip our own buttons are appended to. The strip's parent is a plain Widget,
    /// not a stack, so the two rows sit on top of each other.
    ///
    /// Hiding that row cannot be done with <c>IsVisible</c>: the row is the <c>{Actions}</c>
    /// ListPanel, and a binding on that widget resolves against the Actions list itself - not the
    /// panel VM - so a flag on this mixin is never found and the row stays visible. What CAN be
    /// bound on it is <c>DataSource</c>, which resolves on the inherited panel context. So the
    /// patch points the row at this property: the real <c>Actions</c> while the mod is off or
    /// failed to start, an empty list while it runs - an empty list renders nothing, which hides
    /// the row without touching visibility at all.
    ///
    /// **The rising (design 07 §6).** A civil war's rebels fight under a real <c>Kingdom</c>
    /// at war with the realm, and vanilla builds this tab's war list from
    /// <c>_playerKingdom.FactionsAtWarWith</c>, keeping every entry whose two sides are kingdoms
    /// (IL of <c>RefreshDiplomacyList</c>, v1.4.8). So the rising was listed as an enemy kingdom
    /// with nothing to negotiate - its war ends by the rules of design 07, never at a table - and
    /// for a player among the rebels it was their own side. It is removed after each rebuild of
    /// the list; the civil war is shown on the Court tab instead.
    ///
    /// Hooked on <c>RefreshDiplomacyList</c>, not <c>RefreshValues</c>: the list is rebuilt there,
    /// including after every war and peace, and <c>RefreshValues</c> does not call it (IL). The
    /// proposal switch reads only the mod's health and setting, so it is equally right on either
    /// hook and cannot go stale when the selected item changes.
    /// </summary>
    [ViewModelMixin("RefreshDiplomacyList")]
    internal sealed class KingdomDiplomacyVMMixin : BaseViewModelMixin<KingdomDiplomacyVM>
    {
        private static readonly MBBindingList<KingdomDiplomacyProposalActionItemVM> NoActions =
            new MBBindingList<KingdomDiplomacyProposalActionItemVM>();

        /// <summary>Vanilla's own "select the first war, else the first truce" (private, IL-verified).</summary>
        private static readonly MethodInfo SetDefaultSelectedItem =
            typeof(KingdomDiplomacyVM).GetMethod("SetDefaultSelectedItem", BindingFlags.Instance | BindingFlags.NonPublic);

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

            // Its own try: a failure here must not undo the proposal switch above.
            try
            {
                if (SubModule.Healthy) RemoveRisings();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Keeping the rising out of the Diplomacy tab failed; it stays listed.", ex);
            }
        }

        private void RemoveRisings()
        {
            var vm = ViewModel;
            if (vm == null) return;

            var removed = RemoveFrom(vm.PlayerWars) | RemoveFrom(vm.PlayerTruces);
            if (!removed) return;

            // The counts beside the two headings, as vanilla writes them.
            GameTexts.SetVariable("STR", vm.PlayerWars.Count);
            vm.NumOfPlayerWarsText = GameTexts.FindText("str_STR_in_parentheses").ToString();
            GameTexts.SetVariable("STR", vm.PlayerTruces.Count);
            vm.NumOfPlayerTrucesText = GameTexts.FindText("str_STR_in_parentheses").ToString();

            // Vanilla selected its default before we removed anything, so that may be the rising.
            var selected = vm.CurrentSelectedDiplomacyItem;
            if (selected != null && (IsRising(selected.Faction1) || IsRising(selected.Faction2)))
                SetDefaultSelectedItem?.Invoke(vm, null);
        }

        private static bool RemoveFrom<T>(MBBindingList<T> list) where T : KingdomDiplomacyItemVM
        {
            if (list == null) return false;
            var removed = false;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var item = list[i];
                if (item == null || (!IsRising(item.Faction1) && !IsRising(item.Faction2))) continue;
                list.RemoveAt(i);
                removed = true;
            }
            return removed;
        }

        private static bool IsRising(IFaction faction) => faction is Kingdom kingdom && InternalWars.IsFaction(kingdom);
    }
}
