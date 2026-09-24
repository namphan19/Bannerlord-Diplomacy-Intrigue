using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// The Court tab: the player's own court, in full. Phase 2.7, the design the lead
    /// approved on 2026-09-23 (artifact "Court Intrigue Screen", board "Your own court").
    ///
    /// **Every number here is read from the resolver the AI uses** - LoyaltyModel,
    /// BlocModel, LegitimacyRegistry, GrievanceRegistry, SuccessionModel - never recomputed
    /// for display. CLAUDE.md §3: a number shown in the UI is the number the AI used. The
    /// loyalty breakdown in particular is <see cref="LoyaltyModel.Explain"/> term for term,
    /// the same object `diplomacy.loyalty` prints.
    ///
    /// Your own court is shown exactly; a rival court is not shown here at all (design 02
    /// §9.1 - it gets bands only, on its Encyclopedia page).
    /// </summary>
    internal sealed class DiCourtVM : ViewModel
    {
        // Band colours, the same four as the approved design and the loyalty legend.
        internal static readonly Color ReliableColor = Color.ConvertStringToColor("#8FB35EFF");
        internal static readonly Color TransactionalColor = Color.ConvertStringToColor("#D9B54AFF");
        internal static readonly Color DisaffectedColor = Color.ConvertStringToColor("#D98C4AFF");
        internal static readonly Color DefectionColor = Color.ConvertStringToColor("#D9695AFF");
        internal static readonly Color TextColor = Color.ConvertStringToColor("#E0CFA8FF");
        internal static readonly Color MutedColor = Color.ConvertStringToColor("#A89878FF");

        /// <summary>Hides every other panel; supplied by the management mixin.</summary>
        private readonly Action _onShow;

        private bool _show;
        private bool _tabVisible;
        private string _realmName = string.Empty;
        private string _courtLine = string.Empty;
        private string _legitimacyText = string.Empty;
        private string _legitimacyNote = string.Empty;
        private int _legitimacyAmount;
        private Color _legitimacyColor = ReliableColor;
        private string _worstWarText = string.Empty;
        private string _worstWarNote = string.Empty;
        private string _clanCountText = string.Empty;
        private string _successionTitle = string.Empty;
        private string _successionDetail = string.Empty;
        private bool _hasBlocs;
        private bool _isAtWar;
        private DiCivilWarVM _civilWar;
        private DiCourtClanVM _selected;
        private MBBindingList<DiCourtBlocVM> _blocs = new MBBindingList<DiCourtBlocVM>();
        private MBBindingList<DiCourtClanVM> _clans = new MBBindingList<DiCourtClanVM>();
        private MBBindingList<DiCourtTermVM> _terms = new MBBindingList<DiCourtTermVM>();
        private MBBindingList<DiCourtGrievanceVM> _grievances = new MBBindingList<DiCourtGrievanceVM>();

        /// <summary>
        /// The live Court view model while the Kingdom screen is open, for
        /// <c>diplomacy.test_court_select</c> only. The GABS bridge clicks the first widget
        /// whose text matches, and every clan name in this panel also exists in vanilla's
        /// hidden Clans list earlier in the tree - so a row here cannot be clicked from a
        /// tool call at all. Cleared by the management mixin when the screen closes.
        /// </summary>
        internal static DiCourtVM Current;

        public DiCourtVM(Action onShow)
        {
            _onShow = onShow;
            Current = this;
            RefreshTabGate();
            Rebuild();
        }

        /// <summary>Selects a clan by name, as a row click would. Test hook; see <see cref="Current"/>.</summary>
        internal string SelectByName(string name)
        {
            // In a civil war the court is shown split by side, and the rows are the war's.
            if (_isAtWar && _civilWar != null) return _civilWar.SelectByName(name);

            for (var i = 0; i < _clans.Count; i++)
            {
                if (_clans[i].Clan.Name.ToString() != name) continue;
                Select(_clans[i]);
                return "Selected " + name + ": " + _clans[i].LoyaltyText + " " + _clans[i].BandText
                       + ", " + _terms.Count + " terms, " + _grievances.Count + " grievance(s).";
            }
            return "No clan named \"" + name + "\" in the court list.";
        }

        // ----- bound surface ---------------------------------------------------

        [DataSourceProperty]
        public bool Show
        {
            get => _show;
            set { if (value == _show) return; _show = value; OnPropertyChangedWithValue(value, nameof(Show)); }
        }

        [DataSourceProperty]
        public bool TabVisible
        {
            get => _tabVisible;
            set { if (value == _tabVisible) return; _tabVisible = value; OnPropertyChangedWithValue(value, nameof(TabVisible)); }
        }

        [DataSourceProperty] public string TabText => "Court";

        [DataSourceProperty]
        public string RealmName
        {
            get => _realmName;
            set { if (value == _realmName) return; _realmName = value; OnPropertyChangedWithValue(value, nameof(RealmName)); }
        }

        [DataSourceProperty]
        public string CourtLine
        {
            get => _courtLine;
            set { if (value == _courtLine) return; _courtLine = value; OnPropertyChangedWithValue(value, nameof(CourtLine)); }
        }

        [DataSourceProperty]
        public string LegitimacyText
        {
            get => _legitimacyText;
            set { if (value == _legitimacyText) return; _legitimacyText = value; OnPropertyChangedWithValue(value, nameof(LegitimacyText)); }
        }

        [DataSourceProperty]
        public string LegitimacyNote
        {
            get => _legitimacyNote;
            set { if (value == _legitimacyNote) return; _legitimacyNote = value; OnPropertyChangedWithValue(value, nameof(LegitimacyNote)); }
        }

        /// <summary>0-100, for the fill bar. An int: the vanilla bar's InitialAmount is Int32.</summary>
        [DataSourceProperty]
        public int LegitimacyAmount
        {
            get => _legitimacyAmount;
            set { if (value == _legitimacyAmount) return; _legitimacyAmount = value; OnPropertyChangedWithValue(value, nameof(LegitimacyAmount)); }
        }

        [DataSourceProperty]
        public Color LegitimacyColor
        {
            get => _legitimacyColor;
            set { if (value == _legitimacyColor) return; _legitimacyColor = value; OnPropertyChangedWithValue(value, nameof(LegitimacyColor)); }
        }

        [DataSourceProperty]
        public string WorstWarText
        {
            get => _worstWarText;
            set { if (value == _worstWarText) return; _worstWarText = value; OnPropertyChangedWithValue(value, nameof(WorstWarText)); }
        }

        [DataSourceProperty]
        public string WorstWarNote
        {
            get => _worstWarNote;
            set { if (value == _worstWarNote) return; _worstWarNote = value; OnPropertyChangedWithValue(value, nameof(WorstWarNote)); }
        }

        [DataSourceProperty]
        public string ClanCountText
        {
            get => _clanCountText;
            set { if (value == _clanCountText) return; _clanCountText = value; OnPropertyChangedWithValue(value, nameof(ClanCountText)); }
        }

        [DataSourceProperty]
        public string SuccessionTitle
        {
            get => _successionTitle;
            set { if (value == _successionTitle) return; _successionTitle = value; OnPropertyChangedWithValue(value, nameof(SuccessionTitle)); }
        }

        [DataSourceProperty]
        public string SuccessionDetail
        {
            get => _successionDetail;
            set { if (value == _successionDetail) return; _successionDetail = value; OnPropertyChangedWithValue(value, nameof(SuccessionDetail)); }
        }

        [DataSourceProperty]
        public bool HasBlocs
        {
            get => _hasBlocs;
            set { if (value == _hasBlocs) return; _hasBlocs = value; OnPropertyChangedWithValue(value, nameof(HasBlocs)); OnPropertyChangedWithValue(!value, nameof(NoBlocs)); }
        }

        [DataSourceProperty] public bool NoBlocs => !_hasBlocs;

        /// <summary>
        /// The player's kingdom is at war with itself: the court is shown as the war shows it -
        /// two sides, the prices, conceding - instead of as blocs and loyalty (design 07 §6).
        /// </summary>
        [DataSourceProperty]
        public bool IsAtWar
        {
            get => _isAtWar;
            set { if (value == _isAtWar) return; _isAtWar = value; OnPropertyChangedWithValue(value, nameof(IsAtWar)); OnPropertyChangedWithValue(!value, nameof(IsAtPeace)); }
        }

        [DataSourceProperty] public bool IsAtPeace => !_isAtWar;

        [DataSourceProperty]
        public DiCivilWarVM CivilWar
        {
            get => _civilWar;
            set { if (value == _civilWar) return; _civilWar = value; OnPropertyChangedWithValue(value, nameof(CivilWar)); }
        }

        [DataSourceProperty]
        public DiCourtClanVM Selected
        {
            get => _selected;
            set { if (value == _selected) return; _selected = value; OnPropertyChangedWithValue(value, nameof(Selected)); OnPropertyChangedWithValue(value != null, nameof(HasSelection)); }
        }

        [DataSourceProperty] public bool HasSelection => _selected != null;

        [DataSourceProperty]
        public MBBindingList<DiCourtBlocVM> Blocs
        {
            get => _blocs;
            set { if (value == _blocs) return; _blocs = value; OnPropertyChangedWithValue(value, nameof(Blocs)); }
        }

        [DataSourceProperty]
        public MBBindingList<DiCourtClanVM> Clans
        {
            get => _clans;
            set { if (value == _clans) return; _clans = value; OnPropertyChangedWithValue(value, nameof(Clans)); }
        }

        /// <summary>The selected clan's loyalty, term by term.</summary>
        [DataSourceProperty]
        public MBBindingList<DiCourtTermVM> Terms
        {
            get => _terms;
            set { if (value == _terms) return; _terms = value; OnPropertyChangedWithValue(value, nameof(Terms)); }
        }

        /// <summary>What the selected clan holds against the crown.</summary>
        [DataSourceProperty]
        public MBBindingList<DiCourtGrievanceVM> Grievances
        {
            get => _grievances;
            set { if (value == _grievances) return; _grievances = value; OnPropertyChangedWithValue(value, nameof(Grievances)); OnPropertyChangedWithValue(value.Count == 0, nameof(NoGrievances)); }
        }

        [DataSourceProperty] public bool NoGrievances => _grievances.Count == 0;

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
                Log.Error("UI", "Opening the Court tab failed.", ex);
            }
        }

        /// <summary>A clan row was clicked. Called by the row, never by Gauntlet directly.</summary>
        internal void Select(DiCourtClanVM row)
        {
            try
            {
                for (var i = 0; i < _clans.Count; i++) _clans[i].IsSelected = _clans[i] == row;
                Selected = row;
                ComposeSelection();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Selecting a clan in the Court tab failed.", ex);
            }
        }

        // ----- contents --------------------------------------------------------

        /// <summary>
        /// The tab appears only when the pillar it shows is running. Court intrigue has its own
        /// toggle, independent of diplomacy, and a tab onto a switched-off system would show a
        /// court that nothing is actually tracking.
        /// </summary>
        private void RefreshTabGate()
        {
            TabVisible = SubModule.Healthy && Settings.Current.EnableIntrigue;
        }

        internal void Rebuild()
        {
            try
            {
                Compose();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "The Court panel could not be rebuilt.", ex);
            }
        }

        private void Compose()
        {
            var state = CoreBehavior.State;
            var kingdom = Clan.PlayerClan?.Kingdom;

            var blocs = new MBBindingList<DiCourtBlocVM>();
            var clans = new MBBindingList<DiCourtClanVM>();

            if (state == null || kingdom == null || kingdom.RulingClan == null)
            {
                RealmName = "No realm";
                CourtLine = "You hold no fealty and keep no court.";
                LegitimacyText = string.Empty;
                LegitimacyNote = string.Empty;
                LegitimacyAmount = 0;
                ClanCountText = string.Empty;
                WorstWarText = string.Empty;
                WorstWarNote = string.Empty;
                SuccessionTitle = string.Empty;
                SuccessionDetail = string.Empty;
                Blocs = blocs;
                HasBlocs = false;
                Clans = clans;
                Selected = null;
                Terms = new MBBindingList<DiCourtTermVM>();
                Grievances = new MBBindingList<DiCourtGrievanceVM>();
                IsAtWar = false;
                CivilWar = null;
                return;
            }

            // A civil war replaces the body of the tab. The header - realm, crown legitimacy -
            // is the same in both.
            var war = Settings.Current.EnableIntrigue ? InternalWars.OngoingIn(state, kingdom) : null;
            CivilWar = war == null ? null : new DiCivilWarVM(state, war, Rebuild, _civilWar?.SelectedClan);
            IsAtWar = war != null;

            var ruling = kingdom.RulingClan;
            var playerRules = ruling == Clan.PlayerClan;

            RealmName = kingdom.Name.ToString().ToUpperInvariant();
            CourtLine = playerRules
                ? "The court of Clan " + ruling.Name + " - your own clan"
                : "The court of Clan " + ruling.Name + " - you serve " + (kingdom.Leader == null ? "its ruler" : kingdom.Leader.Name.ToString());

            // Crown legitimacy: the pool, the bar, and what last moved it.
            var legitimacy = LegitimacyRegistry.Of(state, kingdom);
            LegitimacyText = legitimacy.ToString("0.0");
            LegitimacyAmount = (int)Math.Round(legitimacy);
            LegitimacyColor = CrownColor(CourtBands.CrownOf(legitimacy));
            LegitimacyNote = LastLegitimacyReason(state, kingdom);

            // Blocs, strongest first, with the share of the court each carries.
            var courtBlocs = BlocModel.BlocsOf(state, kingdom);
            var totalPower = 0f;
            for (var i = 0; i < courtBlocs.Count; i++) totalPower += courtBlocs[i].Power;
            for (var i = 0; i < courtBlocs.Count; i++)
                blocs.Add(new DiCourtBlocVM(courtBlocs[i], totalPower));
            Blocs = blocs;
            HasBlocs = blocs.Count > 0;

            var worst = WarExhaustion.Worst(state, kingdom);
            WorstWarText = worst.ToString("0.0");
            WorstWarNote = worst >= DiplomacyConstants.ExhaustionCourtPressure
                ? "exhaustion - the doves have found their voice"
                : "exhaustion - doves gather at " + DiplomacyConstants.ExhaustionCourtPressure.ToString("0");

            // The court: every sworn clan but the crown's own, most influential first.
            var standing = 0;
            var members = new List<Clan>();
            foreach (var clan in Court.MembersOf(kingdom))
                if (clan != ruling) members.Add(clan);
            members.Sort((a, b) => b.Influence.CompareTo(a.Influence));

            for (var i = 0; i < members.Count; i++)
            {
                var clan = members[i];
                var bloc = BlocModel.BlocOf(state, clan);
                var pressing = SuccessionModel.HasPowerClaim(state, clan, kingdom)
                               || SuccessionModel.ClaimOf(state, clan) != null;
                if (pressing) standing++;
                clans.Add(new DiCourtClanVM(this, clan, LoyaltyModel.Explain(state, clan), bloc, pressing,
                    clan == Clan.PlayerClan));
            }
            Clans = clans;
            ClanCountText = members.Count + (members.Count == 1 ? " clan owes" : " clans owe")
                            + (playerRules ? " you fealty" : " fealty to " + ruling.Name);

            // The succession watch.
            var claims = SuccessionModel.PretendersTo(state, kingdom);
            if (claims.Count > 0)
            {
                // Worded for whoever is reading: the ruler, a vassal, or the claimant. The
                // second-person version was the only one until a live test on 2026-09-24 put the
                // player among the claimants and the footer told them they claimed their own throne.
                var throne = playerRules ? "your throne" : "the throne";
                var playerClaims = false;
                for (var i = 0; i < claims.Count; i++)
                    if (claims[i].Claimant == Hero.MainHero) playerClaims = true;
                SuccessionTitle = claims.Count == 1
                    ? (playerClaims ? "You claim the throne." : claims[0].Claimant.Name + " claims " + throne + ".")
                    : claims.Count + " houses claim " + throne + (playerClaims ? ", yours among them." : ".");
                var standingWord = playerRules ? "Your standing" : "The crown's standing";
                SuccessionDetail = LegitimacyRegistry.IsWeak(state, kingdom)
                    ? standingWord + " is low enough that a claimant's faction can gather openly."
                    : "While " + standingWord.ToLowerInvariant() + " holds above " + IntrigueConstants.LegitimacyPretenderThreshold.ToString("0")
                      + ", no faction dares rally to a claim.";
            }
            else if (standing > 0)
            {
                SuccessionTitle = "If the throne fell vacant today, " + standing
                                  + (standing == 1 ? " house would" : " houses would") + " press a claim.";
                SuccessionDetail = "Each is strong enough at court and disaffected enough to want it.";
            }
            else
            {
                SuccessionTitle = "No house would contest a succession today.";
                SuccessionDetail = "None is both strong enough at court and disaffected enough to try.";
            }

            // Keep the selection across rebuilds when that clan is still at court.
            DiCourtClanVM reselect = null;
            if (_selected != null)
                for (var i = 0; i < clans.Count; i++)
                    if (clans[i].Clan == _selected.Clan) reselect = clans[i];
            if (reselect == null && clans.Count > 0) reselect = clans[0];

            if (reselect != null) Select(reselect);
            else
            {
                Selected = null;
                Terms = new MBBindingList<DiCourtTermVM>();
                Grievances = new MBBindingList<DiCourtGrievanceVM>();
            }
        }

        /// <summary>The right-hand column: why the selected clan feels as it does.</summary>
        private void ComposeSelection()
        {
            var terms = new MBBindingList<DiCourtTermVM>();
            var grievances = new MBBindingList<DiCourtGrievanceVM>();
            var state = CoreBehavior.State;

            if (_selected != null && state != null)
            {
                var e = _selected.Explained;
                terms.Add(new DiCourtTermVM("A court starts here", e.Base, neutral: true));
                terms.Add(new DiCourtTermVM("How they feel about their ruler", e.Relation));
                terms.Add(new DiCourtTermVM("What they hold against the crown", e.Grievances));
                terms.Add(new DiCourtTermVM("Land they think they merit", e.Fiefs));
                terms.Add(new DiCourtTermVM("The war weighing on them", e.WarExhaustion));
                terms.Add(new DiCourtTermVM("The crown's standing", e.Legitimacy));

                var ruling = _selected.Clan.Kingdom?.RulingClan;
                foreach (var g in GrievanceRegistry.Of(state, _selected.Clan))
                    if (g.Target == ruling) grievances.Add(new DiCourtGrievanceVM(g));
            }

            Terms = terms;
            Grievances = grievances;
        }

        private static string LastLegitimacyReason(ModState state, Kingdom kingdom)
        {
            for (var i = 0; i < state.Legitimacy.Count; i++)
            {
                var record = state.Legitimacy[i];
                if (record.Kingdom != kingdom) continue;
                if (string.IsNullOrEmpty(record.LastReason) || record.LastReason == "founded") break;
                return "last change: " + record.LastReason;
            }
            return "last change: none yet";
        }

        // ----- shared helpers for the row VMs ------------------------------------

        internal static Color BandColor(LoyaltyBand band)
        {
            switch (band)
            {
                case LoyaltyBand.Reliable: return ReliableColor;
                case LoyaltyBand.Transactional: return TransactionalColor;
                case LoyaltyBand.Disaffected: return DisaffectedColor;
                default: return DefectionColor;
            }
        }

        /// <summary>The same three colours as a rival crown's band on its Encyclopedia page.</summary>
        internal static Color CrownColor(CrownStanding standing)
        {
            switch (standing)
            {
                case CrownStanding.Failing: return DefectionColor;
                case CrownStanding.Questioned: return DisaffectedColor;
                default: return ReliableColor;
            }
        }

        internal static string BandName(LoyaltyBand band)
        {
            switch (band)
            {
                case LoyaltyBand.Reliable: return "RELIABLE";
                case LoyaltyBand.Transactional: return "TRANSACTIONAL";
                case LoyaltyBand.Disaffected: return "DISAFFECTED";
                default: return "DEFECTION RISK";
            }
        }

        internal static string AgendaName(CourtAgenda agenda)
        {
            switch (agenda)
            {
                case CourtAgenda.Doves: return "Doves";
                case CourtAgenda.Hawks: return "Hawks";
                case CourtAgenda.Autonomists: return "Autonomists";
                case CourtAgenda.Centralists: return "Centralists";
                case CourtAgenda.Pretenders: return "Pretenders";
                default: return "-";
            }
        }

        internal static Color AgendaColor(CourtAgenda agenda)
        {
            switch (agenda)
            {
                case CourtAgenda.Doves: return Color.ConvertStringToColor("#8FB0C8FF");
                case CourtAgenda.Hawks: return Color.ConvertStringToColor("#D98C4AFF");
                case CourtAgenda.Autonomists: return Color.ConvertStringToColor("#A594D0FF");
                case CourtAgenda.Centralists: return Color.ConvertStringToColor("#D9B54AFF");
                case CourtAgenda.Pretenders: return Color.ConvertStringToColor("#D9695AFF");
                default: return MutedColor;
            }
        }
    }

    /// <summary>One faction of the court.</summary>
    internal sealed class DiCourtBlocVM : ViewModel
    {
        public DiCourtBlocVM(CourtBloc bloc, float totalPower)
        {
            Name = DiCourtVM.AgendaName(bloc.Agenda);
            AccentColor = DiCourtVM.AgendaColor(bloc.Agenda);
            var share = totalPower > 0f ? bloc.Power / totalPower : 0f;
            ShareAmount = (int)Math.Round(share * 100f);
            ShareText = ShareAmount + "%";
            LeaderText = "Speaks through " + (bloc.Leader == null ? "nobody" : bloc.Leader.Name.ToString());
            CountText = bloc.Members.Count + (bloc.Members.Count == 1 ? " clan" : " clans");
            PowerText = "influence " + bloc.Power.ToString("N0");
            // Loyalty beats agenda (design 02 §3): members at 70+ vote with the ruler anyway,
            // so a bloc's real weight can be less than its size suggests.
            LoyalNote = bloc.LoyalMembers > 0
                ? bloc.LoyalMembers + " of them will vote with the crown regardless"
                : string.Empty;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public int ShareAmount { get; }
        [DataSourceProperty] public string ShareText { get; }
        [DataSourceProperty] public string LeaderText { get; }
        [DataSourceProperty] public string CountText { get; }
        [DataSourceProperty] public string PowerText { get; }
        [DataSourceProperty] public string LoyalNote { get; }
        [DataSourceProperty] public bool HasLoyalNote => !string.IsNullOrEmpty(LoyalNote);
    }

    /// <summary>One clan row in the court list.</summary>
    internal sealed class DiCourtClanVM : ViewModel
    {
        private readonly DiCourtVM _owner;
        private bool _isSelected;

        public DiCourtClanVM(DiCourtVM owner, Clan clan, LoyaltyBreakdown explained, CourtBloc bloc,
            bool pressing, bool isPlayer)
        {
            _owner = owner;
            Clan = clan;
            Explained = explained;

            var loyalty = explained.Total;
            var band = LoyaltyModel.Band(loyalty);

            Name = clan.Name.ToString() + (isPlayer ? " (you)" : string.Empty);
            BlocText = bloc == null ? "-" : DiCourtVM.AgendaName(bloc.Agenda)
                                            + (bloc.Leader == clan ? " (leads)" : string.Empty);
            InfluenceText = clan.Influence.ToString("N0");
            LoyaltyText = loyalty.ToString("0.0");
            LoyaltyAmount = (int)Math.Round(loyalty);
            BandText = DiCourtVM.BandName(band);
            BandColor = DiCourtVM.BandColor(band);
            BandDescription = LoyaltyModel.Describe(band);
            IsPressing = pressing;
        }

        internal Clan Clan { get; }
        internal LoyaltyBreakdown Explained { get; }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string BlocText { get; }
        [DataSourceProperty] public string InfluenceText { get; }
        [DataSourceProperty] public string LoyaltyText { get; }
        [DataSourceProperty] public int LoyaltyAmount { get; }
        [DataSourceProperty] public string BandText { get; }
        [DataSourceProperty] public Color BandColor { get; }
        [DataSourceProperty] public string BandDescription { get; }
        [DataSourceProperty] public bool IsPressing { get; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value == _isSelected) return; _isSelected = value; OnPropertyChangedWithValue(value, nameof(IsSelected)); }
        }

        /// <summary>Row click. Named OnSelect to match vanilla's clan tuple.</summary>
        public void OnSelect()
        {
            try
            {
                _owner?.Select(this);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Selecting a court clan failed.", ex);
            }
        }
    }

    /// <summary>One term of the loyalty sum.</summary>
    internal sealed class DiCourtTermVM : ViewModel
    {
        public DiCourtTermVM(string label, float value, bool neutral = false)
        {
            Label = label;
            ValueText = value.ToString("+0.0;-0.0;0.0");
            ValueColor = neutral || Math.Abs(value) < 0.05f
                ? DiCourtVM.TextColor
                : (value > 0f ? DiCourtVM.ReliableColor : DiCourtVM.DefectionColor);
        }

        [DataSourceProperty] public string Label { get; }
        [DataSourceProperty] public string ValueText { get; }
        [DataSourceProperty] public Color ValueColor { get; }
    }

    /// <summary>One thing a clan has not forgotten.</summary>
    internal sealed class DiCourtGrievanceVM : ViewModel
    {
        public DiCourtGrievanceVM(Grievance g)
        {
            Title = TitleOf(g.Type);
            WeightText = g.Weight.ToString("0.0");
            var days = g.Created.ElapsedDaysUntilNow;
            AgeText = (days < 1f ? "today" : days.ToString("0") + " days ago")
                      + " - fading by " + IntrigueConstants.GrievanceDecayPerDay.ToString("0.00") + " a day";
        }

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string WeightText { get; }
        [DataSourceProperty] public string AgeText { get; }

        /// <summary>Plain words for the player, not the enum name. Design: "An unjust war", not "UnjustWar 7.8".</summary>
        private static string TitleOf(GrievanceType type)
        {
            switch (type)
            {
                case GrievanceType.FiefToRival: return "A fief given to another";
                case GrievanceType.UnjustWar: return "An unjust war";
                case GrievanceType.HumiliatingTribute: return "Tribute paid to a foreign crown";
                case GrievanceType.RelativeInCaptivity: return "Kin left in an enemy cell";
                case GrievanceType.FiefLostToEnemy: return "A fief the crown failed to defend";
                case GrievanceType.PolicyAgainstAgenda: return "A policy against their interest";
                case GrievanceType.PeaceWhileWinning: return "Peace made while they were winning";
                case GrievanceType.RequestRefused: return "A request refused";
                case GrievanceType.SuccessionPassedOver: return "Their candidate for the throne passed over";
                default: return "An old slight";
            }
        }
    }
}
