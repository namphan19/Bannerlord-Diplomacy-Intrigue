using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using StatecraftModel = DiplomacyIntrigue.Statecraft.StatecraftModel;
using StatecraftTerms = DiplomacyIntrigue.Statecraft.StatecraftTerms;
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

        /// <summary>
        /// An agreement is drawn in the warning colour once it has less than a season left (21
        /// days). It was 60 until 2026-09-27, a calendar reflex: a one-year truce is 84 days, so
        /// it read as expiring for 71% of its term, which made the colour mean nothing.
        /// </summary>
        private const float ExpiringSoonDays = 21f;
        internal static readonly Color NeutralColor = Color.ConvertStringToColor("#E0CFA8FF");
        internal static readonly Color MutedColor = Color.ConvertStringToColor("#A89878FF");
        internal static readonly Color GoldColor = Color.ConvertStringToColor("#D9A441FF");

        /// <summary>Hides the five vanilla categories; supplied by the management mixin.</summary>
        private readonly Action _onShow;

        /// <summary>Switches to the Court tab, where a civil war is shown; supplied by the management mixin.</summary>
        private readonly Action _openCourt;

        private bool _show;
        private bool _tabVisible;
        private string _tabText = DiText.T("DI_REALM_REALM_2", "Realm");
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

        /// <summary>The ruler-only counter-intelligence section at the foot of the tab (Phase 3.7).</summary>
        // ----- static labels, moved out of the prefab (story 4.1 §9) ---------------------

        [DataSourceProperty] public string WarsTitleText => DiText.T("DI_REALM_OUR_WARS_TITLE", "Our wars");
        [DataSourceProperty] public string SphereTitleText => DiText.T("DI_REALM_OUR_SPHERE_TITLE", "Our sphere");
        [DataSourceProperty] public string SpheresTitleText => DiText.T("DI_REALM_SPHERES_ON_THE_MAP", "Spheres on the map");
        [DataSourceProperty] public string ClaimsTitleText => DiText.T("DI_REALM_OUR_CLAIMS_TITLE", "Our claims");
        [DataSourceProperty] public string ClaimExplainsText => DiText.T("DI_REALM_A_CLAIM_IS_WHAT_THE_PEACE", "A claim is what the peace table will let you ask for. Land needs one; nothing else does.");
        [DataSourceProperty] public string AgreementsTitleText => DiText.T("DI_REALM_OUR_AGREEMENTS_TITLE", "Our agreements");
        [DataSourceProperty] public string TributeCaptionText => DiText.T("DI_REALM_TRIBUTE_PER_PERIOD", "Tribute, per period");
        [DataSourceProperty] public string ReportButtonText => DiText.T("DI_REALM_WRITE_A_REPORT_TO_FILE", "Write a report to file");
        [DataSourceProperty] public string ReportNoteText => DiText.T("DI_REALM_EVERYTHING_ON_THIS_TAB_PLUS", "Everything on this tab, plus every kingdom's numbers, to Documents - DiplomacyIntrigue - Reports.");

        [DataSourceProperty] public DiCounterIntelVM CounterIntel { get; } = new DiCounterIntelVM();

        /// <summary>Who holds each political office of the realm and what their skill moves (design 08 §10).</summary>
        [DataSourceProperty] public DiStatecraftVM Statecraft { get; } = new DiStatecraftVM();

        public DiRealmVM(Action onShow, Action openCourt = null)
        {
            _standingColor = GoldColor;
            _greedColor = MutedColor;
            _onShow = onShow;
            _openCourt = openCourt;
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
            // First, and on its own gate: the section follows the espionage toggle, not diplomacy's.
            CounterIntel.Rebuild();
            Statecraft.Rebuild();

            StandingTitle = DiText.T("DI_REALM_NO_REALM_2", "No realm");
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
                StandingTitle = DiText.T("DI_REALM_VASSAL_OF_NAME_2", "Vassal of {NAME}", ("NAME", patron.Name));
                StandingColor = NegativeColor;
                StandingDetail = DiText.T("DI_REALM_ONE_KINGDOM_ANSWERS_TO_NAME_2",
                    "one kingdom answers to {NAME}",
                    ("NAME", patron.Name));
            }
            else if (vassalCount > 0)
            {
                StandingTitle = DiText.T("DI_REALM_HEGEMON_2", "Hegemon");
                StandingColor = GoldColor;
                StandingDetail = vassalCount == 1 ? DiText.T("DI_REALM_ONE_KINGDOM_ANSWERS_TO_YOU_2", "one kingdom answers to you") : DiText.T("DI_REALM_KINGDOMS_ANSWER_TO_YOU_VASSALCOUNT_2",
                    "{VASSALCOUNT} kingdoms answer to you",
                    ("VASSALCOUNT", vassalCount));
                // The mockup's right-hand note. A hegemon is derived from its vassalage
                // links, so the note is the truth about the title rather than flavour.
                StandingNote = DiText.T("DI_REALM_HEGEMON_IS_DERIVED_NEVER_STORED_2",
                    "A hegemon is derived, never stored.");
            }
            else
            {
                StandingTitle = DiText.T("DI_REALM_INDEPENDENT_2", "Independent");
                StandingColor = MutedColor;
                StandingDetail = DiText.T("DI_REALM_NO_KINGDOM_ANSWERS_TO_YOU_AND_2",
                    "no kingdom answers to you, and you answer to none");
            }

            // A realm at war with itself says so first (design 07 §6). What it is abroad is kept
            // in the detail line: a divided hegemon is still a hegemon.
            var civil = Settings.Current.EnableIntrigue ? InternalWars.OngoingIn(state, us) : null;
            if (civil != null)
            {
                var held = civil.Faction?.Fiefs.Count ?? 0;
                StandingTitle = DiText.T("DI_REALM_DIVIDED_2", "Divided");
                StandingColor = NegativeColor;
                StandingDetail = SideChange.SideName(civil, true) + " holds " + held + " of the realm's "
                                 + us.Fiefs.Count + " fiefs"
                                 + (patron != null ? ", and the realm still answers to " + patron.Name
                                    : vassalCount > 0 ? ", and " + vassalCount + (vassalCount == 1 ? " kingdom still answers" : " kingdoms still answer") + " to you"
                                    : string.Empty);
                StandingNote = civil.IsRebel(Clan.PlayerClan) ? DiText.T("DI_REALM_YOU_FIGHT_UNDER_THE_REALM_FOREIGN_SIDENAME_2",
                    "You fight under {SIDENAME}: the realm's foreign wars are the crown's, not yours.",
                    ("SIDENAME", SideChange.SideName(civil, true))) : DiText.T("DI_REALM_THE_HOUSES_OF_THE_RISING_KEEP_2",
                    "The houses of the rising keep their seats and votes while they fight.");
            }

            var sphereHead = Hegemony.SphereHead(state, us);
            SphereStrengthText = DiText.T("DI_REALM_SPHERE_SPHERESTRENGTH_2",
                "sphere {SPHERESTRENGTH}",
                ("SPHERESTRENGTH", Hegemony.SphereStrength(state, sphereHead).ToString("0")));
            var dominance = Power.Dominance(us);
            var ambition = Power.Ambition(us);
            var greed = Power.Greed(state, us);
            DominanceText = DiText.T("DI_REALM_DOMINANCE_DOMINANCE_2",
                "dominance {DOMINANCE}",
                ("DOMINANCE", dominance.ToString("0.00")));
            AmbitionText = DiText.T("DI_REALM_AMBITION_AMBITION_2",
                "ambition {AMBITION}",
                ("AMBITION", ambition.ToString("0.00")));
            GreedText = DiText.T("DI_REALM_GREED_GREED_2", "greed {GREED}", ("GREED", greed.ToString("0.00")));
            GreedColor = greed > 0f ? NegativeColor : PositiveColor;

            // The bond we answer to, if there is one.
            var ourLink = Hegemony.VassalageOf(state, us);
            if (ourLink != null)
            {
                PatronLine = DiText.T("DI_REALM_WE_ANSWER_TO_HOLD_TRIBUTE_PER_NAME_HOLDOF_HOLDMEANING_2",
                    "We answer to {NAME}   -   hold {HOLDOF} ({HOLDMEANING})   -   tribute {TRIBUTEAMOUNT} per period   -   {TERMLEFT}",
                    ("NAME", ourLink.DominantParty.Name),
                    ("HOLDOF", Hegemony.HoldOf(ourLink).ToString("0")),
                    ("HOLDMEANING", DiplomacyMenu.HoldMeaning(state, ourLink)),
                    ("TRIBUTEAMOUNT", ourLink.TributeAmount),
                    ("TERMLEFT", TermLeft(ourLink)));
                if (ourLink.DefianceMarks > 0)
                    PatronLine += DiText.T("DI_REALM_WE_HAVE_DEFIED_THEM_TIME_DEFIANCEMARKS_2",
                        "   -   we have defied them {DEFIANCEMARKS} time(s)",
                        ("DEFIANCEMARKS", ourLink.DefianceMarks));
            }
        }

        // ----- the wars strip --------------------------------------------------

        private void ComposeWars(ModState state, Kingdom us, MBBindingList<DiRealmWarVM> wars)
        {
            // A civil war heads the strip, and only points to the Court tab, where it is shown in
            // full: sides, prices, conceding (design 07 §6). It has no war score and no peace table.
            var civil = Settings.Current.EnableIntrigue ? InternalWars.OngoingIn(state, us) : null;
            var playerRebel = civil != null && civil.IsRebel(Clan.PlayerClan);
            if (civil != null)
            {
                var ours = playerRebel ? civil.RebelExhaustion : civil.CrownExhaustion;
                var theirs = playerRebel ? civil.CrownExhaustion : civil.RebelExhaustion;
                var against = 0;
                foreach (var clan in Court.MembersOf(us))
                    if (civil.IsRebel(clan) != playerRebel) against++;

                wars.Add(new DiRealmWarVM(
                    playerRebel ? DiText.T("DI_REALM_CIVIL_WAR_AGAINST_THE_CROWN_2",
                        "Civil war: against the crown") : DiText.T("DI_REALM_CIVIL_WAR_SIDENAME_2",
                        "Civil war: {SIDENAME}",
                        ("SIDENAME", SideChange.SideName(civil, true))),
                    DiText.T("DI_REALM_DAYS_FOR_THE_THRONE_ELAPSEDDAYSUNTILNOW_2",
                        "{ELAPSEDDAYSUNTILNOW} days, for the throne",
                        ("ELAPSEDDAYSUNTILNOW", (int)civil.StartedOn.ElapsedDaysUntilNow)),
                    against == 1 ? DiText.T("DI_REALM_OUR_EXHAUSTION_THEIRS_HOUSE_AGAINST_US_OURS_THEIRS_AGAINST_2",
                        "our exhaustion {OURS}   -   theirs {THEIRS}   -   {AGAINST} house against us",
                        ("OURS", ours.ToString("0.0")),
                        ("THEIRS", theirs.ToString("0.0")),
                        ("AGAINST", against)) : DiText.T("DI_REALM_OUR_EXHAUSTION_THEIRS_HOUSES_AGAINST_US_OURS_THEIRS_AGAINST_2",
                        "our exhaustion {OURS}   -   theirs {THEIRS}   -   {AGAINST} houses against us",
                        ("OURS", ours.ToString("0.0")),
                        ("THEIRS", theirs.ToString("0.0")),
                        ("AGAINST", against)),
                    string.Empty,
                    MutedColor,
                    civil.Faction == null ? NegativeColor : Color.FromUint(civil.Faction.Color),
                    DiText.T("DI_REALM_OPEN_THE_COURT_2", "Open the court"),
                    DiText.T("DI_REALM_SIDES_PRICES_AND_CONCEDING_ARE_ON_2",
                        "Sides, prices and conceding are on the Court tab."),
                    DiText.T("DI_REALM_THE_CIVIL_WAR_IS_SHOWN_ON_2",
                        "The civil war is shown on the Court tab: both sides, what each house would cost to change sides, and how it ends."),
                    () => _openCourt?.Invoke(),
                    string.Empty));
            }

            foreach (var war in state.OngoingWarsOf(us))
            {
                var enemy = war.Other(us);
                if (enemy == null) continue;
                var target = enemy;
                var score = WarScore.For(war, us);
                var budget = PeaceTable.BudgetFor(war, us);
                var allowance = PeaceTable.DescribeAllowance(state, war, us);
                // The mockup's button label and sub differ by what the war has earned, and
                // for whom: our own table when it earned us something, the loser's table
                // when it earned them something (ShowPeace opens that one too), a white
                // peace only when neither side has anything to ask.
                var theirBudget = PeaceTable.BudgetFor(war, enemy);
                var label = DiplomacyMenu.PeaceButtonLabel(budget, theirBudget);
                var sub = DiplomacyMenu.PeaceButtonSub(budget, theirBudget, enemy);
                wars.Add(new DiRealmWarVM(
                    enemy.Name.ToString(),
                    war.DaysElapsed.ToString("0") + " days"
                        + (war.Justification == CasusBelliType.None
                            ? ", no claim on record"
                            : " over " + war.Justification)
                        + (war.IsObligationWar && war.CalledBy != null
                            ? "   -   called in by " + war.CalledBy.Name : "")
                        // The rising is at war with the crown and nobody else, so a rebel's
                        // parties are not in this war at all.
                        + (playerRebel ? "   -   the crown's war, not yours" : ""),
                    "our exhaustion " + war.ExhaustionOf(us).ToString("0.0")
                        + (StatecraftModel.Enabled
                            ? " (resolve " + StatecraftModel.Factor(StatecraftTerms.ResolveFactor(us)) + ")"
                            : "")
                        + "   -   their condition "
                        + ExhaustionBands.Condition(war.ExhaustionOf(enemy)),
                    (score >= 0f ? "+" : "") + score.ToString("0"),
                    score >= 0f ? PositiveColor : NegativeColor,
                    Color.FromUint(enemy.Color),
                    label,
                    sub,
                    DiText.T("DI_REALM_OPENS_THE_PEACE_TABLE_WHAT_THIS_ALLOWANCE_2",
                        "Opens the peace table: what this war has earned, and what they will sign. {ALLOWANCE}",
                        ("ALLOWANCE", allowance)),
                    () => DiplomacyMenu.ShowPeace(state, us, target)));
            }
            WarsCountText = wars.Count.ToString("0");
        }

        // ----- column 1: our sphere --------------------------------------------

        private void ComposeVassals(ModState state, Kingdom us, MBBindingList<DiRealmVassalVM> vassals)
        {
            var links = new List<Treaty>();
            Hegemony.CollectVassalages(state, us, links);
            // AC1: the control is the ruler's alone. A lord of the realm would be shown a button for
            // an order he cannot give, and a realm with no vassal has no row to put it on.
            var maySummon = us.Leader == Hero.MainHero && links.Count > 0;

            for (var i = 0; i < links.Count; i++)
            {
                var link = links[i];
                var hold = Hegemony.HoldOf(link);
                var target = Hegemony.HoldTarget(state, link, out _);
                var threshold = Hegemony.SecessionThreshold(state, link);
                var subordinate = link.SubordinateParty;

                // Quoted here, not in the row: the composing method already has the state, and a
                // row reaching for CoreBehavior.State would be a second way to ask (CLAUDE.md 3).
                // The act is injected as the war row's is, so the click does not resolve anything.
                Summons.Quote summonQuote = null;
                Action issueSummon = null;
                if (maySummon && subordinate != null)
                {
                    summonQuote = Summons.QuoteFor(state, us, subordinate);
                    var who = subordinate;
                    issueSummon = () => IssueSummon(state, us, who);
                }

                vassals.Add(new DiRealmVassalVM(
                    link.SubordinateParty.Name.ToString(),
                    subordinate != null ? Color.FromUint(subordinate.Color) : GoldColor,
                    (int)hold,
                    (int)(threshold < 0f ? 0f : threshold > 100f ? 100f : threshold),
                    (int)(target < 0f ? 0f : target > 100f ? 100f : target),
                    "hold " + hold.ToString("0") + "  ->  " + DriftWord(hold, target)
                        + " " + target.ToString("0"),
                    DiText.T("DI_REALM_REVOLTS_BELOW_THRESHOLD_2",
                        "revolts below {THRESHOLD}",
                        ("THRESHOLD", threshold.ToString("0"))),
                    link.DefianceMarks,
                    DiText.T("DI_REALM_PER_PERIOD_TRIBUTEAMOUNT_2",
                        "{TRIBUTEAMOUNT} per period",
                        ("TRIBUTEAMOUNT", link.TributeAmount)),
                    DiText.T("DI_REALM_RENEWS_IN_TERMLEFT_2",
                        "renews in {TERMLEFT}",
                        ("TERMLEFT", TermLeft(link))),
                    BuildTermChips(state, link),
                    summonQuote,
                    issueSummon));
            }

            if (vassals.Count > 0)
            {
                var first = links[0].SubordinateParty;
                VassalNote = DiText.T("DI_REALM_PROTECTION_IS_THE_HALF_OF_THE_NAME_2",
                    "Protection is the half of the bargain you owe: answer {NAME} when it is attacked, or watch this bar fall.",
                    ("NAME", first.Name));
            }
        }

        /// <summary>
        /// The act the row's button makes, and nothing else: <see cref="Summons.Issue"/> is the one
        /// resolver the AI and the console levers reach too, so the price on the button is the price
        /// charged (CLAUDE.md 3).
        ///
        /// The panel is rebuilt either way. A refusal moves nothing and is worth reading off a row
        /// that says so, and Issue re-quotes rather than trusting this row's snapshot, so a world
        /// that moved between the quote and the click cannot buy at a stale figure.
        /// </summary>
        private void IssueSummon(ModState state, Kingdom us, Kingdom vassal)
        {
            try
            {
                if (!Summons.Issue(state, us, vassal, out var failed))
                    Log.Notify("No summons: " + failed, Colors.Red);
                Rebuild();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Giving a summons from the Realm tab failed.", ex);
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
            AddChip(chips, DiText.T("DI_REALM_PROTECTION_2", "protection"), t.Protection);
            // Only when it pulls: the row already holds nine chips, and a tenth that reads 0.0
            // on nearly every link would crowd the others for nothing. Zero adds nothing to the
            // sum the chips spell out, so leaving it out keeps them matching the target.
            if (t.LegalNeglect > 0f) AddChip(chips, DiText.T("DI_REALM_LEGAL_NEGLECT_2", "legal neglect"), -t.LegalNeglect);
            AddChip(chips, DiText.T("DI_REALM_FEAR_2", "fear"), t.Fear);
            AddChip(chips, DiText.T("DI_REALM_TRUST_2", "trust"), t.Trust);
            AddChip(chips, DiText.T("DI_REALM_TRIBUTE_2", "tribute"), -t.Tribute);
            AddChip(chips, DiText.T("DI_REALM_WARS_2", "wars"), -t.Wars);
            AddChip(chips, DiText.T("DI_REALM_RIVAL_2", "rival"), -t.Rival);
            AddChip(chips, DiText.T("DI_REALM_CULTURE_2", "culture"), -t.Culture);
            AddChip(chips, DiText.T("DI_REALM_DREAD_2", "dread"), -t.Dread);
            AddChip(chips, DiText.T("DI_REALM_AUTHORITY_2", "authority"), t.Authority);
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
            if (target > hold + 1f) return DiText.T("DI_REALM_DRIFTING_UP_TO_2", "drifting up to");
            if (target < hold - 1f) return DiText.T("DI_REALM_DRIFTING_DOWN_TO_2", "drifting down to");
            return DiText.T("DI_REALM_STEADY_AT_2", "steady at");
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
                if (!other.IsRealm() || !Hegemony.IsHegemon(state, other)) continue;
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
                SphereGapNote = biggestRivalStrength >= ourStrength ? DiText.T("DI_REALM_SPHERE_OUTWEIGHS_YOURS_BY_EVERY_COURT_HEADTEXT_GAP_2",
                    "{HEADTEXT}'s sphere outweighs yours by {GAP}. Every court that belongs to neither reads that gap when it decides whom to pact with.",
                    ("HEADTEXT", biggestRival.HeadText),
                    ("GAP", gap.ToString("0"))) : DiText.T("DI_REALM_YOUR_SPHERE_OUTWEIGHS_BY_EVERY_COURT_HEADTEXT_GAP_2",
                    "Your sphere outweighs {HEADTEXT} by {GAP}. Every court that belongs to neither reads that gap when it decides whom to pact with.",
                    ("HEADTEXT", biggestRival.HeadText),
                    ("GAP", gap.ToString("0")));
            }
        }

        // ----- column 2: our claims --------------------------------------------

        private void ComposeClaims(ModState state, Kingdom us,
            MBBindingList<DiRealmClaimGroupVM> groups, MBBindingList<DiRealmFabricationVM> fabrications)
        {
            var live = 0;
            foreach (var other in Kingdom.All)
            {
                if (other == us || !other.IsRealm()) continue;
                var rows = new MBBindingList<DiRealmClaimVM>();
                foreach (var claim in ClaimRegistry.LiveClaims(state, us, other))
                {
                    live++;
                    var footer = claim.AllowsFiefDemands ? DiText.T("DI_REALM_ENTITLES_LAND_2", "entitles land") : "";
                    if (claim.ExpiresOn != CampaignTime.Never)
                    {
                        var daysLeft = (claim.ExpiresOn - CampaignTime.Now).ToDays;
                        var expiry = DiText.T("DI_REALM_AGES_OUT_IN_DAYS_MAX_2",
                            "ages out in {MAX} days",
                            ("MAX", Math.Max(0, (int)daysLeft)));
                        footer = footer.Length > 0 ? footer + "   -   " + expiry : expiry;
                    }
                    // A broken-treaty claim carries its own story: the mockup's line under
                    // the claim name, with the year the pact died from the claim's record.
                    var note = claim.Type == CasusBelliType.BrokenTreaty ? DiText.T("DI_REALM_THEY_TORE_UP_PACT_IN_WAR_GETYEAR_2",
                        "They tore up a pact in {GETYEAR}. A war on this needs no excuse.",
                        ("GETYEAR", claim.AcquiredOn.GetYear)) : "";
                    rows.Add(new DiRealmClaimVM(
                        CasusBelli.NameOf(claim.Type),
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

            ClaimsCountText = DiText.T("DI_REALM_LIVE_BEING_FABRICATED_LIVE_COUNT_2",
                "{LIVE} live   -   {COUNT} being fabricated",
                ("LIVE", live),
                ("COUNT", fabrications.Count));
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
                    Models.Treaty.NameOf(treaty.Type),
                    Color.FromUint(other.Color),
                    TermLeft(treaty),
                    daysLeft >= 0f && daysLeft < ExpiringSoonDays ? NegativeColor : MutedColor,
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
            return DiText.T("DI_REALM_TREATY_2",
                "{TREATY}",
                ("TREATY", treaty.ExpiresOn == CampaignTime.Never ? "open-ended" : Duration(treaty)));
        }

        /// <summary>Compact duration the mockup uses: "1y 40d", "6y", "45d".</summary>
        private static string Duration(Treaty treaty)
        {
            var days = (float)(treaty.ExpiresOn - CampaignTime.Now).ToDays;
            if (days <= 0f) return DiText.T("DI_REALM_EXPIRED_2", "expired");
            // The campaign year is 84 days (four seasons of 21).
            var years = (int)(days / 84f);
            var rem = (int)(days - years * 84f);
            if (years <= 0f) return DiText.T("DI_REALM_REM_2", "{REM}d", ("REM", rem));
            return rem > 0 ? years + "y " + rem + "d" : years + "y";
        }

        // The six kinds of agreement are named by Treaty.NameOf, in one place, because eight other
        // call sites were writing the enum and printing "NonAggressionPact" on a screen.

        /// <summary>What each treaty type means for us, in the direction it actually runs.</summary>
        private static string AgreementDetail(Treaty treaty, Kingdom us, Kingdom other)
        {
            switch (treaty.Type)
            {
                case TreatyType.Vassalage:
                    return treaty.DominantParty == us ? DiText.T("DI_REALM_PAYS_YOU_AND_FIGHTS_FOR_YOU_NAME_2",
                        "{NAME} - pays you, and fights for you",
                        ("NAME", other.Name)) : DiText.T("DI_REALM_YOU_PAY_THEM_AND_ANSWER_THEIR_NAME_2",
                        "{NAME} - you pay them, and answer their call",
                        ("NAME", other.Name));
                case TreatyType.Alliance:
                    return DiText.T("DI_REALM_ANSWERS_ANY_CALL_AND_YOU_ANSWER_NAME_2",
                        "{NAME} - answers any call, and you answer theirs",
                        ("NAME", other.Name));
                case TreatyType.DefensivePact:
                    return DiText.T("DI_REALM_JOINS_IF_YOU_ARE_ATTACKED_AND_NAME_2",
                        "{NAME} - joins if you are attacked, and you if they are",
                        ("NAME", other.Name));
                case TreatyType.TributaryPact:
                    return treaty.TributePayer == other
                        ? other.Name + " - " + treaty.TributeAmount + " to you, no army owed"
                        : other.Name + " - you pay " + treaty.TributeAmount + ", no army owed";
                case TreatyType.NonAggressionPact:
                    return DiText.T("DI_REALM_NEITHER_MAY_DECLARE_WAR_NAME_2",
                        "{NAME} - neither may declare war",
                        ("NAME", other.Name));
                default:
                    return DiText.T("DI_REALM_ACTIVE_TRUCE_NAME_2",
                        "{NAME} - active truce",
                        ("NAME", other.Name));
            }
        }
    }

    /// <summary>One ongoing war of ours: the enemy, the story, both exhaustions, the score.</summary>
    internal sealed class DiRealmWarVM : ViewModel
    {
        private readonly Action _negotiate;

        public DiRealmWarVM(string name, string detail, string conditionText,
            string scoreText, Color scoreColor, Color accentColor,
            string buttonLabel, string buttonExplanation, string buttonHint, Action negotiate,
            string scoreCaption = "war score")
        {
            Name = name;
            Detail = detail;
            ConditionText = conditionText;
            ScoreCaption = scoreCaption;
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

        /// <summary>"war score" for a war between kingdoms; empty for a civil war, which has none.</summary>
        [DataSourceProperty] public string ScoreCaption { get; }
        [DataSourceProperty] public string ScoreText { get; }
        [DataSourceProperty] public Color ScoreColor { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public string ButtonLabel { get; }
        [DataSourceProperty] public string ButtonExplanation { get; }
        [DataSourceProperty] public TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel ButtonHint { get; }

        // The row's HintWidget binds both of these (DiRealmPanel.xml) and this class declared
        // neither, so hovering a war row asked a view model that had no such method. Gauntlet
        // binds a missing command to nothing and the tooltip never showed, which is why a price
        // that was there to be read went unread. Empty on purpose: the hint is a constant, so
        // there is nothing for the hover to compute.
        public void ExecuteBeginHint() { }

        public void ExecuteEndHint() { }

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
    /// the terms pulling it, its defiance marks, its tribute and its renewal - and, for the
    /// ruler alone, what it would cost to call its parties up to serve under us
    /// (design 04 5.2a, story 1.10c).
    ///
    /// The summon surface is built from <see cref="Summons.QuoteFor"/>'s one quote and nothing
    /// else: the label, the price strip and the disabled reason are the numbers the act is
    /// decided on, so they cannot drift from it (CLAUDE.md 3). The row does not resolve anything
    /// itself - the act arrives as an injected <see cref="Action"/>, like the war row's.
    /// </summary>
    internal sealed class DiRealmVassalVM : ViewModel
    {
        private const int BarWidth = 160;

        private static readonly Color EmptyDotColor = Color.ConvertStringToColor("#3A2F24FF");
        private static readonly Color GoldColor = Color.ConvertStringToColor("#D9A441FF");

        /// <summary>
        /// Summons marks an excuse with this prefix so that one refusal path reads both kinds
        /// correctly. The row says what the excuse is instead of repeating the marker.
        /// </summary>
        private const string ExcusedMarker = "excused: ";

        private readonly Summons.Quote _summon;
        private readonly Action _issueSummon;
        private bool _armed;
        private string _summonText = string.Empty;
        private string _summonNote = string.Empty;

        public DiRealmVassalVM(string name, Color accentColor, int holdValue, int thresholdValue,
            int driftValue, string holdText, string thresholdText, int defianceMarks,
            string tributeText, string termText, MBBindingList<DiRealmTermVM> terms,
            Summons.Quote summonQuote = null, Action issueSummon = null)
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
            DefianceText = defianceMarks > 0 ? DiText.T("DI_REALM_DEFIANCE_MARK_DEFIANCEMARKS_2",
                "defiance: {DEFIANCEMARKS} mark(s)",
                ("DEFIANCEMARKS", defianceMarks)) : DiText.T("DI_REALM_DEFIANCE_NONE_MARKS_AND_THE_LINK_DEFIANCEMARKSTOLAPSE_2",
                "defiance: none - {DEFIANCEMARKSTOLAPSE} marks and the link does not renew",
                ("DEFIANCEMARKSTOLAPSE", DiplomacyConstants.DefianceMarksToLapse));
            // One slot per mark the bond can take before it lapses, filled as they land.
            Dot1Color = defianceMarks >= 1 ? GoldColor : EmptyDotColor;
            Dot2Color = defianceMarks >= 2 ? GoldColor : EmptyDotColor;
            Dot3Color = defianceMarks >= 3 ? GoldColor : EmptyDotColor;
            TributeText = tributeText;
            TermText = termText;
            Terms = terms;

            // A null quote is the whole gate: nobody but the ruler is given one, so nothing is
            // drawn for a lord, and a realm with no vassals has no row to draw it on (AC1).
            if (summonQuote == null) return;
            _summon = summonQuote;
            _issueSummon = issueSummon;

            HasSummon = true;
            HasSummonPrice = summonQuote.Eligible;
            SummonInfluence = summonQuote.Influence;
            SummonGold = summonQuote.Gold;
            SummonEnabled = summonQuote.Eligible && summonQuote.Affordable;
            // Eagerly: a hover must not have to reach for campaign state to describe a quote that
            // was taken when the panel was built, and a tooltip may not wrap, so it is one line.
            var hint = HintOf(summonQuote);
            SummonHint = new TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel(() => hint);
            ComposeSummon();
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

        // ----- the summons -----------------------------------------------------

        [DataSourceProperty] public bool HasSummon { get; }
        [DataSourceProperty] public bool HasSummonPrice { get; }
        [DataSourceProperty] public int SummonInfluence { get; }
        [DataSourceProperty] public int SummonGold { get; }
        [DataSourceProperty] public bool SummonEnabled { get; }
        [DataSourceProperty] public TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel SummonHint { get; }

        [DataSourceProperty]
        public string SummonText
        {
            get => _summonText;
            set { if (value == _summonText) return; _summonText = value; OnPropertyChangedWithValue(value, nameof(SummonText)); }
        }

        /// <summary>
        /// The reason the order cannot be given, in full, under the button - a short note is never
        /// enough here, because "not now" with nothing after it is how a button looks broken.
        /// What it is worth when it is available is a line, and the arithmetic is in the hint.
        /// </summary>
        [DataSourceProperty]
        public string SummonNote
        {
            get => _summonNote;
            set { if (value == _summonNote) return; _summonNote = value; OnPropertyChangedWithValue(value, nameof(SummonNote)); }
        }

        [DataSourceProperty]
        public Color SummonNoteColor
        {
            get => SummonEnabled ? DiRealmVM.MutedColor : DiRealmVM.NegativeColor;
        }

        private void ComposeSummon()
        {
            if (_summon == null) return;
            var q = _summon;
            var price = DiText.T("DI_REALM_INFLUENCE_DENARS_INFLUENCE_GOLD_2",
                "{INFLUENCE} influence, {GOLD} denars",
                ("INFLUENCE", q.Influence.ToString("N0")),
                ("GOLD", q.Gold.ToString("N0")));
            // A quote refused before it was priced has no price and no party count to name: the
            // first build printed "Summon 0 parties - 0 influence, 0 denars" on a button that
            // was disabled for want of a war (seen live 2026-10-01).
            SummonText = !q.Priced
                ? "Summon"
                : _armed
                    ? "Confirm: pay " + price
                    : "Summon " + q.PartyCount + (q.PartyCount == 1 ? " party - " : " parties - ") + price;

            if (!q.Eligible)
                SummonNote = (q.Excused ? "Excused: " : "Not now: ") + Plain(q.Reason) + ".";
            else if (!q.Affordable)
                SummonNote = "Cannot pay: " + Plain(q.Short) + ".";
            else if (_armed)
                SummonNote = DiText.T("DI_REALM_CLICK_AGAIN_TO_PAY_THE_PRICE_2",
                    "Click again to pay. The price is charged now and nothing is refunded, whether they serve or refuse.");
            else
                SummonNote = q.WillServe ? DiText.T("DI_REALM_ITS_RULER_OWN_PARTY_IS_NEVER_2",
                    "Its ruler's own party is never taken, and they are sent home before your army takes anything they are at peace with. They would serve.") : DiText.T("DI_REALM_ITS_RULER_OWN_PARTY_IS_NEVER_3",
                    "Its ruler's own party is never taken, and they are sent home before your army takes anything they are at peace with. They may refuse - and a refusal is a mark of defiance.");
        }

        /// <summary>
        /// Two clicks: the first arms and names the price, the second gives the order. The same
        /// shape as making amends, for the same two reasons - the act is dear and cannot be undone,
        /// and the test bridge can click a panel button but not inside an inquiry, which is also
        /// why the price and the reason sit on the panel rather than in a prompt.
        /// </summary>
        public void ExecuteSummon()
        {
            try
            {
                if (_summon == null || !_summon.Eligible || !_summon.Affordable) return;
                if (!_armed)
                {
                    _armed = true;
                    ComposeSummon();
                    return;
                }
                _armed = false;
                _issueSummon?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Ordering a summons from the Realm tab failed.", ex);
            }
        }

        // The war row, above, binds these same two names to its own hint and declares neither: a
        // pre-existing gap on this panel, left alone because the war button's hover is not this
        // change's business. Declared here so this row's own hint is not in that position.
        public void ExecuteBeginHint() { }

        public void ExecuteEndHint() { }

        private static string Plain(string reason)
            => reason != null && reason.StartsWith(ExcusedMarker)
                ? reason.Substring(ExcusedMarker.Length)
                : reason ?? string.Empty;

        /// <summary>
        /// The whole quote in one line for the hover: the war, what would be taken and from how
        /// many, the answer Hold gives, and the price term by term with both skill factors. This is
        /// the arithmetic the note under the button has no room for, and it is the same arithmetic
        /// <c>diplomacy.summons_value</c> prints - one resolver, three readers.
        /// </summary>
        private static string HintOf(Summons.Quote q)
        {
            var sb = new StringBuilder();
            if (!q.Eligible)
            {
                sb.Append("No summons can be given. ").Append(Plain(q.Reason)).Append('.');
                if (q.Link != null) sb.Append(" Hold ").Append(q.Hold.ToString("0")).Append('.');
                if (q.CooldownLeft > 0f)
                    sb.Append(" It can be asked again in ").Append(q.CooldownLeft.ToString("0")).Append(" days.");
                if (q.Excused) sb.Append(" An excuse costs nobody anything: no mark, no charge.");
                return sb.ToString();
            }

            sb.Append("Serving ").Append(q.Vassal.Name).Append(" in the war against ").Append(q.Enemy != null ? q.Enemy.Name.ToString() : "?")
              .Append(": ").Append(q.PartyCount).Append(" of ").Append(q.Available)
              .Append(" eligible war parties, nearest you first, its ruler's own party never taken. Hold ")
              .Append(q.Hold.ToString("0")).Append(" - ")
              .Append(q.WillServe ? "it would serve." : "it would refuse: " + q.Refusal + ".");

            sb.Append(" Influence ").Append(DiplomacyConstants.SummonsInfluenceBase).Append(" + ")
              .Append(DiplomacyConstants.SummonsInfluencePerParty).Append(" x ").Append(q.PartyCount)
              .Append(" = ").Append(DiplomacyConstants.SummonsInfluenceBase + DiplomacyConstants.SummonsInfluencePerParty * q.PartyCount)
              .Append(" x ").Append(q.InfluenceFactor.ToString("0.00"))
              .Append(" (Leadership against its ruler) = ").Append(q.Influence.ToString("N0")).Append('.');
            sb.Append(" Gold ").Append(DiplomacyConstants.SummonsGoldBase).Append(" + ")
              .Append(DiplomacyConstants.SummonsGoldPerParty).Append(" x ").Append(q.PartyCount)
              .Append(" = ").Append(DiplomacyConstants.SummonsGoldBase + DiplomacyConstants.SummonsGoldPerParty * q.PartyCount)
              .Append(" x ").Append(q.GoldFactor.ToString("0.00"))
              .Append(" (Trade, the realm's treasurer) = ").Append(q.Gold.ToString("N0")).Append(" denars.");

            sb.Append(" They march ").Append(q.DurationDays.ToString("0"))
              .Append(" days or until the war ends, and cannot be summoned again for ")
              .Append(DiplomacyConstants.SummonsCooldownDays.ToString("0")).Append(" days.");
            sb.Append(" The army charges its own per-party influence cost to each leader's clan on top of this, "
                    + "and a refusal refunds nothing (design 04 5.2a).");
            return sb.ToString();
        }
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
        /// <summary>The column heading the claim row's figures sit under (story 4.1 §9).</summary>
        [DataSourceProperty] public string LandColumnText => DiText.T("DI_REALM_LAND_COLUMN", "LAND");

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
