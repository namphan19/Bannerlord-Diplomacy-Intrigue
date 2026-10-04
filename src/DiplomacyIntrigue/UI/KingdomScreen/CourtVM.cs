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
        private string _amendsAllText = string.Empty;
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
                return DiText.T("DI_COURT_SELECTED_TERMS_GRIEVANCE_NAME_LOYALTYTEXT_BANDTEXT_COUNT_2",
                    "Selected {NAME}: {LOYALTYTEXT} {BANDTEXT}, {COUNT} terms, {COUNT_2} grievance(s).",
                    ("NAME", name),
                    ("LOYALTYTEXT", _clans[i].LoyaltyText),
                    ("BANDTEXT", _clans[i].BandText),
                    ("COUNT", _terms.Count),
                    ("COUNT_2", _grievances.Count));
            }
            return DiText.T("DI_COURT_NO_CLAN_NAMED_IN_THE_COURT_NAME_2",
                "No clan named \"{NAME}\" in the court list.",
                ("NAME", name));
        }

        // ----- bound surface ---------------------------------------------------

        // ----- static labels, moved out of the prefab (story 4.1 §9) ---------------------

        [DataSourceProperty] public string WorstWarTitleText => DiText.T("DI_COURT_THE_REALMS_WORST_WAR", "THE REALM'S WORST WAR");
        [DataSourceProperty] public string LegitimacyTitleText => DiText.T("DI_COURT_CROWN_LEGITIMACY", "CROWN LEGITIMACY");
        [DataSourceProperty] public string BlocsTitleText => DiText.T("DI_COURT_COURT_BLOCS", "Court blocs");
        [DataSourceProperty] public string NoBlocsText => DiText.T("DI_COURT_NO_FRACTION_HAS_FORMED_NO_CAUSE", "No faction has formed. No cause is pulling hard enough on any clan to organise it.");
        [DataSourceProperty] public string BlocExplainsText => DiText.T("DI_COURT_A_BLOC_VOTES_AS_ONE_ITS_MEMBERS", "A bloc votes as one: its members back whatever its leader wants. A clan at loyalty 70 or above ignores its bloc and votes with the crown.");
        [DataSourceProperty] public string OfficesTitleText => DiText.T("DI_COURT_OFFICES_CHOOSE_A_SEAT_THEN_A", "OFFICES - CHOOSE A SEAT, THEN A HOUSE");
        [DataSourceProperty] public string CourtTitleText => DiText.T("DI_COURT_THE_COURT", "The court");
        [DataSourceProperty] public string ClanColumnText => DiText.T("DI_COURT_CLAN_COLUMN", "CLAN");
        [DataSourceProperty] public string BlocColumnText => DiText.T("DI_COURT_BLOC_COLUMN", "BLOC");
        [DataSourceProperty] public string InfluenceColumnText => DiText.T("DI_COURT_INFL_COLUMN", "INFL.");
        [DataSourceProperty] public string LoyaltyColumnText => DiText.T("DI_COURT_LOYALTY_COLUMN", "LOYALTY");
        [DataSourceProperty] public string StandingColumnText => DiText.T("DI_COURT_STANDING_COLUMN", "STANDING");
        [DataSourceProperty] public string WhyColumnText => DiText.T("DI_COURT_WHY_COLUMN", "WHY");
        [DataSourceProperty] public string NotForgottenTitleText => DiText.T("DI_COURT_WHAT_THEY_HAVE_NOT_FORGOTTEN", "WHAT THEY HAVE NOT FORGOTTEN");
        [DataSourceProperty] public string NoGrievanceText => DiText.T("DI_COURT_NOTHING_IS_HELD_AGAINST_THE_CROWN", "Nothing is held against the crown by this clan.");

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

        [DataSourceProperty] public string TabText => DiText.T("DI_COURT_COURT", "Court");

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
        public string AmendsAllText
        {
            get => _amendsAllText;
            set
            {
                if (value == _amendsAllText) return;
                _amendsAllText = value;
                OnPropertyChangedWithValue(value, nameof(AmendsAllText));
                OnPropertyChangedWithValue(!string.IsNullOrEmpty(value), nameof(HasAmendsAll));
            }
        }

        [DataSourceProperty] public bool HasAmendsAll => !string.IsNullOrEmpty(_amendsAllText);

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

        // ----- The offices (design 09 C2, the court canvas's "The offices" board) -------------
        //
        // Column 1 lists the five seats; selecting one turns the lower half of column 3 from the
        // selected house's grievances into giving that seat to the selected house. The approved
        // mockup had a separate chooser; column 3 is 310 wide and already full, so the house is
        // chosen from the roster instead, the way every other act on a house is.

        private MBBindingList<DiCourtSeatVM> _seats = new MBBindingList<DiCourtSeatVM>();
        private Portfolio? _selectedSeat;
        private bool _mayAct;
        private bool _dismissArmed;
        private bool _appointArmed;
        private Offices.AppointQuote _appointQuote;
        private bool _canDismiss;
        private string _dismissText = string.Empty;
        private string _dismissNote = string.Empty;
        private string _seatHeader = string.Empty;
        private bool _hasAppoint;
        private bool _appointEnabled;
        private string _appointOutcome = string.Empty;
        private string _appointText = string.Empty;
        private string _appointNote = string.Empty;
        private MBBindingList<DiCourtTermVM> _appointLines = new MBBindingList<DiCourtTermVM>();

        private void Set<T>(ref T field, T value, string name) where T : class
        {
            if (Equals(field, value)) return;
            field = value;
            OnPropertyChangedWithValue(value, name);
        }

        private void Set(ref bool field, bool value, string name)
        {
            if (field == value) return;
            field = value;
            OnPropertyChangedWithValue(value, name);
        }

        [DataSourceProperty] public MBBindingList<DiCourtSeatVM> Seats { get => _seats; set => Set(ref _seats, value, nameof(Seats)); }
        [DataSourceProperty] public bool SeatMode => _selectedSeat.HasValue;
        [DataSourceProperty] public bool GrievanceMode => !_selectedSeat.HasValue;
        [DataSourceProperty] public bool CanDismiss { get => _canDismiss; set => Set(ref _canDismiss, value, nameof(CanDismiss)); }
        [DataSourceProperty] public string DismissText { get => _dismissText; set => Set(ref _dismissText, value, nameof(DismissText)); }
        [DataSourceProperty] public string DismissNote { get => _dismissNote; set => Set(ref _dismissNote, value, nameof(DismissNote)); }
        [DataSourceProperty] public string SeatHeader { get => _seatHeader; set => Set(ref _seatHeader, value, nameof(SeatHeader)); }
        [DataSourceProperty] public bool HasAppoint { get => _hasAppoint; set => Set(ref _hasAppoint, value, nameof(HasAppoint)); }
        [DataSourceProperty] public bool AppointEnabled { get => _appointEnabled; set => Set(ref _appointEnabled, value, nameof(AppointEnabled)); }
        [DataSourceProperty] public string AppointOutcome { get => _appointOutcome; set => Set(ref _appointOutcome, value, nameof(AppointOutcome)); }
        [DataSourceProperty] public string AppointText { get => _appointText; set => Set(ref _appointText, value, nameof(AppointText)); }
        [DataSourceProperty] public string AppointNote { get => _appointNote; set => Set(ref _appointNote, value, nameof(AppointNote)); }
        [DataSourceProperty] public MBBindingList<DiCourtTermVM> AppointLines { get => _appointLines; set => Set(ref _appointLines, value, nameof(AppointLines)); }

        /// <summary>A seat row was clicked: select it, or clicked again, go back to the grievances.</summary>
        internal void SelectSeat(DiCourtSeatVM row)
        {
            try
            {
                _selectedSeat = _selectedSeat == row.Seat ? (Portfolio?)null : row.Seat;
                _dismissArmed = false;
                _appointArmed = false;
                for (var i = 0; i < _seats.Count; i++) _seats[i].IsSelected = _seats[i].Seat == _selectedSeat;
                OnPropertyChangedWithValue(SeatMode, nameof(SeatMode));
                OnPropertyChangedWithValue(GrievanceMode, nameof(GrievanceMode));
                ComposeDismiss();
                ComposeAppoint();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Selecting a court seat failed.", ex);
            }
        }

        /// <summary>Selects a seat by name, as a row click would. Test hook for <c>diplomacy.test_court_seat</c>.</summary>
        internal string SelectSeatByName(string name)
        {
            for (var i = 0; i < _seats.Count; i++)
            {
                if (!string.Equals(_seats[i].Seat.ToString(), name, StringComparison.OrdinalIgnoreCase)) continue;
                SelectSeat(_seats[i]);
                return SeatMode ? "Selected the " + _seats[i].Seat + "'s seat. " + (HasAppoint ? AppointText : AppointNote)
                                : "Back to the grievances.";
            }
            return DiText.T("DI_COURT_NO_SEAT_NAMED_NAME_2", "No seat named \"{NAME}\".", ("NAME", name));
        }

        /// <summary>Two clicks: the first names what taking the seat back costs, the second does it.</summary>
        public void ExecuteDismiss()
        {
            try
            {
                if (!_canDismiss || !_selectedSeat.HasValue) return;
                if (!_dismissArmed)
                {
                    _dismissArmed = true;
                    ComposeDismiss();
                    return;
                }
                var kingdom = Clan.PlayerClan?.Kingdom;
                if (!Offices.Dismiss(CoreBehavior.State, kingdom, _selectedSeat.Value, out var failed))
                    Log.Notify("The seat could not be taken back: " + failed, Colors.Red);
                _dismissArmed = false;
                Rebuild();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Taking a court seat back failed.", ex);
            }
        }

        /// <summary>Two clicks, as amends: the first names the price, the second pays it.</summary>
        public void ExecuteAppoint()
        {
            try
            {
                var q = _appointQuote;
                if (q == null || !q.Eligible || !q.Affordable) return;
                if (!_appointArmed)
                {
                    _appointArmed = true;
                    ComposeAppoint();
                    return;
                }
                if (!Offices.Appoint(CoreBehavior.State, q.Kingdom, q.Seat, q.Candidate, out var failed))
                    Log.Notify("The seat could not be given: " + failed, Colors.Red);
                _appointArmed = false;
                Rebuild();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Giving a court seat failed.", ex);
            }
        }

        /// <summary>The five seats, with who holds each and who speaks for it.</summary>
        private void ComposeSeats(ModState state, Kingdom kingdom)
        {
            var seats = new MBBindingList<DiCourtSeatVM>();
            foreach (var seat in Offices.Seats)
            {
                var record = Offices.RecordOf(state, kingdom, seat);
                var speaker = Statecraft.StatecraftModel.Actor(kingdom, seat);
                var skill = Statecraft.StatecraftModel.SkillOf(seat);
                var holder = record != null && Offices.Stands(record)
                    ? Statecraft.StatecraftModel.NameOf(record.Holder) + (record.Holder.Clan == kingdom.RulingClan ? "" : ", " + record.Holder.Clan?.Name)
                      + (record.Holder == speaker ? "" : " (cannot act)")
                    : "Empty - " + Statecraft.StatecraftModel.NameOf(speaker);
                var row = new DiCourtSeatVM(this, seat, Statecraft.StatecraftModel.TitleOf(seat), holder,
                    Statecraft.StatecraftModel.SkillName(skill) + " " + (speaker == null ? 0 : speaker.GetSkillValue(skill)));
                row.IsSelected = seat == _selectedSeat;
                seats.Add(row);
            }
            Seats = seats;
            ComposeDismiss();
        }

        private void ComposeDismiss()
        {
            var state = CoreBehavior.State;
            var kingdom = Clan.PlayerClan?.Kingdom;
            var record = _selectedSeat.HasValue ? Offices.RecordOf(state, kingdom, _selectedSeat.Value) : null;
            if (!_mayAct || record == null)
            {
                CanDismiss = false;
                DismissText = string.Empty;
                DismissNote = string.Empty;
                return;
            }
            var name = Statecraft.StatecraftModel.NameOf(record.Holder);
            var title = Statecraft.StatecraftModel.TitleOf(record.Seat);
            CanDismiss = true;
            DismissText = _dismissArmed ? DiText.T("DI_COURT_CONFIRM_TAKE_THE_SEAT_FROM_NAME_2",
                "Confirm: take the seat from {NAME}",
                ("NAME", name)) : DiText.T("DI_COURT_TAKE_THE_SEAT_BACK_TITLE_2",
                "Take the {TITLE}'s seat back",
                ("TITLE", title));
            var house = record.Holder?.Clan;
            DismissNote = house != null && house != kingdom.RulingClan ? DiText.T("DI_COURT_FREE_BUT_TAKES_GRIEVANCE_LOYALTY_AND_NAME_2",
                "Free, but {NAME} takes a grievance ({GRIEVANCEDISMISSEDFROMOFFICE}): loyalty -{LOYALTYGRIEVANCEFACTOR} and the favour gone.",
                ("NAME", house.Name),
                ("GRIEVANCEDISMISSEDFROMOFFICE", IntrigueConstants.GrievanceDismissedFromOffice.ToString("0")),
                ("LOYALTYGRIEVANCEFACTOR", (IntrigueConstants.GrievanceDismissedFromOffice * IntrigueConstants.LoyaltyGrievanceFactor).ToString("0.0"))) : DiText.T("DI_COURT_FREE_YOUR_OWN_HOUSE_TAKES_NO_2",
                "Free; your own house takes no grievance.");
        }

        /// <summary>Giving the selected seat to the selected house: the price, term by term, and the button.</summary>
        private void ComposeAppoint()
        {
            var state = CoreBehavior.State;
            var house = _selected?.Clan;
            var kingdom = Clan.PlayerClan?.Kingdom;
            _appointQuote = null;
            var lines = new MBBindingList<DiCourtTermVM>();
            HasAppoint = false;
            AppointEnabled = false;
            AppointOutcome = string.Empty;
            AppointText = string.Empty;

            if (!_selectedSeat.HasValue || house == null || kingdom == null)
            {
                SeatHeader = string.Empty;
                AppointNote = string.Empty;
                AppointLines = lines;
                return;
            }

            var seat = _selectedSeat.Value;
            var skill = Statecraft.StatecraftModel.SkillOf(seat);
            SeatHeader = "THE " + Statecraft.StatecraftModel.TitleOf(seat).ToUpperInvariant() + "'S SEAT ("
                         + Statecraft.StatecraftModel.SkillName(skill).ToUpperInvariant() + ") FOR THIS HOUSE";

            if (!_mayAct)
            {
                AppointNote = DiText.T("DI_COURT_ONLY_THE_RULER_GIVES_SEATS_CLICK_2",
                    "Only the ruler gives seats. Click the seat again to see the grievances.");
                AppointLines = lines;
                return;
            }

            var candidate = Offices.CandidateFrom(state, house, seat);
            if (candidate == null)
            {
                AppointNote = DiText.T("DI_COURT_NOBODY_OF_THIS_HOUSE_CAN_TAKE_2",
                    "Nobody of this house can take the seat today. Click the seat again to see the grievances.");
                AppointLines = lines;
                return;
            }

            var q = Offices.QuoteAppointment(state, kingdom, seat, candidate);
            if (!q.Eligible)
            {
                AppointNote = DiText.T("DI_COURT_CANNOT_BE_GIVEN_REASON_2",
                    "Cannot be given: {REASON}.",
                    ("REASON", q.Reason));
                AppointLines = lines;
                return;
            }

            _appointQuote = q;
            foreach (var term in Offices.PriceTerms(q)) lines.Add(new DiCourtTermVM(term.Key, term.Value));
            AppointLines = lines;
            HasAppoint = true;
            AppointEnabled = q.Affordable;
            AppointOutcome = q.FavouredHouse == null ? DiText.T("DI_COURT_YOUR_OWN_HOUSE_SKILLED_VOICE_NO_2",
                "Your own house: a skilled voice, no favour to give") : DiText.T("DI_COURT_IN_YOUR_FAVOUR_LOYALTY_LOYALTYBEFORE_LOYALTYAFTER_BAND_2",
                "In your favour: loyalty {LOYALTYBEFORE} -> {LOYALTYAFTER}, {BAND}",
                ("LOYALTYBEFORE", q.LoyaltyBefore.ToString("0.0")),
                ("LOYALTYAFTER", q.LoyaltyAfter.ToString("0.0")),
                ("BAND", LoyaltyModel.Band(q.LoyaltyAfter)));
            var price = DiText.T("DI_COURT_INFLUENCE_DENARS_INFLUENCE_GOLD_2",
                "{INFLUENCE} influence, {GOLD} denars",
                ("INFLUENCE", q.Influence.ToString("N0")),
                ("GOLD", q.Gold.ToString("N0")));
            var who = Statecraft.StatecraftModel.NameOf(candidate);
            AppointText = _appointArmed ? "Confirm: pay " + price : "Appoint " + who + " - " + price;
            AppointNote = !q.Affordable ? "Cannot pay: " + q.Short + "."
                : _appointArmed ? "Click again to pay. Anything else leaves it unpaid."
                : "The denars are " + who + "'s stipend; the influence is spent.";
        }

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
                _appointArmed = false;
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
                RealmName = DiText.T("DI_COURT_NO_REALM_2", "No realm");
                CourtLine = DiText.T("DI_COURT_YOU_HOLD_NO_FEALTY_AND_KEEP_2",
                    "You hold no fealty and keep no court.");
                LegitimacyText = string.Empty;
                LegitimacyNote = string.Empty;
                LegitimacyAmount = 0;
                ClanCountText = string.Empty;
                AmendsAllText = string.Empty;
                _mayAct = false;
                _selectedSeat = null;
                Seats = new MBBindingList<DiCourtSeatVM>();
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
            // A house with the rising does not serve the ruler it is fighting: the claimant read
            // "you serve Aradwyr" over a war against Aradwyr (live 2026-09-25).
            var rulerName = kingdom.Leader == null
                ? DiText.T("DI_COURT_ITS_RULER", "its ruler")
                : kingdom.Leader.Name.ToString();
            var playerRebel = war != null && war.IsRebel(Clan.PlayerClan);
            CourtLine = playerRules
                ? DiText.T("DI_COURT_YOUR_OWN_COURT", "The court of Clan {CLAN} - your own clan", ("CLAN", ruling.Name))
                : playerRebel
                    // Vanilla's own tabs read the player's map faction, which is the rising, and
                    // that is kept on purpose (STATUS 2026-09-26): its clans, fiefs and armies are
                    // the host a rebel actually commands. Said here so the switch is not a surprise.
                    ? DiText.T("DI_COURT_IN_ARMS_AGAINST",
                        "The court of Clan {CLAN} - you are in arms against {RULER} (the vanilla tabs show {HOST}, your host)",
                        ("CLAN", ruling.Name), ("RULER", rulerName),
                        ("HOST", war.Faction == null ? DiText.T("DI_COURT_THE_RISING", "the rising") : war.Faction.Name.ToString()))
                    : DiText.T("DI_COURT_YOU_SERVE", "The court of Clan {CLAN} - you serve {RULER}",
                        ("CLAN", ruling.Name), ("RULER", rulerName));

            // Crown legitimacy: the pool, the bar, and what last moved it.
            var legitimacy = LegitimacyRegistry.Of(state, kingdom);
            LegitimacyText = legitimacy.ToString("0.0");
            LegitimacyAmount = (int)Math.Round(legitimacy);
            LegitimacyColor = CrownColor(CourtBands.CrownOf(legitimacy));
            LegitimacyNote = LastLegitimacyReason(state, kingdom);
            if (Statecraft.StatecraftModel.Enabled)
            {
                // Design 08 S-6: what the steward makes of a year of peace.
                var steward = Statecraft.StatecraftModel.Actor(kingdom, Portfolio.Steward);
                var dividend = DiText.T("DI_COURT_YEAR_OF_PEACE_RESTORES_PEACEDIVIDENDOF_STEWARD_2",
                    "A year of peace restores {PEACEDIVIDENDOF}{STEWARD}",
                    ("PEACEDIVIDENDOF", LegitimacyRegistry.PeaceDividendOf(kingdom).ToString("0.0")),
                    ("STEWARD", steward == null ? "." : ", at the pace of " + Statecraft.StatecraftModel.Who(steward, TaleWorlds.Core.DefaultSkills.Steward) + "."));
                LegitimacyNote = string.IsNullOrEmpty(LegitimacyNote) ? dividend : LegitimacyNote + " " + dividend;
            }

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
            WorstWarNote = worst >= DiplomacyConstants.ExhaustionCourtPressure ? DiText.T("DI_COURT_EXHAUSTION_THE_DOVES_HAVE_FOUND_THEIR_2",
                "exhaustion - the doves have found their voice") : DiText.T("DI_COURT_EXHAUSTION_DOVES_GATHER_AT_EXHAUSTIONCOURTPRESSURE_2",
                "exhaustion - doves gather at {EXHAUSTIONCOURTPRESSURE}",
                ("EXHAUSTIONCOURTPRESSURE", DiplomacyConstants.ExhaustionCourtPressure.ToString("0")));

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

            // Design 09 C1: what answering the whole court would cost - the line that says the
            // verbs are for choosing, not for keeping everybody content. The ruler's view only.
            AmendsAllText = playerRules && war == null ? (Amends.AllOpenCost(state, kingdom) ?? string.Empty) : string.Empty;

            // Design 09 C2: the seats. Only the ruler's view acts on them, and not in a civil war.
            _mayAct = playerRules && war == null;
            ComposeSeats(state, kingdom);

            // The succession watch.
            var claims = SuccessionModel.PretendersTo(state, kingdom);
            if (claims.Count > 0)
            {
                // Worded for whoever is reading: the ruler, a vassal, or the claimant. The
                // second-person version was the only one until a live test on 2026-09-24 put the
                // player among the claimants and the footer told them they claimed their own throne.
                var throne = playerRules ? DiText.T("DI_COURT_YOUR_THRONE_2", "your throne") : DiText.T("DI_COURT_THE_THRONE_2", "the throne");
                var playerClaims = false;
                for (var i = 0; i < claims.Count; i++)
                    if (claims[i].Claimant == Hero.MainHero) playerClaims = true;
                SuccessionTitle = claims.Count == 1
                    ? (playerClaims ? "You claim the throne." : claims[0].Claimant.Name + " claims " + throne + ".")
                    : claims.Count + " houses claim " + throne + (playerClaims ? ", yours among them." : ".");
                var standingWord = playerRules ? DiText.T("DI_COURT_YOUR_STANDING_2", "Your standing") : DiText.T("DI_COURT_THE_CROWN_STANDING_2", "The crown's standing");
                SuccessionDetail = LegitimacyRegistry.IsWeak(state, kingdom) ? DiText.T("DI_COURT_IS_LOW_ENOUGH_THAT_CLAIMANT_FACTION_STANDINGWORD_2",
                    "{STANDINGWORD} is low enough that a claimant's faction can gather openly.",
                    ("STANDINGWORD", standingWord)) : DiText.T("DI_COURT_WHILE_HOLDS_ABOVE_NO_FACTION_DARES_TOLOWERINVARIANT_2",
                    "While {STANDINGWORD} holds above {LEGITIMACYPRETENDERTHRESHOLD}, no faction dares rally to a claim.",
                    ("STANDINGWORD", standingWord.ToLowerInvariant()),
                    ("LEGITIMACYPRETENDERTHRESHOLD", IntrigueConstants.LegitimacyPretenderThreshold.ToString("0")));

                // Design 08 S-8: a claimant's own Charm, in every house's choice at a succession.
                if (Statecraft.StatecraftModel.Enabled)
                {
                    var charm = new List<string>();
                    for (var i = 0; i < claims.Count; i++)
                    {
                        var claimant = claims[i].Claimant;
                        if (claimant == null) continue;
                        charm.Add(Statecraft.StatecraftModel.NameOf(claimant) + " (Charm "
                                  + claimant.GetSkillValue(TaleWorlds.Core.DefaultSkills.Charm) + ") "
                                  + Statecraft.StatecraftModel.Signed(Statecraft.StatecraftTerms.Backing(claimant)));
                    }
                    if (charm.Count > 0)
                        SuccessionDetail += DiText.T("DI_COURT_CHARM_AT_COURT_IN_EVERY_HOUSE_JOIN_2",
                            " Charm at court, in every house's choice: {NAMES}.",
                            ("NAMES", string.Join(", ", charm)));
                }
            }
            else if (standing > 0)
            {
                SuccessionTitle = standing == 1 ? DiText.T("DI_COURT_IF_THE_THRONE_FELL_VACANT_TODAY_STANDING_2",
                    "If the throne fell vacant today, {STANDING} house would press a claim.",
                    ("STANDING", standing)) : DiText.T("DI_COURT_IF_THE_THRONE_FELL_VACANT_TODAY_STANDING_3",
                    "If the throne fell vacant today, {STANDING} houses would press a claim.",
                    ("STANDING", standing));
                SuccessionDetail = DiText.T("DI_COURT_EACH_IS_STRONG_ENOUGH_AT_COURT_2",
                    "Each is strong enough at court and disaffected enough to want it.");
            }
            else
            {
                SuccessionTitle = DiText.T("DI_COURT_NO_HOUSE_WOULD_CONTEST_SUCCESSION_TODAY_2",
                    "No house would contest a succession today.");
                SuccessionDetail = DiText.T("DI_COURT_NONE_IS_BOTH_STRONG_ENOUGH_AT_2",
                    "None is both strong enough at court and disaffected enough to try.");
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
                terms.Add(new DiCourtTermVM(DiText.T("DI_COURT_COURT_STARTS_HERE_2", "A court starts here"), e.Base, neutral: true));
                terms.Add(new DiCourtTermVM(DiText.T("DI_COURT_HOW_THEY_FEEL_ABOUT_THEIR_RULER_2",
                    "How they feel about their ruler"), e.Relation));
                terms.Add(new DiCourtTermVM(DiText.T("DI_COURT_WHAT_THEY_HOLD_AGAINST_THE_CROWN_2",
                    "What they hold against the crown"), e.Grievances));
                terms.Add(new DiCourtTermVM(DiText.T("DI_COURT_LAND_THEY_THINK_THEY_MERIT_2", "Land they think they merit"), e.Fiefs));
                terms.Add(new DiCourtTermVM(DiText.T("DI_COURT_THE_WAR_WEIGHING_ON_THEM_2", "The war weighing on them"), e.WarExhaustion));
                terms.Add(new DiCourtTermVM(DiText.T("DI_COURT_THE_CROWN_STANDING", "The crown's standing"), e.Legitimacy));
                if (Statecraft.StatecraftModel.Enabled)
                {
                    var ruler = _selected.Clan.Kingdom?.Leader;
                    terms.Add(new DiCourtTermVM(ruler == null ? DiText.T("DI_COURT_THE_CROWN_PRESENCE_2", "The crown's presence") : DiText.T("DI_COURT_THE_CROWN_PRESENCE_LEADERSHIP_NAME_GETSKILLVALUE_2",
                        "The crown's presence ({NAME}, Leadership {GETSKILLVALUE})",
                        ("NAME", ruler.Name),
                        ("GETSKILLVALUE", ruler.GetSkillValue(TaleWorlds.Core.DefaultSkills.Leadership))),
                        e.Presence));
                }
                if (e.ForeignGold != 0f)
                    terms.Add(new DiCourtTermVM(DiText.T("DI_COURT_FOREIGN_GOLD_NOBODY_KNOWS_WHOSE_2",
                        "Foreign gold - nobody knows whose"), e.ForeignGold));
                if (e.Office != 0f)
                    terms.Add(new DiCourtTermVM(DiText.T("DI_COURT_IN_THE_CROWN_FAVOUR_SEAT_AT_2",
                        "In the crown's favour: a seat at court"), e.Office));

                // Design 09 C1: only the ruler makes amends, so only the ruler's view carries the
                // price and the button. A vassal reads the same ledger, and no civil war is running.
                var kingdom = _selected.Clan.Kingdom;
                var ruling = kingdom?.RulingClan;
                var mayAmend = ruling != null && ruling == Clan.PlayerClan && !_isAtWar;
                foreach (var g in GrievanceRegistry.Of(state, _selected.Clan))
                    if (g.Target == ruling) grievances.Add(new DiCourtGrievanceVM(state, g, mayAmend, Rebuild));
                foreach (var g in GrievanceRegistry.AnsweredOf(state, _selected.Clan, ruling))
                    grievances.Add(DiCourtGrievanceVM.Answered(g));
            }

            Terms = terms;
            Grievances = grievances;
            ComposeAppoint();
        }

        private static string LastLegitimacyReason(ModState state, Kingdom kingdom)
        {
            for (var i = 0; i < state.Legitimacy.Count; i++)
            {
                var record = state.Legitimacy[i];
                if (record.Kingdom != kingdom) continue;
                if (string.IsNullOrEmpty(record.LastReason) || record.LastReason == "founded") break;
                return DiText.T("DI_COURT_LAST_CHANGE_LASTREASON_2",
                    "last change: {LASTREASON}",
                    ("LASTREASON", record.LastReason));
            }
            return DiText.T("DI_COURT_LAST_CHANGE_NONE_YET_2", "last change: none yet");
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
                case LoyaltyBand.Reliable: return DiText.T("DI_COURT_RELIABLE_2", "RELIABLE");
                case LoyaltyBand.Transactional: return DiText.T("DI_COURT_TRANSACTIONAL_2", "TRANSACTIONAL");
                case LoyaltyBand.Disaffected: return DiText.T("DI_COURT_DISAFFECTED_2", "DISAFFECTED");
                default: return DiText.T("DI_COURT_DEFECTION_RISK_2", "DEFECTION RISK");
            }
        }

        internal static string AgendaName(CourtAgenda agenda)
        {
            switch (agenda)
            {
                case CourtAgenda.Doves: return DiText.T("DI_COURT_DOVES_2", "Doves");
                case CourtAgenda.Hawks: return DiText.T("DI_COURT_HAWKS_2", "Hawks");
                case CourtAgenda.Autonomists: return DiText.T("DI_COURT_AUTONOMISTS_2", "Autonomists");
                case CourtAgenda.Centralists: return DiText.T("DI_COURT_CENTRALISTS_2", "Centralists");
                case CourtAgenda.Pretenders: return DiText.T("DI_COURT_PRETENDERS_2", "Pretenders");
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
            CountText = bloc.Members.Count == 1 ? DiText.T("DI_COURT_CLAN_COUNT_2", "{COUNT} clan", ("COUNT", bloc.Members.Count)) : DiText.T("DI_COURT_CLANS_COUNT_2", "{COUNT} clans", ("COUNT", bloc.Members.Count));
            PowerText = DiText.T("DI_COURT_INFLUENCE_POWER_2",
                "influence {POWER}",
                ("POWER", bloc.Power.ToString("N0")));
            // Loyalty beats agenda (design 02 §3): members at 70+ vote with the ruler anyway,
            // so a bloc's real weight can be less than its size suggests.
            LoyalNote = bloc.LoyalMembers > 0 ? DiText.T("DI_COURT_OF_THEM_WILL_VOTE_WITH_THE_LOYALMEMBERS_2",
                "{LOYALMEMBERS} of them will vote with the crown regardless",
                ("LOYALMEMBERS", bloc.LoyalMembers)) : "";
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
        /// <summary>The row's own label for a clan that is the rising's claimant (story 4.1 §9).</summary>
        [DataSourceProperty] public string ClaimantLabelText => DiText.T("DI_COURT_CLAIMANT", "claimant");
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

            Name = isPlayer ? DiText.T("DI_COURT_YOU_NAME_2", "{NAME} (you)", ("NAME", clan.Name.ToString())) : clan.Name.ToString();
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

    /// <summary>One seat at court in column 1 (design 09 C2). A button, as the clan rows are.</summary>
    internal sealed class DiCourtSeatVM : ViewModel
    {
        private readonly DiCourtVM _owner;
        private bool _isSelected;

        public DiCourtSeatVM(DiCourtVM owner, Portfolio seat, string title, string holderText, string skillText)
        {
            _owner = owner;
            Seat = seat;
            Title = title;
            HolderText = holderText;
            SkillText = skillText;
        }

        internal Portfolio Seat { get; }

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string HolderText { get; }
        [DataSourceProperty] public string SkillText { get; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value == _isSelected) return; _isSelected = value; OnPropertyChangedWithValue(value, nameof(IsSelected)); }
        }

        public void OnSelect()
        {
            try
            {
                _owner?.SelectSeat(this);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Selecting a court seat failed.", ex);
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

        /// <summary>A line whose value is already words - a term of an amends price (design 09 C1).</summary>
        public DiCourtTermVM(string label, string valueText)
        {
            Label = label;
            ValueText = valueText;
            ValueColor = DiCourtVM.TextColor;
        }

        [DataSourceProperty] public string Label { get; }
        [DataSourceProperty] public string ValueText { get; }
        [DataSourceProperty] public Color ValueColor { get; }
    }

    /// <summary>
    /// One thing a clan has not forgotten - and, for the ruler, what answering it would cost
    /// (design 09 C1). The price is <see cref="Amends.QuoteFor"/>, the number the AI weighs; the
    /// button re-prices through <see cref="Amends.Execute"/>, so it cannot buy at a stale price.
    /// </summary>
    internal sealed class DiCourtGrievanceVM : ViewModel
    {
        private readonly ModState _state;
        private readonly Grievance _grievance;
        private readonly Action _onChanged;
        private readonly Amends.Quote _quote;
        private bool _armed;
        private string _amendText = string.Empty;
        private string _amendNote = string.Empty;

        public DiCourtGrievanceVM(ModState state, Grievance g, bool mayAmend, Action onChanged)
        {
            _state = state;
            _grievance = g;
            _onChanged = onChanged;

            Title = TitleOf(g.Type, g.Holder);
            WeightText = g.Weight.ToString("0.0");
            var days = g.Created.ElapsedDaysUntilNow;
            AgeText = days < 1f ? DiText.T("DI_COURT_TODAY_FADING_BY_DAY_FADEPERDAY_2",
                "today - fading by {FADEPERDAY} a day",
                ("FADEPERDAY", GrievanceRegistry.FadePerDay(g.Target).ToString("0.000"))) : DiText.T("DI_COURT_DAYS_AGO_FADING_BY_DAY_DAYS_FADEPERDAY_2",
                "{DAYS} days ago - fading by {FADEPERDAY} a day",
                ("DAYS", days.ToString("0")),
                ("FADEPERDAY", GrievanceRegistry.FadePerDay(g.Target).ToString("0.000")));
            AccentColor = DiCourtVM.DisaffectedColor;
            PriceLines = new MBBindingList<DiCourtTermVM>();

            if (!mayAmend) return;
            _quote = Amends.QuoteFor(state, g);
            if (!_quote.Eligible)
            {
                // Shown rather than hidden: a ruler who cannot answer a grievance should see why.
                HasAmendNote = true;
                _amendNote = DiText.T("DI_COURT_AMENDS_CANNOT_BE_MADE_REASON_2",
                    "Amends cannot be made: {REASON}.",
                    ("REASON", _quote.Reason));
                return;
            }

            HasAmends = true;
            HasAmendNote = true;
            foreach (var term in Amends.PriceTerms(_quote))
                PriceLines.Add(new DiCourtTermVM(term.Key, term.Value));
            OutcomeText = DiText.T("DI_COURT_LOYALTY_LOYALTYBEFORE_LOYALTYAFTER_BAND_2",
                "Loyalty {LOYALTYBEFORE} -> {LOYALTYAFTER}, {BAND}",
                ("LOYALTYBEFORE", _quote.LoyaltyBefore.ToString("0.0")),
                ("LOYALTYAFTER", _quote.LoyaltyAfter.ToString("0.0")),
                ("BAND", LoyaltyModel.Band(_quote.LoyaltyAfter)));
            AmendEnabled = _quote.Affordable;
            ComposeButton();
        }

        private DiCourtGrievanceVM(Grievance g)
        {
            Title = DiText.T("DI_COURT_ANSWERED_TITLEOF_2",
                "{TITLEOF} - answered",
                ("TITLEOF", TitleOf(g.Type, g.Holder)));
            WeightText = "0.0";
            AgeText = DiText.T("DI_COURT_ANSWERED_DAYS_AGO_REMEMBERED_FOR_YEARS_ELAPSEDDAYSUNTILNOW_2",
                "Answered {ELAPSEDDAYSUNTILNOW} days ago. Remembered for {AMENDSMEMORYYEARS} years: the same wrong again weighs x{AMENDSREPEATWRONGFACTOR}, and amends to this house cost x{AMENDSREPEATPRICEFACTOR}.",
                ("ELAPSEDDAYSUNTILNOW", g.AnsweredOn.ElapsedDaysUntilNow.ToString("0")),
                ("AMENDSMEMORYYEARS", IntrigueConstants.AmendsMemoryYears.ToString("0")),
                ("AMENDSREPEATWRONGFACTOR", IntrigueConstants.AmendsRepeatWrongFactor.ToString("0.#")),
                ("AMENDSREPEATPRICEFACTOR", IntrigueConstants.AmendsRepeatPriceFactor.ToString("0.#")));
            AccentColor = DiCourtVM.ReliableColor;
            PriceLines = new MBBindingList<DiCourtTermVM>();
        }

        /// <summary>A wrong the crown answered, still remembered: shown under the live ones.</summary>
        internal static DiCourtGrievanceVM Answered(Grievance g) => new DiCourtGrievanceVM(g);

        private void ComposeButton()
        {
            if (_quote == null) return;
            var price = DiText.T("DI_COURT_INFLUENCE_DENARS_INFLUENCE_GOLD",
                "{INFLUENCE} influence, {GOLD} denars",
                ("INFLUENCE", _quote.Influence.ToString("N0")),
                ("GOLD", _quote.Gold.ToString("N0")));
            AmendText = _armed ? DiText.T("DI_COURT_CONFIRM_PAY_PRICE_2", "Confirm: pay {PRICE}", ("PRICE", price)) : DiText.T("DI_COURT_MAKE_AMENDS_PRICE_2", "Make amends - {PRICE}", ("PRICE", price));
            AmendNote = !_quote.Affordable ? "Cannot pay: " + _quote.Short + "."
                : _armed ? "Click again to pay. Anything else leaves it unpaid."
                : "Paid to the house's head; the influence is spent.";
        }

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string WeightText { get; }
        [DataSourceProperty] public string AgeText { get; }
        [DataSourceProperty] public Color AccentColor { get; }

        [DataSourceProperty] public bool HasAmends { get; }
        [DataSourceProperty] public bool HasAmendNote { get; }
        [DataSourceProperty] public MBBindingList<DiCourtTermVM> PriceLines { get; }
        [DataSourceProperty] public string OutcomeText { get; } = string.Empty;
        [DataSourceProperty] public bool AmendEnabled { get; }

        [DataSourceProperty]
        public string AmendText
        {
            get => _amendText;
            set { if (value == _amendText) return; _amendText = value; OnPropertyChangedWithValue(value, nameof(AmendText)); }
        }

        [DataSourceProperty]
        public string AmendNote
        {
            get => _amendNote;
            set { if (value == _amendNote) return; _amendNote = value; OnPropertyChangedWithValue(value, nameof(AmendNote)); }
        }

        /// <summary>
        /// Two clicks: the first arms the button and names the price, the second pays. The same
        /// shape as conceding a civil war, for the same two reasons: the act is dear and cannot be
        /// undone, and the test bridge can click a panel button but not inside an inquiry.
        /// </summary>
        public void ExecuteAmend()
        {
            try
            {
                if (_quote == null || !_quote.Eligible || !_quote.Affordable) return;
                if (!_armed)
                {
                    _armed = true;
                    ComposeButton();
                    return;
                }

                if (!Amends.Execute(_state, _grievance, out var failed))
                    Log.Notify("Amends could not be made: " + failed, Colors.Red);
                _onChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Making amends from the Court tab failed.", ex);
            }
        }

        /// <summary>The slight's name, from <see cref="GrievanceRegistry.TitleOf"/>, its one home.</summary>
        internal static string TitleOf(GrievanceType type, Clan holder) => GrievanceRegistry.TitleOf(type, holder);
    }
}
