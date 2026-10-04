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

        // ----- static labels, moved out of the prefab (story 4.1 §9) ---------------------

        [DataSourceProperty] public string WhitePeaceText => DiText.T("DI_PEACE_WHITE_PEACE", "White peace");
        [DataSourceProperty] public string CancelText => DiText.T("DI_PEACE_CANCEL", "Cancel");
        [DataSourceProperty] public string RefuseText => DiText.T("DI_PEACE_REFUSE", "Refuse");

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
                Title = _weAreWinner ? DiText.T("DI_PEACE_ASKS_FOR_PEACE_NAME_2",
                    "{NAME} asks for peace",
                    ("NAME", _them.Name)) : DiText.T("DI_PEACE_NAMES_ITS_PRICE_FOR_PEACE_NAME_2",
                    "{NAME} names its price for peace",
                    ("NAME", _them.Name));
                Subtitle = DiText.T("DI_PEACE_YOUR_OWN_CONDITION_STORY_CONDITION_2",
                    "{STORY}   -   your own condition: {CONDITION}",
                    ("STORY", story),
                    ("CONDITION", ExhaustionBands.Condition(war.ExhaustionOf(_us))));
                BuildIncoming();
            }
            else
            {
                Title = _weAreWinner ? DiText.T("DI_PEACE_PEACE_WITH_NAME_2", "Peace with {NAME}", ("NAME", _them.Name)) : DiText.T("DI_PEACE_SUE_FOR_PEACE_WITH_NAME_2",
                    "Sue for peace with {NAME}",
                    ("NAME", _them.Name));
                Subtitle = story + "   -   their condition: "
                           + ExhaustionBands.Condition(war.ExhaustionOf(_them));
                BuildEditable();
            }
        }

        private void BuildEditable()
        {
            ShowOfferButtons = true;
            ShowAnswerButtons = false;
            OfferText = DiText.T("DI_PEACE_OFFER_THESE_TERMS_2", "Offer these terms");
            OfferColor = PeaceTermRowVM.OnNameColor;

            var winner = _weAreWinner ? _us : _them;
            var loser = _weAreWinner ? _them : _us;
            var budget = PeaceTable.BudgetFor(_war, winner);

            BudgetLabel = _weAreWinner ? DiText.T("DI_PEACE_WHAT_THIS_WAR_HAS_EARNED_2", "What this war has earned") : DiText.T("DI_PEACE_WHAT_THIS_WAR_HAS_EARNED_THEM_2", "What this war has earned them");
            BudgetText = budget.ToString("0");
            TermsHeader = _weAreWinner ? DiText.T("DI_PEACE_WHAT_YOU_DEMAND_2", "What you demand") : DiText.T("DI_PEACE_WHAT_YOU_OFFER_2", "What you offer");
            PriceHeader = _weAreWinner ? DiText.T("DI_PEACE_PRICE_2", "price") : DiText.T("DI_PEACE_WORTH_2", "worth");
            BudgetNote = budget > 0f && Statecraft.StatecraftModel.Enabled ? DiText.T("DI_PEACE_ABOVE_THE_CLIFF_WINNER_ASKS_FOR_NEGOTIATIONLINE_FOR_2",
                "Above the cliff a winner asks for standing, not coin. Below it, only for what coin can buy. {NEGOTIATIONLINE} (war score {FOR}).",
                ("NEGOTIATIONLINE", Statecraft.StatecraftTerms.NegotiationLine(winner, loser)),
                ("FOR", WarScore.For(_war, winner).ToString("0"))) : DiText.T("DI_PEACE_ABOVE_THE_CLIFF_WINNER_ASKS_FOR_2",
                "Above the cliff a winner asks for standing, not coin. Below it, only for what coin can buy.");

            var cliff = PeaceTable.SubjugationCost;
            ShowCliff = budget >= cliff;
            CliffPercent = budget <= 0f ? 0 : (int)Math.Min(100f, cliff / budget * 100f);
            CliffText = DiText.T("DI_PEACE_THE_CLIFF_CLIFF_2",
                "the cliff   -   {CLIFF}",
                ("CLIFF", cliff.ToString("0")));

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
            AcceptText = _incoming.IsWhitePeace ? DiText.T("DI_PEACE_MAKE_PEACE_2", "Make peace") : DiText.T("DI_PEACE_ACCEPT_THESE_TERMS_2", "Accept these terms");
            OfferColor = PeaceTermRowVM.OnNameColor;

            // _weAreWinner here means the offer is a loser's concession to us (the
            // mockup's board 3b); otherwise it is a winner's demand of us. The package is
            // priced the same way either way - CostOf against the winner's BudgetFor - but
            // what it means to the reader is opposite, so the words follow the side.
            var conceding = _weAreWinner;
            var winner = _incoming.Winner;
            var cost = PeaceTable.CostOf(_incoming);
            var theirScore = WarScore.For(_war, _them);
            BudgetLabel = conceding ? DiText.T("DI_PEACE_WHAT_OFFER_COSTS_THEM_NAME_2",
                "What {NAME}'s offer costs them",
                ("NAME", _them.Name)) : DiText.T("DI_PEACE_WHAT_ASKS_OF_YOU_NAME_2",
                "What {NAME} asks of you",
                ("NAME", _them.Name));
            BudgetText = cost.ToString("0");
            SpentText = theirScore < 0f ? DiText.T("DI_PEACE_OF_WAR_THEY_ARE_LOSING_AT_THEIRSCORE_THEIRSCORE_2_2",
                "of a war they are losing at score {THEIRSCORE}{THEIRSCORE_2}",
                ("THEIRSCORE", theirScore >= 0f ? "+" : ""),
                ("THEIRSCORE_2", theirScore.ToString("0"))) : DiText.T("DI_PEACE_OF_WAR_THEY_ARE_WINNING_AT_THEIRSCORE_THEIRSCORE_2_2",
                "of a war they are winning at score {THEIRSCORE}{THEIRSCORE_2}",
                ("THEIRSCORE", theirScore >= 0f ? "+" : ""),
                ("THEIRSCORE_2", theirScore.ToString("0")));
            SpentColor = PeaceTermRowVM.MutedColor;
            var budget = PeaceTable.BudgetFor(_war, winner);
            FillPercent = budget <= 0f ? (cost > 0f ? 100 : 0)
                : (int)Math.Min(100f, cost / budget * 100f);
            FillColor = "#4D7F52FF";
            ShowCliff = false;
            BudgetNote = conceding ? DiText.T("DI_PEACE_THE_SAME_FORMULA_EITHER_SIDE_OF_2",
                "The same formula either side of the table reads: this is what their own court judged affordable, not a gift.") : DiText.T("DI_PEACE_THE_SAME_FORMULA_EITHER_SIDE_OF_3",
                "The same formula either side of the table reads: this is priced against what the war has earned them, the figure your own table would show them.");
            TermsHeader = conceding ? DiText.T("DI_PEACE_WHAT_THEY_OFFER_2", "What they offer") : DiText.T("DI_PEACE_WHAT_THEY_DEMAND_2", "What they demand");
            PriceHeader = conceding ? DiText.T("DI_PEACE_WORTH", "worth") : DiText.T("DI_PEACE_PRICE", "price");
            TableNote = DiText.T("DI_PEACE_THIS_IS_THEIR_TABLE_NOT_YOURS_2",
                "This is their table, not yours   -   nothing here is editable. What you can change is only whether you sign.");

            // Read only: the rows are the standard catalogue with their package ticked.
            // A line left out says so; a line the model would not allow at all keeps the
            // model's own reason, so a blank never pretends to be a choice.
            var notIncluded = conceding ? DiText.T("DI_PEACE_NOT_IN_THEIR_OFFER_2", "Not in their offer.") : DiText.T("DI_PEACE_NOT_IN_THEIR_DEMAND_2", "Not in their demand.");
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
                _weAreWinner ? DiText.T("DI_PEACE_THEIR_CAPTIVES_RETURNED_2", "Their captives returned") : DiText.T("DI_PEACE_RELEASE_THEIR_CAPTIVES_2", "Release their captives"),
                _weAreWinner ? DiText.T("DI_PEACE_EVERY_LORD_OF_YOURS_THEY_HOLD_2",
                    "Every lord of yours they hold walks free.") : DiText.T("DI_PEACE_WE_FREE_EVERY_HERO_OF_THEIRS_2",
                    "We free every hero of theirs we hold."),
                winner, loser, t => t.ReleasePrisoners = true);

            // Indemnity, sized by the same resolver the AI ladders use: the war score says how
            // many points, the loser's treasury what a point is worth. An offer already on the
            // table shows its own figure - the denars the AI actually put in it - so the row can
            // never quote a number the offer does not carry.
            var gold = _incoming != null && _incoming.IndemnityGold > 0
                ? _incoming.IndemnityGold
                : PeaceTable.LargestIndemnity(_war, winner, loser);
            if (gold >= 1000)
                Add(specs, PeaceTermKind.Indemnity, null,
                    DiText.T("DI_PEACE_AN_INDEMNITY_OF_DENARS_GOLD_2",
                        "An indemnity of {GOLD} denars",
                        ("GOLD", gold)),
                    _weAreWinner ? DiText.T("DI_PEACE_SIZED_BY_WHAT_THE_WAR_EARNED_DESCRIBEINDEMNITY_2",
                        "Sized by what the war earned you, against their treasury: {DESCRIBEINDEMNITY}.",
                        ("DESCRIBEINDEMNITY", PeaceTable.DescribeIndemnity(gold, loser))) : DiText.T("DI_PEACE_SIZED_BY_WHAT_THIS_WAR_EARNED_DESCRIBEINDEMNITY_2",
                        "Sized by what this war earned them, against our treasury: {DESCRIBEINDEMNITY}.",
                        ("DESCRIBEINDEMNITY", PeaceTable.DescribeIndemnity(gold, loser))),
                    winner, loser, t => t.IndemnityGold = gold);

            // Tribute.
            Add(specs, PeaceTermKind.Tribute, null,
                _weAreWinner ? DiText.T("DI_PEACE_TRIBUTE_PER_PERIOD_AIDEFAULTTRIBUTEPERPERIOD_2",
                    "Tribute, {AIDEFAULTTRIBUTEPERPERIOD} per period",
                    ("AIDEFAULTTRIBUTEPERPERIOD", DiplomacyConstants.AiDefaultTributePerPeriod)) : DiText.T("DI_PEACE_AGREE_TO_PAY_TRIBUTE_2", "Agree to pay tribute"),
                _weAreWinner ? DiText.T("DI_PEACE_THEY_BUY_THE_PEACE_THEY_OWE_2",
                    "They buy the peace. They owe you no army.") : DiText.T("DI_PEACE_TRIBUTARY_PAYS_FOR_PEACE_AND_KEEPS_2",
                    "A tributary pays for peace and keeps everything else."),
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
                        ? DiText.T("DI_PEACE_YOUR_CLAUSE_IS_WHAT_ENTITLES_YOU_TO",
                            "Your {CLAUSE} is what entitles you to ask for land at all.",
                            ("CLAUSE", claim != null ? CasusBelli.NameOf(claim.Type) : DiText.T("DI_PEACE_AN_UNSTATED_CLAIM", "unstated claim")))
                          + "."
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
                _weAreWinner ? DiText.T("DI_PEACE_THEY_RELEASE_THEIR_VASSALS_2", "They release their vassals") : DiText.T("DI_PEACE_RELEASE_OUR_VASSALS_2", "Release our vassals"),
                held.Count == 0 ? DiText.T("DI_PEACE_THERE_IS_NO_SPHERE_TO_BREAK_2",
                    "There is no sphere to break up.") : DiText.T("DI_PEACE_WALK_FREE_AND_THEIR_SPHERE_ENDS_JOIN_2",
                    "{JOIN} walk free, and their sphere ends with them.",
                    ("JOIN", string.Join(", ", names.ToArray()))),
                winner, loser, t => t.DissolveHegemony = true);
            Add(specs, PeaceTermKind.Submission, null,
                _weAreWinner ? DiText.T("DI_PEACE_THEIR_SUBMISSION_AS_YOUR_VASSAL_2",
                    "Their submission as your vassal") : DiText.T("DI_PEACE_SUBMIT_AS_THEIR_VASSAL_2", "Submit as their vassal"),
                _weAreWinner ? DiText.T("DI_PEACE_THEY_KNEEL_TRIBUTE_AND_THEIR_ARMY_2",
                    "They kneel: tribute, and their army answers your call. Only above the cliff.") : DiText.T("DI_PEACE_WE_KEEP_OUR_RULER_AND_LANDS_2",
                    "We keep our ruler and lands, and owe troops, tribute and foreign policy."),
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
                    Name = _weAreWinner ? DiText.T("DI_PEACE_THEIR_LAND_FIEFS_COUNT_2",
                        "Their land ({COUNT} fiefs)",
                        ("COUNT", land.Count)) : DiText.T("DI_PEACE_OUR_LAND_FIEFS_COUNT_2",
                        "Our land ({COUNT} fiefs)",
                        ("COUNT", land.Count)),
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
                DisabledReason = DiText.T("DI_PEACE_REASON_2",
                    "{REASON}",
                    ("REASON", reason ?? "Cannot be asked in this war.")),
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
                VerdictText = DiText.T("DI_PEACE_THEY_REFUSE_2", "They refuse");
                VerdictColor = Color.ConvertStringToColor("#E08070FF");
                VerdictWhy = DiText.T("DI_PEACE_YOU_ARE_ASKING_MORE_THAN_THIS_LEFT_2",
                    "You are asking {LEFT} more than this war has earned. Drop a term, or fight on.",
                    ("LEFT", (-left).ToString("0")));
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
                VerdictText = ours ? DiText.T("DI_PEACE_WORTH_TAKING_2", "Worth taking") : DiText.T("DI_PEACE_WORTH_REFUSING_2", "Worth refusing");
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
                VerdictText = DiText.T("DI_PEACE_THEY_WILL_SIGN_2", "They will sign");
                VerdictColor = Color.ConvertStringToColor("#9AC26AFF");
                VerdictWhy = DiText.T("DI_PEACE_EVERY_TERM_HERE_IS_INSIDE_WHAT_LEFT_2",
                    "Every term here is inside what the war has earned. {LEFT} left unspent.",
                    ("LEFT", left.ToString("0")));
                OfferEnabled = true;
            }
            else
            {
                VerdictText = DiText.T("DI_PEACE_THEY_REFUSE", "They refuse");
                VerdictColor = Color.ConvertStringToColor("#E08070FF");
                VerdictWhy = DiText.T("DI_PEACE_REASON", "{REASON}", ("REASON", reason ?? "Not now."));
                OfferEnabled = false;
            }
        }
    }
}
