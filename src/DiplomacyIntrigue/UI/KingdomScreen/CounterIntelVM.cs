using System;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Espionage;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// The Realm tab's counter-intelligence section (Phase 3.7, the approved mockup's board 4):
    /// the realm's defence against spies, what it is made of, the ruler's weekly order, and the
    /// foreign agents the realm has caught. Shown only to the ruler, who alone pays for it
    /// (design 03 §9, decision 9).
    ///
    /// Every figure comes from <see cref="CounterIntelligence.Explain"/>, the resolver network
    /// growth, mission odds and exposure all read. "From next week" is that same explanation with
    /// the order in place of last week's payment - the clamp included - not a second formula.
    ///
    /// **Only caught agents are listed.** A network nobody has caught is invisible to the player
    /// as it is to every AI court (the AI orders its defence from wars and exposures alone), so the
    /// page does not hint at what the realm has not found.
    /// </summary>
    internal sealed class DiCounterIntelVM : ViewModel
    {
        /// <summary>A step of the order's buttons: one point of counter-intelligence a week.</summary>
        private static int Step => (int)EspionageConstants.CounterIntelligenceGoldPerPoint;

        private bool _show;
        private string _valueText = string.Empty;
        private string _securityLabel = string.Empty;
        private string _securityText = string.Empty;
        private string _budgetLabel = string.Empty;
        private string _budgetText = string.Empty;
        private string _orderText = string.Empty;
        private string _nextText = string.Empty;
        private string _purseLine = string.Empty;
        private Color _purseColor;
        private string _effectText = string.Empty;
        private bool _canLower;
        private bool _hasCaught;
        private MBBindingList<DiCaughtVM> _caught = new MBBindingList<DiCaughtVM>();

        [DataSourceProperty] public bool Show { get => _show; set { if (value == _show) return; _show = value; OnPropertyChangedWithValue(value, nameof(Show)); } }
        [DataSourceProperty] public string ValueText { get => _valueText; set => SetText(ref _valueText, value, nameof(ValueText)); }
        [DataSourceProperty] public string BaseText => EspionageConstants.CounterIntelligenceBase.ToString("0.0");
        [DataSourceProperty] public string SecurityLabel { get => _securityLabel; set => SetText(ref _securityLabel, value, nameof(SecurityLabel)); }
        [DataSourceProperty] public string SecurityText { get => _securityText; set => SetText(ref _securityText, value, nameof(SecurityText)); }
        [DataSourceProperty] public string BudgetLabel { get => _budgetLabel; set => SetText(ref _budgetLabel, value, nameof(BudgetLabel)); }
        [DataSourceProperty] public string BudgetText { get => _budgetText; set => SetText(ref _budgetText, value, nameof(BudgetText)); }
        [DataSourceProperty] public string OrderText { get => _orderText; set => SetText(ref _orderText, value, nameof(OrderText)); }
        [DataSourceProperty] public string NextText { get => _nextText; set => SetText(ref _nextText, value, nameof(NextText)); }
        [DataSourceProperty] public string PurseLine { get => _purseLine; set => SetText(ref _purseLine, value, nameof(PurseLine)); }
        [DataSourceProperty] public Color PurseColor { get => _purseColor; set { if (value.Equals(_purseColor)) return; _purseColor = value; OnPropertyChangedWithValue(value, nameof(PurseColor)); } }
        [DataSourceProperty] public string EffectText { get => _effectText; set => SetText(ref _effectText, value, nameof(EffectText)); }
        [DataSourceProperty] public bool CanLower { get => _canLower; set { if (value == _canLower) return; _canLower = value; OnPropertyChangedWithValue(value, nameof(CanLower)); } }
        [DataSourceProperty] public bool HasCaught { get => _hasCaught; set { if (value == _hasCaught) return; _hasCaught = value; OnPropertyChangedWithValue(value, nameof(HasCaught)); } }
        [DataSourceProperty] public MBBindingList<DiCaughtVM> Caught { get => _caught; set { if (value == _caught) return; _caught = value; OnPropertyChangedWithValue(value, nameof(Caught)); } }

        public void ExecuteOrderUp() => ChangeOrder(Step);

        public void ExecuteOrderDown() => ChangeOrder(-Step);

        private void ChangeOrder(int delta)
        {
            try
            {
                var state = CoreBehavior.State;
                var realm = Clan.PlayerClan?.Kingdom;
                if (state == null || realm == null || realm.Leader != Hero.MainHero) return;
                var current = CounterIntelligence.BudgetOf(state, realm)?.WeeklyBudget ?? 0;
                CounterIntelligence.SetBudget(state, realm, Math.Max(0, current + delta), out var reason);
                if (reason != null) Log.Notify(reason, Colors.Red);
                Rebuild();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Changing the counter-intelligence order failed.", ex);
            }
        }

        internal void Rebuild()
        {
            try
            {
                Compose();
            }
            catch (Exception ex)
            {
                Show = false;
                Log.Error("UI", "The counter-intelligence section could not be rebuilt.", ex);
            }
        }

        private void Compose()
        {
            var state = CoreBehavior.State;
            var realm = Clan.PlayerClan?.Kingdom;
            var ruler = Hero.MainHero;
            Show = state != null && SubModule.Healthy && Settings.Current.EnableEspionage
                   && realm != null && realm.IsRealm() && realm.Leader == ruler;
            if (!Show) return;

            var t = CounterIntelligence.Explain(state, realm);
            ValueText = t.Total.ToString("0.0");
            SecurityLabel = "Security of our towns and castles, average " + t.AverageSecurity.ToString("0");
            SecurityText = Signed(t.FromSecurity);
            BudgetLabel = "Paid last week, " + t.WeeklySpent.ToString("N0");
            BudgetText = Signed(t.FromBudget);

            // From next week: the same explanation, with the order paid in full in place of last
            // week's payment. The purse may not cover it; the line below says so.
            var order = t.WeeklyBudget;
            var withOrder = CounterIntelligence.Explain(state, realm);
            withOrder.FromBudget = order / EspionageConstants.CounterIntelligenceGoldPerPoint;
            var withNothing = CounterIntelligence.Explain(state, realm);
            withNothing.FromBudget = 0f;

            OrderText = order.ToString("N0");
            CanLower = order > 0;
            NextText = "From next week, if paid in full: " + withOrder.Total.ToString("0.0") + ". Every "
                       + Step.ToString("N0") + " paid is one point.";

            var purse = ruler.Gold;
            PurseLine = order > purse
                ? "More than your purse holds (" + purse.ToString("N0") + "): only that much would be paid, and you would be told."
                : (purse > 0 ? (100f * order / purse).ToString("0") : "0") + "% of your purse of " + purse.ToString("N0")
                  + " a week - the purse that also pays your troops.";
            PurseColor = order > purse || (purse > 0 && order > purse / 10) ? DiRealmVM.NegativeColor : DiRealmVM.MutedColor;

            var points = withOrder.Total - withNothing.Total;
            EffectText = "Against every foreign network in " + realm.Name + ", this order means "
                         + Pct(points * EspionageConstants.MissionChancePerCounterIntelligence) + " less chance for each of their operations to succeed, "
                         + Pct(points * EspionageConstants.ExposurePerCounterIntelligence) + " more to be caught when one fails, and "
                         + (points * EspionageConstants.NetworkCounterIntelligenceDrag).ToString("0.00") + " a week off each network's growth. "
                         + "Foreign courts read the same formula against our networks; none of them sees this order.";

            var caught = new MBBindingList<DiCaughtVM>();
            foreach (var other in Kingdom.All)
            {
                if (other == null || other == realm || other.IsEliminated) continue;
                foreach (var claim in ClaimRegistry.LiveClaims(state, realm, other))
                {
                    if (claim.Type != CasusBelliType.EspionageExposed) continue;
                    var days = (int)Math.Floor((CampaignTime.Now - claim.AcquiredOn).ToDays);
                    caught.Add(new DiCaughtVM(other.Name.ToString(), Color.FromUint(other.Color),
                        "caught " + (days <= 0 ? "today" : days == 1 ? "yesterday" : days + " days ago")
                        + " · we hold a casus belli against " + other.Name + ": Espionage exposed, "
                        + claim.Legitimacy.ToString("0.00")));
                }
            }
            Caught = caught;
            HasCaught = caught.Count > 0;
        }

        // ASCII hyphen: the game's Fira Sans has no U+2212 and draws it as an underscore.
        private static string Signed(float x) => (x >= 0f ? "+" : "-") + Math.Abs(x).ToString("0.0");

        private static string Pct(float share) => (share * 100f).ToString("0.0") + "%";

        private void SetText(ref string field, string value, string name)
        {
            value = value ?? string.Empty;
            if (value == field) return;
            field = value;
            OnPropertyChangedWithValue(value, name);
        }
    }

    /// <summary>One foreign intrusion the realm caught, and the claim it left us.</summary>
    internal sealed class DiCaughtVM : ViewModel
    {
        public DiCaughtVM(string name, Color accent, string line) { Name = name; AccentColor = accent; Line = line; }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public string Line { get; }
    }
}
