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
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// What the mod knows about the other kingdom, and what the player can do about it,
    /// drawn in the Kingdom screen's Diplomacy tab with the game's own widgets.
    ///
    /// Layout and copy follow the approved mockup (docs/ui-proposal/1-diplomacy-tab.dc.html):
    /// a coloured header word between the banners, the one-line headline, a note about
    /// what the bars mean, then the mod's rows at the top of the game's own comparison
    /// list. The mockup draws the note below its bars; here it sits above ours, because
    /// our rows and the game's share one list and the note's "continue below" line has to
    /// precede the rows it describes.
    ///
    /// Comparison rows go into the panel's own <c>Stats</c> list as vanilla
    /// <c>KingdomWarComparableStatVM</c> rows, so TaleWorlds' own template draws them.
    /// That only works hooked on <c>UpdateDiplomacyProperties</c>: it starts with
    /// <c>Stats.Clear()</c> and runs on every selection, so anything appended after an
    /// earlier method is erased on the next click, and anything computed in the mixin
    /// constructor alone goes stale. An earlier revision hooked <c>RefreshValues</c>
    /// (which <c>KingdomTruceItemVM</c> does not even override) and kept its own
    /// parallel bar list with copied markup; both workarounds are gone.
    ///
    /// <b>Our exhaustion exactly, theirs as a band</b> (spec 01 §8). The enemy side of
    /// the exhaustion row carries the floor of their band, never their figure - "Weary"
    /// reads as 40 whether they are at 41 or 59 - and the hover hint says so. A nicer
    /// surface is not a reason to give away what Phase 3's ReadCourt is meant to sell.
    ///
    /// Two concrete mixins rather than one on the base: each item type rebuilds its own
    /// rows in its own <c>UpdateDiplomacyProperties</c> override, and the hook must name
    /// the method that rebuilds the data it touches.
    /// </summary>
    internal abstract class DiplomacyItemMixinBase<T> : BaseViewModelMixin<T>
        where T : KingdomDiplomacyItemVM
    {
        // Palette, one entry per meaning (the mockup's): green is good for us, red is
        // bad for us, gold is a threshold or a bond of ours.
        internal static readonly Color PositiveColor = Color.ConvertStringToColor("#9AC26AFF");
        internal static readonly Color NegativeColor = Color.ConvertStringToColor("#E08070FF");
        internal static readonly Color NeutralColor = Color.ConvertStringToColor("#E0CFA8FF");
        internal static readonly Color MutedColor = Color.ConvertStringToColor("#A89878FF");
        internal static readonly Color GoldColor = Color.ConvertStringToColor("#D9A441FF");

        private string _headerWord = string.Empty;
        private Color _headerWordColor;
        private string _headline = string.Empty;
        private string _note = string.Empty;
        private string _rowStatus = string.Empty;
        private string _rowValue = string.Empty;
        private Color _rowValueColor;
        private bool _showPacts;
        private string _pactValueText = string.Empty;
        private MBBindingList<DiplomacyActionVM> _actions = new MBBindingList<DiplomacyActionVM>();
        private MBBindingList<DiPactRungVM> _pactRungs = new MBBindingList<DiPactRungVM>();

        protected DiplomacyItemMixinBase(T vm) : base(vm)
        {
            _headerWordColor = MutedColor;
            _rowValueColor = MutedColor;
            // The vanilla constructor already ran UpdateDiplomacyProperties before this
            // mixin existed, so the first population is done by hand.
            Rebuild();
        }

        /// <summary>
        /// The word between the banners: "AT WAR", "ANSWERS TO US", "ALLIED", "PAYS US",
        /// "AT PEACE" - what this kingdom is to us, before any detail.
        /// </summary>
        [DataSourceProperty]
        public string DiHeaderWord
        {
            get => _headerWord;
            set
            {
                if (value == _headerWord) return;
                _headerWord = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiHeaderWord));
            }
        }

        [DataSourceProperty]
        public Color DiHeaderWordColor
        {
            get => _headerWordColor;
            set
            {
                if (value.Equals(_headerWordColor)) return;
                _headerWordColor = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiHeaderWordColor));
            }
        }

        [DataSourceProperty] public bool DiHasHeaderWord => !string.IsNullOrEmpty(_headerWord);

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

        [DataSourceProperty] public bool DiHasHeadline => !string.IsNullOrEmpty(_headline);

        /// <summary>What the bars mean here - the mockup's note line under the headline.</summary>
        [DataSourceProperty]
        public string DiNote
        {
            get => _note;
            set
            {
                if (value == _note) return;
                _note = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiNote));
            }
        }

        [DataSourceProperty] public bool DiHasNote => !string.IsNullOrEmpty(_note);

        /// <summary>The relationship, on the war/truce list row ("Our vassal", "At war for 74 days").</summary>
        [DataSourceProperty]
        public string DiRowStatus
        {
            get => _rowStatus;
            set
            {
                if (value == _rowStatus) return;
                _rowStatus = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiRowStatus));
            }
        }

        [DataSourceProperty] public bool DiHasRowStatus => !string.IsNullOrEmpty(_rowStatus);

        /// <summary>The one figure that matters on the row ("Exhausted  +122", "Hold  52").</summary>
        [DataSourceProperty]
        public string DiRowValue
        {
            get => _rowValue;
            set
            {
                if (value == _rowValue) return;
                _rowValue = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiRowValue));
            }
        }

        [DataSourceProperty]
        public Color DiRowValueColor
        {
            get => _rowValueColor;
            set
            {
                if (value.Equals(_rowValueColor)) return;
                _rowValueColor = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiRowValueColor));
            }
        }

        [DataSourceProperty] public bool DiHasRowValue => !string.IsNullOrEmpty(_rowValue);

        /// <summary>
        /// "What their court would sign": the one number their court decides on, and the
        /// three rungs it is weighed against. Hidden until the Propose-a-treaty action
        /// opens it, as the mockup has it.
        /// </summary>
        [DataSourceProperty]
        public bool DiShowPacts
        {
            get => _showPacts;
            set
            {
                if (value == _showPacts) return;
                _showPacts = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiShowPacts));
            }
        }

        [DataSourceProperty]
        public string DiPactValueText
        {
            get => _pactValueText;
            set
            {
                if (value == _pactValueText) return;
                _pactValueText = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiPactValueText));
            }
        }

        [DataSourceProperty]
        public MBBindingList<DiPactRungVM> DiPactRungs
        {
            get => _pactRungs;
            set
            {
                if (value == _pactRungs) return;
                _pactRungs = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(DiPactRungs));
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
                DiHeaderWord = string.Empty;
                DiHeadline = string.Empty;
                DiNote = string.Empty;
                DiRowStatus = string.Empty;
                DiRowValue = string.Empty;
                DiActions = new MBBindingList<DiplomacyActionVM>();
            }
        }

        private void Compose()
        {
            DiHeaderWord = string.Empty;
            DiHeadline = string.Empty;
            DiNote = string.Empty;
            DiRowStatus = string.Empty;
            DiRowValue = string.Empty;
            var actions = new MBBindingList<DiplomacyActionVM>();

            var state = CoreBehavior.State;
            var faction1 = ViewModel?.Faction1 as Kingdom;
            var faction2 = ViewModel?.Faction2 as Kingdom;

            if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy
                || state == null || faction1 == null || faction2 == null)
            {
                DiActions = actions;
                return;
            }

            // Which side of the pair we are on is not guaranteed by the list, so it is
            // resolved against the player's own kingdom rather than assumed.
            var player = Clan.PlayerClan?.Kingdom;
            var us = faction1;
            var them = faction2;
            if (player != null && them == player)
            {
                us = faction2;
                them = faction1;
            }

            var war = state.OngoingWarBetween(us, them);
            DiHeadline = war != null ? WarHeadline(war, them) : PeaceHeadline(state, us, them);
            ComposeHeaderWord(state, us, them, war);
            DiNote = NoteFor(state, us, them, war);
            ComposeRow(state, us, them, war);
            DiPactRungs = new MBBindingList<DiPactRungVM>();
            DiPactValueText = string.Empty;
            // Closed until the Propose-a-treaty action opens it - the mockup starts with
            // showPacts false and toggles it from the action strip.
            DiShowPacts = false;

            // Vanilla's own comparison rows, in vanilla's own order (Faction1, Faction2)
            // so our bars sit beside theirs rather than mirrored. Vanilla has just
            // cleared and refilled Stats, so inserting at a fixed index is stable and
            // cannot duplicate.
            BuildStats(state, faction1, faction2, war);

            // Foreign policy belongs to the ruler. A vassal still sees everything - looking
            // is free, and a vassal has every reason to watch - but acts through no button.
            if (player != null && us == player && player.Leader == Hero.MainHero)
            {
                BuildActions(state, us, them, war, actions);
                if (war == null) BuildPactRungs(state, us, them);
            }

            DiActions = actions;
        }

        // ----- the word between the banners ------------------------------------

        private void ComposeHeaderWord(ModState state, Kingdom us, Kingdom them, WarRecord war)
        {
            if (war != null)
            {
                DiHeaderWord = "AT WAR";
                DiHeaderWordColor = NegativeColor;
                return;
            }

            var theirLink = Hegemony.VassalageOf(state, them);
            if (theirLink != null && theirLink.DominantParty == us)
            {
                DiHeaderWord = "ANSWERS TO US";
                DiHeaderWordColor = GoldColor;
                return;
            }

            var ourLink = Hegemony.VassalageOf(state, us);
            if (ourLink != null && ourLink.DominantParty == them)
            {
                DiHeaderWord = "ANSWERS TO THEM";
                DiHeaderWordColor = GoldColor;
                return;
            }

            if (state.ActiveTreatyBetween(us, them, TreatyType.Alliance) != null)
            {
                DiHeaderWord = "ALLIED";
                DiHeaderWordColor = PositiveColor;
                return;
            }

            var tribute = state.ActiveTreatyBetween(us, them, TreatyType.TributaryPact);
            if (tribute != null && tribute.TributeAmount > 0)
            {
                DiHeaderWord = tribute.TributePayer == them ? "PAYS US" : "WE PAY THEM";
                DiHeaderWordColor = tribute.TributePayer == them ? GoldColor : MutedColor;
                return;
            }

            DiHeaderWord = "AT PEACE";
            DiHeaderWordColor = PositiveColor;
        }

        // ----- the line under the banners -------------------------------------

        private static string WarHeadline(WarRecord war, Kingdom them)
        {
            var band = ExhaustionBands.Of(war.ExhaustionOf(them));
            // Wars the mod did not start have no claim on record - "over None" reads as
            // a bug, so the honest phrasing is that no claim was ever pressed.
            var text = war.Justification == CasusBelliType.None
                ? "War with no claim on record"
                : "War over " + war.Justification
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

                switch (treaty.Type)
                {
                    case TreatyType.Alliance:
                        parts.Add("Alliance - they answer your call in any war, and you answer theirs");
                        break;
                    case TreatyType.DefensivePact:
                        parts.Add("DefensivePact - each of you joins when the other is attacked");
                        break;
                    case TreatyType.NonAggressionPact:
                        parts.Add("NonAggressionPact - neither of you may declare war while it stands");
                        break;
                    case TreatyType.TributaryPact:
                        parts.Add(treaty.TributePayer == them
                            ? "TributaryPact (" + treaty.TributeAmount + " from " + them.Name
                              + ") - they buy the peace, they owe you no army"
                            : "TributaryPact (" + treaty.TributeAmount + " to " + them.Name
                              + ") - you buy the peace, they owe you no army");
                        break;
                    case TreatyType.Vassalage:
                        parts.Add(treaty.DominantParty == us
                            ? "Vassalage (" + treaty.TributeAmount + " from " + them.Name
                              + ") - " + them.Name + " answers to " + us.Name
                              + " at hold " + Hegemony.HoldOf(treaty).ToString("0")
                            : "Vassalage (" + treaty.TributeAmount + " to " + them.Name
                              + ") - we answer to " + them.Name
                              + " at hold " + Hegemony.HoldOf(treaty).ToString("0"));
                        break;
                }
            }

            var patron = Hegemony.PatronOf(state, them);
            if (patron != null)
            {
                var link = Hegemony.VassalageOf(state, them);
                var relation = parts.Count == 0 ? "No agreements with you" : string.Join("   -   ", parts.ToArray());
                return relation + "   -   " + them.Name + " answers to " + patron.Name
                          + " at hold " + Hegemony.HoldOf(link).ToString("0");
            }

            return parts.Count == 0 ? "No agreements with you." : string.Join("   -   ", parts.ToArray());
        }

        // ----- the note about the bars ----------------------------------------

        private static string NoteFor(ModState state, Kingdom us, Kingdom them, WarRecord war)
        {
            if (war != null)
            {
                return war.IsObligationWar
                    ? "An obligation war: you were called in, so it carries no weariness of its own."
                    : "Their exhaustion shows the floor of their band, never the figure. "
                      + "The exact number is what an espionage report buys.";
            }

            var theirLink = Hegemony.VassalageOf(state, them);
            if (theirLink != null && theirLink.DominantParty == us)
                return "Hold, its terms and the revolt line live one tab over, in Realm - "
                       + "they are about your sphere, not about this pair.";

            if (state.ActiveTreatyBetween(us, them, TreatyType.Alliance) != null)
                return "Nothing stands above an alliance, so there is nothing left to propose here.";

            if (state.ActiveTreatyBetween(us, them, TreatyType.TributaryPact) != null)
                return "A tributary pays and nothing more. A vassal pays and fights - "
                       + "that is a peace-table demand, not a proposal.";

            var theirTrust = TrustRegistry.Get(state, them, us);
            if (theirTrust < DiplomacyConstants.TrustFloorForPacts)
                return "Below a trust of " + DiplomacyConstants.TrustFloorForPacts.ToString("0")
                       + " they will sign nothing but a truce or the terms that end a war.";

            return "They trust you enough to talk. Whether they sign is their own valuation, shown below.";
        }

        // ----- the list row ---------------------------------------------------

        private void ComposeRow(ModState state, Kingdom us, Kingdom them, WarRecord war)
        {
            if (war != null)
            {
                DiRowStatus = "At war for " + war.DaysElapsed.ToString("0") + " days";
                var band = ExhaustionBands.Of(war.ExhaustionOf(them));
                var score = war.ScoreFor(us);
                DiRowValue = ExhaustionBands.Name(band) + "  " + (score >= 0f ? "+" : "-")
                             + Math.Abs(score).ToString("0");
                DiRowValueColor = score >= 0f ? PositiveColor : NegativeColor;
                return;
            }

            // The row's right edge: what this kingdom is to us, and the one figure that
            // matters most about it.
            var theirLink = Hegemony.VassalageOf(state, them);
            if (theirLink != null && theirLink.DominantParty == us)
            {
                DiRowStatus = "Our vassal";
                SetHoldValue(Hegemony.HoldOf(theirLink));
                return;
            }

            var ourLink = Hegemony.VassalageOf(state, us);
            if (ourLink != null && ourLink.DominantParty == them)
            {
                DiRowStatus = "Our patron";
                SetHoldValue(Hegemony.HoldOf(ourLink));
                return;
            }

            if (theirLink != null)
            {
                // A rival's bond is described, never numbered: the exact hold is what
                // Phase 3's espionage sells, and the mockup's figure is sample data. The
                // row carries the band's short word ("loyal", "resisting"); the full
                // sentence lives in the detail pane.
                DiRowStatus = "Vassal of " + theirLink.DominantParty.Name;
                DiRowValue = ShortHold(DiplomacyMenu.HoldMeaning(state, theirLink));
                DiRowValueColor = MutedColor;
                return;
            }

            var tribute = state.ActiveTreatyBetween(us, them, TreatyType.TributaryPact);
            if (tribute != null && tribute.TributeAmount > 0)
            {
                DiRowStatus = "Tributary";
                DiRowValue = tribute.TributePayer == them
                    ? tribute.TributeAmount + " to us"
                    : tribute.TributeAmount + " to them";
                DiRowValueColor = tribute.TributePayer == them ? PositiveColor : NegativeColor;
                return;
            }

            foreach (var treaty in state.ActiveTreatiesOf(us))
            {
                if (!treaty.IsBetween(us, them) || treaty.Type == TreatyType.Truce
                    || treaty.Type == TreatyType.Vassalage) continue;
                DiRowStatus = ShortTreatyName(treaty.Type);
                DiRowValue = TermLeft(treaty);
                DiRowValueColor = MutedColor;
                return;
            }

            DiRowStatus = "Independent";
        }

        private void SetHoldValue(float hold)
        {
            DiRowValue = "Hold  " + hold.ToString("0");
            DiRowValueColor = hold >= DiplomacyConstants.HoldRenewThreshold ? PositiveColor
                : hold >= DiplomacyConstants.HoldPassiveResistanceThreshold ? GoldColor
                    : NegativeColor;
        }

        /// <summary>
        /// The first clause of <see cref="DiplomacyMenu.HoldMeaning"/> - "loyal" out of
        /// "loyal; will renew when the term ends" - for the row, which has one line. The
        /// full sentence is the detail pane's, and the band boundaries are still the
        /// resolver's own.
        /// </summary>
        private static string ShortHold(string meaning)
        {
            if (string.IsNullOrEmpty(meaning)) return string.Empty;
            var cut = meaning.IndexOfAny(new[] { ';', ',' });
            return cut > 0 ? meaning.Substring(0, cut).Trim() : meaning;
        }

        private static string ShortTreatyName(TreatyType type)
        {
            switch (type)
            {
                case TreatyType.NonAggressionPact: return "Non-aggression pact";
                case TreatyType.DefensivePact: return "Defensive pact";
                case TreatyType.Alliance: return "Alliance";
                case TreatyType.TributaryPact: return "Tributary";
                case TreatyType.Vassalage: return "Vassalage";
                default: return "Truce";
            }
        }

        /// <summary>
        /// Time left, the mockup's way: "2 years left" on the clock, or how long an
        /// open-ended pact has already stood ("6 years").
        /// </summary>
        private static string TermLeft(Treaty treaty)
        {
            if (treaty.ExpiresOn == CampaignTime.Never)
            {
                var age = (float)(CampaignTime.Now - treaty.SignedOn).ToDays;
                return Duration(age);
            }
            var days = (float)(treaty.ExpiresOn - CampaignTime.Now).ToDays;
            return Duration(days) + " left";
        }

        private static string Duration(float days)
        {
            return days < 84f ? days.ToString("0") + " days" : (days / 84f).ToString("0.0") + " years";
        }

        /// <summary>
        /// The pact chooser's three rungs. One number decides all of them - their court's
        /// own valuation, shown honestly - and each rung differs only in the bar it has
        /// to clear and the price the court charges us for asking.
        /// </summary>
        private void BuildPactRungs(ModState state, Kingdom us, Kingdom them)
        {
            var theirValue = (int)AiDiplomacy.PactValue(state, them, us);
            DiPactValueText = them.Name + " values a pact with you at " + theirValue;

            var rungs = new MBBindingList<DiPactRungVM>();
            AddRung(rungs, state, us, them, TreatyType.NonAggressionPact,
                "Non-aggression pact", theirValue);
            AddRung(rungs, state, us, them, TreatyType.DefensivePact,
                "Defensive pact", theirValue);
            AddRung(rungs, state, us, them, TreatyType.Alliance,
                "Alliance", theirValue);
            DiPactRungs = rungs;
        }

        private void AddRung(MBBindingList<DiPactRungVM> rungs, ModState state, Kingdom us,
            Kingdom them, TreatyType type, string name, int theirValue)
        {
            var canSign = TreatyRegistry.CanSign(state, us, them, type, out var reason);
            var threshold = (int)DiplomacyMenu.ThresholdFor(type);
            var cost = DiplomacyConstants.TreatyInfluenceCost(type);
            var hint = canSign
                ? cost + " influence. " + them.Name + " weighs this at " + theirValue
                  + " and needs " + threshold + " - the same number their court would use."
                : reason;
            rungs.Add(new DiPactRungVM(name, theirValue, threshold, canSign, cost, hint,
                () => DiplomacyMenu.ProposePact(state, us, them, type)));
        }

        // ----- comparison rows (vanilla's list, vanilla's row type) --------------

        private void BuildStats(ModState state, Kingdom faction1, Kingdom faction2, WarRecord war)
        {
            var stats = ViewModel?.Stats;
            if (stats == null) return;

            var player = Clan.PlayerClan?.Kingdom;
            // The player's own side reads exactly; any other side reads as a band.
            // With no player kingdom there is no secret to keep, so the first side
            // stands in as the exact one, as before.
            var exact1 = player != null ? faction1 == player : true;
            var exact2 = player != null ? faction2 == player : false;

            // The mockup's three rows and no more: war exhaustion, trust, standing
            // claims. The game's own rows (strength, fiefs, clans) continue below ours.
            var rows = new List<KingdomWarComparableStatVM>(3);

            if (war != null)
            {
                var exhaustHint1 = ExhaustionHint(war, faction1, exact1);
                var exhaustHint2 = ExhaustionHint(war, faction2, exact2);
                rows.Add(new KingdomWarComparableStatVM(
                    DisplayExhaustion(war, faction1, exact1),
                    DisplayExhaustion(war, faction2, exact2),
                    new TextObject("War Exhaustion"),
                    ColourOf(faction1), ColourOf(faction2), 100,
                    new BasicTooltipViewModel(() => exhaustHint1),
                    new BasicTooltipViewModel(() => exhaustHint2)));
            }

            var trust12 = TrustRegistry.Get(state, faction1, faction2);
            var trust21 = TrustRegistry.Get(state, faction2, faction1);
            var trustHint1 = TrustHint(faction1, faction2, trust12);
            var trustHint2 = TrustHint(faction2, faction1, trust21);
            rows.Add(new KingdomWarComparableStatVM(
                (int)trust12, (int)trust21,
                new TextObject("Diplomatic Trust"),
                ColourOf(faction1), ColourOf(faction2), 100,
                new BasicTooltipViewModel(() => trustHint1),
                new BasicTooltipViewModel(() => trustHint2)));

            var claims12 = Count(ClaimRegistry.LiveClaims(state, faction1, faction2));
            var claims21 = Count(ClaimRegistry.LiveClaims(state, faction2, faction1));
            if (claims12 > 0 || claims21 > 0)
            {
                var best12 = ClaimRegistry.Best(state, faction1, faction2);
                var best21 = ClaimRegistry.Best(state, faction2, faction1);
                var claimHint1 = ClaimHint(faction1, faction2, best12);
                var claimHint2 = ClaimHint(faction2, faction1, best21);
                rows.Add(new KingdomWarComparableStatVM(
                    claims12, claims21,
                    new TextObject("Standing Claims"),
                    ColourOf(faction1), ColourOf(faction2),
                    Math.Max(5, Math.Max(claims12, claims21)),
                    new BasicTooltipViewModel(() => claimHint1),
                    new BasicTooltipViewModel(() => claimHint2)));
            }

            for (var i = 0; i < rows.Count; i++)
                stats.Insert(Math.Min(i, stats.Count), rows[i]);
        }

        /// <summary>
        /// What the bar shows for one side: the exact figure where the player may know
        /// it, the band floor elsewhere. Hint strings are built eagerly and handed over
        /// as constants, so hovering them cannot throw into Gauntlet.
        /// </summary>
        private static int DisplayExhaustion(WarRecord war, Kingdom side, bool exact)
        {
            var value = war.ExhaustionOf(side);
            return exact ? (int)value : (int)ExhaustionBands.Floor(ExhaustionBands.Of(value));
        }

        private static string ExhaustionHint(WarRecord war, Kingdom side, bool exact)
        {
            var value = war.ExhaustionOf(side);
            if (exact)
                return side.Name + " exhaustion exactly: " + value.ToString("0.0")
                       + ". A court sues for peace at "
                       + DiplomacyConstants.ExhaustionSeekPeace.ToString("0") + ".";
            var band = ExhaustionBands.Of(value);
            return "Shown as a band, never a figure: " + ExhaustionBands.Name(band) + " - "
                   + ExhaustionBands.Meaning(band)
                   + " The exact number is what an espionage report buys.";
        }

        private static string TrustHint(Kingdom holder, Kingdom other, float value)
        {
            return "What " + holder.Name + " makes of " + other.Name + "'s word: "
                   + value.ToString("0") + ". Trust is reputation, not feeling: it fades only if"
                   + " nobody tends it - goodwill within two years, a grudge far more slowly - and"
                   + " a war drives it down. Below " + DiplomacyConstants.TrustFloorForPacts.ToString("0")
                   + " they will sign nothing but a truce or the terms that end a war.";
        }

        private static string ClaimHint(Kingdom holder, Kingdom other, Claim best)
        {
            if (best == null)
                return holder.Name + " holds no claim on " + other.Name + ".";
            var text = holder.Name + "'s best: " + best.Type + " at legitimacy "
                       + CasusBelli.Legitimacy(best.Type).ToString("0.00");
            if (best.AllowsFiefDemands)
                text += ". It entitles them to land at the peace table.";
            return text + ".";
        }

        // ----- buttons ---------------------------------------------------------
        // The strip these sit in is narrow: every explanation must fit its own
        // column (~220px, about 32 characters) or it bleeds into the neighbour's.
        // Anything longer lives in the hover hint instead - the numbers there are the
        // same ones the AI uses, so nothing is hidden, only shortened on the surface.

        private void BuildActions(ModState state, Kingdom us, Kingdom them, WarRecord war,
            ICollection<DiplomacyActionVM> into)
        {
            var ourLink = Hegemony.VassalageOf(state, us);

            if (war != null)
            {
                var allowance = PeaceTable.DescribeAllowance(state, war, us);
                var budget = PeaceTable.BudgetFor(war, us);
                into.Add(new DiplomacyActionVM("Negotiate peace",
                    budget <= 0f ? "White peace only - this war has earned nothing yet."
                        : "Budget " + budget.ToString("0") + " - see hint for the price list.",
                    0, true,
                    "Opens the peace table: what this war has earned, and what they will sign. " + allowance,
                    () => DiplomacyMenu.ShowPeace(state, us, them),
                    DiplomacyActionVM.PositiveText));

                // Kneeling is the other way out: a free kingdom submits outright, a vassal's
                // version is a defection to its attacker.
                if (ourLink == null)
                {
                    var can = AiDiplomacy.CanSubmitTo(state, us, them, out var why, out _);
                    into.Add(new DiplomacyActionVM("Kneel to them",
                        can ? "Ends this war as our submission." : "Cannot (see hint).",
                        0, can,
                        can
                            ? "The oath is the peace: the war ends, we keep our ruler and lands,"
                              + " and they owe us protection. Tribute "
                              + DiplomacyConstants.AiDefaultTributePerPeriod + " per period."
                            : why,
                        () => DiplomacyMenu.OfferSubmission(state, us, them),
                        DiplomacyActionVM.DangerText));
                }
                else
                {
                    var can = AiDiplomacy.CanDefectToAttacker(state, us, them, out var why, out _);
                    into.Add(new DiplomacyActionVM("Beg their mercy",
                        can ? "Ends this war as our defection." : "Cannot (see hint).",
                        0, can,
                        can
                            ? "We abandon " + ourLink.DominantParty.Name + ", which would not"
                              + " defend us, and kneel to our attacker - they are named the"
                              + " oathbreaker in every court."
                            : why,
                        () => DiplomacyMenu.DefectToAttacker(state, us, them),
                        DiplomacyActionVM.DangerText));
                }
            }
            else
            {
                // Pacts live in the chooser, but the mockup's action strip carries the
                // button that opens it - "Propose a treaty" toggles the chooser closed
                // and open, and says why when the top rung already binds us.
                var vassalage = state.ActiveTreatyBetween(us, them, TreatyType.Vassalage);
                var alliance = state.ActiveTreatyBetween(us, them, TreatyType.Alliance);
                if (vassalage != null)
                {
                    into.Add(new DiplomacyActionVM("Propose a treaty",
                        "A vassalage already binds the two of you.",
                        0, string.Empty, false,
                        "Nothing stands above a vassalage except its release. The chooser stays shut.",
                        () => { }, DiplomacyActionVM.BlockedText));
                }
                else if (alliance != null)
                {
                    into.Add(new DiplomacyActionVM("Propose a treaty",
                        "An alliance already stands - the top rung.",
                        0, string.Empty, false,
                        "Nothing stands above an alliance. The chooser stays shut.",
                        () => { }, DiplomacyActionVM.BlockedText));
                }
                else
                {
                    var lo = DiplomacyConstants.TreatyInfluenceCost(TreatyType.NonAggressionPact);
                    var hi = DiplomacyConstants.TreatyInfluenceCost(TreatyType.Alliance);
                    into.Add(new DiplomacyActionVM("Propose a treaty",
                        "Three rungs, one valuation. See what clears their bar.",
                        0, lo + " - " + hi, true,
                        "Opens \"what their court would sign\": their own valuation against the"
                        + " three rungs it could buy, each with its influence price.",
                        () => DiShowPacts = !DiShowPacts, DiplomacyActionVM.GoldText));
                }

                var block = TreatyEnforcement.WhyWarBlocked(state, us, them);
                into.Add(new DiplomacyActionVM("Declare war",
                    block == TreatyEnforcement.Block.None
                        ? "Puts it to the court's vote."
                        : "Blocked (see hint).",
                    0, block == TreatyEnforcement.Block.None,
                    block == TreatyEnforcement.Block.None
                        ? "Proposes a war decision the realm votes on - the same thing the"
                          + " Decisions tab offers. Our treaties are why it may be blocked."
                        : TreatyEnforcement.Explain(state, us, them, block) + ".",
                    () => DiplomacyMenu.DeclareWar(state, us, them),
                    DiplomacyActionVM.DangerText));

                var canTribute = AiDiplomacy.CanDemandTribute(state, us, them, out var whyTribute);
                into.Add(new DiplomacyActionVM("Demand tribute",
                    canTribute
                        ? DiplomacyConstants.AiDefaultTributePerPeriod + " per period."
                        : "Cannot (see hint).",
                    0, canTribute,
                    canTribute
                        ? "Coercion, not negotiation: our claim makes the pretext and our"
                          + " strength makes the argument - the same demand the AI makes."
                        : whyTribute,
                    () => DiplomacyMenu.DemandTribute(state, us, them)));

                if (ourLink == null)
                {
                    var can = AiDiplomacy.CanSubmitTo(state, us, them, out var why, out _);
                    into.Add(new DiplomacyActionVM("Kneel to them",
                        can ? "We become their vassal." : "Cannot (see hint).",
                        0, can,
                        can
                            ? "Their oath for our foreign policy: tribute "
                              + DiplomacyConstants.AiDefaultTributePerPeriod
                              + " per period, troops in their wars, protection owed to us."
                            : why,
                        () => DiplomacyMenu.OfferSubmission(state, us, them),
                        DiplomacyActionVM.DangerText));
                }

                // Courting somebody else's neglected vassal means war with its patron.
                var theirLink = Hegemony.VassalageOf(state, them);
                if (theirLink != null && theirLink.DominantParty != us)
                {
                    var can = Hegemony.CanPoach(state, us, theirLink, out var value, out var why);
                    into.Add(new DiplomacyActionVM("Court them",
                        can ? "Valued at " + value.ToString("0") + "." : "Cannot (see hint).",
                        0, can,
                        can
                            ? "They leave " + theirLink.DominantParty.Name + " and kneel to us -"
                              + " which means war with " + theirLink.DominantParty.Name + "."
                            : why,
                        () => DiplomacyMenu.CourtVassal(state, us, them),
                        DiplomacyActionVM.DangerText));
                }

                // A greedy patron may tear up a vassal's oath and take its lands.
                if (Hegemony.CouldAnnex(state, us, them))
                {
                    into.Add(new DiplomacyActionVM("Tear up their oath",
                        "Breach, then conquest.", 0, true,
                        "We break " + them.Name + "'s oath at the full price and take their"
                        + " lands - the move a greedy patron makes. Every other vassal we"
                        + " hold takes the lesson in hold.",
                        () => DiplomacyMenu.AnnexVassal(state, us, them),
                        DiplomacyActionVM.DangerText));
                }
            }

            // Independence is the one foreign-policy act a vassal keeps for itself,
            // so it shows on the patron's row in war or in peace.
            if (ourLink != null && ourLink.DominantParty == them)
            {
                var breaking = Hegemony.IsAtBreakingPoint(state, ourLink);
                into.Add(new DiplomacyActionVM("Declare independence",
                    breaking ? "A war of independence." : "Not breaking yet (see hint).",
                    0, breaking,
                    breaking
                        ? "Hold " + Hegemony.HoldOf(ourLink).ToString("0")
                          + ", below the breaking point of "
                          + Hegemony.SecessionThreshold(state, ourLink).ToString("0")
                          + ". We renounce the oath and fight - their other resentful vassals"
                          + " may rise with us."
                        : "Hold " + Hegemony.HoldOf(ourLink).ToString("0")
                          + " against a secession line of "
                          + Hegemony.SecessionThreshold(state, ourLink).ToString("0")
                          + ". Renouncing the oath is always possible; a war of independence"
                          + " needs a realm already breaking.",
                    () => DiplomacyMenu.SecedeFromPatron(state, us, them),
                    DiplomacyActionVM.DangerText));
            }

            var breakable = DiplomacyMenu.FirstBreakableTreaty(state, us, them);
            if (breakable != null)
            {
                // "Release X" when it is our vassal going free, "Renounce X" for a pact
                // torn up between equals - the mockup names both cases differently.
                var ours = breakable.Type == TreatyType.Vassalage && breakable.DominantParty == us;
                into.Add(new DiplomacyActionVM(
                    ours ? "Release " + them.Name : "Renounce " + breakable.Type,
                    ours ? "Ends the vassalage. Costs trust with every court."
                        : "Breaks the pact. "
                          + DiplomacyConstants.TrustTreatyBrokenVictim.ToString("0")
                          + " trust with them, "
                          + DiplomacyConstants.TrustTreatyBrokenObserver.ToString("0")
                          + " with every other court.",
                    0, true,
                    "-" + (-DiplomacyConstants.TrustTreatyBrokenVictim).ToString("0")
                    + " trust with them, -"
                    + (-DiplomacyConstants.TrustTreatyBrokenObserver).ToString("0")
                    + " with every other court. Always possible, never free: breaking a treaty "
                    + "on purpose is a story beat, not an accident. They gain a reason for war.",
                    () => DiplomacyMenu.BreakTreaty(state, us, them),
                    DiplomacyActionVM.DangerText));
            }

            into.Add(new DiplomacyActionVM("Fabricate a claim",
                DiplomacyConstants.FabricateClaimDurationDays + " days and "
                + DiplomacyConstants.FabricateClaimGoldCost + " denars, "
                + (DiplomacyConstants.FabricateClaimExposureChance * 100f).ToString("0")
                + "% chance of exposure.",
                DiplomacyConstants.FabricateClaimInfluenceCost, true,
                "A " + (DiplomacyConstants.FabricateClaimExposureChance * 100f).ToString("0")
                + "% chance of being caught, which damages relations with every court and hands "
                + "the target a claim of their own.",
                () => DiplomacyMenu.ShowFabricationTargets(state, us, them)));
        }

        // ----- helpers ---------------------------------------------------------

        private static string ColourOf(IFaction faction)
            => faction == null ? "#FFFFFFFF" : Color.FromUint(faction.Color).ToString();

        private static int Count(IEnumerable<Claim> claims)
        {
            var n = 0;
            foreach (var unused in claims) n++;
            return n;
        }
    }

    /// <summary>
    /// The mod's reading of a war, on the row and in the detail pane. Hooked on the
    /// method that rebuilds the rows, which is also what selection calls.
    /// </summary>
    [ViewModelMixin("UpdateDiplomacyProperties")]
    internal sealed class WarItemMixin : DiplomacyItemMixinBase<KingdomWarItemVM>
    {
        public WarItemMixin(KingdomWarItemVM vm) : base(vm) { }
    }

    /// <summary>
    /// The same, for a kingdom we are at peace with. This type does not override
    /// <c>RefreshValues</c>, which is why that hook never fired here.
    /// </summary>
    [ViewModelMixin("UpdateDiplomacyProperties")]
    internal sealed class TruceItemMixin : DiplomacyItemMixinBase<KingdomTruceItemVM>
    {
        public TruceItemMixin(KingdomTruceItemVM vm) : base(vm) { }
    }
}
