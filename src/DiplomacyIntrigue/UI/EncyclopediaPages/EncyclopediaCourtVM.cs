using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using DiplomacyIntrigue.UI.KingdomScreen;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.EncyclopediaPages
{
    /// <summary>
    /// The court section of a kingdom's Encyclopedia page. Phase 2.7, the rival half of the
    /// design the lead approved on 2026-09-23 (artifact "Court Intrigue Screen", board
    /// "A rival court"), placed where the lead chose: the kingdom's own Encyclopedia page.
    ///
    /// **Bands, never figures** (design 02 §9.1). Every word here comes from
    /// <see cref="CourtBands"/>, whose edges are the thresholds the AI acts on, so the page
    /// says which side of a line a court sits on and never how far. The exact numbers are
    /// what Phase 3's ReadCourt mission will sell; nothing on this page may leak one, which
    /// is why this VM exposes only text and colours - there is no number property to bind.
    ///
    /// Your own kingdom gets a pointer to the Court tab instead: the full ledger is one
    /// screen away, and an envoy's report of your own court would be a strange thing to read.
    /// </summary>
    internal sealed class DiEncyclopediaCourtVM : ViewModel
    {
        private const float StandingSegmentWidth = 120f;
        private const float MoodBarWidth = 300f;
        private const float MoodBarGap = 3f;

        private static readonly Color DimColor = Color.ConvertStringToColor("#4A3F30FF");

        private readonly Kingdom _kingdom;

        private bool _visible;
        private bool _isOwnCourt;
        private string _headerLine = string.Empty;
        private string _standingText = string.Empty;
        private Color _standingColor = DiCourtVM.ReliableColor;
        private string _standingMeaning = string.Empty;
        private string _factionReport = string.Empty;
        private string _defectionReport = string.Empty;
        private string _moodText = string.Empty;
        private string _houseCountText = string.Empty;
        private string _claimText = string.Empty;
        private MBBindingList<DiBandSegmentVM> _standingSegments = new MBBindingList<DiBandSegmentVM>();
        private MBBindingList<DiBandSegmentVM> _moodSegments = new MBBindingList<DiBandSegmentVM>();
        private MBBindingList<DiEnvoyHouseVM> _houses = new MBBindingList<DiEnvoyHouseVM>();

        public DiEncyclopediaCourtVM(Kingdom kingdom)
        {
            _kingdom = kingdom;
            Rebuild();
        }

        // ----- bound surface ---------------------------------------------------

        /// <summary>False hides the whole section, divider included.</summary>
        [DataSourceProperty]
        public bool Visible
        {
            get => _visible;
            set { if (value == _visible) return; _visible = value; OnPropertyChangedWithValue(value, nameof(Visible)); }
        }

        [DataSourceProperty]
        public bool IsOwnCourt
        {
            get => _isOwnCourt;
            set
            {
                if (value == _isOwnCourt) return;
                _isOwnCourt = value;
                OnPropertyChangedWithValue(value, nameof(IsOwnCourt));
                OnPropertyChangedWithValue(!value, nameof(IsRivalCourt));
            }
        }

        [DataSourceProperty] public bool IsRivalCourt => !_isOwnCourt;

        [DataSourceProperty] public string SectionTitle => "Court";

        [DataSourceProperty]
        public string OwnCourtText =>
            "This is your own court. Every figure behind it - each house's loyalty, what it holds "
            + "against the crown, the crown's standing - is on the Court tab of the Kingdom screen.";

        [DataSourceProperty]
        public string HeaderLine
        {
            get => _headerLine;
            set { if (value == _headerLine) return; _headerLine = value; OnPropertyChangedWithValue(value, nameof(HeaderLine)); }
        }

        [DataSourceProperty]
        public string StandingText
        {
            get => _standingText;
            set { if (value == _standingText) return; _standingText = value; OnPropertyChangedWithValue(value, nameof(StandingText)); }
        }

        [DataSourceProperty]
        public Color StandingColor
        {
            get => _standingColor;
            set { if (value == _standingColor) return; _standingColor = value; OnPropertyChangedWithValue(value, nameof(StandingColor)); }
        }

        [DataSourceProperty]
        public string StandingMeaning
        {
            get => _standingMeaning;
            set { if (value == _standingMeaning) return; _standingMeaning = value; OnPropertyChangedWithValue(value, nameof(StandingMeaning)); }
        }

        [DataSourceProperty]
        public string FactionReport
        {
            get => _factionReport;
            set { if (value == _factionReport) return; _factionReport = value; OnPropertyChangedWithValue(value, nameof(FactionReport)); }
        }

        [DataSourceProperty]
        public string DefectionReport
        {
            get => _defectionReport;
            set { if (value == _defectionReport) return; _defectionReport = value; OnPropertyChangedWithValue(value, nameof(DefectionReport)); }
        }

        [DataSourceProperty]
        public string MoodText
        {
            get => _moodText;
            set { if (value == _moodText) return; _moodText = value; OnPropertyChangedWithValue(value, nameof(MoodText)); }
        }

        [DataSourceProperty]
        public string HouseCountText
        {
            get => _houseCountText;
            set { if (value == _houseCountText) return; _houseCountText = value; OnPropertyChangedWithValue(value, nameof(HouseCountText)); }
        }

        [DataSourceProperty]
        public string ClaimText
        {
            get => _claimText;
            set { if (value == _claimText) return; _claimText = value; OnPropertyChangedWithValue(value, nameof(ClaimText)); }
        }

        [DataSourceProperty]
        public string ThresholdNote =>
            "Every edge above is a real threshold in their court: you are told which side of it "
            + "a house sits on, never how far.";

        [DataSourceProperty]
        public string LedgerNote =>
            "What each house holds against its ruler, and by how much, is not something an envoy "
            + "can count. An agent inside their court would read you the figures.";

        [DataSourceProperty]
        public MBBindingList<DiBandSegmentVM> StandingSegments
        {
            get => _standingSegments;
            set { if (value == _standingSegments) return; _standingSegments = value; OnPropertyChangedWithValue(value, nameof(StandingSegments)); }
        }

        [DataSourceProperty]
        public MBBindingList<DiBandSegmentVM> MoodSegments
        {
            get => _moodSegments;
            set { if (value == _moodSegments) return; _moodSegments = value; OnPropertyChangedWithValue(value, nameof(MoodSegments)); }
        }

        [DataSourceProperty]
        public MBBindingList<DiEnvoyHouseVM> Houses
        {
            get => _houses;
            set { if (value == _houses) return; _houses = value; OnPropertyChangedWithValue(value, nameof(Houses)); }
        }

        // ----- contents --------------------------------------------------------

        internal void Rebuild()
        {
            try
            {
                Compose();
            }
            catch (Exception ex)
            {
                Visible = false;
                Log.Error("UI", "The court section of an Encyclopedia page could not be built.", ex);
            }
        }

        private void Compose()
        {
            var state = CoreBehavior.State;
            var kingdom = _kingdom;

            // The section exists only while the pillar it reports on is running, and only for
            // a realm that still has a court to report on.
            Visible = SubModule.Healthy && Settings.Current.EnableIntrigue && state != null
                      && kingdom != null && !kingdom.IsEliminated && kingdom.RulingClan != null;
            if (!Visible) return;

            // Your own court is the Court tab's business - the same test that tab uses to
            // decide whose court it shows, so the pointer is never to an empty tab.
            IsOwnCourt = Clan.PlayerClan != null && Clan.PlayerClan.Kingdom == kingdom;
            if (IsOwnCourt) return;

            var ruling = kingdom.RulingClan;
            var ruler = kingdom.Leader;
            var female = ruler != null && ruler.IsFemale;
            var him = female ? "her" : "him";
            var his = female ? "her" : "his";

            HeaderLine = "The court of Clan " + ruling.Name + " - what your envoys can tell you.";

            // The crown, as a band.
            var standing = CourtBands.CrownOf(LegitimacyRegistry.Of(state, kingdom));
            StandingText = CourtBands.Name(standing);
            StandingColor = DiCourtVM.CrownColor(standing);
            StandingMeaning = CourtBands.Meaning(standing, his);

            var segments = new MBBindingList<DiBandSegmentVM>();
            foreach (var band in new[] { CrownStanding.Failing, CrownStanding.Questioned, CrownStanding.Secure })
            {
                var lit = band == standing;
                var colour = DiCourtVM.CrownColor(band);
                segments.Add(new DiBandSegmentVM(StandingSegmentWidth, lit ? colour : DimColor,
                    CourtBands.Name(band), lit ? colour : DiCourtVM.MutedColor));
            }
            StandingSegments = segments;

            // The houses: every sworn clan but the crown's own, the weightiest first. The
            // order is something an envoy would know; the figure behind it is not.
            var members = new List<Clan>();
            foreach (var clan in Court.MembersOf(kingdom))
                if (clan != ruling) members.Add(clan);
            members.Sort((a, b) => b.Influence.CompareTo(a.Influence));

            var counts = new int[4];
            var houses = new MBBindingList<DiEnvoyHouseVM>();
            for (var i = 0; i < members.Count; i++)
            {
                var band = LoyaltyModel.BandOf(state, members[i]);
                counts[(int)band]++;
                houses.Add(new DiEnvoyHouseVM(members[i], band, CourtBands.WeightOf(members[i], kingdom)));
            }
            Houses = houses;
            HouseCountText = members.Count == 0
                ? "none but " + his + " own house"
                : Words(members.Count) + (members.Count == 1 ? " owes " : " owe ") + him + " fealty";

            MoodSegments = MoodBar(counts, members.Count);
            MoodText = MoodSummary(counts, members.Count, female);

            FactionReport = Factions(BlocModel.BlocsOf(state, kingdom), his);
            DefectionReport = Defections(counts[(int)LoyaltyBand.DefectionRisk], him);
            ClaimText = Claims(SuccessionModel.PretendersTo(state, kingdom), kingdom);
        }

        /// <summary>The houses by mood, worst first, as one bar. Widths are shares of the court.</summary>
        private static MBBindingList<DiBandSegmentVM> MoodBar(int[] counts, int total)
        {
            var bar = new MBBindingList<DiBandSegmentVM>();
            if (total == 0) return bar;

            var order = new[] { LoyaltyBand.DefectionRisk, LoyaltyBand.Disaffected, LoyaltyBand.Transactional, LoyaltyBand.Reliable };
            var present = 0;
            for (var i = 0; i < order.Length; i++) if (counts[(int)order[i]] > 0) present++;
            var usable = MoodBarWidth - MoodBarGap * (present - 1);

            for (var i = 0; i < order.Length; i++)
            {
                var n = counts[(int)order[i]];
                if (n == 0) continue;
                bar.Add(new DiBandSegmentVM(usable * n / total, DiCourtVM.BandColor(order[i]),
                    string.Empty, DiCourtVM.MutedColor));
            }
            return bar;
        }

        private static string MoodSummary(int[] counts, int total, bool female)
        {
            var him = female ? "her" : "him";
            if (total == 0) return "There is no court beyond " + (female ? "her" : "his") + " own house.";

            var reliable = counts[(int)LoyaltyBand.Reliable];
            var bought = counts[(int)LoyaltyBand.Transactional];
            var restless = counts[(int)LoyaltyBand.Disaffected] + counts[(int)LoyaltyBand.DefectionRisk];

            string lead;
            if (reliable * 2 > total) lead = "Most of the court is " + (female ? "hers" : "his") + " in earnest.";
            else if (restless * 2 > total) lead = "The court is turning against " + him + ".";
            else if (bought >= reliable && bought >= restless) lead = "Mostly bought rather than won.";
            else lead = "A court of divided loyalties.";

            var tail = restless == 0
                ? " None of the houses is restless."
                : " " + Capital(Words(restless)) + (restless == 1 ? " house is restless." : " houses are restless.");
            return lead + tail;
        }

        private static string Factions(List<CourtBloc> blocs, string his)
        {
            if (blocs == null || blocs.Count == 0)
                return "No faction has formed around any cause. The lords quarrel with each other, not with "
                       + his + " crown.";

            var first = blocs[0];
            var text = new StringBuilder();
            text.Append("The ").Append(DiCourtVM.AgendaName(first.Agenda))
                .Append(" are the strongest party at court");
            if (first.Leader != null) text.Append(", and speak through Clan ").Append(first.Leader.Name);
            text.Append('.');

            if (blocs.Count == 2)
                text.Append(" The ").Append(DiCourtVM.AgendaName(blocs[1].Agenda)).Append(" are organised too.");
            else if (blocs.Count > 2)
            {
                text.Append(' ');
                for (var i = 1; i < blocs.Count; i++)
                {
                    if (i > 1) text.Append(i == blocs.Count - 1 ? " and " : ", ");
                    text.Append(i == 1 ? "The " : "the ").Append(DiCourtVM.AgendaName(blocs[i].Agenda));
                }
                text.Append(" are organised too.");
            }
            return text.ToString();
        }

        private static string Defections(int readyToBreak, string him)
        {
            if (readyToBreak == 0)
                return "No house is spoken of as ready to break with " + him + ".";
            if (readyToBreak == 1)
                return "One house is spoken of as ready to break with " + him
                       + ". Whether it would act on it, your men could not say.";
            return Capital(Words(readyToBreak)) + " houses are spoken of as ready to break with " + him
                   + ". Whether they would act on it, your men could not say.";
        }

        /// <summary>
        /// Standing pretenders are public knowledge - a claim that nobody hears of is not a
        /// claim - so they are named, as the Court tab names them.
        /// </summary>
        private static string Claims(List<Pretender> claims, Kingdom kingdom)
        {
            if (claims.Count == 0)
                return "No one presses a claim to the throne of " + kingdom.Name + " that your envoys know of.";
            if (claims.Count == 1)
                return claims[0].Claimant.Name + " presses a claim to the throne of " + kingdom.Name + ".";

            var names = new StringBuilder();
            for (var i = 0; i < claims.Count; i++)
            {
                if (i > 0) names.Append(i == claims.Count - 1 ? " and " : ", ");
                names.Append(claims[i].Claimant.Name);
            }
            return Capital(Words(claims.Count)) + " claimants press for the throne of " + kingdom.Name
                   + ": " + names + ".";
        }

        private static readonly string[] SmallNumbers =
        {
            "none", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve",
        };

        /// <summary>An envoy counts houses in words. Past twelve, digits read better than prose.</summary>
        private static string Words(int n) => n >= 0 && n < SmallNumbers.Length ? SmallNumbers[n] : n.ToString();

        private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>
        /// The section as text, field by field, for <c>diplomacy.court_bands</c>: the same
        /// object the page binds, so the command checks what the page will show rather than a
        /// second rendering of it.
        /// </summary>
        internal string Describe()
        {
            if (!Visible) return "(section hidden)" + Environment.NewLine;
            if (IsOwnCourt) return OwnCourtText + Environment.NewLine;

            var sb = new StringBuilder();
            sb.AppendLine(HeaderLine);
            sb.AppendLine("  Crown's standing: " + StandingText + " - " + StandingMeaning);
            sb.AppendLine("  " + FactionReport);
            sb.AppendLine("  " + DefectionReport);
            sb.AppendLine("  Mood: " + MoodText);
            sb.AppendLine("  Houses (" + HouseCountText + "):");
            for (var i = 0; i < _houses.Count; i++)
                sb.AppendLine("    " + _houses[i].Name.PadRight(16) + _houses[i].MoodText.PadRight(17) + _houses[i].WeightText);
            sb.AppendLine("  " + ClaimText);
            return sb.ToString();
        }
    }

    /// <summary>One segment of a band bar: a width, a colour, and an optional label under it.</summary>
    internal sealed class DiBandSegmentVM : ViewModel
    {
        public DiBandSegmentVM(float width, Color color, string label, Color labelColor)
        {
            SegmentWidth = width;
            Color = color;
            Label = label;
            LabelColor = labelColor;
        }

        [DataSourceProperty] public float SegmentWidth { get; }
        [DataSourceProperty] public Color Color { get; }
        [DataSourceProperty] public string Label { get; }
        [DataSourceProperty] public Color LabelColor { get; }
    }

    /// <summary>One house of a rival court, named but not measured.</summary>
    internal sealed class DiEnvoyHouseVM : ViewModel
    {
        public DiEnvoyHouseVM(Clan clan, LoyaltyBand band, CourtWeight weight)
        {
            Name = clan.Name.ToString();
            MoodText = CourtBands.MoodName(band);
            MoodColor = DiCourtVM.BandColor(band);
            WeightText = CourtBands.Name(weight);
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string MoodText { get; }
        [DataSourceProperty] public Color MoodColor { get; }
        [DataSourceProperty] public string WeightText { get; }
    }
}
