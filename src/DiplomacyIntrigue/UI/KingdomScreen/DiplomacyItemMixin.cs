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
    /// What the mod knows about the other kingdom, and what the player can do about it,
    /// drawn in the Kingdom screen's Diplomacy tab with the game's own widgets.
    ///
    /// **Three attempts, and the third is the one that works.** A label/value list of our
    /// own was readable and looked nothing like Bannerlord, which the lead rejected on
    /// sight. Appending to the panel's own <c>Stats</c> and <c>Actions</c> lists would have
    /// been ideal and silently does nothing: those lists are rebuilt after UIExtenderEx's
    /// hook runs, so the rows disappear with no error logged anywhere - the mixin stays
    /// alive the whole time, as its other bindings kept proving. What is left, and what this
    /// is, is our own lists drawn by a prefab that copies TaleWorlds' markup: their
    /// comparison bars, their proposal buttons, their brushes.
    ///
    /// **Our exhaustion exactly, theirs as a band** (spec 01 §8). The enemy bar carries the
    /// floor of their band, never their figure - "Weary" reads as 40 whether they are at 41
    /// or 59 - and the hover hint says so. A nicer surface is not a reason to give away what
    /// Phase 3's ReadCourt is meant to sell.
    ///
    /// Two concrete mixins rather than one on the base with derived types: the derived
    /// RefreshValues is what the panel calls, and hooking the base one put us at the wrong
    /// point in the sequence.
    /// </summary>
    internal abstract class DiplomacyItemMixinBase<T> : BaseViewModelMixin<T>
        where T : KingdomDiplomacyItemVM
    {
        private static readonly char[] NewlineChars = { '\n', '\r' };

        private string _rowSummary = string.Empty;
        private string _headline = string.Empty;
        private MBBindingList<DiplomacyStatVM> _stats = new MBBindingList<DiplomacyStatVM>();
        private MBBindingList<DiplomacyActionVM> _actions = new MBBindingList<DiplomacyActionVM>();

        protected DiplomacyItemMixinBase(T vm) : base(vm)
        {
            Rebuild();
        }

        /// <summary>The war list row: how they look, and what the war is worth so far.</summary>
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

        /// <summary>
        /// One line under the two banners: the casus belli of a war and what the enemy's
        /// condition means, or the agreements standing between the two and who answers to
        /// whom.
        /// </summary>
        [DataSourceProperty]
        public string DiHeadline
        {
            get => _headline;
            set
            {
                if (value == _headline) return;
                _headline = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiHeadline));
            }
        }

        [DataSourceProperty]
        public bool DiHasHeadline => !string.IsNullOrEmpty(_headline);

        [DataSourceProperty]
        public MBBindingList<DiplomacyStatVM> DiStats
        {
            get => _stats;
            set
            {
                if (value == _stats) return;
                _stats = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiStats));
            }
        }

        [DataSourceProperty]
        public MBBindingList<DiplomacyActionVM> DiActions
        {
            get => _actions;
            set
            {
                if (value == _actions) return;
                _actions = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiActions));
            }
        }

        public override void OnRefresh() => Rebuild();

        private void Rebuild()
        {
            // A throw here would come out of Gauntlet's refresh rather than our own code, so
            // anything unexpected leaves the tab exactly as vanilla drew it.
            try
            {
                Compose();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Diplomacy tab additions failed; the tab stays vanilla.", ex);
                DiRowSummary = string.Empty;
                DiHeadline = string.Empty;
                DiStats = new MBBindingList<DiplomacyStatVM>();
                DiActions = new MBBindingList<DiplomacyActionVM>();
            }
        }

        private void Compose()
        {
            DiRowSummary = string.Empty;
            DiHeadline = string.Empty;
            var stats = new MBBindingList<DiplomacyStatVM>();
            var actions = new MBBindingList<DiplomacyActionVM>();

            var state = CoreBehavior.State;
            var us = ViewModel?.Faction1 as Kingdom;
            var them = ViewModel?.Faction2 as Kingdom;

            if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy
                || state == null || us == null || them == null)
            {
                DiStats = stats;
                DiActions = actions;
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

            var war = state.OngoingWarBetween(us, them);
            DiHeadline = war != null ? WarHeadline(war, them) : PeaceHeadline(state, us, them);

            if (war != null)
            {
                var band = ExhaustionBands.Of(war.ExhaustionOf(them));
                var score = war.ScoreFor(us);
                DiRowSummary = ExhaustionBands.Name(band)
                               + "   " + (score >= 0f ? "+" : string.Empty) + score.ToString("0");
            }

            BuildStats(state, us, them, war, stats);

            // Foreign policy belongs to the ruler. A vassal still sees everything - looking
            // is free, and a vassal has every reason to watch - but acts through no button.
            if (player != null && us == player && player.Leader == Hero.MainHero)
                BuildActions(state, us, them, war, actions);

            DiStats = stats;
            DiActions = actions;
        }

        // ----- the line under the banners -------------------------------------

        private static string WarHeadline(WarRecord war, Kingdom them)
        {
            var band = ExhaustionBands.Of(war.ExhaustionOf(them));
            var text = "War over " + war.Justification
                       + "   legitimacy " + CasusBelli.Legitimacy(war.Justification).ToString("0.00");

            if (war.IsObligationWar && war.CalledBy != null)
                text += "   called in by " + war.CalledBy.Name;

            return text + "   -   " + ExhaustionBands.Meaning(band);
        }

        private static string PeaceHeadline(ModState state, Kingdom us, Kingdom them)
        {
            var parts = new List<string>();

            foreach (var treaty in state.ActiveTreatiesOf(us))
            {
                if (!treaty.IsBetween(us, them)) continue;

                var text = treaty.Type.ToString();
                if (treaty.TributeAmount > 0 && treaty.TributePayer != null)
                    text += " (" + treaty.TributeAmount + " from " + treaty.TributePayer.Name + ")";
                parts.Add(text);
            }

            var patron = Hegemony.PatronOf(state, them);
            if (patron != null)
            {
                var link = Hegemony.VassalageOf(state, them);
                parts.Add(them.Name + " answers to " + patron.Name
                          + " at hold " + Hegemony.HoldOf(link).ToString("0"));
            }

            return parts.Count == 0 ? "No standing agreements." : string.Join("   -   ", parts.ToArray());
        }

        // ----- comparison rows -------------------------------------------------

        private void BuildStats(ModState state, Kingdom us, Kingdom them, WarRecord war,
            ICollection<DiplomacyStatVM> into)
        {
            var ourColour = ColourOf(us);
            var theirColour = ColourOf(them);

            if (war != null)
            {
                var ours = war.ExhaustionOf(us);
                var band = ExhaustionBands.Of(war.ExhaustionOf(them));

                into.Add(new DiplomacyStatVM("War Exhaustion",
                    (int)ours, (int)ExhaustionBands.Floor(band), 100, ourColour, theirColour,
                    "Ours exactly: " + ours.ToString("0.0") + ". A court sues for peace at "
                    + DiplomacyConstants.ExhaustionSeekPeace.ToString("0") + ".",
                    "Shown as a band, never a figure: " + ExhaustionBands.Name(band) + " - "
                    + ExhaustionBands.Meaning(band)
                    + " The exact number is what an espionage report buys."));

// War score has no row of its own: the list row already carries it, the
                // headline explains the war, and the pane only has room for so many bars
                // before it starts fighting the game's own.
            }

            into.Add(new DiplomacyStatVM("Diplomatic Trust",
                (int)TrustRegistry.Get(state, us, them), (int)TrustRegistry.Get(state, them, us),
                100, ourColour, theirColour,
                "What we make of their word. Trust never decays: it is reputation, not feeling.",
                "What they make of ours. Below "
                + DiplomacyConstants.TrustFloorForPacts.ToString("0")
                + " they will sign nothing but a truce."));

            var ourClaims = Count(ClaimRegistry.LiveClaims(state, us, them));
            var theirClaims = Count(ClaimRegistry.LiveClaims(state, them, us));
            if (ourClaims <= 0 && theirClaims <= 0) return;

            var ourBest = ClaimRegistry.Best(state, us, them);
            var theirBest = ClaimRegistry.Best(state, them, us);
            into.Add(new DiplomacyStatVM("Standing Claims",
                ourClaims, theirClaims, 5, ourColour, theirColour,
                ourBest == null
                    ? "We hold no claim on them."
                    : "Best: " + ourBest.Type + " at legitimacy "
                      + CasusBelli.Legitimacy(ourBest.Type).ToString("0.00")
                      + (ourBest.AllowsFiefDemands ? ". It entitles us to land at the peace table." : "."),
                theirBest == null
                    ? "They hold no claim on us."
                    : "Their best: " + theirBest.Type + " at legitimacy "
                      + CasusBelli.Legitimacy(theirBest.Type).ToString("0.00") + "."));
        }

        // ----- buttons ---------------------------------------------------------

        private void BuildActions(ModState state, Kingdom us, Kingdom them, WarRecord war,
            ICollection<DiplomacyActionVM> into)
        {
            if (war != null)
            {
                into.Add(new DiplomacyActionVM("Negotiate peace",
                    FirstLine(PeaceTable.DescribeAllowance(state, war, us)), 0, true,
                    "Opens the peace table: what this war has earned, and what they will sign.",
                    () => DiplomacyMenu.ShowPeace(state, us, them)));
            }
            else
            {
                AddPact(state, us, them, TreatyType.NonAggressionPact, "non-aggression pact", into);
                AddPact(state, us, them, TreatyType.DefensivePact, "defensive pact", into);
                AddPact(state, us, them, TreatyType.Alliance, "alliance", into);
            }

            var breakable = DiplomacyMenu.FirstBreakableTreaty(state, us, them);
            if (breakable != null)
            {
                into.Add(new DiplomacyActionVM("Renounce " + breakable.Type,
                    "-" + (-DiplomacyConstants.TrustTreatyBrokenVictim).ToString("0")
                    + " trust with them, -"
                    + (-DiplomacyConstants.TrustTreatyBrokenObserver).ToString("0")
                    + " with every other court.",
                    0, true,
                    "Always possible, never free. Breaking a treaty on purpose is a story beat, "
                    + "not an accident, so the mod never blocks it - it only prices it. They gain "
                    + "a reason for war.",
                    () => DiplomacyMenu.BreakTreaty(state, us, them)));
            }

            into.Add(new DiplomacyActionVM("Fabricate a claim",
                DiplomacyConstants.FabricateClaimDurationDays + " days and "
                + DiplomacyConstants.FabricateClaimGoldCost + " denars.",
                DiplomacyConstants.FabricateClaimInfluenceCost, true,
                "A " + (DiplomacyConstants.FabricateClaimExposureChance * 100f).ToString("0")
                + "% chance of being caught, which damages relations with every court and hands "
                + "the target a claim of their own.",
                () => DiplomacyMenu.ShowFabricationTargets(state, us, them)));
        }

        private void AddPact(ModState state, Kingdom us, Kingdom them, TreatyType type,
            string name, ICollection<DiplomacyActionVM> into)
        {
            var allowed = TreatyRegistry.CanSign(state, us, them, type, out var reason);
            var cost = DiplomacyConstants.TreatyInfluenceCost(type);

            // Their own valuation, shown honestly: this is the number their court decides on.
            var theirValue = AiDiplomacy.PactValue(state, them, us);
            var threshold = DiplomacyMenu.ThresholdFor(type);

            into.Add(new DiplomacyActionVM("Propose " + name,
                allowed
                    ? them.Name + " values it at " + theirValue.ToString("0")
                      + ", and needs " + threshold.ToString("0") + "."
                    : reason,
                cost, allowed && theirValue >= threshold,
                allowed
                    ? "They sign only if their own valuation clears the bar. The number shown is "
                      + "the one their court uses - there is no separate figure for the player."
                    : reason,
                () => DiplomacyMenu.ProposePact(state, us, them, type)));
        }

        // ----- helpers ---------------------------------------------------------

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var cut = text.IndexOfAny(NewlineChars);
            return (cut < 0 ? text : text.Substring(0, cut)).Trim();
        }

        private static string ColourOf(IFaction faction)
            => faction == null ? "#FFFFFFFF" : Color.FromUint(faction.Color).ToString();

        private static int Count(IEnumerable<Claim> claims)
        {
            var n = 0;
            foreach (var unused in claims) n++;
            return n;
        }
    }

    /// <summary>The mod's reading of a war, on the row and in the detail pane.</summary>
    [ViewModelMixin("RefreshValues")]
    internal sealed class WarItemMixin : DiplomacyItemMixinBase<KingdomWarItemVM>
    {
        public WarItemMixin(KingdomWarItemVM vm) : base(vm) { }
    }

    /// <summary>The same, for a kingdom we are at peace with.</summary>
    [ViewModelMixin("RefreshValues")]
    internal sealed class TruceItemMixin : DiplomacyItemMixinBase<KingdomTruceItemVM>
    {
        public TruceItemMixin(KingdomTruceItemVM vm) : base(vm) { }
    }
}
