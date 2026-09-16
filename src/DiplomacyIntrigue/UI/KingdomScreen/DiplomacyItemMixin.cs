using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// Everything the mod knows about the other kingdom, attached to the game's own row and
    /// detail pane in the Kingdom screen's Diplomacy tab.
    ///
    /// Mixed into <see cref="KingdomDiplomacyItemVM"/> with derived types included, so one
    /// class serves both a war row and a peace row - the game uses two subclasses for those
    /// and the mod has something to say about either.
    ///
    /// **Our exhaustion exactly, theirs as a band.** The design review's decision (spec 01
    /// §8), and it is not relaxed because this surface is nicer than a text menu: a rival's
    /// precise figure is what Phase 3's ReadCourt mission is for.
    /// </summary>
    [ViewModelMixin("RefreshValues", true)]
    internal sealed class DiplomacyItemMixin : BaseViewModelMixin<KingdomDiplomacyItemVM>
    {
        private string _rowSummary = string.Empty;
        private MBBindingList<DiplomacyLineVM> _lines = new MBBindingList<DiplomacyLineVM>();

        public DiplomacyItemMixin(KingdomDiplomacyItemVM vm) : base(vm)
        {
            Rebuild();
        }

        // ----- the list row ---------------------------------------------------

        /// <summary>
        /// The compact form for the war list: a band bar and the score, and nothing more.
        /// It shares the row with the kingdom's name, so it is deliberately short - the
        /// first attempt put the whole summary here and squeezed "Khuzait" into a column
        /// one letter wide.
        /// </summary>
        [DataSourceProperty]
        public string DiRowSummary
        {
            get => _rowSummary;
            set
            {
                if (value == _rowSummary) return;
                _rowSummary = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiRowSummary));
            }
        }

        [DataSourceProperty]
        public bool DiHasRowSummary => !string.IsNullOrEmpty(_rowSummary);

        // ----- the detail pane ------------------------------------------------

        [DataSourceProperty]
        public MBBindingList<DiplomacyLineVM> DiDetailLines
        {
            get => _lines;
            set
            {
                if (value == _lines) return;
                _lines = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiDetailLines));
            }
        }

        [DataSourceProperty]
        public bool DiHasDetail => _lines != null && _lines.Count > 0;

        [DataSourceProperty]
        public string DiDetailTitle => "DIPLOMACY & INTRIGUE";

        public override void OnRefresh() => Rebuild();

        // ----- building -------------------------------------------------------

        private void Rebuild()
        {
            // A view model must never take the game down: a throw here would come out of
            // Gauntlet's refresh, not out of our code. Anything unexpected leaves the tab
            // exactly as vanilla drew it.
            try
            {
                Compose();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Diplomacy tab block failed; the tab stays vanilla.", ex);
                DiRowSummary = string.Empty;
                DiDetailLines = new MBBindingList<DiplomacyLineVM>();
            }
        }

        private void Compose()
        {
            DiRowSummary = string.Empty;
            var lines = new MBBindingList<DiplomacyLineVM>();

            if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy)
            {
                DiDetailLines = lines;
                return;
            }

            var state = CoreBehavior.State;
            if (state == null)
            {
                DiDetailLines = lines;
                return;
            }

            var us = ViewModel?.Faction1 as Kingdom;
            var them = ViewModel?.Faction2 as Kingdom;
            if (us == null || them == null)
            {
                DiDetailLines = lines;
                return;
            }

            // Which side of the pair we are on is not guaranteed by the list, so it is
            // resolved against the player's own kingdom rather than assumed.
            var player = Clan.PlayerClan?.Kingdom;
            if (player != null && them == player)
            {
                var swap = us;
                us = them;
                them = swap;
            }

            AddWarLines(state, us, them, lines);
            AddTreatyLine(state, us, them, lines);
            AddTrustLine(state, us, them, lines);
            AddClaimLines(state, us, them, lines);
            AddHegemonyLines(state, us, them, lines);

            DiDetailLines = lines;
        }

        private void AddWarLines(ModState state, Kingdom us, Kingdom them, ICollection<DiplomacyLineVM> lines)
        {
            var war = state.OngoingWarBetween(us, them);
            if (war == null) return;

            var ours = war.ExhaustionOf(us);
            var theirs = war.ExhaustionOf(them);
            var band = ExhaustionBands.Of(theirs);
            var score = war.ScoreFor(us);
            var signedScore = (score >= 0f ? "+" : string.Empty) + score.ToString("0.0");

            DiRowSummary = ExhaustionBands.Bar(band) + "  " + signedScore;

            lines.Add(new DiplomacyLineVM("Casus belli",
                war.Justification + "   legitimacy " + CasusBelli.Legitimacy(war.Justification).ToString("0.00")));
            lines.Add(new DiplomacyLineVM("War exhaustion",
                "ours " + ours.ToString("0.0") + "   theirs " + ExhaustionBands.Bar(band)
                + " " + ExhaustionBands.Name(band)));
            lines.Add(new DiplomacyLineVM("What that means", ExhaustionBands.Meaning(band)));
            lines.Add(new DiplomacyLineVM("War score",
                signedScore + "   after " + war.DaysElapsed.ToString("0") + " days"));

            if (war.IsObligationWar)
                lines.Add(new DiplomacyLineVM("Called in by", war.CalledBy?.Name?.ToString() ?? "an ally"));
        }

        private void AddTreatyLine(ModState state, Kingdom us, Kingdom them, ICollection<DiplomacyLineVM> lines)
        {
            foreach (var treaty in state.ActiveTreatiesOf(us))
            {
                if (!treaty.IsBetween(us, them)) continue;

                var detail = treaty.Type.ToString();
                if (treaty.TributeAmount > 0 && treaty.TributePayer != null)
                    detail += "   tribute " + treaty.TributeAmount + " from " + treaty.TributePayer.Name;

                lines.Add(new DiplomacyLineVM("Agreement", detail));
            }
        }

        private void AddTrustLine(ModState state, Kingdom us, Kingdom them, ICollection<DiplomacyLineVM> lines)
        {
            var toThem = TrustRegistry.Get(state, us, them);
            var toUs = TrustRegistry.Get(state, them, us);
            lines.Add(new DiplomacyLineVM("Trust",
                "we trust them " + Signed(toThem) + "   they trust us " + Signed(toUs)));
        }

        private void AddClaimLines(ModState state, Kingdom us, Kingdom them, ICollection<DiplomacyLineVM> lines)
        {
            var ourClaim = ClaimRegistry.Best(state, us, them);
            var theirClaim = ClaimRegistry.Best(state, them, us);
            if (ourClaim == null && theirClaim == null) return;

            lines.Add(new DiplomacyLineVM("Claims",
                Describe("we hold", ourClaim) + "   " + Describe("they hold", theirClaim)));
        }

        private void AddHegemonyLines(ModState state, Kingdom us, Kingdom them, ICollection<DiplomacyLineVM> lines)
        {
            var theirPatron = Hegemony.PatronOf(state, them);
            if (theirPatron != null)
            {
                var link = Hegemony.VassalageOf(state, them);
                lines.Add(new DiplomacyLineVM("They answer to",
                    theirPatron.Name + "   hold " + Hegemony.HoldOf(link).ToString("0.0")));
            }

            var ourPatron = Hegemony.PatronOf(state, us);
            if (ourPatron != null)
                lines.Add(new DiplomacyLineVM("Our patron", ourPatron.Name.ToString()));
        }

        private static string Describe(string prefix, Claim claim)
            => claim == null
                ? prefix + " none"
                : prefix + " " + claim.Type + " (" + CasusBelli.Legitimacy(claim.Type).ToString("0.00") + ")";

        private static string Signed(float value)
            => (value >= 0f ? "+" : string.Empty) + value.ToString("0");
    }
}
