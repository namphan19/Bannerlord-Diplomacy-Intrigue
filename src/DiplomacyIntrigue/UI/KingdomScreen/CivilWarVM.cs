using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// The Court tab while the player's kingdom is at war with itself (design 07 §6, Phase 2.6c;
    /// the "Civil war" row of the court canvas). Both sides, how it ends, the court split by
    /// side with each house's price, and the two acts: buying a house, and conceding.
    ///
    /// **Every figure is read, not computed here.** Exhaustion and captivity come off the
    /// <see cref="InternalWar"/> record the daily tick advances; every price and whether it can
    /// be paid comes from <see cref="SideChange.QuoteFor"/> and <see cref="SideChange.AiWouldPay"/>,
    /// the same calls the weekly AI makes. CLAUDE.md §3: a number shown is the number the AI used.
    ///
    /// Rebuilt whole by <see cref="DiCourtVM"/> each time the tab is composed; only the
    /// selection and the concede button's two-step state change within one life.
    /// </summary>
    internal sealed class DiCivilWarVM : ViewModel
    {
        private readonly ModState _state;
        private readonly InternalWar _war;
        private readonly Action _onChanged;

        private DiCivilHouseVM _selected;
        private SideChange.Quote _quote;
        private bool _concedeArmed;

        private string _selectedTitle = string.Empty;
        private string _priceText = string.Empty;
        private string _purseText = string.Empty;
        private bool _actionVisible;
        private bool _actionEnabled;
        private string _actionText = string.Empty;
        private string _actionNote = string.Empty;
        private string _concedeButtonText = string.Empty;
        private MBBindingList<DiCivilPriceLineVM> _priceLines = new MBBindingList<DiCivilPriceLineVM>();

        public DiCivilWarVM(ModState state, InternalWar war, Action onChanged)
        {
            _state = state;
            _war = war;
            _onChanged = onChanged;
            Compose();
        }

        // ----- the war band ------------------------------------------------------

        [DataSourceProperty] public string DayText { get; private set; } = string.Empty;

        [DataSourceProperty] public string CrownSideNote { get; private set; } = string.Empty;
        [DataSourceProperty] public string CrownLedBy { get; private set; } = string.Empty;
        [DataSourceProperty] public string CrownCounts { get; private set; } = string.Empty;
        [DataSourceProperty] public string CrownExhaustionText { get; private set; } = string.Empty;
        [DataSourceProperty] public int CrownExhaustionAmount { get; private set; }
        [DataSourceProperty] public Color CrownExhaustionColor { get; private set; }
        [DataSourceProperty] public string CrownHeldText { get; private set; } = string.Empty;

        [DataSourceProperty] public string RisingTitle { get; private set; } = string.Empty;
        [DataSourceProperty] public string RisingSideNote { get; private set; } = string.Empty;
        [DataSourceProperty] public string RisingLedBy { get; private set; } = string.Empty;
        [DataSourceProperty] public string RisingCounts { get; private set; } = string.Empty;
        [DataSourceProperty] public string RisingExhaustionText { get; private set; } = string.Empty;
        [DataSourceProperty] public int RisingExhaustionAmount { get; private set; }
        [DataSourceProperty] public Color RisingExhaustionColor { get; private set; }
        [DataSourceProperty] public string RisingHeldText { get; private set; } = string.Empty;

        [DataSourceProperty] public string CrownLosesWhen { get; private set; } = string.Empty;
        [DataSourceProperty] public string CrownLosesResult { get; private set; } = string.Empty;
        [DataSourceProperty] public string RisingLosesWhen { get; private set; } = string.Empty;
        [DataSourceProperty] public string RisingLosesResult { get; private set; } = string.Empty;
        [DataSourceProperty] public string StalemateWhen { get; private set; } = string.Empty;

        // ----- the court divided --------------------------------------------------

        [DataSourceProperty] public string CrownHeading { get; private set; } = string.Empty;
        [DataSourceProperty] public string RisingHeading { get; private set; } = string.Empty;
        [DataSourceProperty] public MBBindingList<DiCivilHouseVM> CrownHouses { get; } = new MBBindingList<DiCivilHouseVM>();
        [DataSourceProperty] public MBBindingList<DiCivilHouseVM> RisingHouses { get; } = new MBBindingList<DiCivilHouseVM>();

        // ----- the selected house -------------------------------------------------

        [DataSourceProperty]
        public string SelectedTitle { get => _selectedTitle; private set => SetField(ref _selectedTitle, value, nameof(SelectedTitle)); }

        [DataSourceProperty]
        public string PriceText { get => _priceText; private set => SetField(ref _priceText, value, nameof(PriceText)); }

        [DataSourceProperty]
        public string PurseText { get => _purseText; private set => SetField(ref _purseText, value, nameof(PurseText)); }

        [DataSourceProperty]
        public MBBindingList<DiCivilPriceLineVM> PriceLines { get => _priceLines; private set => SetField(ref _priceLines, value, nameof(PriceLines)); }

        [DataSourceProperty]
        public bool ActionVisible { get => _actionVisible; private set => SetField(ref _actionVisible, value, nameof(ActionVisible)); }

        [DataSourceProperty]
        public bool ActionEnabled { get => _actionEnabled; private set => SetField(ref _actionEnabled, value, nameof(ActionEnabled)); }

        [DataSourceProperty]
        public string ActionText { get => _actionText; private set => SetField(ref _actionText, value, nameof(ActionText)); }

        [DataSourceProperty]
        public string ActionNote { get => _actionNote; private set => SetField(ref _actionNote, value, nameof(ActionNote)); }

        // ----- conceding ------------------------------------------------------------

        [DataSourceProperty] public bool CanConcede { get; private set; }
        [DataSourceProperty] public string ConcedeBody { get; private set; } = string.Empty;
        [DataSourceProperty] public string ConcedeRuleNote { get; private set; } = string.Empty;

        [DataSourceProperty]
        public string ConcedeButtonText { get => _concedeButtonText; private set => SetField(ref _concedeButtonText, value, nameof(ConcedeButtonText)); }

        // ----- commands -------------------------------------------------------------

        /// <summary>
        /// The one action button under the price: buy the selected house when the player leads
        /// the side it would join, or take the other leader's offer when it is the player's own.
        /// Both go through <see cref="SideChange.Execute"/>, which re-checks everything.
        /// </summary>
        public void ExecuteSideAction()
        {
            try
            {
                var clan = _selected?.Clan;
                if (clan == null || !_war.IsOngoing) return;

                if (clan == Clan.PlayerClan)
                {
                    // The other leader decides by its own rule, re-asked now.
                    var fresh = SideChange.QuoteFor(_state, _war, clan);
                    if (fresh == null || !fresh.Eligible || !SideChange.AiWouldPay(fresh, out var why))
                    {
                        Log.Notify("The offer no longer stands - " + (fresh?.Eligible == false ? fresh.Reason : WhyNot(fresh)), Colors.Red);
                        _onChanged?.Invoke();
                        return;
                    }
                }

                if (!SideChange.Execute(_state, _war, clan, paid: true, out var failed))
                    Log.Notify("Could not change sides: " + failed, Colors.Red);
                _onChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Changing sides from the Court tab failed.", ex);
            }
        }

        /// <summary>
        /// Two clicks: the first arms the button, the second concedes. Inside the panel rather
        /// than a confirmation inquiry, because the test bridge can click a panel button and
        /// cannot click inside an inquiry (design 07 §6).
        /// </summary>
        public void ExecuteConcede()
        {
            try
            {
                if (!CanConcede || !_war.IsOngoing) return;
                if (!_concedeArmed)
                {
                    _concedeArmed = true;
                    ConcedeButtonText = "Click again to concede";
                    return;
                }

                var rising = InternalWars.LeaderOf(_war, true) == Hero.MainHero;
                if (!InternalWars.Concede(_state, _war, rising, out var failed))
                    Log.Notify("Could not concede: " + failed, Colors.Red);
                _onChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Conceding from the Court tab failed.", ex);
            }
        }

        /// <summary>A row was clicked. Called by the row, never by Gauntlet directly.</summary>
        internal void Select(DiCivilHouseVM row)
        {
            try
            {
                for (var i = 0; i < CrownHouses.Count; i++) CrownHouses[i].IsSelected = CrownHouses[i] == row;
                for (var i = 0; i < RisingHouses.Count; i++) RisingHouses[i].IsSelected = RisingHouses[i] == row;
                _selected = row;
                ComposeSelection();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Selecting a house in the civil war failed.", ex);
            }
        }

        /// <summary>Selects a house by name, as a row click would. Test hook, like the court's.</summary>
        internal string SelectByName(string name)
        {
            foreach (var list in new[] { CrownHouses, RisingHouses })
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Clan.Name.ToString() != name) continue;
                    Select(list[i]);
                    return "Selected " + name + ": " + PriceText + (ActionVisible ? " [" + ActionText + (ActionEnabled ? "" : ", disabled") + "]" : "")
                           + (string.IsNullOrEmpty(ActionNote) ? "" : " - " + ActionNote);
                }
            return "No house named \"" + name + "\" in the civil war.";
        }

        // ----- composing --------------------------------------------------------------

        private void Compose()
        {
            var kingdom = _war.Kingdom;
            var ruler = kingdom.Leader;
            var claimant = _war.Claimant;
            var playerRebel = _war.IsRebel(Clan.PlayerClan);
            var playerLeadsCrown = ruler != null && ruler == Hero.MainHero;
            var playerLeadsRising = claimant != null && claimant == Hero.MainHero;
            var claimantName = claimant == null ? "The claimant" : claimant.Name.ToString();

            DayText = "CIVIL WAR, DAY " + ((int)_war.StartedOn.ElapsedDaysUntilNow).ToString();

            // The two sides, from the same membership the war itself reads.
            var crown = new List<Clan>();
            var rising = new List<Clan>();
            foreach (var clan in Court.MembersOf(kingdom))
                (_war.IsRebel(clan) ? rising : crown).Add(clan);
            crown.Sort((a, b) => b.Influence.CompareTo(a.Influence));
            rising.Sort((a, b) => b.Influence.CompareTo(a.Influence));

            var crownShare = (int)Math.Round(_war.CrownShareAtStart * 100f);
            CrownSideNote = playerRebel ? string.Empty : "your side";
            CrownLedBy = playerLeadsCrown ? "led by you" : "led by " + (ruler == null ? "nobody" : ruler.Name.ToString());
            CrownCounts = Counts(crown, crownShare);
            CrownExhaustionText = _war.CrownExhaustion.ToString("0.0");
            CrownExhaustionAmount = (int)Math.Round(_war.CrownExhaustion);
            CrownExhaustionColor = ExhaustionColor(_war.CrownExhaustion);
            CrownHeldText = HeldText(ruler, _war.RulerCaptiveDays);

            RisingTitle = SideChange.SideName(_war, true).ToUpperInvariant();
            RisingSideNote = playerRebel ? "your side" : string.Empty;
            RisingLedBy = playerLeadsRising ? "led by you" : "Clan " + _war.Banner?.Name + ", for " + claimantName;
            RisingCounts = Counts(rising, 100 - crownShare);
            RisingExhaustionText = _war.RebelExhaustion.ToString("0.0");
            RisingExhaustionAmount = (int)Math.Round(_war.RebelExhaustion);
            RisingExhaustionColor = ExhaustionColor(_war.RebelExhaustion);
            RisingHeldText = HeldText(claimant, _war.ClaimantCaptiveDays);

            var collapse = IntrigueConstants.InternalWarCollapseExhaustion.ToString("0");
            var days = IntrigueConstants.InternalWarCaptiveDays;
            CrownLosesWhen = "The crown's side reaches " + collapse + ", "
                             + (playerLeadsCrown ? "you are" : (ruler == null ? "the ruler" : ruler.Name.ToString()) + " is")
                             + " held " + days + " days, or " + (playerLeadsCrown ? "you concede" : "concedes");
            CrownLosesResult = (playerLeadsRising ? "You take" : claimantName + " takes") + " the throne";
            RisingLosesWhen = "The rising reaches " + collapse + ", "
                              + (playerLeadsRising ? "you are" : claimantName + " is") + " held " + days
                              + " days or dies, or " + (playerLeadsRising ? "you concede" : "concedes");
            RisingLosesResult = playerLeadsRising ? "Your claim is retired" : claimantName + "'s claim is retired";
            StalemateWhen = "Both sides past " + IntrigueConstants.InternalWarStalemateExhaustion.ToString("0")
                            + " - the mark on each bar - and nothing moves";

            CrownHeading = "WITH THE CROWN - what " + claimantName + " would pay to take them";
            RisingHeading = "WITH THE RISING - what " + (ruler == null ? "the crown" : ruler.Name.ToString()) + " would pay to win them back";

            for (var i = 0; i < crown.Count; i++) CrownHouses.Add(Row(crown[i]));
            for (var i = 0; i < rising.Count; i++) RisingHouses.Add(Row(rising[i]));

            // Conceding: the two leaders only.
            CanConcede = playerLeadsCrown || playerLeadsRising;
            if (playerLeadsCrown)
            {
                var keeps = _war.CrownShareAtStart >= IntrigueConstants.SuccessionPretenderShare;
                ConcedeBody = "Ends it now, exactly as if your side had reached " + collapse + ": " + claimantName
                              + "'s house takes the throne. Your house stays at court, and your side held "
                              + crownShare + "% of it at the start - "
                              + (keeps ? "enough to keep a claim of your own alive." : "too little to keep a claim of your own.");
                ConcedeButtonText = "Concede the throne to " + claimantName;
            }
            else if (playerLeadsRising)
            {
                ConcedeBody = "Ends it now, exactly as if your side had reached " + collapse + ": your claim is retired and the crown's"
                              + " standing rises by " + IntrigueConstants.LegitimacyWonJustWar.ToString("0")
                              + ". Your house keeps its seat and what the war has left it.";
                ConcedeButtonText = "Give up your claim";
            }
            ConcedeRuleNote = "The other side concedes on its own once it passes "
                              + IntrigueConstants.InternalWarConcedeExhaustion.ToString("0") + " while yours is under "
                              + IntrigueConstants.InternalWarConcedeOtherBelow.ToString("0") + ".";

            // A house that is not a leader starts on itself: its own choice is the one it has.
            // A leader starts on the first house it could buy.
            DiCivilHouseVM first = null;
            var mine = playerLeadsCrown ? RisingHouses : playerLeadsRising ? CrownHouses : null;
            if (mine == null)
            {
                foreach (var list in new[] { CrownHouses, RisingHouses })
                    for (var i = 0; i < list.Count && first == null; i++)
                        if (list[i].Clan == Clan.PlayerClan) first = list[i];
            }
            else if (mine.Count > 0) first = mine[0];
            if (first == null) first = CrownHouses.Count > 0 ? CrownHouses[0] : RisingHouses.Count > 0 ? RisingHouses[0] : null;
            if (first != null) Select(first);
        }

        private DiCivilHouseVM Row(Clan clan)
        {
            var q = SideChange.QuoteFor(_state, _war, clan);
            var loyalty = LoyaltyModel.Of(_state, clan);

            string note;
            var noteColor = DiCourtVM.MutedColor;
            if (clan == _war.Kingdom.RulingClan) note = "the crown's own house";
            else if (clan == _war.Banner) note = "the claimant's house";
            else if (q == null || !q.Eligible)
                note = q != null && _war.HasChangedSides(clan) ? "changed sides" : "cannot change now";
            else if (q.Buyer != Hero.MainHero && SideChange.AiWouldPay(q, out _))
            {
                note = q.Buyer.Name + " would pay";
                // A house the other leader would buy from our own side is a threat, not a fact.
                var ours = _war.IsRebel(Clan.PlayerClan) == _war.IsRebel(clan);
                noteColor = ours ? DiCourtVM.DefectionColor : DiCourtVM.MutedColor;
            }
            else note = string.Empty;

            return new DiCivilHouseVM(this, clan, clan == Clan.PlayerClan,
                clan.Fiefs.Count.ToString(),
                loyalty.ToString("0.0"), DiCourtVM.BandColor(LoyaltyModel.Band(loyalty)),
                q != null && q.Eligible ? q.Price.ToString("N0") : "-",
                note, noteColor);
        }

        private void ComposeSelection()
        {
            _concedeArmed = false;
            if (CanConcede)
                ConcedeButtonText = InternalWars.LeaderOf(_war, false) == Hero.MainHero
                    ? "Concede the throne to " + (_war.Claimant == null ? "the claimant" : _war.Claimant.Name.ToString())
                    : "Give up your claim";

            var lines = new MBBindingList<DiCivilPriceLineVM>();
            var clan = _selected?.Clan;
            _quote = clan == null ? null : SideChange.QuoteFor(_state, _war, clan);
            var q = _quote;

            ActionVisible = false;
            ActionEnabled = false;
            ActionText = string.Empty;
            ActionNote = string.Empty;

            if (q == null)
            {
                SelectedTitle = string.Empty;
                PriceText = string.Empty;
                PurseText = string.Empty;
                PriceLines = lines;
                return;
            }

            // The two leaders' own houses never change sides, so they have no price to show -
            // a figure there would read as an offer that cannot be made.
            if (clan == _war.Kingdom.RulingClan || clan == _war.Banner)
            {
                SelectedTitle = clan.Name.ToString().ToUpperInvariant();
                PriceText = "-";
                PurseText = string.Empty;
                PriceLines = lines;
                ActionNote = q.Reason;
                return;
            }

            for (var i = 0; i < q.Lines.Count; i++)
                lines.Add(new DiCivilPriceLineVM(q.Lines[i].Key, q.Lines[i].Value, i < q.BaseLineCount));
            PriceLines = lines;
            PriceText = q.Price.ToString("N0");

            var buyer = q.Buyer;
            var side = SideChange.SideName(_war, q.ToRising);
            PurseText = buyer == null ? string.Empty
                : buyer == Hero.MainHero ? "you hold " + buyer.Gold.ToString("N0")
                : buyer.Name + " holds " + buyer.Gold.ToString("N0");

            if (buyer == Hero.MainHero)
            {
                SelectedTitle = (q.ToRising ? "WIN OVER " : "WIN BACK ") + clan.Name.ToString().ToUpperInvariant();
                ActionVisible = true;
                ActionText = "Pay " + q.Price.ToString("N0") + " - they come over to " + side;
                if (!q.Eligible) ActionNote = q.Reason;
                else if (buyer.Gold < q.Price) ActionNote = "You hold only " + buyer.Gold.ToString("N0") + ".";
                else
                {
                    ActionEnabled = true;
                    ActionNote = WalkedOutOnNote(q);
                }
            }
            else if (clan == Clan.PlayerClan)
            {
                SelectedTitle = "YOUR HOUSE";
                ActionVisible = true;
                ActionText = "Go over to " + side + " - receive " + q.Price.ToString("N0");
                if (!q.Eligible) ActionNote = q.Reason;
                else if (!SideChange.AiWouldPay(q, out var why)) ActionNote = why;
                else
                {
                    ActionEnabled = true;
                    ActionNote = WalkedOutOnNote(q);
                }
            }
            else
            {
                SelectedTitle = clan.Name.ToString().ToUpperInvariant();
                ActionNote = !q.Eligible
                    ? q.Reason
                    : buyer == null ? string.Empty
                    : SideChange.AiWouldPay(q, out var why)
                        ? buyer.Name + " would pay this to take them to " + side + "."
                        : "Not bought this week: " + why;
            }
        }

        private string WalkedOutOnNote(SideChange.Quote q)
        {
            var left = InternalWars.LeaderOf(_war, !q.ToRising);
            return (left == null ? "The leader they leave" : left.Name.ToString()) + "'s opinion of their head falls by "
                   + (-IntrigueConstants.SideChangeRelationPenalty) + ". They cannot change sides again in this war.";
        }

        private static string WhyNot(SideChange.Quote q)
        {
            if (q == null) return "no house to price.";
            SideChange.AiWouldPay(q, out var why);
            return why ?? "no reason given.";
        }

        private static string Counts(List<Clan> clans, int sharePercent)
        {
            var fiefs = 0;
            for (var i = 0; i < clans.Count; i++) fiefs += clans[i].Fiefs.Count;
            return clans.Count + (clans.Count == 1 ? " clan   " : " clans   ") + fiefs + (fiefs == 1 ? " fief   " : " fiefs   ")
                   + sharePercent + "% of the court at the start";
        }

        private static string HeldText(Hero leader, int daysHeld)
        {
            var who = leader == null ? "Nobody" : leader == Hero.MainHero ? "You" : leader.Name.ToString();
            var verb = leader == Hero.MainHero ? " are " : " is ";
            var limit = IntrigueConstants.InternalWarCaptiveDays;
            return daysHeld > 0
                ? who + verb + "held - " + daysHeld + " of " + limit + " days"
                : who + verb + "free - 0 of " + limit + " days held";
        }

        /// <summary>Gold under the stalemate line, orange past it, red once a concession is in reach.</summary>
        private static Color ExhaustionColor(float exhaustion)
        {
            if (exhaustion >= IntrigueConstants.InternalWarConcedeExhaustion) return DiCourtVM.DefectionColor;
            if (exhaustion >= IntrigueConstants.InternalWarStalemateExhaustion) return DiCourtVM.DisaffectedColor;
            return DiCourtVM.TransactionalColor;
        }
    }

    /// <summary>One house in the court divided.</summary>
    internal sealed class DiCivilHouseVM : ViewModel
    {
        private readonly DiCivilWarVM _owner;
        private bool _isSelected;

        public DiCivilHouseVM(DiCivilWarVM owner, Clan clan, bool isPlayer, string fiefsText,
            string loyaltyText, Color loyaltyColor, string priceText, string noteText, Color noteColor)
        {
            _owner = owner;
            Clan = clan;
            Name = clan.Name.ToString() + (isPlayer ? " (you)" : string.Empty);
            FiefsText = fiefsText;
            LoyaltyText = loyaltyText;
            LoyaltyColor = loyaltyColor;
            PriceText = priceText;
            NoteText = noteText;
            NoteColor = noteColor;
        }

        internal Clan Clan { get; }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string FiefsText { get; }
        [DataSourceProperty] public string LoyaltyText { get; }
        [DataSourceProperty] public Color LoyaltyColor { get; }
        [DataSourceProperty] public string PriceText { get; }
        [DataSourceProperty] public string NoteText { get; }
        [DataSourceProperty] public Color NoteColor { get; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value == _isSelected) return; _isSelected = value; OnPropertyChangedWithValue(value, nameof(IsSelected)); }
        }

        /// <summary>Row click. Named OnSelect to match vanilla's clan tuple, as the court's rows are.</summary>
        public void OnSelect()
        {
            try
            {
                _owner?.Select(this);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Selecting a house failed.", ex);
            }
        }
    }

    /// <summary>One line of a house's price: a base part, or the gold a factor adds or removes.</summary>
    internal sealed class DiCivilPriceLineVM : ViewModel
    {
        public DiCivilPriceLineVM(string label, int gold, bool isBase)
        {
            Label = label;
            ValueText = isBase ? gold.ToString("N0") : gold.ToString("+#,0;-#,0;0");
            // A base part is just what the house is. A factor that raises the price is against
            // whoever pays, one that lowers it is for them.
            ValueColor = isBase || gold == 0 ? DiCourtVM.TextColor
                : gold > 0 ? DiCourtVM.DisaffectedColor : DiCourtVM.ReliableColor;
        }

        [DataSourceProperty] public string Label { get; }
        [DataSourceProperty] public string ValueText { get; }
        [DataSourceProperty] public Color ValueColor { get; }
    }
}
