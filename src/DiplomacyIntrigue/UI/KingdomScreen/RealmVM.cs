using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// The Realm tab: everything the mod knows about *our* kingdom on one surface,
    /// where the Diplomacy tab covers each pair of kingdoms in turn.
    ///
    /// Standing, our wars with their scores, our vassals with the state of each bond,
    /// the other spheres on the map, our claims and our agreements - the same
    /// information the Ctrl+D menu walks through one question at a time, laid out the
    /// way the design proposal asked for it (docs/ui-proposal/2-realm-tab.dc.html).
    /// The tab lives inside the game's own Kingdom screen as a sixth header button; see
    /// <see cref="KingdomManagementVMMixin"/> for how it shares the panel area with the
    /// five vanilla categories.
    ///
    /// Rebuilt when the tab is opened rather than continuously: the numbers are a
    /// snapshot of the moment the player clicked, which is honest - a hold that drifts
    /// while the panel sits open is not pretending otherwise.
    /// </summary>
    internal sealed class DiRealmVM : ViewModel
    {
        // Palette shared by every row - matches the design proposal's card colours,
        // reused here instead of a new brush per state because Brush.FontColor and a
        // Widget's Color both take a bound TaleWorlds.Library.Color directly.
        internal static readonly Color PositiveColor = Color.ConvertStringToColor("#9AC26AFF");
        internal static readonly Color NegativeColor = Color.ConvertStringToColor("#E08070FF");
        internal static readonly Color NeutralColor = Color.ConvertStringToColor("#E0CFA8FF");
        internal static readonly Color MutedColor = Color.ConvertStringToColor("#A89878FF");
        internal static readonly Color GoldColor = Color.ConvertStringToColor("#D9A441FF");

        /// <summary>Hides the five vanilla categories; supplied by the management mixin.</summary>
        private readonly Action _onShow;

        private bool _show;
        private bool _tabVisible;
        private string _tabText = "Realm";
        private string _standingTitle = string.Empty;
        private string _standingDetail = string.Empty;
        private string _standingNote = string.Empty;
        private string _patronLine = string.Empty;
        private string _sphereStrengthText = string.Empty;
        private string _dominanceText = string.Empty;
        private string _ambitionText = string.Empty;
        private string _greedText = string.Empty;
        private Color _standingColor;
        private Color _greedColor;
        private string _warsCountText = string.Empty;
        private string _vassalNote = string.Empty;
        private string _sphereGapNote = string.Empty;
        private string _claimsCountText = string.Empty;
        private string _tributeText = string.Empty;
        private MBBindingList<DiRealmWarVM> _wars = new MBBindingList<DiRealmWarVM>();
        private MBBindingList<DiRealmVassalVM> _vassals = new MBBindingList<DiRealmVassalVM>();
        private MBBindingList<DiRealmSphereVM> _spheres = new MBBindingList<DiRealmSphereVM>();
        private MBBindingList<DiRealmClaimGroupVM> _claimGroups = new MBBindingList<DiRealmClaimGroupVM>();
        private MBBindingList<DiRealmFabricationVM> _fabrications = new MBBindingList<DiRealmFabricationVM>();
        private MBBindingList<DiRealmAgreementVM> _agreements = new MBBindingList<DiRealmAgreementVM>();

        public DiRealmVM(Action onShow)
        {
            _standingColor = GoldColor;
            _greedColor = MutedColor;
            _onShow = onShow;
            RefreshTabGate();
            Rebuild();
        }

        // ----- bound surface ---------------------------------------------------

        /// <summary>Panel visibility, and the tab button's selected state.</summary>
        [DataSourceProperty]
        public bool Show
        {
            get => _show;
            set
            {
                if (value == _show) return;
                _show = value;
                OnPropertyChangedWithValue(value, nameof(Show));
            }
        }

        /// <summary>Whether the tab button renders at all: the mod is healthy and on.</summary>
        [DataSourceProperty]
        public bool TabVisible
        {
            get => _tabVisible;
            set
            {
                if (value == _tabVisible) return;
                _tabVisible = value;
                OnPropertyChangedWithValue(value, nameof(TabVisible));
            }
        }

        [DataSourceProperty] public string TabText => _tabText;

        [DataSourceProperty]
        public string StandingTitle
        {
            get => _standingTitle;
            set
            {
                if (value == _standingTitle) return;
                _standingTitle = value;
                OnPropertyChangedWithValue(value, nameof(StandingTitle));
            }
        }

        [DataSourceProperty]
        public Color StandingColor
        {
            get => _standingColor;
            set
            {
                if (value.Equals(_standingColor)) return;
                _standingColor = value;
                OnPropertyChangedWithValue(value, nameof(StandingColor));
            }
        }

        /// <summary>How the realm answers to us, or we to it - the mockup's dash line.</summary>
        [DataSourceProperty]
        public string StandingDetail
        {
            get => _standingDetail;
            set
            {
                if (value == _standingDetail) return;
                _standingDetail = value;
                OnPropertyChangedWithValue(value, nameof(StandingDetail));
            }
        }

        /// <summary>"A hegemon is derived, never stored." - only while we are one.</summary>
        [DataSourceProperty]
        public string StandingNote
        {
            get => _standingNote;
            set
            {
                if (value == _standingNote) return;
                _standingNote = value;
                OnPropertyChangedWithValue(value, nameof(StandingNote));
            }
        }

        [DataSourceProperty] public bool HasStandingNote => !string.IsNullOrEmpty(_standingNote);

        [DataSourceProperty]
        public string SphereStrengthText
        {
            get => _sphereStrengthText;
            set
            {
                if (value == _sphereStrengthText) return;
                _sphereStrengthText = value;
                OnPropertyChangedWithValue(value, nameof(SphereStrengthText));
            }
        }

        [DataSourceProperty]
        public string DominanceText
        {
            get => _dominanceText;
            set
            {
                if (value == _dominanceText) return;
                _dominanceText = value;
                OnPropertyChangedWithValue(value, nameof(DominanceText));
            }
        }

        [DataSourceProperty]
        public string AmbitionText
        {
            get => _ambitionText;
            set
            {
                if (value == _ambitionText) return;
                _ambitionText = value;
                OnPropertyChangedWithValue(value, nameof(AmbitionText));
            }
        }

        [DataSourceProperty]
        public string GreedText
        {
            get => _greedText;
            set
            {
                if (value == _greedText) return;
                _greedText = value;
                OnPropertyChangedWithValue(value, nameof(GreedText));
            }
        }

        [DataSourceProperty]
        public Color GreedColor
        {
            get => _greedColor;
            set
            {
                if (value.Equals(_greedColor)) return;
                _greedColor = value;
                OnPropertyChangedWithValue(value, nameof(GreedColor));
            }
        }

        [DataSourceProperty]
        public string PatronLine
        {
            get => _patronLine;
            set
            {
                if (value == _patronLine) return;
                _patronLine = value;
                OnPropertyChangedWithValue(value, nameof(PatronLine));
            }
        }

        [DataSourceProperty] public bool HasPatronLine => !string.IsNullOrEmpty(_patronLine);

        [DataSourceProperty]
        public string WarsCountText
        {
            get => _warsCountText;
            set
            {
                if (value == _warsCountText) return;
                _warsCountText = value;
                OnPropertyChangedWithValue(value, nameof(WarsCountText));
            }
        }

        [DataSourceProperty]
        public MBBindingList<DiRealmWarVM> Wars
        {
            get => _wars;
            set
            {
                if (value == _wars) return;
                _wars = value;
                OnPropertyChangedWithValue(value, nameof(Wars));
            }
        }

        [DataSourceProperty] public bool HasWars => _wars.Count > 0;

        [DataSourceProperty]
        public MBBindingList<DiRealmVassalVM> Vassals
        {
            get => _vassals;
            set
            {
                if (value == _vassals) return;
                _vassals = value;
                OnPropertyChangedWithValue(value, nameof(Vassals));
            }
        }

        [DataSourceProperty] public bool HasVassals => _vassals.Count > 0;

        /// <summary>The mockup's line under the sphere card: what protection costs.</summary>
        [DataSourceProperty]
        public string VassalNote
        {
            get => _vassalNote;
            set
            {
                if (value == _vassalNote) return;
                _vassalNote = value;
                OnPropertyChangedWithValue(value, nameof(VassalNote));
            }
        }

        [DataSourceProperty] public bool HasVassalNote => !string.IsNullOrEmpty(_vassalNote);

        [DataSourceProperty]
        public MBBindingList<DiRealmSphereVM> Spheres
        {
            get => _spheres;
            set
            {
                if (value == _spheres) return;
                _spheres = value;
                OnPropertyChangedWithValue(value, nameof(Spheres));
            }
        }

        [DataSourceProperty] public bool HasSpheres => _spheres.Count > 0;

        /// <summary>How the biggest rival sphere compares with ours.</summary>
        [DataSourceProperty]
        public string SphereGapNote
        {
            get => _sphereGapNote;
            set
            {
                if (value == _sphereGapNote) return;
                _sphereGapNote = value;
                OnPropertyChangedWithValue(value, nameof(SphereGapNote));
            }
        }

        [DataSourceProperty] public bool HasSphereGapNote => !string.IsNullOrEmpty(_sphereGapNote);

        [DataSourceProperty]
        public string ClaimsCountText
        {
            get => _claimsCountText;
            set
            {
                if (value == _claimsCountText) return;
                _claimsCountText = value;
                OnPropertyChangedWithValue(value, nameof(ClaimsCountText));
            }
        }

        [DataSourceProperty]
        public MBBindingList<DiRealmClaimGroupVM> ClaimGroups
        {
            get => _claimGroups;
            set
            {
                if (value == _claimGroups) return;
                _claimGroups = value;
                OnPropertyChangedWithValue(value, nameof(ClaimGroups));
            }
        }

        [DataSourceProperty] public bool HasClaims => _claimGroups.Count > 0;

        [DataSourceProperty]
        public MBBindingList<DiRealmFabricationVM> Fabrications
        {
            get => _fabrications;
            set
            {
                if (value == _fabrications) return;
                _fabrications = value;
                OnPropertyChangedWithValue(value, nameof(Fabrications));
            }
        }

        [DataSourceProperty] public bool HasFabrications => _fabrications.Count > 0;

        [DataSourceProperty]
        public MBBindingList<DiRealmAgreementVM> Agreements
        {
            get => _agreements;
            set
            {
                if (value == _agreements) return;
                _agreements = value;
                OnPropertyChangedWithValue(value, nameof(Agreements));
            }
        }

        [DataSourceProperty] public bool HasAgreements => _agreements.Count > 0;

        /// <summary>What the standing pacts would move per period, in and out.</summary>
        [DataSourceProperty]
        public string TributeText
        {
            get => _tributeText;
            set
            {
                if (value == _tributeText) return;
                _tributeText = value;
                OnPropertyChangedWithValue(value, nameof(TributeText));
            }
        }

        [DataSourceProperty] public bool HasTribute => !string.IsNullOrEmpty(_tributeText);

        // ----- commands --------------------------------------------------------

        /// <summary>The tab button: take the panel area over and fill it.</summary>
        public void ExecuteShow()
        {
            try
            {
                _onShow?.Invoke();
                RefreshTabGate();
                Rebuild();
                Show = true;
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Opening the Realm tab failed.", ex);
            }
        }

        public void ExecuteWriteReport()
        {
            try
            {
                var state = CoreBehavior.State;
                if (state == null) { Log.Notify("Diplomacy is only available in a campaign."); return; }
                DiplomacyMenu.WriteReport(state);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Writing the report failed.", ex);
            }
        }

        // ----- contents --------------------------------------------------------

        private void RefreshTabGate()
        {
            TabVisible = SubModule.Healthy && Settings.Current.EnableDiplomacy;
        }

        internal void Rebuild()
        {
            try
            {
                Compose();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "The Realm panel could not be rebuilt.", ex);
            }
        }

        private void Compose()
        {
            StandingTitle = "No realm";
            StandingColor = MutedColor;
            StandingDetail = string.Empty;
            StandingNote = string.Empty;
            SphereStrengthText = string.Empty;
            DominanceText = string.Empty;
            AmbitionText = string.Empty;
            GreedText = string.Empty;
            GreedColor = MutedColor;
            PatronLine = string.Empty;
            WarsCountText = string.Empty;
            VassalNote = string.Empty;
            SphereGapNote = string.Empty;
            ClaimsCountText = string.Empty;
            TributeText = string.Empty;
            var wars = new MBBindingList<DiRealmWarVM>();
            var vassals = new MBBindingList<DiRealmVassalVM>();
            var spheres = new MBBindingList<DiRealmSphereVM>();
            var groups = new MBBindingList<DiRealmClaimGroupVM>();
            var fabrications = new MBBindingList<DiRealmFabricationVM>();
            var agreements = new MBBindingList<DiRealmAgreementVM>();

            var state = CoreBehavior.State;
            var us = Clan.PlayerClan?.Kingdom;
            if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy || state == null || us == null)
            {
                Wars = wars; Vassals = vassals; Spheres = spheres;
                ClaimGroups = groups; Fabrications = fabrications; Agreements = agreements;
                return;
            }

            ComposeStanding(state, us);
            ComposeWars(state, us, wars);
            ComposeVassals(state, us, vassals);
            ComposeSpheres(state, us, spheres);
            ComposeClaims(state, us, groups, fabrications);
            ComposeAgreements(state, us, agreements);

            Wars = wars; Vassals = vassals; Spheres = spheres;
            ClaimGroups = groups; Fabrications = fabrications; Agreements = agreements;
        }

        // ----- the standing strip ---------------------------------------------

        private void ComposeStanding(ModState state, Kingdom us)
        {
            var patron = Hegemony.PatronOf(state, us);
            var vassalCount = Hegemony.VassalCount(state, us);

            if (patron != null)
            {
                StandingTitle = "Vassal of " + patron.Name;
                StandingColor = NegativeColor;
                StandingDetail = "one kingdom answers to " + patron.Name;
            }
            else if (vassalCount > 0)
            {
                StandingTitle = "Hegemon";
                StandingColor = GoldColor;
                StandingDetail = vassalCount == 1
                    ? "one kingdom answers to you"
                    : vassalCount + " kingdoms answer to you";
                // The mockup's right-hand note. A hegemon is derived from its vassalage
                // links, so the note is the truth about the title rather than flavour.
                StandingNote = "A hegemon is derived, never stored.";
            }
            else
            {
                StandingTitle = "Independent";
                StandingColor = MutedColor;
                StandingDetail = "no kingdom answers to you, and you answer to none";
            }

            var sphereHead = Hegemony.SphereHead(state, us);
            SphereStrengthText = "sphere " + Hegemony.SphereStrength(state, sphereHead).ToString("0");
            var dominance = Power.Dominance(us);
            var ambition = Power.Ambition(us);
            var greed = Power.Greed(state, us);
            DominanceText = "dominance " + dominance.ToString("0.00");
            AmbitionText = "ambition " + ambition.ToString("0.00");
            GreedText = "greed " + greed.ToString("0.00");
            GreedColor = greed > 0f ? NegativeColor : PositiveColor;

            // The bond we answer to, if there is one.
            var ourLink = Hegemony.VassalageOf(state, us);
            if (ourLink != null)
            {
                PatronLine = "We answer to " + ourLink.DominantParty.Name
                             + "   -   hold " + Hegemony.HoldOf(ourLink).ToString("0")
                             + " (" + DiplomacyMenu.HoldMeaning(state, ourLink) + ")"
                             + "   -   tribute " + ourLink.TributeAmount + " per period"
                             + "   -   " + TermLeft(ourLink);
                if (ourLink.DefianceMarks > 0)
                    PatronLine += "   -   we have defied them " + ourLink.DefianceMarks + " time(s)";
            }
        }

        // ----- the wars strip --------------------------------------------------

        private void ComposeWars(ModState state, Kingdom us, MBBindingList<DiRealmWarVM> wars)
        {
            foreach (var war in state.OngoingWarsOf(us))
            {
                var enemy = war.Other(us);
                if (enemy == null) continue;
                var target = enemy;
                var score = war.ScoreFor(us);
                var budget = PeaceTable.BudgetFor(war, us);
                var allowance = PeaceTable.DescribeAllowance(state, war, us);
                // The mockup's button label and sub differ by what the war has earned:
                // a real negotiation past the cliff, or the white peace that is all that
                // is on offer.
                var earned = budget > 0f;
                wars.Add(new DiRealmWarVM(
                    enemy.Name.ToString(),
                    war.DaysElapsed.ToString("0") + " days"
                        + (war.Justification == CasusBelliType.None
                            ? ", no claim on record"
                            : " over " + war.Justification)
                        + (war.IsObligationWar && war.CalledBy != null
                            ? "   -   called in by " + war.CalledBy.Name : ""),
                    "our exhaustion " + war.ExhaustionOf(us).ToString("0.0")
                        + "   -   their condition "
                        + ExhaustionBands.Describe(war.ExhaustionOf(enemy)),
                    (score >= 0f ? "+" : "") + score.ToString("0"),
                    score >= 0f ? PositiveColor : NegativeColor,
                    Color.FromUint(enemy.Color),
                    earned ? "Negotiate peace" : "White peace only",
                    earned
                        ? "Budget " + budget.ToString("0") + " - see hint for the price list."
                        : "White peace only - this war has earned nothing yet.",
                    "Opens the peace table: what this war has earned, and what they will sign. " + allowance,
                    () => DiplomacyMenu.ShowPeace(state, us, target)));
            }
            WarsCountText = wars.Count.ToString("0");
        }

        // ----- column 1: our sphere --------------------------------------------

        private void ComposeVassals(ModState state, Kingdom us, MBBindingList<DiRealmVassalVM> vassals)
        {
            var links = new List<Treaty>();
            Hegemony.CollectVassalages(state, us, links);
            for (var i = 0; i < links.Count; i++)
            {
                var link = links[i];
                var hold = Hegemony.HoldOf(link);
                var target = Hegemony.HoldTarget(state, link, out _);
                var threshold = Hegemony.SecessionThreshold(state, link);
                var subordinate = link.SubordinateParty;
                vassals.Add(new DiRealmVassalVM(
                    link.SubordinateParty.Name.ToString(),
                    subordinate != null ? Color.FromUint(subordinate.Color) : GoldColor,
                    (int)hold,
                    (int)(threshold < 0f ? 0f : threshold > 100f ? 100f : threshold),
                    (int)(target < 0f ? 0f : target > 100f ? 100f : target),
                    "hold " + hold.ToString("0") + "  ->  " + DriftWord(hold, target)
                        + " " + target.ToString("0"),
                    "revolts below " + threshold.ToString("0"),
                    link.DefianceMarks,
                    link.TributeAmount + " per period",
                    "renews in " + TermLeft(link),
                    BuildTermChips(state, link)));
            }

            if (vassals.Count > 0)
            {
                var first = links[0].SubordinateParty;
                VassalNote = "Protection is the half of the bargain you owe: answer "
                             + first.Name + " when it is attacked, or watch this bar fall.";
            }
        }
        /// <summary>
        /// The hold terms as chips, signed the way the hold formula sums them - the same
        /// numbers <see cref="Hegemony.HoldTarget"/> writes into its explanation.
        /// </summary>
        private static MBBindingList<DiRealmTermVM> BuildTermChips(ModState state, Treaty link)
        {
            var chips = new MBBindingList<DiRealmTermVM>();
            var t = Hegemony.HoldTermsOf(state, link);
            AddChip(chips, "protection", t.Protection);
            AddChip(chips, "fear", t.Fear);
            AddChip(chips, "trust", t.Trust);
            AddChip(chips, "tribute", -t.Tribute);
            AddChip(chips, "wars", -t.Wars);
            AddChip(chips, "rival", -t.Rival);
            AddChip(chips, "culture", -t.Culture);
            AddChip(chips, "dread", -t.Dread);
            return chips;
        }

        private static void AddChip(MBBindingList<DiRealmTermVM> chips, string name, float value)
        {
            var rounded = (float)Math.Round(value, 1);
            chips.Add(new DiRealmTermVM(
                name + " " + (rounded > 0f ? "+" : "") + rounded.ToString("0.0"),
                rounded > 0f ? PositiveColor : rounded < 0f ? NegativeColor : MutedColor));
        }

        private static string DriftWord(float hold, float target)
        {
            if (target > hold + 1f) return "drifting up to";
            if (target < hold - 1f) return "drifting down to";
            return "steady at";
        }

        private void ComposeSpheres(ModState state, Kingdom us, MBBindingList<DiRealmSphereVM> spheres)
        {
            // Rival spheres are shown as the mockup shows them - who answers to whom and
            // how strong the sphere is. A rival's bond is described in words, never
            // numbered: the exact hold is what Phase 3's espionage sells.
            DiRealmSphereVM ours = null;
            DiRealmSphereVM biggestRival = null;
            var biggestRivalStrength = 0f;
            var ourStrength = Hegemony.SphereStrength(state, Hegemony.SphereHead(state, us));

            foreach (var other in Kingdom.All)
            {
                if (other == null || other.IsEliminated || !Hegemony.IsHegemon(state, other)) continue;
                var held = new List<Treaty>();
                Hegemony.CollectVassalages(state, other, held);
                if (held.Count == 0) continue;

                var names = new List<string>(held.Count);
                for (var i = 0; i < held.Count; i++)
                    names.Add(held[i].SubordinateParty.Name + " (" + DiplomacyMenu.HoldMeaning(state, held[i]) + ")");

                var strength = Hegemony.SphereStrength(state, other);
                var row = new DiRealmSphereVM(
                    Color.FromUint(other.Color),
                    other == us ? other.Name + "  -  us" : other.Name.ToString(),
                    string.Join(", ", names.ToArray()),
                    strength.ToString("0"));
                spheres.Add(row);

                if (other == us) ours = row;
                else if (strength > biggestRivalStrength)
                {
                    biggestRivalStrength = strength;
                    biggestRival = row;
                }
            }

            // The mockup's gap line: how the biggest rival sphere reads against ours.
            if (biggestRival != null)
            {
                var gap = Math.Abs(biggestRivalStrength - ourStrength);
                SphereGapNote = biggestRivalStrength >= ourStrength
                    ? biggestRival.HeadText + "'s sphere outweighs yours by " + gap.ToString("0")
                      + ". Every court that belongs to neither reads that gap when it decides whom to pact with."
                    : "Your sphere outweighs " + biggestRival.HeadText + " by " + gap.ToString("0")
                      + ". Every court that belongs to neither reads that gap when it decides whom to pact with.";
            }
        }

        // ----- column 2: our claims --------------------------------------------

        private void ComposeClaims(ModState state, Kingdom us,
            MBBindingList<DiRealmClaimGroupVM> groups, MBBindingList<DiRealmFabricationVM> fabrications)
        {
            var live = 0;
            foreach (var other in Kingdom.All)
            {
                if (other == us || other.IsEliminated) continue;
                var rows = new MBBindingList<DiRealmClaimVM>();
                foreach (var claim in ClaimRegistry.LiveClaims(state, us, other))
                {
                    live++;
                    var footer = claim.AllowsFiefDemands ? "entitles land" : string.Empty;
                    if (claim.ExpiresOn != CampaignTime.Never)
                    {
                        var daysLeft = (claim.ExpiresOn - CampaignTime.Now).ToDays;
                        var expiry = "ages out in " + Math.Max(0, (int)daysLeft) + " days";
                        footer = footer.Length > 0 ? footer + "   -   " + expiry : expiry;
                    }
                    // A broken-treaty claim carries its own story: the mockup's line under
                    // the claim name, with the year the pact died from the claim's record.
                    var note = claim.Type == CasusBelliType.BrokenTreaty
                        ? "They tore up a pact in " + claim.AcquiredOn.GetYear
                          + ". A war on this needs no excuse."
                        : string.Empty;
                    rows.Add(new DiRealmClaimVM(
                        claim.Type.ToString(),
                        claim.Legitimacy.ToString("0.00"),
                        claim.Legitimacy >= 0.5f ? PositiveColor : NegativeColor,
                        claim.AllowsFiefDemands,
                        footer,
                        note));
                }
                if (rows.Count == 0) continue;
                groups.Add(new DiRealmClaimGroupVM(
                    other.Name.ToString(),
                    Color.FromUint(other.Color),
                    rows));
            }

            for (var i = 0; i < state.Fabrications.Count; i++)
            {
                var f = state.Fabrications[i];
                if (f == null || f.Claimant != us) continue;
                var progress = 100 - (int)(100f * f.DaysRemaining / DiplomacyConstants.FabricateClaimDurationDays);
                if (progress < 0) progress = 0;
                if (progress > 100) progress = 100;
                var targetKingdom = f.TargetKingdom;
                fabrications.Add(new DiRealmFabricationVM(
                    targetKingdom == null ? "?" : targetKingdom.Name.ToString(),
                    targetKingdom != null ? Color.FromUint(targetKingdom.Color) : MutedColor,
                    f.DaysRemaining.ToString("0") + " days left",
                    progress));
            }

            ClaimsCountText = live + " live   -   " + fabrications.Count + " being fabricated";
        }

        // ----- column 3: our agreements ----------------------------------------

        private void ComposeAgreements(ModState state, Kingdom us,
            MBBindingList<DiRealmAgreementVM> agreements)
        {
            var tributeIn = 0;
            var tributeOut = 0;

            foreach (var treaty in state.ActiveTreatiesOf(us))
            {
                var other = treaty.Other(us);
                if (other == null) continue;
                float daysLeft = -1f;
                if (treaty.ExpiresOn != CampaignTime.Never)
                    daysLeft = (float)(treaty.ExpiresOn - CampaignTime.Now).ToDays;
                agreements.Add(new DiRealmAgreementVM(
                    TreatyLabel(treaty.Type),
                    Color.FromUint(other.Color),
                    TermLeft(treaty),
                    daysLeft >= 0f && daysLeft < 60f ? NegativeColor : MutedColor,
                    AgreementDetail(treaty, us, other)));

                if (treaty.TributeAmount > 0 && treaty.TributePayer != null)
                {
                    if (treaty.TributePayer == us) tributeOut += treaty.TributeAmount;
                    else tributeIn += treaty.TributeAmount;
                }
            }

            // The mockup's tribute card: what the standing pacts move per period. The
            // "last paid" day is a payment-history question this card does not answer,
            // so it is left out rather than guessed.
            if (tributeIn > 0 || tributeOut > 0)
                TributeText = "+" + tributeIn.ToString("0") + " in   /   " + tributeOut.ToString("0") + " out";
        }

        private static string TermLeft(Treaty treaty)
        {
            return treaty.ExpiresOn == CampaignTime.Never ? "open-ended" : Duration(treaty);
        }

        /// <summary>Compact duration the mockup uses: "1y 40d", "6y", "45d".</summary>
        private static string Duration(Treaty treaty)
        {
            var days = (float)(treaty.ExpiresOn - CampaignTime.Now).ToDays;
            if (days <= 0f) return "expired";
            // The campaign year is 84 days (four seasons of 21).
            var years = (int)(days / 84f);
            var rem = (int)(days - years * 84f);
            if (years <= 0f) return rem + "d";
            return rem > 0 ? years + "y " + rem + "d" : years + "y";
        }

        private static string TreatyLabel(TreatyType type)
        {
            switch (type)
            {
                case TreatyType.NonAggressionPact: return "Non-aggression pact";
                case TreatyType.DefensivePact: return "Defensive pact";
                case TreatyType.Alliance: return "Alliance";
                case TreatyType.TributaryPact: return "Tributary pact";
                case TreatyType.Vassalage: return "Vassalage";
                default: return "Truce";
            }
        }

        /// <summary>What each treaty type means for us, in the direction it actually runs.</summary>
        private static string AgreementDetail(Treaty treaty, Kingdom us, Kingdom other)
        {
            switch (treaty.Type)
            {
                case TreatyType.Vassalage:
                    return treaty.DominantParty == us
                        ? other.Name + " - pays you, and fights for you"
                        : other.Name + " - you pay them, and answer their call";
                case TreatyType.Alliance:
                    return other.Name + " - answers any call, and you answer theirs";
                case TreatyType.DefensivePact:
                    return other.Name + " - joins if you are attacked, and you if they are";
                case TreatyType.TributaryPact:
                    return treaty.TributePayer == other
                        ? other.Name + " - " + treaty.TributeAmount + " to you, no army owed"
                        : other.Name + " - you pay " + treaty.TributeAmount + ", no army owed";
                case TreatyType.NonAggressionPact:
                    return other.Name + " - neither may declare war";
                default:
                    return other.Name + " - active truce";
            }
        }
    }

    /// <summary>One ongoing war of ours: the enemy, the story, both exhaustions, the score.</summary>
    internal sealed class DiRealmWarVM : ViewModel
    {
        private readonly Action _negotiate;

        public DiRealmWarVM(string name, string detail, string conditionText,
            string scoreText, Color scoreColor, Color accentColor,
            string buttonLabel, string buttonExplanation, string buttonHint, Action negotiate)
        {
            Name = name;
            Detail = detail;
            ConditionText = conditionText;
            ScoreText = scoreText;
            ScoreColor = scoreColor;
            AccentColor = accentColor;
            ButtonLabel = buttonLabel;
            ButtonExplanation = buttonExplanation;
            ButtonHint = new TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel(() => buttonHint);
            _negotiate = negotiate;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Detail { get; }
        [DataSourceProperty] public string ConditionText { get; }
        [DataSourceProperty] public string ScoreText { get; }
        [DataSourceProperty] public Color ScoreColor { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public string ButtonLabel { get; }
        [DataSourceProperty] public string ButtonExplanation { get; }
        [DataSourceProperty] public TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel ButtonHint { get; }

        public void ExecuteNegotiate()
        {
            try
            {
                _negotiate?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Opening the peace table failed.", ex);
            }
        }
    }

    /// <summary>One chip of a hold term: its signed contribution and its meaning colour.</summary>
    internal sealed class DiRealmTermVM : ViewModel
    {
        public DiRealmTermVM(string text, Color color)
        {
            Text = text;
            Color = color;
        }

        [DataSourceProperty] public string Text { get; }
        [DataSourceProperty] public Color Color { get; }
    }

    /// <summary>
    /// One of our vassals: the hold figure as a bar with its drift mark and revolt line,
    /// the terms pulling it, its defiance marks, its tribute and its renewal.
    /// </summary>
    internal sealed class DiRealmVassalVM : ViewModel
    {
        private const int BarWidth = 160;

        private static readonly Color EmptyDotColor = Color.ConvertStringToColor("#3A2F24FF");
        private static readonly Color GoldColor = Color.ConvertStringToColor("#D9A441FF");

        public DiRealmVassalVM(string name, Color accentColor, int holdValue, int thresholdValue,
            int driftValue, string holdText, string thresholdText, int defianceMarks,
            string tributeText, string termText, MBBindingList<DiRealmTermVM> terms)
        {
            Name = name;
            AccentColor = accentColor;
            HoldValue = Clamp(holdValue);
            ThresholdValue = Clamp(thresholdValue);
            ThresholdPixelWidth = (int)(ThresholdValue / 100f * BarWidth);
            DriftPixelOffset = (int)(Clamp(driftValue) / 100f * BarWidth);
            HoldText = holdText;
            ThresholdText = thresholdText;
            DefianceMarks = defianceMarks;
            HasDefiance = defianceMarks > 0;
            // The mockup's defiance line: how many marks, and what the next ones cost.
            DefianceText = defianceMarks > 0
                ? "defiance: " + defianceMarks + " mark(s)"
                : "defiance: none - " + DiplomacyConstants.DefianceMarksToLapse
                  + " marks and the link does not renew";
            // One slot per mark the bond can take before it lapses, filled as they land.
            Dot1Color = defianceMarks >= 1 ? GoldColor : EmptyDotColor;
            Dot2Color = defianceMarks >= 2 ? GoldColor : EmptyDotColor;
            Dot3Color = defianceMarks >= 3 ? GoldColor : EmptyDotColor;
            TributeText = tributeText;
            TermText = termText;
            Terms = terms;
        }

        private static int Clamp(int v) => v < 0 ? 0 : v > 100 ? 100 : v;

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public int HoldValue { get; }
        [DataSourceProperty] public int ThresholdValue { get; }
        [DataSourceProperty] public int ThresholdPixelWidth { get; }
        [DataSourceProperty] public int DriftPixelOffset { get; }
        [DataSourceProperty] public string HoldText { get; }
        [DataSourceProperty] public string ThresholdText { get; }
        [DataSourceProperty] public int DefianceMarks { get; }
        [DataSourceProperty] public bool HasDefiance { get; }
        [DataSourceProperty] public string DefianceText { get; }
        [DataSourceProperty] public Color Dot1Color { get; }
        [DataSourceProperty] public Color Dot2Color { get; }
        [DataSourceProperty] public Color Dot3Color { get; }
        [DataSourceProperty] public string TributeText { get; }
        [DataSourceProperty] public string TermText { get; }
        [DataSourceProperty] public MBBindingList<DiRealmTermVM> Terms { get; }
        [DataSourceProperty] public bool HasTerms => Terms != null && Terms.Count > 0;
    }

    /// <summary>Another hegemon's sphere: who answers to it and how strong it is.</summary>
    internal sealed class DiRealmSphereVM : ViewModel
    {
        public DiRealmSphereVM(Color accentColor, string headText, string vassalsText, string strengthText)
        {
            AccentColor = accentColor;
            HeadText = headText;
            VassalsText = vassalsText;
            StrengthText = strengthText;
        }

        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public string HeadText { get; }
        [DataSourceProperty] public string VassalsText { get; }
        [DataSourceProperty] public string StrengthText { get; }
    }

    /// <summary>One row inside a claim card: the claim type, its legitimacy, its footer.</summary>
    internal sealed class DiRealmClaimVM : ViewModel
    {
        public DiRealmClaimVM(string typeText, string legitimacyText, Color legitimacyColor,
            bool isLand, string footerText, string noteText)
        {
            TypeText = typeText;
            LegitimacyText = legitimacyText;
            LegitimacyColor = legitimacyColor;
            IsLand = isLand;
            FooterText = footerText;
            HasFooter = !string.IsNullOrEmpty(footerText);
            NoteText = noteText;
            HasNote = !string.IsNullOrEmpty(noteText);
        }

        [DataSourceProperty] public string TypeText { get; }
        [DataSourceProperty] public string LegitimacyText { get; }
        [DataSourceProperty] public Color LegitimacyColor { get; }
        [DataSourceProperty] public bool IsLand { get; }
        [DataSourceProperty] public string FooterText { get; }
        [DataSourceProperty] public bool HasFooter { get; }

        /// <summary>The mockup's story line under a broken-treaty claim.</summary>
        [DataSourceProperty] public string NoteText { get; }
        [DataSourceProperty] public bool HasNote { get; }
    }

    /// <summary>A claim card: one target kingdom and every claim we hold on it.</summary>
    internal sealed class DiRealmClaimGroupVM : ViewModel
    {
        public DiRealmClaimGroupVM(string kingdomText, Color accentColor,
            MBBindingList<DiRealmClaimVM> rows)
        {
            KingdomText = kingdomText;
            AccentColor = accentColor;
            Rows = rows;
        }

        [DataSourceProperty] public string KingdomText { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public MBBindingList<DiRealmClaimVM> Rows { get; }
    }

    /// <summary>A claim still being fabricated: against whom, and how far along.</summary>
    internal sealed class DiRealmFabricationVM : ViewModel
    {
        public DiRealmFabricationVM(string kingdomText, Color accentColor, string detailText, int progress)
        {
            KingdomText = kingdomText;
            AccentColor = accentColor;
            DetailText = detailText;
            Progress = progress < 0 ? 0 : progress > 100 ? 100 : progress;
        }

        [DataSourceProperty] public string KingdomText { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public string DetailText { get; }
        [DataSourceProperty] public int Progress { get; }
    }

    /// <summary>One standing agreement: its type, its term, and what it means for us.</summary>
    internal sealed class DiRealmAgreementVM : ViewModel
    {
        public DiRealmAgreementVM(string typeText, Color accentColor, string termText, Color termColor,
            string detailText)
        {
            TypeText = typeText;
            AccentColor = accentColor;
            TermText = termText;
            TermColor = termColor;
            DetailText = detailText;
        }

        [DataSourceProperty] public string TypeText { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public string TermText { get; }
        [DataSourceProperty] public Color TermColor { get; }
        [DataSourceProperty] public string DetailText { get; }
    }
}
