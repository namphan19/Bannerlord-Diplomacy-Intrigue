using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using DiplomacyIntrigue.Core;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.ClanScreen
{
    /// <summary>
    /// Gives the Clan screen a fifth tab, Intelligence (Phase 3.7), beside Members, Parties,
    /// Fiefs and Other, without touching the game's own category logic.
    ///
    /// The Clan screen's tab strip is plain buttons, not the Kingdom screen's compiled tab
    /// control: each vanilla button calls <c>SetSelectedCategory(index)</c>, which clears every
    /// category's <c>IsSelected</c>, sets one, and copies each into <c>IsMembersSelected</c> and
    /// its siblings (read from its IL, v1.4.8). It has no early return for the index already
    /// selected, so a vanilla tab clicked after ours always shows again.
    ///
    /// So the coordination runs in two directions, as the Kingdom screen's does: our tab button
    /// calls <see cref="DiIntelligenceVM.ExecuteShow"/>, which asks this mixin to clear every
    /// vanilla flag through the same public setters the game's method uses; and this mixin listens
    /// to the screen's own <c>Is...Selected</c> flags, dropping our panel the moment a vanilla one
    /// turns on. Unlike the Kingdom screen there is no per-frame method to hook as a second guard;
    /// the property-changed path is the whole mechanism, and vanilla raises it on every switch.
    ///
    /// **It is the bool event, not <c>PropertyChanged</c>.** Those setters call
    /// <c>OnPropertyChangedWithValue(bool, name)</c>, which raises only
    /// <c>PropertyChangedWithBoolValue</c> (IL, v1.4.8). The first build listened to
    /// <c>PropertyChanged</c>, heard nothing, and left Members drawn over this tab. The Kingdom
    /// screen's mixin is right to use <c>PropertyChanged</c>: <c>KingdomCategoryVM.Show</c> raises the
    /// plain event.
    /// </summary>
    [ViewModelMixin("RefreshValues")]
    internal sealed class ClanManagementVMMixin : BaseViewModelMixin<ClanManagementVM>
    {
        private readonly DiIntelligenceVM _intelligence;

        public ClanManagementVMMixin(ClanManagementVM vm) : base(vm)
        {
            _intelligence = new DiIntelligenceVM(HideVanillaCategories);
            DiIntelligence = _intelligence;
            if (vm != null) vm.PropertyChangedWithBoolValue += OnScreenFlagChanged;
        }

        private DiIntelligenceVM _intelligenceProperty;

        [DataSourceProperty]
        public DiIntelligenceVM DiIntelligence
        {
            get => _intelligenceProperty;
            private set
            {
                if (value == _intelligenceProperty) return;
                _intelligenceProperty = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiIntelligence));
            }
        }

        /// <summary>After vanilla's own refresh: keep an open Intelligence tab current.</summary>
        public override void OnRefresh()
        {
            try
            {
                if (_intelligence.Show) _intelligence.Rebuild();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Intelligence tab refresh failed.", ex);
            }
        }

        public override void OnFinalize()
        {
            try
            {
                var vm = ViewModel;
                if (vm != null) vm.PropertyChangedWithBoolValue -= OnScreenFlagChanged;
                // Do not keep a closed screen's view model reachable from a static.
                if (DiIntelligenceVM.Current == _intelligence) DiIntelligenceVM.Current = null;
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Intelligence tab cleanup failed.", ex);
            }
        }

        /// <summary>A vanilla category turning on pushes our panel out.</summary>
        private void OnScreenFlagChanged(object sender, PropertyChangedWithBoolValueEventArgs e)
        {
            try
            {
                if (!_intelligence.Show || !e.Value) return;
                switch (e.PropertyName)
                {
                    case nameof(ClanManagementVM.IsMembersSelected):
                    case nameof(ClanManagementVM.IsPartiesSelected):
                    case nameof(ClanManagementVM.IsFiefsSelected):
                    case nameof(ClanManagementVM.IsIncomeSelected):
                        _intelligence.Show = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Intelligence tab coordination failed.", ex);
            }
        }

        /// <summary>What our tab button asks for first: every vanilla panel hidden, and every vanilla tab unlit.</summary>
        private void HideVanillaCategories()
        {
            var vm = ViewModel;
            if (vm == null) return;
            if (vm.ClanMembers != null) vm.ClanMembers.IsSelected = false;
            if (vm.ClanParties != null) vm.ClanParties.IsSelected = false;
            if (vm.ClanFiefs != null) vm.ClanFiefs.IsSelected = false;
            if (vm.ClanIncome != null) vm.ClanIncome.IsSelected = false;
            vm.IsMembersSelected = false;
            vm.IsPartiesSelected = false;
            vm.IsFiefsSelected = false;
            vm.IsIncomeSelected = false;
        }
    }
}
