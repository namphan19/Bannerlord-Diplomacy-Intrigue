using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using DiplomacyIntrigue.Core;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// Gives the Kingdom screen two tabs of our own - Realm (sixth) and Court (seventh,
    /// Phase 2.7) - without touching the game's own category logic.
    ///
    /// Vanilla's tab switch is private (<c>SetSelectedCategory</c> flips <c>Show</c> on
    /// the five category VMs), so the coordination runs the other way: our tab button
    /// calls <see cref="DiRealmVM.ExecuteShow"/>, which asks this mixin to clear every
    /// vanilla <c>Show</c> itself - the same setters the game's method calls - and in
    /// the other direction the mixin listens for a vanilla category turning visible
    /// and drops <see cref="DiRealmVM.Show"/>. The compiled tab control keeps working
    /// untouched: it reads each vanilla panel's visibility to light its button, and
    /// our button's selected state binds to <c>Show</c> the same way the panels do.
    ///
    /// The belt behind the braces is <c>OnFrameTick</c>: hooked as the refresh method,
    /// it re-checks the same overlap every frame, so a missed PropertyChanged leaves a
    /// one-frame seam rather than two panels on top of each other.
    /// </summary>
    [ViewModelMixin("OnFrameTick")]
    internal sealed class KingdomManagementVMMixin : BaseViewModelMixin<KingdomManagementVM>
    {
        private readonly DiRealmVM _realm;
        private readonly DiCourtVM _court;
        private readonly List<KingdomCategoryVM> _categories = new List<KingdomCategoryVM>();

        public KingdomManagementVMMixin(KingdomManagementVM vm) : base(vm)
        {
            // Each of our tabs clears everything else when it opens: the five vanilla
            // categories AND the other one of ours. The lambdas read the fields at call time,
            // so the order these two are constructed in does not matter.
            // The Realm tab's civil-war row opens the Court tab, where that war is shown (design 07 §6).
            _realm = new DiRealmVM(() => { HideVanillaCategories(); if (_court != null) _court.Show = false; },
                () => _court?.ExecuteShow());
            _court = new DiCourtVM(() => { HideVanillaCategories(); if (_realm != null) _realm.Show = false; });
            DiRealm = _realm;
            DiCourt = _court;

            if (vm != null)
            {
                Subscribe(vm.Clan);
                Subscribe(vm.Settlement);
                Subscribe(vm.Policy);
                Subscribe(vm.Army);
                Subscribe(vm.Diplomacy);
            }
        }

        private DiRealmVM _realmProperty;

        [DataSourceProperty]
        public DiRealmVM DiRealm
        {
            get => _realmProperty;
            private set
            {
                if (value == _realmProperty) return;
                _realmProperty = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiRealm));
            }
        }

        private DiCourtVM _courtProperty;

        [DataSourceProperty]
        public DiCourtVM DiCourt
        {
            get => _courtProperty;
            private set
            {
                if (value == _courtProperty) return;
                _courtProperty = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiCourt));
            }
        }

        /// <summary>Runs after every <c>OnFrameTick</c>: no two panels may be visible at once.</summary>
        public override void OnRefresh()
        {
            try
            {
                if (!_realm.Show && !_court.Show) return;
                var vm = ViewModel;
                if (vm == null) return;
                if (Shown(vm.Clan) || Shown(vm.Settlement) || Shown(vm.Policy)
                    || Shown(vm.Army) || Shown(vm.Diplomacy))
                {
                    _realm.Show = false;
                    _court.Show = false;
                }
                // Both of ours at once can only happen if a click raced a frame; the one the
                // player opened last wins, which is Court only if Realm was not just opened.
                else if (_realm.Show && _court.Show)
                {
                    _realm.Show = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Realm tab coordination failed.", ex);
            }
        }

        public override void OnFinalize()
        {
            try
            {
                for (var i = 0; i < _categories.Count; i++)
                    _categories[i].PropertyChanged -= OnCategoryPropertyChanged;
                _categories.Clear();

                // Do not keep a closed screen's view model reachable from a static.
                if (DiCourtVM.Current == _court) DiCourtVM.Current = null;
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Realm tab cleanup failed.", ex);
            }
        }

        private void Subscribe(KingdomCategoryVM category)
        {
            if (category == null) return;
            _categories.Add(category);
            category.PropertyChanged += OnCategoryPropertyChanged;
        }

        /// <summary>A vanilla category becoming visible pushes our panel out.</summary>
        private void OnCategoryPropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName != "Show" || (!_realm.Show && !_court.Show)) return;
                if (sender is KingdomCategoryVM category && category.Show)
                {
                    _realm.Show = false;
                    _court.Show = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Realm tab coordination failed.", ex);
            }
        }

        /// <summary>What either of our tab buttons asks for first: every vanilla panel hidden.</summary>
        private void HideVanillaCategories()
        {
            var vm = ViewModel;
            if (vm == null) return;
            if (vm.Clan != null) vm.Clan.Show = false;
            if (vm.Settlement != null) vm.Settlement.Show = false;
            if (vm.Policy != null) vm.Policy.Show = false;
            if (vm.Army != null) vm.Army.Show = false;
            if (vm.Diplomacy != null) vm.Diplomacy.Show = false;
        }

        private static bool Shown(KingdomCategoryVM category)
            => category != null && category.Show;
    }
}
