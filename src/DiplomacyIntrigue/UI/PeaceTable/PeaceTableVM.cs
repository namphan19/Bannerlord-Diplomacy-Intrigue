using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.Negotiation
{
    /// <summary>
    /// The peace table - the one Gauntlet screen this project owns (the mockup's board
    /// 3a, and board 3b as its read-only half).
    ///
    /// Three faces of one shell: <b>demand</b> (we are the winner pricing what to take),
    /// <b>offer</b> (we are the loser pricing what to give) and <b>incoming</b> (their
    /// package, read only - "this is their table, not yours"). Every figure on it is
    /// re-read from <see cref="PeaceTable"/> whenever a box is ticked: the budget bar is
    /// <see cref="PeaceTable.BudgetFor"/> against <see cref="PeaceTable.CostOf"/>, the
    /// gold cliff mark is <see cref="PeaceTable.SubjugationCost"/>, row prices and row
    /// availability come from the same two resolvers, conflicting rows untick through
    /// <see cref="PeaceTerms.AreExclusive"/>, and the verdict is the willingness check
    /// for whichever side is not clicking.
    /// </summary>
    internal sealed class PeaceTableVM : ViewModel
    {
        /// <summary>What each button does, supplied by the caller.</summary>
        internal sealed class Callbacks
        {
            public Action<PeaceTerms> OnOffer;
            public Action OnWhitePeace;
            public Action OnAccept;
            public Action OnRefuse;
            public Action OnClose;
        }

        /// <summary>One candidate line, priced and availability-checked before display.</summary>
        private sealed class Spec
        {
            public PeaceTermKind Kind;
            public Settlement Fief;
            public string Name;
            public string Desc;
            public string Price;
            public bool Enabled;
            public string DisabledReason;
            public Action<PeaceTerms> Apply;
        }

        private readonly ModState _state;
        private readonly WarRecord _war;
        private readonly Kingdom _us;
        private readonly Kingdom _them;
        private readonly bool _weAreWinner;
        private readonly PeaceTerms _incoming;
        private readonly Callbacks _cb;
        private readonly List<Spec> _specs = new List<Spec>();
        private MBBindingList<PeaceTermRowVM> _rows = new MBBindingList<PeaceTermRowVM>();

        public PeaceTableVM(ModState state, WarRecord war, Kingdom us, Kingdom them,
            bool weAreWinner, PeaceTerms incoming, Callbacks cb)
        {
            _state = state;
            _war = war;
            _us = us;
            _them = them;
            _weAreWinner = weAreWinner;
            _incoming = incoming;
            _cb = cb;
            Rebuild();
        }

        // ----- header ----------------------------------------------------------

        [DataSourceProperty] public string Title { get; private set; } = string.Empty;
        [DataSourceProperty] public string Subtitle { get; private set; } = string.Empty;
        [DataSourceProperty] public Color OurColor { get; private set; }
        [DataSourceProperty] public Color TheirColor { get; private set; }

        // ----- budget block ----------------------------------------------------
        // Anything RefreshTotals touches after construction needs a notifying setter:
        // a bound value only redraws when the setter raises the change (UI-INTEGRATION
        // §1). The tick-time members are exactly the running budget and the verdict.

        [DataSourceProperty] public string BudgetLabel { get; private set; } = string.Empty;
        [DataSourceProperty] public string BudgetText { get; private set; } = string.Empty;

        [DataSourceProperty]
        public string SpentText
        {
            get => _spentText;
            set
            {
                if (value == _spentText) return;
                _spentText = value;
                OnPropertyChangedWithValue(value, nameof(SpentText));
            }
        }
        private string _spentText = string.Empty;

        [DataSourceProperty]
        public Color SpentColor
        {
            get => _spentColor;
            set
            {
                if (value.Equals(_spentColor)) return;
                _spentColor = value;
                OnPropertyChangedWithValue(value, nameof(SpentColor));
            }
        }
        private Color _spentColor;

        [DataSourceProperty]
        public int FillPercent
        {
            get => _fillPercent;
            set
            {
                if (value == _fillPercent) return;
                _fillPercent = value;
                OnPropertyChangedWithValue(value, nameof(FillPercent));
            }
        }
        private int _fillPercent;

        [DataSourceProperty]
        public string FillColor
        {
            get => _fillColor;
            set
            {
                if (value == _fillColor) return;
                _fillColor = value;
                OnPropertyChangedWithValue(value, nameof(FillColor));
            }
        }
        private string _fillColor = "#4D7F52FF";

        [DataSourceProperty] public bool ShowCliff { get; private set; }
        [DataSourceProperty] public int CliffPercent { get; private set; }
        [DataSourceProperty] public string CliffText { get; private set; } = string.Empty;
        [DataSourceProperty] public string BudgetNote { get; private set; } = string.Empty;

        // ----- terms -----------------------------------------------------------

        [DataSourceProperty] public string TermsHeader { get; private set; } = string.Empty;
        [DataSourceProperty] public string PriceHeader { get; private set; } = string.Empty;
        [DataSourceProperty] public string TableNote { get; private set; } = string.Empty;
        [DataSourceProperty] public bool HasTableNote => !string.IsNullOrEmpty(TableNote);

        [DataSourceProperty]
        public MBBindingList<PeaceTermRowVM> Rows
        {
            get => _rows;
            set
            {
                if (value == _rows) return;
                _rows = value;
                OnPropertyChangedWithValue(value, nameof(Rows));
            }
        }

        // ----- verdict + buttons -----------------------------------------------

        [DataSourceProperty]
        public string VerdictText
        {
            get => _verdictText;
            set
            {
                if (value == _verdictText) return;
                _verdictText = value;
                OnPropertyChangedWithValue(value, nameof(VerdictText));
            }
        }
        private string _verdictText = string.Empty;

        [DataSourceProperty]
        public Color VerdictColor
        {
            get => _verdictColor;
            set
            {
                if (value.Equals(_verdictColor)) return;
                _verdictColor = value;
                OnPropertyChangedWithValue(value, nameof(VerdictColor));
            }
        }
        private Color _verdictColor;

        [DataSourceProperty]
        public string VerdictWhy
        {
            get => _verdictWhy;
            set
            {
                if (value == _verdictWhy) return;
                _verdictWhy = value;
                OnPropertyChangedWithValue(value, nameof(VerdictWhy));
            }
        }
        private string _verdictWhy = string.Empty;

        [DataSourceProperty] public bool ShowOfferButtons { get; private set; }
        [DataSourceProperty] public bool ShowAnswerButtons { get; private set; }
        [DataSourceProperty] public string OfferText { get; private set; } = string.Empty;
        [DataSourceProperty] public string AcceptText { get; private set; } = string.Empty;

        [DataSourceProperty]
        public bool OfferEnabled
        {
            get => _offerEnabled;
            set
            {
                if (value == _offerEnabled) return;
                _offerEnabled = value;
                OnPropertyChangedWithValue(value, nameof(OfferEnabled));
            }
        }
        private bool _offerEnabled;

        [DataSourceProperty] public Color OfferColor { get; private set; }

        // ----- buttons ---------------------------------------------------------

        public void ExecuteWhitePeace()
        {
            try { _cb?.OnWhitePeace?.Invoke(); }
            catch (Exception ex) { Log.Error("UI", "White peace failed.", ex); }
            finally { Close(); }
        }

        public void ExecuteOffer()
        {
            try { _cb?.OnOffer?.Invoke(Selection()); }
            catch (Exception ex) { Log.Error("UI", "Offering terms failed.", ex); }
            finally { Close(); }
        }

        public void ExecuteAccept()
        {
            try { _cb?.OnAccept?.Invoke(); }
            catch (Exception ex) { Log.Error("UI", "Accepting the offer failed.", ex); }
            finally { Close(); }
        }

        public void ExecuteRefuse()
        {
            try { _cb?.OnRefuse?.Invoke(); }
            catch (Exception ex) { Log.Error("UI", "Refusing the offer failed.", ex); }
            finally { Close(); }
        }

        public void ExecuteCancel() => Close();

        private void Close()
        {
            try { _cb?.OnClose?.Invoke(); }
            catch (Exception ex) { Log.Error("UI", "Closing the peace table failed.", ex); }
        }

        // ----- composition -----------------------------------------------------

        private void Rebuild()
        {
            OurColor = Color.FromUint(_us.Color);
            TheirColor = Color.FromUint(_them.Color);

            var war = _war;
            // Wars the mod did not start have no claim on record; the Diplomacy tab's
            // headline words it the same way rather than printing "over None".
            var story = (war.Justification == CasusBelliType.None
                            ? "War with no claim on record"
                            : "War over " + war.Justification)
                        + "   -   " + war.DaysElapsed.ToString("0")
                        + (war.DaysElapsed.ToString("0") == "1" ? " day" : " days")
                        + (war.IsObligationWar && war.CalledBy != null
                            ? "   -   called in by " + war.CalledBy.Name : "");

            if (_incoming != null)
            {
                // Two different offers arrive through this face: a losing court buying
                // its peace with concessions, and a winning court naming the price of
                // one. The title says which, because every line below reads differently.
                Title = _weAreWinner
                    ? _them.Name + " asks for peace"
                    : _them.Name + " names its price for peace";
                Subtitle = story + "   -   your own condition: "
                           + ExhaustionBands.Condition(war.ExhaustionOf(_us));
                BuildIncoming();
            }
            else
            {
                Title = (_weAreWinner ? "Peace with " : "Sue for peace with ") + _them.Name;
                Subtitle = story + "   -   their condition: "
                           + ExhaustionBands.Condition(war.ExhaustionOf(_them));
                BuildEditable();
            }
        }

        private void BuildEditable()
        {
            ShowOfferButtons = true;
            ShowAnswerButtons = false;
            OfferText = "Offer these terms";
            OfferColor = PeaceTermRowVM.OnNameColor;

            var winner = _weAreWinner ? _us : _them;
            var loser = _weAreWinner ? _them : _us;
            var budget = PeaceTable.BudgetFor(_war, winner);

            BudgetLabel = _weAreWinner ? "What this war has earned" : "What this war has earned them";
            BudgetText = budget.ToString("0");
            TermsHeader = _weAreWinner ? "What you demand" : "What you offer";
            PriceHeader = _weAreWinner ? "price" : "worth";
            BudgetNote = "Above the cliff a winner asks for standing, not coin. Below it,"
                         + " only for what coin can buy."
                         // Design 08 S-2: the budget is the score as the envoys argued it.
                         + (budget > 0f && Statecraft.StatecraftModel.Enabled
                             ? " " + Statecraft.StatecraftTerms.NegotiationLine(winner, loser)
                               + " (war score " + _war.ScoreFor(winner).ToString("0") + ")."
                             : "");

            var cliff = PeaceTable.SubjugationCost;
            ShowCliff = budget >= cliff;
            CliffPercent = budget <= 0f ? 0 : (int)Math.Min(100f, cliff / budget * 100f);
            CliffText = "the cliff   -   " + cliff.ToString("0");

            _specs.Clear();
            foreach (var spec in Catalogue(winner, loser)) _specs.Add(spec);

            var rows = new MBBindingList<PeaceTermRowVM>();
            foreach (var spec in _specs)
            {
                var captured = spec;
                rows.Add(new PeaceTermRowVM(spec.Kind, spec.Fief, spec.Name, spec.Desc,
                    spec.Price, spec.Enabled, spec.DisabledReason,
                    () => Toggle(captured)));
            }
            Rows = rows;

            RefreshTotals();
        }

        private void BuildIncoming()
        {
            ShowOfferButtons = false;
            ShowAnswerButtons = true;
            AcceptText = _incoming.IsWhitePeace ? "Make peace" : "Accept these terms";
            OfferColor = PeaceTermRowVM.OnNameColor;

            // _weAreWinner here means the offer is a loser's concession to us (the
            // mockup's board 3b); otherwise it is a winner's demand of us. The package is
            // priced the same way either way - CostOf against the winner's BudgetFor - but
            // what it means to the reader is opposite, so the words follow the side.
            var conceding = _weAreWinner;
            var winner = _incoming.Winner;
            var cost = PeaceTable.CostOf(_incoming);
            var theirScore = _war.ScoreFor(_them);
            BudgetLabel = conceding
                ? "What " + _them.Name + "'s offer costs them"
                : "What " + _them.Name + " asks of you";
            BudgetText = cost.ToString("0");
            SpentText = "of a war they are " + (theirScore < 0f ? "losing" : "winning")
                        + " at score " + (theirScore >= 0f ? "+" : "") + theirScore.ToString("0");
            SpentColor = PeaceTermRowVM.MutedColor;
            var budget = PeaceTable.BudgetFor(_war, winner);
            FillPercent = budget <= 0f ? (cost > 0f ? 100 : 0)
                : (int)Math.Min(100f, cost / budget * 100f);
            FillColor = "#4D7F52FF";
            ShowCliff = false;
            BudgetNote = conceding
                ? "The same formula either side of the table reads: this is what their"
                  + " own court judged affordable, not a gift."
                : "The same formula either side of the table reads: this is priced against what"
                  + " the war has earned them, the figure your own table would show them.";
            TermsHeader = conceding ? "What they offer" : "What they demand";
            PriceHeader = conceding ? "worth" : "price";
            TableNote = "This is their table, not yours   -   nothing here is editable. What you"
                        + " can change is only whether you sign.";

            // Read only: the rows are the standard catalogue with their package ticked.
            // A line left out says so; a line the model would not allow at all keeps the
            // model's own reason, so a blank never pretends to be a choice.
            var notIncluded = conceding ? "Not in their offer." : "Not in their demand.";
            var rows = new MBBindingList<PeaceTermRowVM>();
            foreach (var spec in Catalogue(_incoming.Winner, _incoming.Loser))
            {
                var included = Contains(_incoming, spec);
                var row = new PeaceTermRowVM(spec.Kind, spec.Fief, spec.Name,
                    spec.Desc, spec.Price, included,
                    spec.Enabled ? notIncluded : spec.DisabledReason,
                    () => { });
                row.IsOn = included;
                rows.Add(row);
            }
            Rows = rows;

            RefreshTotals();
        }

        /// <summary>
        /// Every line the table can show, priced by <see cref="PeaceTable.CostOf"/> and
        /// allowed or refused by <see cref="PeaceTable.IsDemandable"/> on a one-term
        /// trial package - so a line that cannot be asked at all is grey with the model's
        /// own reason, never with a rule of ours.
        /// </summary>
        private List<Spec> Catalogue(Kingdom winner, Kingdom loser)
        {
            var specs = new List<Spec>();

            // Captives.
            Add(specs, PeaceTermKind.Captives, null,
                _weAreWinner ? "Their captives returned" : "Release their captives",
                _weAreWinner ? "Every lord of yours they hold walks free."
                    : "We free every hero of theirs we hold.",
                winner, loser, t => t.ReleasePrisoners = true);

            // Indemnity, sized from the war score by the same resolver the AI ladders use.
            var gold = PeaceTable.LargestIndemnity(_war, winner, loser);
            if (gold >= 1000)
                Add(specs, PeaceTermKind.Indemnity, null,
                    "An indemnity of " + gold + " denars",
                    _weAreWinner
                        ? "Sized from the war score, not from what their treasury happens to hold."
                        : "Sized by what this war earned them.",
                    winner, loser, t => t.IndemnityGold = gold);

            // Tribute.
            Add(specs, PeaceTermKind.Tribute, null,
                _weAreWinner
                    ? "Tribute, " + DiplomacyConstants.AiDefaultTributePerPeriod + " per period"
                    : "Agree to pay tribute",
                _weAreWinner ? "They buy the peace. They owe you no army."
                    : "A tributary pays for peace and keeps everything else.",
                winner, loser, t =>
                {
                    t.ImposeTributaryPact = true;
                    t.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                });

            // Land: one line per fortification, claim-gated by the model.
            var claim = ClaimRegistry.Best(_state, winner, loser);
            var land = new List<Spec>();
            var settlements = loser.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var fief = settlements[i];
                if (!fief.IsFortification) continue;
                var captured = fief;
                Add(land, PeaceTermKind.Land, fief,
                    fief.Name.ToString(),
                    _weAreWinner
                        ? "Your " + (claim != null ? claim.Type.ToString() : "claim")
                          + " is what entitles you to ask for land at all."
                        : "Cede it and it changes hands on signing.",
                    winner, loser, t => t.FiefsCeded.Add(captured));
            }
            specs.AddRange(CollapseRefusedLand(land));

            // The top rung, in whichever face applies: a hegemon gives up its sphere, a
            // free kingdom gives up itself. Both faces are shown; the model refuses the
            // one that does not fit this loser.
            var held = new List<Treaty>();
            Hegemony.CollectVassalages(_state, loser, held);
            var names = new List<string>(held.Count);
            for (var i = 0; i < held.Count; i++) names.Add(held[i].SubordinateParty.Name.ToString());
            Add(specs, PeaceTermKind.Dissolution, null,
                _weAreWinner ? "They release their vassals" : "Release our vassals",
                held.Count == 0
                    ? "There is no sphere to break up."
                    : string.Join(", ", names.ToArray()) + " walk free, and their sphere ends with them.",
                winner, loser, t => t.DissolveHegemony = true);
            Add(specs, PeaceTermKind.Submission, null,
                _weAreWinner ? "Their submission as your vassal" : "Submit as their vassal",
                _weAreWinner
                    ? "They kneel: tribute, and their army answers your call. Only above the cliff."
                    : "We keep our ruler and lands, and owe troops, tribute and foreign policy.",
                winner, loser, t =>
                {
                    t.ImposeVassalage = true;
                    t.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                });

            return specs;
        }

        /// <summary>
        /// When the model refuses every fief for the same reason - usually that the winner
        /// holds no territorial claim - one grey line says so instead of a grey line per
        /// castle, which buried the terms that could be asked (the first live pass listed
        /// fifteen). Any fief the model allows, or any reason that differs from the rest,
        /// keeps the full list: the player needs to see which land is which.
        /// </summary>
        private List<Spec> CollapseRefusedLand(List<Spec> land)
        {
            if (land.Count < 2) return land;

            var reason = land[0].DisabledReason;
            for (var i = 0; i < land.Count; i++)
                if (land[i].Enabled || land[i].DisabledReason != reason) return land;

            return new List<Spec>
            {
                new Spec
                {
                    Kind = PeaceTermKind.Land,
                    Fief = null,
                    Apply = t => { },
                    Name = (_weAreWinner ? "Their land" : "Our land")
                           + " (" + land.Count + " fiefs)",
                    Desc = reason,
                    Price = "-",
                    Enabled = false,
                    DisabledReason = reason,
                },
            };
        }

        private void Add(List<Spec> specs, PeaceTermKind kind, Settlement fief,
            string name, string desc, Kingdom winner, Kingdom loser,
            Action<PeaceTerms> apply)
        {
            var trial = new PeaceTerms(winner, loser);
            apply(trial);
            var enabled = PeaceTable.IsDemandable(_state, _war, trial, out var reason);
            specs.Add(new Spec
            {
                Kind = kind,
                Fief = fief,
                Apply = apply,
                Name = name,
                Desc = desc,
                Price = PeaceTable.CostOf(trial).ToString("0"),
                Enabled = enabled,
                DisabledReason = reason ?? "Cannot be asked in this war.",
            });
        }

        // ----- ticking ---------------------------------------------------------

        private void Toggle(Spec spec)
        {
            if (!spec.Enabled) return;

            var row = RowOf(spec);
            if (row == null) return;

            if (row.IsOn)
            {
                row.IsOn = false;
            }
            else
            {
                row.IsOn = true;
                // The model says what conflicts; the screen only obeys. Ticking one side
                // of an exclusive pair unticks the other.
                foreach (var other in _specs)
                {
                    if (other == spec) continue;
                    if (!PeaceTerms.AreExclusive(spec.Kind, other.Kind)) continue;
                    var otherRow = RowOf(other);
                    if (otherRow != null && otherRow.IsOn) otherRow.IsOn = false;
                }
            }

            RefreshTotals();
        }

        private PeaceTermRowVM RowOf(Spec spec)
        {
            for (var i = 0; i < Rows.Count; i++)
                if (Rows[i].Kind == spec.Kind && Rows[i].Fief == spec.Fief) return Rows[i];
            return null;
        }

        /// <summary>The package the ticked rows describe.</summary>
        private PeaceTerms Selection()
        {
            var winner = _weAreWinner ? _us : _them;
            var loser = _weAreWinner ? _them : _us;
            var terms = new PeaceTerms(winner, loser);
            foreach (var spec in _specs)
            {
                var row = RowOf(spec);
                if (row != null && row.IsOn) spec.Apply(terms);
            }
            return terms;
        }

        private static bool Contains(PeaceTerms terms, Spec spec)
        {
            switch (spec.Kind)
            {
                case PeaceTermKind.Captives: return terms.ReleasePrisoners;
                case PeaceTermKind.Indemnity: return terms.IndemnityGold > 0;
                case PeaceTermKind.Tribute: return terms.ImposeTributaryPact;
                case PeaceTermKind.Dissolution: return terms.DissolveHegemony;
                case PeaceTermKind.Submission: return terms.ImposeVassalage;
                default:
                    return spec.Fief != null && terms.FiefsCeded.Contains(spec.Fief);
            }
        }

        // ----- the running total and the verdict -------------------------------

        private void RefreshTotals()
        {
            var terms = _incoming ?? Selection();
            var winner = terms.Winner;
            var budget = PeaceTable.BudgetFor(_war, winner);
            var cost = PeaceTable.CostOf(terms);
            var left = budget - cost;
            var over = cost > budget;

            FillPercent = budget <= 0f ? (cost > 0f ? 100 : 0)
                : (int)Math.Min(100f, cost / budget * 100f);
            FillColor = over ? "#A3452FFF" : "#4D7F52FF";

            if (_incoming == null)
            {
                SpentText = cost.ToString("0") + " committed   -   "
                            + (over ? (-left).ToString("0") + " over" : left.ToString("0") + " unspent");
                SpentColor = over
                    ? Color.ConvertStringToColor("#E08070FF")
                    : Color.ConvertStringToColor("#9AC26AFF");
            }

            if (over)
            {
                VerdictText = "They refuse";
                VerdictColor = Color.ConvertStringToColor("#E08070FF");
                VerdictWhy = "You are asking " + (-left).ToString("0")
                             + " more than this war has earned. Drop a term, or fight on.";
                if (OfferEnabled) OfferEnabled = false;
                return;
            }

            if (_incoming != null)
            {
                // Our own half of the willingness the AI already applied - the player's
                // half is the click, the formula still has an opinion and it is shown.
                var ours = _incoming.Loser == _us
                    ? PeaceTable.WouldAccept(_state, _war, _incoming, out var why)
                    : PeaceTable.WinnerWouldAccept(_state, _war, _incoming, out why);
                VerdictText = ours ? "Worth taking" : "Worth refusing";
                VerdictColor = ours
                    ? Color.ConvertStringToColor("#9AC26AFF")
                    : Color.ConvertStringToColor("#E08070FF");
                VerdictWhy = ours
                    ? "You are " + ExhaustionBands.Name(ExhaustionBands.Of(_war.ExhaustionOf(_us)))
                      + " at " + _war.DaysElapsed.ToString("0") + " days. This ends it with "
                      + terms + "."
                    : (why ?? "The court would rather fight on.");
                return;
            }

            // Whichever side is not clicking decides the verdict.
            var accepted = _weAreWinner
                ? PeaceTable.WouldAccept(_state, _war, terms, out var reason)
                : PeaceTable.WinnerWouldAccept(_state, _war, terms, out reason);
            if (accepted)
            {
                VerdictText = "They will sign";
                VerdictColor = Color.ConvertStringToColor("#9AC26AFF");
                VerdictWhy = "Every term here is inside what the war has earned. "
                             + left.ToString("0") + " left unspent.";
                OfferEnabled = true;
            }
            else
            {
                VerdictText = "They refuse";
                VerdictColor = Color.ConvertStringToColor("#E08070FF");
                VerdictWhy = reason ?? "Not now.";
                OfferEnabled = false;
            }
        }
    }
}
