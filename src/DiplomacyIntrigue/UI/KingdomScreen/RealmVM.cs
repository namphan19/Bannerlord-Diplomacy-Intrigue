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
    /// way the design proposal asked for it. The tab lives inside the game's own
    /// Kingdom screen as a sixth header button; see
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
        internal static readonly Color GoldColor = Color.ConvertStringToColor("#E6C87FFF");

        /// <summary>Hides the five vanilla categories; supplied by the management mixin.</summary>
        private readonly Action _onShow;

        private bool _show;
        private bool _tabVisible;
        private string _tabText = "Realm";
        private string _standingTitle = string.Empty;
        private string _standingDetail = string.Empty;
        private string _patronLine = string.Empty;
        private string _sphereStrengthText = string.Empty;
        private string _dominanceText = string.Empty;
        private string _ambitionText = string.Empty;
        private string _greedText = string.Empty;
        private Color _standingColor = GoldColor;
        private Color _greedColor = MutedColor;
        private MBBindingList<DiRealmWarVM> _wars = new MBBindingList<DiRealmWarVM>();
        private MBBindingList<DiRealmVassalVM> _vassals = new MBBindingList<DiRealmVassalVM>();
        private MBBindingList<DiRealmSphereVM> _spheres = new MBBindingList<DiRealmSphereVM>();
        private MBBindingList<DiRealmClaimVM> _claims = new MBBindingList<DiRealmClaimVM>();
        private MBBindingList<DiRealmAgreementVM> _agreements = new MBBindingList<DiRealmAgreementVM>();

        public DiRealmVM(Action onShow)
        {
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

        [DataSourceProperty]
        public MBBindingList<DiRealmClaimVM> Claims
        {
            get => _claims;
            set
            {
                if (value == _claims) return;
                _claims = value;
                OnPropertyChangedWithValue(value, nameof(Claims));
            }
        }

        [DataSourceProperty] public bool HasClaims => _claims.Count > 0;

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
            SphereStrengthText = string.Empty;
            DominanceText = string.Empty;
            AmbitionText = string.Empty;
            GreedText = string.Empty;
            GreedColor = MutedColor;
            PatronLine = string.Empty;
            var wars = new MBBindingList<DiRealmWarVM>();
            var vassals = new MBBindingList<DiRealmVassalVM>();
            var spheres = new MBBindingList<DiRealmSphereVM>();
            var claims = new MBBindingList<DiRealmClaimVM>();
            var agreements = new MBBindingList<DiRealmAgreementVM>();

            var state = CoreBehavior.State;
            var us = Clan.PlayerClan?.Kingdom;
            if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy || state == null || us == null)
            {
                Wars = wars; Vassals = vassals; Spheres = spheres;
                Claims = claims; Agreements = agreements;
                return;
            }

            var patron = Hegemony.PatronOf(state, us);
            var vassalCount = Hegemony.VassalCount(state, us);
            StandingTitle = patron != null ? "Vassal of " + patron.Name
                : vassalCount > 0 ? "Hegemon over " + vassalCount + " kingdom(s)"
                : "Independent";
            StandingColor = patron != null ? NegativeColor : vassalCount > 0 ? GoldColor : MutedColor;
            var sphereHead = Hegemony.SphereHead(state, us);
            StandingDetail = Power.Describe(state, us);
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

            foreach (var war in state.OngoingWarsOf(us))
            {
                var enemy = war.Other(us);
                if (enemy == null) continue;
                var target = enemy;
                var score = war.ScoreFor(us);
                wars.Add(new DiRealmWarVM(
                    "vs " + enemy.Name,
                    war.DaysElapsed.ToString("0") + " days"
                        + (war.Justification == CasusBelliType.None
                            ? ", no claim on record"
                            : " over " + war.Justification)
                        + (war.IsObligationWar && war.CalledBy != null
                            ? "   -   called in by " + war.CalledBy.Name : ""),
                    "our exhaustion " + war.ExhaustionOf(us).ToString("0.0"),
                    "they are " + ExhaustionBands.Describe(war.ExhaustionOf(enemy)),
                    (score >= 0f ? "+" : "") + score.ToString("0"),
                    score >= 0f ? PositiveColor : NegativeColor,
                    Color.FromUint(enemy.Color),
                    () => DiplomacyMenu.ShowPeace(state, us, target)));
            }

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
                    "hold " + hold.ToString("0"),
                    DriftText(hold, target),
                    target > hold + 1f ? PositiveColor : target < hold - 1f ? NegativeColor : MutedColor,
                    "revolts below " + threshold.ToString("0"),
                    link.DefianceMarks > 0 ? "defied x" + link.DefianceMarks : string.Empty,
                    link.TributeAmount + " per period",
                    TermLeft(link)));
            }

            // A rival's bond is described, never numbered: the exact hold is the sort
            // of thing Phase 3 espionage is meant to sell.
            foreach (var other in Kingdom.All)
            {
                if (other == us || !other.IsRealm() || !Hegemony.IsHegemon(state, other)) continue;
                var held = new List<Treaty>();
                Hegemony.CollectVassalages(state, other, held);
                if (held.Count == 0) continue;

                var names = new List<string>(held.Count);
                for (var i = 0; i < held.Count; i++)
                    names.Add(held[i].SubordinateParty.Name + " (" + DiplomacyMenu.HoldMeaning(state, held[i]) + ")");

                spheres.Add(new DiRealmSphereVM(
                    Color.FromUint(other.Color),
                    other.Name + " holds " + held.Count + ":",
                    string.Join(", ", names.ToArray()),
                    "sphere " + Hegemony.SphereStrength(state, other).ToString("0")));
            }

            foreach (var other in Kingdom.All)
            {
                if (other == us || !other.IsRealm()) continue;
                foreach (var claim in ClaimRegistry.LiveClaims(state, us, other))
                {
                    var footer = claim.AllowsFiefDemands ? "entitles land" : string.Empty;
                    if (claim.ExpiresOn != CampaignTime.Never)
                    {
                        var daysLeft = (claim.ExpiresOn - CampaignTime.Now).ToDays;
                        var expiry = "ages out in " + Math.Max(0, (int)daysLeft) + " days";
                        footer = footer.Length > 0 ? footer + "   -   " + expiry : expiry;
                    }
                    claims.Add(new DiRealmClaimVM(
                        other.Name.ToString(),
                        Color.FromUint(other.Color),
                        claim.Type.ToString(),
                        claim.Legitimacy.ToString("0.00"),
                        claim.Legitimacy >= 0.5f ? PositiveColor : NegativeColor,
                        claim.AllowsFiefDemands,
                        false,
                        0,
                        footer));
                }
            }
            for (var i = 0; i < state.Fabrications.Count; i++)
            {
                var f = state.Fabrications[i];
                if (f == null || f.Claimant != us) continue;
                var progress = 100 - (int)(100f * f.DaysRemaining / DiplomacyConstants.FabricateClaimDurationDays);
                if (progress < 0) progress = 0;
                if (progress > 100) progress = 100;
                var targetKingdom = f.TargetKingdom;
                claims.Add(new DiRealmClaimVM(
                    targetKingdom == null ? "?" : targetKingdom.Name.ToString(),
                    targetKingdom != null ? Color.FromUint(targetKingdom.Color) : MutedColor,
                    "fabricating" + (f.Target == null ? "" : " over " + f.Target.Name),
                    string.Empty,
                    MutedColor,
                    false,
                    true,
                    progress,
                    f.DaysRemaining.ToString("0") + " days left"));
            }

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
            }

            Wars = wars; Vassals = vassals; Spheres = spheres;
            Claims = claims; Agreements = agreements;
        }

        private static string DriftText(float hold, float target)
        {
            if (target > hold + 1f) return "rising to " + target.ToString("0");
            if (target < hold - 1f) return "falling to " + target.ToString("0");
            return "steady";
        }

        private static string TermLeft(Treaty treaty)
        {
            return treaty.ExpiresOn == CampaignTime.Never ? "open-ended" : "term ends " + treaty.ExpiresOn;
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

        public DiRealmWarVM(string name, string detail, string ourText, string theirText,
            string scoreText, Color scoreColor, Color accentColor, Action negotiate)
        {
            Name = name;
            Detail = detail;
            OurText = ourText;
            TheirText = theirText;
            ConditionText = ourText + "   -   " + theirText;
            ScoreText = scoreText;
            ScoreColor = scoreColor;
            AccentColor = accentColor;
            _negotiate = negotiate;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Detail { get; }
        [DataSourceProperty] public string OurText { get; }
        [DataSourceProperty] public string TheirText { get; }
        [DataSourceProperty] public string ConditionText { get; }
        [DataSourceProperty] public string ScoreText { get; }
        [DataSourceProperty] public Color ScoreColor { get; }
        [DataSourceProperty] public Color AccentColor { get; }

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

    /// <summary>
    /// One of our vassals: the hold figure as a bar and a number, where it is drifting,
    /// the line it revolts under, its defiance marks, its tribute and its term.
    /// </summary>
    internal sealed class DiRealmVassalVM : ViewModel
    {
        public DiRealmVassalVM(string name, Color accentColor, int holdValue, int thresholdValue,
            string holdText, string driftText, Color driftColor, string thresholdText, string marksText,
            string tributeText, string termText)
        {
            const int barWidth = 160;
            Name = name;
            AccentColor = accentColor;
            HoldValue = Clamp(holdValue);
            ThresholdValue = Clamp(thresholdValue);
            ThresholdPixelWidth = (int)(ThresholdValue / 100f * barWidth);
            HoldText = holdText;
            DriftText = driftText;
            DriftColor = driftColor;
            ThresholdText = thresholdText;
            MarksText = marksText;
            TributeText = tributeText;
            TermText = termText;
            HasMarks = !string.IsNullOrEmpty(marksText);
        }

        private static int Clamp(int v) => v < 0 ? 0 : v > 100 ? 100 : v;

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public int HoldValue { get; }
        [DataSourceProperty] public int ThresholdValue { get; }
        [DataSourceProperty] public int ThresholdPixelWidth { get; }
        [DataSourceProperty] public string HoldText { get; }
        [DataSourceProperty] public string DriftText { get; }
        [DataSourceProperty] public Color DriftColor { get; }
        [DataSourceProperty] public string ThresholdText { get; }
        [DataSourceProperty] public string MarksText { get; }
        [DataSourceProperty] public bool HasMarks { get; }
        [DataSourceProperty] public string TributeText { get; }
        [DataSourceProperty] public string TermText { get; }
    }

    /// <summary>Another hegemon's sphere: who answers to it and how that bond behaves.</summary>
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

    /// <summary>One claim against another kingdom, live or still being fabricated.</summary>
    internal sealed class DiRealmClaimVM : ViewModel
    {
        public DiRealmClaimVM(string kingdomText, Color accentColor, string typeText, string legitimacyText,
            Color legitimacyColor, bool isLand, bool isFabricating, int progress, string footerText)
        {
            KingdomText = kingdomText;
            AccentColor = accentColor;
            TypeText = typeText;
            LegitimacyText = legitimacyText;
            LegitimacyColor = legitimacyColor;
            IsLand = isLand;
            IsFabricating = isFabricating;
            HasLegitimacy = !isFabricating;
            Progress = progress < 0 ? 0 : progress > 100 ? 100 : progress;
            FooterText = footerText;
            HasFooter = !string.IsNullOrEmpty(footerText);
        }

        [DataSourceProperty] public string KingdomText { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public string TypeText { get; }
        [DataSourceProperty] public string LegitimacyText { get; }
        [DataSourceProperty] public Color LegitimacyColor { get; }
        [DataSourceProperty] public bool HasLegitimacy { get; }
        [DataSourceProperty] public bool IsLand { get; }
        [DataSourceProperty] public bool IsFabricating { get; }
        [DataSourceProperty] public int Progress { get; }
        [DataSourceProperty] public string FooterText { get; }
        [DataSourceProperty] public bool HasFooter { get; }
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
