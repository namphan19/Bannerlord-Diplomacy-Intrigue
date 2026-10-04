using System;
using System.Collections.Generic;
using System.Linq;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Espionage;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.ClanScreen
{
    /// <summary>
    /// The Clan screen's Intelligence tab (Phase 3.7): the house's spy networks, the operations
    /// each can run, what is under way, and the reports in hand - the layout the lead approved on
    /// 2026-09-25 (artifact "Espionage UI mockup (Phase 3.7)", board 1, with boards 2 and 3 as the
    /// two overlays).
    ///
    /// **On the Clan screen, not the Kingdom screen.** Networks belong to a house (design 03 §9,
    /// decision 5), and a house outside any kingdom has no Kingdom screen to open.
    ///
    /// **Every number is read from the resolver the roll uses.** Growth comes from
    /// <see cref="SpyNetworks.Explain"/>, odds from <see cref="Missions.OddsOf"/>, the gates from
    /// <see cref="Missions.CanPlan"/> and <see cref="Missions.CanLaunch"/>, the handler's ceiling
    /// from <see cref="SpyNetworks.CeilingOf"/>. Nothing here computes a chance of its own, so the
    /// page cannot promise odds the roll does not use - the AI reads the same numbers.
    ///
    /// Rebuilt when the tab opens and after every action on it, never on a timer: the page is a
    /// snapshot of the moment, as the Realm and Court tabs are.
    /// </summary>
    internal sealed class DiIntelligenceVM : ViewModel
    {
        internal static readonly Color PositiveColor = Color.ConvertStringToColor("#9AC26AFF");
        internal static readonly Color NegativeColor = Color.ConvertStringToColor("#E08070FF");
        internal static readonly Color WarningColor = Color.ConvertStringToColor("#E0A860FF");
        internal static readonly Color NeutralColor = Color.ConvertStringToColor("#E0CFA8FF");
        internal static readonly Color MutedColor = Color.ConvertStringToColor("#A89878FF");

        /// <summary>A step of the network budget's buttons, in denars a week.</summary>
        private const int BudgetStep = 500;

        /// <summary>The overall chance of being caught above which a row is marked amber.</summary>
        private const float RiskyExposure = 0.10f;

        private readonly Action _onShow;

        /// <summary>The open screen's view model, for the test levers; cleared when the screen closes.</summary>
        internal static DiIntelligenceVM Current;

        private Kingdom _selectedTarget;
        private SpyMissionType _selectedMission = SpyMissionType.ScoutArmies;

        // Plan overlay state.
        private SpyMissionType _planType;
        private object _planMark;

        // Handler picker state.
        private Kingdom _pickRealm;
        private Hero _pickHero;

        public DiIntelligenceVM(Action onShow)
        {
            _onShow = onShow;
            Current = this;
            RefreshTabGate();
        }

        // ================= bound surface: tab =================

        private bool _show;
        private bool _tabVisible;

        // ----- static labels, moved out of the prefab ---------------------------------
        //
        // A Gauntlet widget's Text is a plain string the widget never resolves a key in, so
        // every label the panel used to carry in XML lives here instead (story 4.1 §9). They are
        // properties rather than constants because a constant cannot hold a DiText call.

        [DataSourceProperty] public string OddsNoteText => DiText.T("DI_INTEL_ODDS_ARE_THE_ONES_THE_ROLL", "Every chance on this tab is the one the roll uses. AI houses read the same numbers.");
        [DataSourceProperty] public string NetworksTitleText => DiText.T("DI_INTEL_NETWORKS_TITLE", "Our networks");
        [DataSourceProperty] public string FoundNetworkText => DiText.T("DI_INTEL_FOUND_A_NETWORK_ABROAD", "Found a network abroad");
        [DataSourceProperty] public string ReportsTitleText => DiText.T("DI_INTEL_REPORTS_IN_HAND", "Reports in hand");
        [DataSourceProperty] public string StrengthColumnText => DiText.T("DI_INTEL_STRENGTH_COLUMN", "STRENGTH");
        [DataSourceProperty] public string NextWeekColumnText => DiText.T("DI_INTEL_NEXT_WEEK_COLUMN", "NEXT WEEK");
        [DataSourceProperty] public string WeekCaptionText => DiText.T("DI_INTEL_A_WEEK", "A week");
        [DataSourceProperty] public string WeeklyBudgetLabelText => DiText.T("DI_INTEL_WEEKLY_BUDGET", "Weekly budget");
        [DataSourceProperty] public string RecallText => DiText.T("DI_INTEL_RECALL", "Recall");
        [DataSourceProperty] public string PostHandlerText => DiText.T("DI_INTEL_POST_A_HANDLER", "Post a handler");
        [DataSourceProperty] public string OddsLegendText => DiText.T("DI_INTEL_SUCCESS_CAUGHT_OVERALL", "success  ·  caught, overall");
        [DataSourceProperty] public string PlanOperationText => DiText.T("DI_INTEL_PLAN_THIS_OPERATION", "Plan this operation");
        [DataSourceProperty] public string OperationsTitleText => DiText.T("DI_INTEL_UNDER_WAY", "Under way");
        [DataSourceProperty] public string NoMarksWarningText => DiText.T("DI_INTEL_NOBODY_OR_NOWHERE_TO_AIM_AT", "Nobody or nowhere the operation could be aimed at.");
        [DataSourceProperty] public string OutcomesTitleText => DiText.T("DI_INTEL_WHAT_CAN_HAPPEN", "What can happen");
        [DataSourceProperty] public string SuccessLabelText => DiText.T("DI_INTEL_SUCCESS", "Success");
        [DataSourceProperty] public string FailureUnseenText => DiText.T("DI_INTEL_FAILURE_UNSEEN", "Failure, unseen");
        [DataSourceProperty] public string CaughtLabelText => DiText.T("DI_INTEL_CAUGHT", "Caught");
        [DataSourceProperty] public string BackText => DiText.T("DI_INTEL_BACK", "Back");
        [DataSourceProperty] public string WhereLabelText => DiText.T("DI_INTEL_WHERE", "Where");
        [DataSourceProperty] public string WhoLabelText => DiText.T("DI_INTEL_WHO", "Who");
        [DataSourceProperty] public string SkillsCanBuildToText => DiText.T("DI_INTEL_ROGUERY_CHARM_CAN_BUILD_TO", "roguery  ·  charm  ·  can build to");
        [DataSourceProperty] public string HandlerRecallNoteText => DiText.T("DI_INTEL_A_HANDLER_GIVEN_A_PARTY_MADE_A", "A handler who is given a party, made a governor or taken prisoner is recalled, and the network sits idle until another goes.");
        [DataSourceProperty] public string CancelText => DiText.T("DI_INTEL_CANCEL", "Cancel");

        [DataSourceProperty] public bool Show { get => _show; set => SetBool(ref _show, value, nameof(Show)); }
        [DataSourceProperty] public bool TabVisible { get => _tabVisible; set => SetBool(ref _tabVisible, value, nameof(TabVisible)); }
        [DataSourceProperty] public string TabText => DiText.T("DI_INTEL_INTELLIGENCE_2", "Intelligence");

        // ================= bound surface: summary and lists =================

        private string _summaryText = string.Empty;
        private string _noNetworksText = string.Empty;
        private bool _hasNetworks;
        private bool _hasReports;
        private bool _hasOperations;
        private MBBindingList<DiNetworkRowVM> _networks = new MBBindingList<DiNetworkRowVM>();
        private MBBindingList<DiReportVM> _reports = new MBBindingList<DiReportVM>();
        private MBBindingList<DiOperationVM> _operations = new MBBindingList<DiOperationVM>();

        [DataSourceProperty] public string SummaryText { get => _summaryText; set => SetText(ref _summaryText, value, nameof(SummaryText)); }
        [DataSourceProperty] public string NoNetworksText { get => _noNetworksText; set => SetText(ref _noNetworksText, value, nameof(NoNetworksText)); }
        [DataSourceProperty] public bool HasNetworks { get => _hasNetworks; set => SetBool(ref _hasNetworks, value, nameof(HasNetworks)); }
        [DataSourceProperty] public bool HasReports { get => _hasReports; set => SetBool(ref _hasReports, value, nameof(HasReports)); }
        [DataSourceProperty] public bool HasOperations { get => _hasOperations; set => SetBool(ref _hasOperations, value, nameof(HasOperations)); }
        [DataSourceProperty] public MBBindingList<DiNetworkRowVM> Networks { get => _networks; set => SetList(ref _networks, value, nameof(Networks)); }
        [DataSourceProperty] public MBBindingList<DiReportVM> Reports { get => _reports; set => SetList(ref _reports, value, nameof(Reports)); }
        [DataSourceProperty] public MBBindingList<DiOperationVM> Operations { get => _operations; set => SetList(ref _operations, value, nameof(Operations)); }

        // ================= bound surface: the selected network =================

        private bool _hasSelection;
        private string _selName = string.Empty;
        private string _selRelation = string.Empty;
        private string _selStrengthText = string.Empty;
        private string _selCeilingText = string.Empty;
        private int _selStrengthAmount;
        private int _selCeilingAmount;
        private bool _selHasCeiling;
        private string _selNetText = string.Empty;
        private Color _selNetColor;
        private string _selHint = string.Empty;
        private Color _selHintColor;
        private string _selBudgetText = string.Empty;
        private string _selHandlerName = string.Empty;
        private string _selHandlerLine = string.Empty;
        private bool _selHasHandler;
        private bool _selNoHandler;
        private bool _canBudgetDown;
        private MBBindingList<DiTermVM> _selTerms = new MBBindingList<DiTermVM>();

        [DataSourceProperty] public bool HasSelection { get => _hasSelection; set => SetBool(ref _hasSelection, value, nameof(HasSelection)); }
        [DataSourceProperty] public string SelName { get => _selName; set => SetText(ref _selName, value, nameof(SelName)); }
        [DataSourceProperty] public string SelRelation { get => _selRelation; set => SetText(ref _selRelation, value, nameof(SelRelation)); }
        [DataSourceProperty] public string SelStrengthText { get => _selStrengthText; set => SetText(ref _selStrengthText, value, nameof(SelStrengthText)); }
        [DataSourceProperty] public string SelCeilingText { get => _selCeilingText; set => SetText(ref _selCeilingText, value, nameof(SelCeilingText)); }
        [DataSourceProperty] public int SelStrengthAmount { get => _selStrengthAmount; set => SetInt(ref _selStrengthAmount, value, nameof(SelStrengthAmount)); }
        [DataSourceProperty] public int SelCeilingAmount { get => _selCeilingAmount; set => SetInt(ref _selCeilingAmount, value, nameof(SelCeilingAmount)); }
        [DataSourceProperty] public bool SelHasCeiling { get => _selHasCeiling; set => SetBool(ref _selHasCeiling, value, nameof(SelHasCeiling)); }
        [DataSourceProperty] public string SelNetText { get => _selNetText; set => SetText(ref _selNetText, value, nameof(SelNetText)); }
        [DataSourceProperty] public Color SelNetColor { get => _selNetColor; set => SetColor(ref _selNetColor, value, nameof(SelNetColor)); }
        [DataSourceProperty] public string SelHint { get => _selHint; set => SetText(ref _selHint, value, nameof(SelHint)); }
        [DataSourceProperty] public Color SelHintColor { get => _selHintColor; set => SetColor(ref _selHintColor, value, nameof(SelHintColor)); }
        [DataSourceProperty] public string SelBudgetText { get => _selBudgetText; set => SetText(ref _selBudgetText, value, nameof(SelBudgetText)); }
        [DataSourceProperty] public string SelHandlerName { get => _selHandlerName; set => SetText(ref _selHandlerName, value, nameof(SelHandlerName)); }
        [DataSourceProperty] public string SelHandlerLine { get => _selHandlerLine; set => SetText(ref _selHandlerLine, value, nameof(SelHandlerLine)); }
        [DataSourceProperty] public bool SelHasHandler { get => _selHasHandler; set => SetBool(ref _selHasHandler, value, nameof(SelHasHandler)); }
        [DataSourceProperty] public bool SelNoHandler { get => _selNoHandler; set => SetBool(ref _selNoHandler, value, nameof(SelNoHandler)); }
        [DataSourceProperty] public bool CanBudgetDown { get => _canBudgetDown; set => SetBool(ref _canBudgetDown, value, nameof(CanBudgetDown)); }
        [DataSourceProperty] public MBBindingList<DiTermVM> SelTerms { get => _selTerms; set => SetList(ref _selTerms, value, nameof(SelTerms)); }

        // ================= bound surface: the operations board =================

        private string _boardTitle = string.Empty;
        private string _boardNote = string.Empty;
        private bool _hasBoardNote;
        private MBBindingList<DiMissionRowVM> _missions = new MBBindingList<DiMissionRowVM>();
        private string _mLabel = string.Empty;
        private string _mTarget = string.Empty;
        private string _mEffect = string.Empty;
        private string _mRisk = string.Empty;
        private string _mWhy = string.Empty;
        private bool _canPlan;

        [DataSourceProperty] public string BoardTitle { get => _boardTitle; set => SetText(ref _boardTitle, value, nameof(BoardTitle)); }
        [DataSourceProperty] public string BoardNote { get => _boardNote; set => SetText(ref _boardNote, value, nameof(BoardNote)); }
        [DataSourceProperty] public bool HasBoardNote { get => _hasBoardNote; set => SetBool(ref _hasBoardNote, value, nameof(HasBoardNote)); }
        [DataSourceProperty] public MBBindingList<DiMissionRowVM> Missions { get => _missions; set => SetList(ref _missions, value, nameof(Missions)); }
        [DataSourceProperty] public string MLabel { get => _mLabel; set => SetText(ref _mLabel, value, nameof(MLabel)); }
        [DataSourceProperty] public string MTarget { get => _mTarget; set => SetText(ref _mTarget, value, nameof(MTarget)); }
        [DataSourceProperty] public string MEffect { get => _mEffect; set => SetText(ref _mEffect, value, nameof(MEffect)); }
        [DataSourceProperty] public string MRisk { get => _mRisk; set => SetText(ref _mRisk, value, nameof(MRisk)); }
        [DataSourceProperty] public string MWhy { get => _mWhy; set => SetText(ref _mWhy, value, nameof(MWhy)); }
        [DataSourceProperty] public bool CanPlan { get => _canPlan; set => SetBool(ref _canPlan, value, nameof(CanPlan)); }

        // ================= bound surface: the plan overlay =================

        private bool _showPlan;
        private string _planTitle = string.Empty;
        private string _planSubtitle = string.Empty;
        private string _planMarkTitle = string.Empty;
        private bool _planHasMarks;
        private bool _planNoMarks;
        private MBBindingList<DiMarkVM> _planMarks = new MBBindingList<DiMarkVM>();
        private string _planSuccessText = string.Empty;
        private string _planSuccessPct = string.Empty;
        private string _planFailText = string.Empty;
        private string _planFailPct = string.Empty;
        private string _planCaughtText = string.Empty;
        private string _planCaughtPct = string.Empty;
        private string _planOddsNote = string.Empty;
        private string _planCostText = string.Empty;
        private string _planWhenText = string.Empty;
        private string _planSendText = string.Empty;
        private string _planBlock = string.Empty;
        private bool _canSend;

        [DataSourceProperty] public bool ShowPlan { get => _showPlan; set => SetBool(ref _showPlan, value, nameof(ShowPlan)); }
        [DataSourceProperty] public string PlanTitle { get => _planTitle; set => SetText(ref _planTitle, value, nameof(PlanTitle)); }
        [DataSourceProperty] public string PlanSubtitle { get => _planSubtitle; set => SetText(ref _planSubtitle, value, nameof(PlanSubtitle)); }
        [DataSourceProperty] public string PlanMarkTitle { get => _planMarkTitle; set => SetText(ref _planMarkTitle, value, nameof(PlanMarkTitle)); }
        [DataSourceProperty] public bool PlanHasMarks { get => _planHasMarks; set => SetBool(ref _planHasMarks, value, nameof(PlanHasMarks)); }
        [DataSourceProperty] public bool PlanNoMarks { get => _planNoMarks; set => SetBool(ref _planNoMarks, value, nameof(PlanNoMarks)); }
        [DataSourceProperty] public MBBindingList<DiMarkVM> PlanMarks { get => _planMarks; set => SetList(ref _planMarks, value, nameof(PlanMarks)); }
        [DataSourceProperty] public string PlanSuccessText { get => _planSuccessText; set => SetText(ref _planSuccessText, value, nameof(PlanSuccessText)); }
        [DataSourceProperty] public string PlanSuccessPct { get => _planSuccessPct; set => SetText(ref _planSuccessPct, value, nameof(PlanSuccessPct)); }
        [DataSourceProperty] public string PlanFailText { get => _planFailText; set => SetText(ref _planFailText, value, nameof(PlanFailText)); }
        [DataSourceProperty] public string PlanFailPct { get => _planFailPct; set => SetText(ref _planFailPct, value, nameof(PlanFailPct)); }
        [DataSourceProperty] public string PlanCaughtText { get => _planCaughtText; set => SetText(ref _planCaughtText, value, nameof(PlanCaughtText)); }
        [DataSourceProperty] public string PlanCaughtPct { get => _planCaughtPct; set => SetText(ref _planCaughtPct, value, nameof(PlanCaughtPct)); }
        [DataSourceProperty] public string PlanOddsNote { get => _planOddsNote; set => SetText(ref _planOddsNote, value, nameof(PlanOddsNote)); }
        [DataSourceProperty] public string PlanCostText { get => _planCostText; set => SetText(ref _planCostText, value, nameof(PlanCostText)); }
        [DataSourceProperty] public string PlanWhenText { get => _planWhenText; set => SetText(ref _planWhenText, value, nameof(PlanWhenText)); }
        [DataSourceProperty] public string PlanSendText { get => _planSendText; set => SetText(ref _planSendText, value, nameof(PlanSendText)); }
        [DataSourceProperty] public string PlanBlock { get => _planBlock; set => SetText(ref _planBlock, value, nameof(PlanBlock)); }
        [DataSourceProperty] public bool CanSend { get => _canSend; set => SetBool(ref _canSend, value, nameof(CanSend)); }

        // ================= bound surface: the handler picker =================

        private bool _showPicker;
        private string _pickerTitle = string.Empty;
        private MBBindingList<DiPickRealmVM> _pickRealms = new MBBindingList<DiPickRealmVM>();
        private MBBindingList<DiPickMemberVM> _pickMembers = new MBBindingList<DiPickMemberVM>();
        private string _pickName = string.Empty;
        private string _pickDetail = string.Empty;
        private string _pickGrowth = string.Empty;
        private Color _pickGrowthColor;
        private string _pickBlock = string.Empty;
        private string _pickSendText = string.Empty;
        private bool _canPostHandler;

        [DataSourceProperty] public bool ShowPicker { get => _showPicker; set => SetBool(ref _showPicker, value, nameof(ShowPicker)); }
        [DataSourceProperty] public string PickerTitle { get => _pickerTitle; set => SetText(ref _pickerTitle, value, nameof(PickerTitle)); }
        [DataSourceProperty] public MBBindingList<DiPickRealmVM> PickRealms { get => _pickRealms; set => SetList(ref _pickRealms, value, nameof(PickRealms)); }
        [DataSourceProperty] public MBBindingList<DiPickMemberVM> PickMembers { get => _pickMembers; set => SetList(ref _pickMembers, value, nameof(PickMembers)); }
        [DataSourceProperty] public string PickName { get => _pickName; set => SetText(ref _pickName, value, nameof(PickName)); }
        [DataSourceProperty] public string PickDetail { get => _pickDetail; set => SetText(ref _pickDetail, value, nameof(PickDetail)); }
        [DataSourceProperty] public string PickGrowth { get => _pickGrowth; set => SetText(ref _pickGrowth, value, nameof(PickGrowth)); }
        [DataSourceProperty] public Color PickGrowthColor { get => _pickGrowthColor; set => SetColor(ref _pickGrowthColor, value, nameof(PickGrowthColor)); }
        [DataSourceProperty] public string PickBlock { get => _pickBlock; set => SetText(ref _pickBlock, value, nameof(PickBlock)); }
        [DataSourceProperty] public string PickSendText { get => _pickSendText; set => SetText(ref _pickSendText, value, nameof(PickSendText)); }
        [DataSourceProperty] public bool CanPostHandler { get => _canPostHandler; set => SetBool(ref _canPostHandler, value, nameof(CanPostHandler)); }

        // ================= commands =================

        /// <summary>The tab button: take the panel area over and fill it.</summary>
        public void ExecuteShow()
        {
            Guard("Opening the Intelligence tab", () =>
            {
                _onShow?.Invoke();
                RefreshTabGate();
                ShowPlan = false;
                ShowPicker = false;
                Rebuild();
                Show = true;
            });
        }

        public void ExecuteBudgetUp() => Guard("Raising the budget", () => ChangeBudget(BudgetStep));

        public void ExecuteBudgetDown() => Guard("Lowering the budget", () => ChangeBudget(-BudgetStep));

        public void ExecuteRecall()
        {
            Guard("Recalling the handler", () =>
            {
                var network = SelectedNetwork();
                if (network?.Handler == null) return;
                SpyNetworks.Release(CoreBehavior.State, network, HandlerLossCause.Recalled, "recalled by " + Clan.PlayerClan.Name);
                Rebuild();
            });
        }

        /// <summary>Opens the picker on the selected network's realm.</summary>
        public void ExecutePostHandler() => Guard("Opening the handler picker", () => OpenPicker(_selectedTarget));

        /// <summary>Opens the picker on the first realm we have no network in.</summary>
        public void ExecuteFoundNetwork() => Guard("Opening the handler picker", () => OpenPicker(null));

        public void ExecuteClosePicker() => Guard("Closing the handler picker", () => ShowPicker = false);

        public void ExecuteSendHandler()
        {
            Guard("Posting a handler", () =>
            {
                var state = CoreBehavior.State;
                if (state == null || _pickRealm == null || _pickHero == null) return;
                var network = SpyNetworks.Assign(state, _pickHero, Clan.PlayerClan, _pickRealm, out var reason);
                if (network == null)
                {
                    Log.Notify("Could not post " + _pickHero.Name + ": " + reason, Colors.Red);
                    return;
                }
                Log.Notify(_pickHero.Name + " sets out to run our network in " + _pickRealm.Name + ".", Colors.Cyan);
                _selectedTarget = _pickRealm;
                ShowPicker = false;
                Rebuild();
            });
        }

        public void ExecutePlan()
        {
            Guard("Opening the plan", () =>
            {
                if (!CanPlan) return;
                _planType = _selectedMission;
                _planMark = null;
                ComposePlan();
                ShowPlan = true;
            });
        }

        public void ExecuteClosePlan() => Guard("Closing the plan", () => ShowPlan = false);

        public void ExecuteSend()
        {
            Guard("Sending the order", () =>
            {
                var state = CoreBehavior.State;
                if (state == null || _selectedTarget == null) return;
                var spec = Espionage.Missions.SpecOf(_planType);
                var mission = Espionage.Missions.Launch(state, Clan.PlayerClan, _selectedTarget, _planType,
                    spec != null && spec.NeedsHero ? _planMark as Hero : null,
                    spec != null && spec.NeedsSettlement ? _planMark as Settlement : null, out var reason);
                if (mission == null)
                {
                    Log.Notify("The order was not sent: " + reason, Colors.Red);
                    ComposePlan();
                    return;
                }
                ShowPlan = false;
                Rebuild();
            });
        }

        // ================= contents =================

        private void RefreshTabGate()
        {
            TabVisible = SubModule.Healthy && Settings.Current.EnableEspionage;
        }

        internal void Rebuild()
        {
            try
            {
                Compose();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "The Intelligence tab could not be rebuilt.", ex);
            }
        }

        /// <summary>For the test lever: select a network's realm as a click on its row would.</summary>
        internal void Select(Kingdom target)
        {
            _selectedTarget = target;
            Rebuild();
        }

        /// <summary>For the test lever: select an operation on the board as a click on its row would.</summary>
        internal void SelectMission(SpyMissionType type)
        {
            _selectedMission = type;
            Rebuild();
        }

        /// <summary>For the test lever: click the plan overlay's mark row at this index.</summary>
        internal bool PickMark(int index)
        {
            if (!ShowPlan || index < 0 || index >= PlanMarks.Count) return false;
            PlanMarks[index].OnSelect();
            return true;
        }

        /// <summary>For the test lever: click the picker's realm row, then its member row, by name.</summary>
        internal bool PickInPicker(string realmOrHero)
        {
            if (!ShowPicker || string.IsNullOrWhiteSpace(realmOrHero)) return false;
            foreach (var r in PickRealms)
                if (string.Equals(r.Name, realmOrHero, StringComparison.OrdinalIgnoreCase)) { r.OnSelect(); return true; }
            foreach (var m in PickMembers)
                if (m.Name.StartsWith(realmOrHero, StringComparison.OrdinalIgnoreCase)) { m.OnSelect(); return true; }
            return false;
        }

        /// <summary>What the tab shows right now, bound value by bound value, for the test lever.</summary>
        internal string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Show " + Show + ", tab visible " + TabVisible + " | " + SummaryText);
            foreach (var n in Networks)
                sb.AppendLine((n.IsSelected ? " > " : "   ") + n.Name + "  " + n.StrengthText + "  " + n.TrendText + "  " + n.HandlerText
                              + "  bar " + n.StrengthAmount + (n.HasCeiling ? " ceil " + n.CeilingAmount : ""));
            if (!HasNetworks) sb.AppendLine("   " + NoNetworksText);
            foreach (var r in Reports) sb.AppendLine("  report: " + r.Title + " (" + r.Line + ") " + r.Body);
            if (HasSelection)
            {
                sb.AppendLine("Selected: " + SelName + " - " + SelRelation + " | strength " + SelStrengthText + " " + SelCeilingText);
                foreach (var t in SelTerms) sb.AppendLine("   " + t.Label + "  " + t.Value);
                sb.AppendLine("   a week " + SelNetText + " | " + SelHint + " | budget " + SelBudgetText);
                sb.AppendLine("   handler: " + SelHandlerName + " - " + SelHandlerLine);
                sb.AppendLine(BoardTitle + (HasBoardNote ? " [" + BoardNote + "]" : ""));
                foreach (var m in Missions)
                    sb.AppendLine((m.IsSelected ? " > " : "   ") + m.Label.PadRight(22) + m.CostText.PadRight(9) + m.DaysText.PadRight(9) + m.OddsText);
                sb.AppendLine("   " + MLabel + " (" + MTarget + ") " + MEffect);
                sb.AppendLine("   " + MRisk);
                sb.AppendLine("   " + MWhy + " | can plan " + CanPlan);
            }
            foreach (var o in Operations) sb.AppendLine("  under way: " + o.Title + " - " + o.Line + " [" + o.CancelText + "]");
            if (ShowPlan)
            {
                sb.AppendLine("PLAN: " + PlanTitle + " - " + PlanSubtitle + " | " + PlanMarkTitle);
                for (var i = 0; i < PlanMarks.Count; i++)
                    sb.AppendLine((PlanMarks[i].IsSelected ? "  >" : "   ") + i + " " + PlanMarks[i].Name + " - " + PlanMarks[i].Line);
                sb.AppendLine("   success " + PlanSuccessPct + ": " + PlanSuccessText);
                sb.AppendLine("   fail " + PlanFailPct + ": " + PlanFailText);
                sb.AppendLine("   caught " + PlanCaughtPct + ": " + PlanCaughtText);
                sb.AppendLine("   " + PlanOddsNote);
                sb.AppendLine("   " + PlanCostText + " | " + PlanWhenText + " | send: " + PlanSendText + " enabled " + CanSend + " " + PlanBlock);
            }
            if (ShowPicker)
            {
                sb.AppendLine("PICKER: " + PickerTitle);
                foreach (var r in PickRealms) sb.AppendLine((r.IsSelected ? "  >" : "   ") + r.Name + " - " + r.Line);
                foreach (var m in PickMembers)
                    sb.AppendLine((m.IsSelected ? "  >" : "   ") + m.Name + "  rog " + m.RogueryText + " cha " + m.CharmText + "  " + m.CeilingText);
                sb.AppendLine("   " + PickName + ": " + PickDetail + " | " + PickGrowth + " | " + PickBlock + " | " + PickSendText + " enabled " + CanPostHandler);
            }
            return sb.ToString();
        }

        private void Compose()
        {
            var state = CoreBehavior.State;
            var us = Clan.PlayerClan;
            var nets = new MBBindingList<DiNetworkRowVM>();
            var reports = new MBBindingList<DiReportVM>();
            var ops = new MBBindingList<DiOperationVM>();

            if (state == null || us == null || !SubModule.Healthy)
            {
                Networks = nets; Reports = reports; Operations = ops;
                HasNetworks = false; HasReports = false; HasOperations = false; HasSelection = false;
                NoNetworksText = DiText.T("DI_INTEL_ESPIONAGE_IS_ONLY_AVAILABLE_IN_CAMPAIGN_2",
                    "Espionage is only available in a campaign.");
                SummaryText = string.Empty;
                return;
            }

            var owned = SpyNetworks.OwnedBy(state, us);
            owned.Sort((a, b) => string.Compare(a.Target?.Name?.ToString(), b.Target?.Name?.ToString(), StringComparison.Ordinal));
            if (_selectedTarget != null && owned.All(n => n.Target != _selectedTarget)) _selectedTarget = null;
            if (_selectedTarget == null && owned.Count > 0)
                _selectedTarget = (owned.FirstOrDefault(n => n.Handler != null) ?? owned[0]).Target;

            var weekly = 0;
            foreach (var n in owned)
            {
                var t = SpyNetworks.Explain(state, n);
                weekly += n.Handler != null ? n.WeeklyBudget : 0;
                var target = n.Target;
                nets.Add(new DiNetworkRowVM(
                    target?.Name?.ToString() ?? "?",
                    KingdomColor(target),
                    n.Strength.ToString("0.0") + (n.Handler != null ? " of " + t.Ceiling.ToString("0") : ""),
                    Signed(t.NetOverAWeek) + " / week",
                    t.NetOverAWeek >= 0f ? PositiveColor : NegativeColor,
                    DiText.T("DI_INTEL_TOSTRING_2",
                        "{TOSTRING}",
                        ("TOSTRING", n.Handler?.Name?.ToString() ?? "no handler")),
                    (int)Math.Round(n.Strength),
                    n.Handler != null ? (int)Math.Round(t.Ceiling) : 0,
                    n.Handler != null && t.Ceiling < EspionageConstants.NetworkMaxStrength,
                    target == _selectedTarget,
                    () => { _selectedTarget = target; Rebuild(); }));
            }
            Networks = nets;
            HasNetworks = nets.Count > 0;
            NoNetworksText = nets.Count > 0 ? "" : DiText.T("DI_INTEL_OUR_HOUSE_RUNS_NO_NETWORK_ABROAD_2",
                "Our house runs no network abroad. Found one: a member or companion goes to a foreign realm and builds it week by week.");

            // Operations under way, house-wide, and the reports in hand.
            var pending = 0;
            for (var i = 0; i < state.SpyMissions.Count; i++)
            {
                var m = state.SpyMissions[i];
                if (m.Owner != us || !m.IsPending) continue;
                pending++;
                var network = SpyNetworks.Get(state, us, m.Target);
                var odds = Espionage.Missions.OddsOf(state, network, m.Type);
                var daysLeft = Math.Max(0, (int)Math.Ceiling((m.ResolvesOn - CampaignTime.Now).ToDays));
                var mission = m;
                ops.Add(new DiOperationVM(
                    Capitalise(Espionage.Missions.Describe(m.Type)) + " · " + m.Target?.Name + MarkSuffix(m),
                    // "now": the figure is today's, and the roll uses the day it resolves (story 3.11 R3).
                    m.Handler?.Name + " · " + Days(daysLeft) + " left · " + Espionage.Missions.Pct(odds.Success)
                    + " to succeed now · " + Denars(m.GoldPaid) + " paid",
                    DiText.T("DI_INTEL_CALL_OFF_LOST_DENARS_2",
                        "Call off ({DENARS} lost)",
                        ("DENARS", Denars(m.GoldPaid))),
                    () => Guard("Calling off an operation", () =>
                    {
                        if (Espionage.Missions.Cancel(CoreBehavior.State, mission))
                            Log.Notify(Capitalise(Espionage.Missions.Describe(mission.Type)) + " in " + mission.Target?.Name
                                       + " is called off. The " + Denars(mission.GoldPaid) + " is not coming back.", Colors.Cyan);
                        Rebuild();
                    })));
            }
            Operations = ops;
            HasOperations = ops.Count > 0;

            foreach (var n in owned)
            {
                var target = n.Target;
                var armies = Espionage.Missions.RevealDaysLeft(state, us, target, SpyMissionType.ScoutArmies);
                if (armies > 0f)
                    reports.Add(new DiReportVM(DiText.T("DI_INTEL_ARMIES_NAME_2", "{NAME}'s armies", ("NAME", target.Name)), "good for " + More((int)Math.Ceiling(armies)),
                        Capitalise(Espionage.Missions.ArmiesReport(target)) + "."));
                var court = Espionage.Missions.RevealDaysLeft(state, us, target, SpyMissionType.ReadCourt);
                if (court > 0f)
                    reports.Add(new DiReportVM(DiText.T("DI_INTEL_COURT_NAME_2", "{NAME}'s court", ("NAME", target.Name)), "good for " + More((int)Math.Ceiling(court)),
                        DiText.T("DI_INTEL_ITS_FIGURES_ARE_ON_ENCYCLOPEDIA_PAGE_NAME_2",
                            "Its figures are on {NAME}'s Encyclopedia page, under \"Their ledger\".",
                            ("NAME", target.Name))));
            }
            Reports = reports;
            HasReports = reports.Count > 0;

            SummaryText = owned.Count + (owned.Count == 1 ? " network" : " networks") + "    " + Denars(weekly)
                          + " denars a week    " + pending + (pending == 1 ? " operation" : " operations") + " under way    "
                          + reports.Count + (reports.Count == 1 ? " report" : " reports") + " in hand";

            ComposeSelected(state, us);
        }

        private void ComposeSelected(ModState state, Clan us)
        {
            var network = SelectedNetwork();
            HasSelection = network != null;
            var rows = new MBBindingList<DiMissionRowVM>();
            if (network == null)
            {
                Missions = rows;
                SelTerms = new MBBindingList<DiTermVM>();
                BoardTitle = string.Empty;
                HasBoardNote = false;
                CanPlan = false;
                return;
            }

            var target = network.Target;
            var t = SpyNetworks.Explain(state, network);
            var hasHandler = network.Handler != null;

            SelName = target.Name.ToString();
            SelRelation = t.AtWar ? DiText.T("DI_INTEL_AT_WAR_WITH_US_BORDERS_ARE_2",
                "at war with us - borders are watched, the network grows at half pace") : DiText.T("DI_INTEL_AT_PEACE_WITH_US_2", "at peace with us");
            SelStrengthText = network.Strength.ToString("0.0");
            SelCeilingText = hasHandler
                ? "of " + t.Ceiling.ToString("0") + (t.Ceiling >= EspionageConstants.NetworkMaxStrength ? ", the most any network holds" : ", the most this handler can build")
                : "of 100 - no handler, no ceiling to reach for";
            SelStrengthAmount = (int)Math.Round(network.Strength);
            SelCeilingAmount = hasHandler ? (int)Math.Round(t.Ceiling) : 0;
            SelHasCeiling = hasHandler && t.Ceiling < EspionageConstants.NetworkMaxStrength;

            var terms = new MBBindingList<DiTermVM>();
            if (t.Idle != null)
                terms.Add(new DiTermVM("Gold - " + t.Idle + ", nothing is spent", Signed(0f), MutedColor));
            else
            {
                var goldLabel = "Gold, " + Denars(t.Spend) + " a week";
                if (t.Spend < network.WeeklyBudget) goldLabel += " of " + Denars(network.WeeklyBudget) + " ordered";
                if (t.AtWar) goldLabel += DiText.T("DI_INTEL_HALVED_AT_WAR_2", ", halved at war");
                terms.Add(new DiTermVM(goldLabel, Signed(t.Investment), t.Investment > 0f ? PositiveColor : MutedColor));
            }
            terms.Add(new DiTermVM(DiText.T("DI_INTEL_THEIR_COUNTER_INTELLIGENCE_COUNTERINTELLIGENCE_2",
                "Their counter-intelligence, {COUNTERINTELLIGENCE}",
                ("COUNTERINTELLIGENCE", t.CounterIntelligence.ToString("0.0"))), Signed(-t.FromCounterIntelligence), NegativeColor));
            terms.Add(new DiTermVM(DiText.T("DI_INTEL_ATTRITION_2", "Attrition"), Signed(-t.Attrition), NegativeColor));
            terms.Add(new DiTermVM("Decay, " + EspionageConstants.NetworkDailyDecay.ToString("0.0") + " a day", Signed(-t.WeekOfDecay), NegativeColor));
            SelTerms = terms;

            SelNetText = Signed(t.NetOverAWeek);
            SelNetColor = t.NetOverAWeek >= 0f ? PositiveColor : NegativeColor;
            if (t.Idle != null)
            {
                SelHint = DiText.T("DI_INTEL_IDLE_IT_ONLY_SHRINKS_AND_NO_IDLE_2",
                    "Idle ({IDLE}): it only shrinks, and no operation can be launched.",
                    ("IDLE", t.Idle));
                SelHintColor = WarningColor;
            }
            else if (t.NetOverAWeek < 0f && network.Strength <= 0f)
            {
                // Nothing left to lose: say what it takes to start, not that it is shrinking.
                var perDenar0 = (1f + t.Roguery / EspionageConstants.NetworkRogueryScale) / EspionageConstants.NetworkGoldPerPoint
                                * (t.AtWar ? EspionageConstants.NetworkWartimeGrowth : 1f);
                var start = perDenar0 > 0f
                    ? (int)Math.Ceiling((t.FromCounterIntelligence + t.Attrition + t.WeekOfDecay) / perDenar0 / 100f) * 100 : 0;
                SelHint = start > 0 ? DiText.T("DI_INTEL_NOT_YET_BUILT_IT_STARTS_TO_DENARS_2",
                    "Not yet built. It starts to grow above about {DENARS} a week.",
                    ("DENARS", Denars(start))) : DiText.T("DI_INTEL_NOT_YET_BUILT_2", "Not yet built.");
                SelHintColor = WarningColor;
            }
            else if (t.NetOverAWeek < 0f)
            {
                // The budget that would hold it steady, from the same terms the upkeep applies:
                // the gold term is linear in the spend, so the spend that cancels the losses is
                // the losses over what one denar buys this network.
                var perDenar = t.Spend > 0 ? t.Investment / t.Spend
                    : (1f + t.Roguery / EspionageConstants.NetworkRogueryScale) / EspionageConstants.NetworkGoldPerPoint
                      * (t.AtWar ? EspionageConstants.NetworkWartimeGrowth : 1f);
                var losses = t.FromCounterIntelligence + t.Attrition + t.WeekOfDecay;
                var hold = perDenar > 0f ? (int)Math.Ceiling(losses / perDenar / 100f) * 100 : 0;
                SelHint = hold > 0 ? "Shrinking. About " + Denars(hold) + " a week would hold it steady." : "Shrinking.";
                SelHintColor = WarningColor;
            }
            else if (network.Strength >= t.Ceiling - 0.05f)
            {
                SelHint = DiText.T("DI_INTEL_AT_THE_MOST_THIS_HANDLER_CAN_2",
                    "At the most this handler can build.");
                SelHintColor = MutedColor;
            }
            else
            {
                SelHint = "Growing toward " + t.Ceiling.ToString("0") + ".";
                SelHintColor = MutedColor;
            }

            SelBudgetText = Denars(network.WeeklyBudget);
            CanBudgetDown = network.WeeklyBudget > 0;
            SelHasHandler = hasHandler;
            SelNoHandler = !hasHandler;
            if (hasHandler)
            {
                var h = network.Handler;
                SelHandlerName = h.Name.ToString();
                SelHandlerLine = "roguery " + h.GetSkillValue(DefaultSkills.Roguery) + ", charm " + h.GetSkillValue(DefaultSkills.Charm)
                                 + (h.CurrentSettlement != null ? " · stationed in " + h.CurrentSettlement.Name : "");
            }
            else
            {
                SelHandlerName = DiText.T("DI_INTEL_NO_HANDLER_2", "No handler");
                SelHandlerLine = DiText.T("DI_INTEL_MEMBER_OF_THE_HOUSE_OR_COMPANION_NAME_2",
                    "A member of the house or a companion must go to {NAME}.",
                    ("NAME", target.Name));
            }

            // The board: every operation, read through the launch's own gates.
            BoardTitle = "Operations in " + target.Name;
            var pendingHere = Espionage.Missions.PendingOn(state, us, target);
            BoardNote = !hasHandler ? "No handler in " + target.Name + ": post one before any operation."
                : pendingHere != null ? "One operation at a time per network: " + Espionage.Missions.Describe(pendingHere.Type) + " is under way."
                : string.Empty;
            HasBoardNote = !string.IsNullOrEmpty(BoardNote);

            MissionSpec selectedSpec = null;
            MissionOdds selectedOdds = null;
            string selectedWhy = null;
            var selectedOpen = false;
            foreach (var spec in Espionage.Missions.AllSpecs)
            {
                var odds = Espionage.Missions.OddsOf(state, network, spec.Type);
                var open = Espionage.Missions.CanPlan(state, us, target, spec.Type, out var why);
                var strongEnough = network.Strength >= spec.Required;
                var oddsText = !hasHandler ? "-"
                    : strongEnough ? Espionage.Missions.Pct(odds.Success) + "  ·  " + Espionage.Missions.Pct(odds.Exposure)
                    : "needs " + spec.Required.ToString("0");
                var type = spec.Type;
                var selected = type == _selectedMission;
                rows.Add(new DiMissionRowVM(
                    TitleOf(type), Denars(spec.Gold), Days(spec.Days), oddsText,
                    !hasHandler || !strongEnough ? MutedColor : odds.Exposure > RiskyExposure ? WarningColor : NeutralColor,
                    open ? NeutralColor : MutedColor,
                    selected,
                    () => { _selectedMission = type; Rebuild(); }));
                if (selected)
                {
                    selectedSpec = spec;
                    selectedOdds = odds;
                    selectedWhy = why;
                    selectedOpen = open;
                }
            }
            Missions = rows;

            if (selectedSpec == null) { CanPlan = false; return; }
            MLabel = TitleOf(selectedSpec.Type);
            MTarget = "target: " + MarkNeed(selectedSpec);
            MEffect = EffectOf(selectedSpec.Type);
            MRisk = DiText.T("DI_INTEL_IF_CAUGHT_SUBSTRING_2",
                "If caught, t{SUBSTRING}",
                ("SUBSTRING", CaughtText(selectedSpec.Type, target).Substring(1)));
            MWhy = DiText.T("DI_INTEL_EMPTY_2",
                "{EMPTY}",
                ("EMPTY", selectedOpen
                ? Espionage.Missions.Pct(selectedOdds.Success) + " to succeed. If it fails, " + Espionage.Missions.Pct(selectedOdds.ExposureOnFailure)
                  + " that we are caught: " + Espionage.Missions.Pct(selectedOdds.Exposure) + " overall."
                : selectedWhy ?? string.Empty));
            CanPlan = selectedOpen;
        }

        // ================= the plan overlay =================

        private void ComposePlan()
        {
            var state = CoreBehavior.State;
            var network = SelectedNetwork();
            var spec = Espionage.Missions.SpecOf(_planType);
            if (state == null || network == null || spec == null) { ShowPlan = false; return; }

            var target = network.Target;
            var odds = Espionage.Missions.OddsOf(state, network, spec.Type);
            PlanTitle = TitleOf(spec.Type);
            PlanSubtitle = "in " + target.Name + " · run by " + network.Handler?.Name + " · network " + network.Strength.ToString("0.0");

            var marks = new MBBindingList<DiMarkVM>();
            if (spec.NeedsHero || spec.NeedsSettlement)
            {
                foreach (var candidate in MarksFor(state, target, spec))
                {
                    if (!Espionage.Missions.CanLaunch(state, Clan.PlayerClan, target, spec.Type,
                            candidate.Hero, candidate.Settlement, out _)) continue;
                    var mark = (object)candidate.Hero ?? candidate.Settlement;
                    if (_planMark == null) _planMark = mark;
                    marks.Add(new DiMarkVM(candidate.Name, candidate.Line, mark == _planMark,
                        () => { _planMark = mark; ComposePlan(); }));
                }
            }
            PlanMarks = marks;
            PlanHasMarks = marks.Count > 0;
            PlanNoMarks = (spec.NeedsHero || spec.NeedsSettlement) && marks.Count == 0;
            PlanMarkTitle = spec.NeedsSettlement ? "Which fief" : spec.NeedsHero ? "Which lord" : "No mark to choose";

            var markName = _planMark is Hero hero ? hero.Name.ToString()
                : _planMark is Settlement settlement ? settlement.Name.ToString() : target.Name.ToString();
            var caught = odds.Exposure;
            var failUnseen = (1f - odds.Success) - caught;
            PlanSuccessText = DiText.T("DI_INTEL_THE_NETWORK_SPENDS_SUCCESSTEXT_MISSIONSUCCESSNETWORKCOST_2",
                "{SUCCESSTEXT} The network spends {MISSIONSUCCESSNETWORKCOST}.",
                ("SUCCESSTEXT", SuccessText(spec.Type, target, markName)),
                ("MISSIONSUCCESSNETWORKCOST", EspionageConstants.MissionSuccessNetworkCost.ToString("0")));
            PlanSuccessPct = Espionage.Missions.Pct(odds.Success);
            PlanFailText = DiText.T("DI_INTEL_NOTHING_HAPPENS_THE_IS_GONE_AND_DENARS_2",
                "Nothing happens. The {DENARS} is gone and the network loses {MISSIONFAILURENETWORKCOST}.",
                ("DENARS", Denars(spec.Gold)),
                ("MISSIONFAILURENETWORKCOST", EspionageConstants.MissionFailureNetworkCost.ToString("0")));
            PlanFailPct = Espionage.Missions.Pct(failUnseen);
            PlanCaughtText = DiText.T("DI_INTEL_THEIR_RULER_LEARNS_WHOSE_AGENTS_THEY_CAUGHTTEXT_2",
                "{CAUGHTTEXT} Their ruler learns whose agents they were.",
                ("CAUGHTTEXT", CaughtText(spec.Type, target)));
            PlanCaughtPct = Espionage.Missions.Pct(caught);
            PlanOddsNote = "Success " + Espionage.Missions.Pct(odds.Success) + ". If it fails, "
                           + Espionage.Missions.Pct(odds.ExposureOnFailure) + " that we are caught (their counter-intelligence "
                           + odds.CounterIntelligence.ToString("0.0") + " against a network of " + odds.Network.ToString("0.0") + ")."
                           // Story 3.11 R1: these are today's odds, and the roll is made on the day the
                           // operation resolves. Said here, under the figures, so a result rolled at
                           // another figure does not read as the page having lied (run 09 D-8: launched
                           // at 68%, resolved at 71%).
                           + "\nThe roll is made on the day it resolves, at that day's odds: their counter-intelligence "
                           + "and our network's strength can change them before then.";
            PlanCostText = DiText.T("DI_INTEL_DENARS_PAID_NOW_DENARS_2",
                "{DENARS} denars, paid now",
                ("DENARS", Denars(spec.Gold)));
            PlanWhenText = "Resolves in " + Days(spec.Days) + ". Calling it off before then does not refund the gold.";
            PlanSendText = DiText.T("DI_INTEL_SEND_THE_ORDER_DENARS_2",
                "Send the order - {DENARS}",
                ("DENARS", Denars(spec.Gold)));

            var ready = Espionage.Missions.CanLaunch(state, Clan.PlayerClan, target, spec.Type,
                _planMark as Hero, _planMark as Settlement, out var block);
            CanSend = ready;
            PlanBlock = ready ? string.Empty : block ?? string.Empty;
        }

        private sealed class Candidate
        {
            public string Name;
            public string Line;
            public Hero Hero;
            public Settlement Settlement;
        }

        /// <summary>
        /// Everyone or everywhere the operation could be aimed at; <see cref="Espionage.Missions.CanLaunch"/>
        /// then keeps only what the launch would accept. A rival house's mood is given as a band, never
        /// a figure: exact loyalty is what ReadCourt sells (design 02 §9.1), and this list must not
        /// give it away.
        /// </summary>
        private static IEnumerable<Candidate> MarksFor(ModState state, Kingdom target, MissionSpec spec)
        {
            if (spec.NeedsSettlement)
            {
                foreach (var fief in target.Fiefs)
                {
                    var s = fief?.Settlement;
                    if (s == null) continue;
                    var garrison = s.Town?.GarrisonParty?.MemberRoster?.TotalManCount ?? 0;
                    var line = (s.IsTown ? "town" : "castle")
                               + (spec.Type == SpyMissionType.SpreadDissent
                                   ? ", loyalty " + (s.Town?.Loyalty ?? 0f).ToString("0")
                                   : ", garrison " + garrison);
                    yield return new Candidate { Name = s.Name.ToString(), Line = line, Settlement = s };
                }
                yield break;
            }

            foreach (var clan in Court.MembersOf(target).Concat(new[] { target.RulingClan }).Distinct())
            {
                if (clan == null) continue;
                foreach (var hero in clan.Heroes)
                {
                    if (hero == null || !hero.IsAlive || hero.IsChild || !hero.IsLord) continue;
                    if (spec.NeedsHouseHead && hero != clan.Leader) continue;
                    var mood = CourtBands.MoodName(LoyaltyModel.BandOf(state, clan));
                    var line = "Clan " + clan.Name + (clan == target.RulingClan ? ", the crown" : ", " + mood.ToLowerInvariant());
                    yield return new Candidate { Name = hero.Name.ToString(), Line = line, Hero = hero };
                }
            }
        }

        // ================= the handler picker =================

        private void OpenPicker(Kingdom realm)
        {
            var state = CoreBehavior.State;
            if (state == null) return;
            _pickRealm = realm ?? RealmsWeCouldWork(state).FirstOrDefault(k => SpyNetworks.Get(state, Clan.PlayerClan, k) == null)
                         ?? RealmsWeCouldWork(state).FirstOrDefault();
            _pickHero = null;
            ComposePicker();
            ShowPicker = true;
        }

        private static IEnumerable<Kingdom> RealmsWeCouldWork(ModState state)
        {
            var us = Clan.PlayerClan;
            return Kingdom.All
                // Realms only: an internal war's rising is a Kingdom to the engine but not a realm (design 07 §3c).
                .Where(k => k != null && !k.IsEliminated && k.IsRealm() && k != us?.Kingdom && k.Fiefs.Any(f => f != null && f.IsTown))
                .OrderBy(k => k.Name.ToString());
        }

        private void ComposePicker()
        {
            var state = CoreBehavior.State;
            var us = Clan.PlayerClan;
            if (state == null || us == null) { ShowPicker = false; return; }

            var realms = new MBBindingList<DiPickRealmVM>();
            foreach (var k in RealmsWeCouldWork(state))
            {
                var existing = SpyNetworks.Get(state, us, k);
                var realm = k;
                var line = existing == null ? "no network"
                    : "network " + existing.Strength.ToString("0.0") + (existing.Handler != null ? ", run by " + existing.Handler.Name : ", idle");
                realms.Add(new DiPickRealmVM(k.Name.ToString(), line, KingdomColor(k), k == _pickRealm,
                    () => { _pickRealm = realm; _pickHero = null; ComposePicker(); }));
            }
            PickRealms = realms;
            PickerTitle = _pickRealm == null ? DiText.T("DI_INTEL_POST_HANDLER_2", "Post a handler") : DiText.T("DI_INTEL_POST_HANDLER_TO_NAME_2",
                "Post a handler to {NAME}",
                ("NAME", _pickRealm.Name));

            var members = new MBBindingList<DiPickMemberVM>();
            if (_pickRealm != null)
            {
                var people = us.Heroes.Concat(us.Companions).Where(h => h != null && h.IsAlive && !h.IsChild).Distinct()
                    .Select(h => new { Hero = h, Ok = SpyNetworks.CanHandle(state, h, us, _pickRealm, out var why), Why = why })
                    .OrderByDescending(x => x.Ok).ThenByDescending(x => SpyNetworks.CeilingOf(x.Hero)).ToList();
                if (_pickHero == null) _pickHero = people.FirstOrDefault(x => x.Ok)?.Hero;
                foreach (var p in people)
                {
                    var hero = p.Hero;
                    members.Add(new DiPickMemberVM(hero.Name.ToString(),
                        hero.GetSkillValue(DefaultSkills.Roguery).ToString(), hero.GetSkillValue(DefaultSkills.Charm).ToString(),
                        p.Ok ? SpyNetworks.CeilingOf(hero).ToString("0") : ShortReason(state, hero, us),
                        p.Ok ? NeutralColor : MutedColor, hero == _pickHero,
                        () => { _pickHero = hero; ComposePicker(); }));
                }
            }
            PickMembers = members;

            PickName = DiText.T("DI_INTEL_TOSTRING",
                "{TOSTRING}",
                ("TOSTRING", _pickHero?.Name?.ToString() ?? "Nobody chosen"));
            if (_pickRealm == null || _pickHero == null)
            {
                CanPostHandler = false;
                PickDetail = string.Empty;
                PickGrowth = string.Empty;
                PickBlock = _pickRealm == null ? DiText.T("DI_INTEL_THERE_IS_NO_FOREIGN_REALM_WITH_2",
                    "There is no foreign realm with a town to send anyone to.") : DiText.T("DI_INTEL_NOBODY_IN_THE_HOUSE_IS_FREE_2",
                    "Nobody in the house is free to go. The clan leader, anyone leading a party or governing a fief, and anyone already running a network elsewhere cannot.");
                PickSendText = DiText.T("DI_INTEL_SEND_2", "Send");
                return;
            }

            var ok = SpyNetworks.CanHandle(state, _pickHero, us, _pickRealm, out var reason);
            CanPostHandler = ok;
            PickBlock = ok ? string.Empty : reason ?? string.Empty;
            PickSendText = "Send " + _pickHero.FirstName;
            var existingNetwork = SpyNetworks.Get(state, us, _pickRealm);
            PickDetail = "Goes to " + _pickRealm.Name + " and stays there. While posted, a handler leads no party and governs no fief."
                         + " The network can grow to " + SpyNetworks.CeilingOf(_pickHero).ToString("0")
                         + (existingNetwork != null ? "; it stands at " + existingNetwork.Strength.ToString("0.0") + " now." : ".");

            // What a week would look like under this handler at the network's present budget, from
            // the upkeep's own explanation: a stand-in record, never saved, carries the handler.
            var budget = existingNetwork?.WeeklyBudget ?? 0;
            var preview = new SpyNetwork(us, _pickRealm);
            preview.SetBudget(budget);
            preview.SetHandler(_pickHero);
            var t = SpyNetworks.Explain(state, preview);
            PickGrowth = budget > 0
                ? "At " + Denars(budget) + " a week: " + Signed(t.NetOverAWeek) + " a week."
                : "With no budget it will not grow: set one on the tab once the handler is posted.";
            PickGrowthColor = budget > 0 && t.NetOverAWeek >= 0f ? PositiveColor : WarningColor;
        }

        /// <summary>The picker's short column: why this member cannot go.</summary>
        private static string ShortReason(ModState state, Hero hero, Clan us)
        {
            if (hero == us.Leader) return DiText.T("DI_INTEL_LEADS_THE_CLAN_2", "leads the clan");
            if (hero.IsPrisoner) return DiText.T("DI_INTEL_PRISONER_2", "a prisoner");
            if (hero.IsPartyLeader) return DiText.T("DI_INTEL_LEADS_PARTY_2", "leads a party");
            if (hero.GovernorOf != null) return DiText.T("DI_INTEL_GOVERNS_2", "governs");
            var current = SpyNetworks.HandledBy(state, hero);
            if (current != null) return "in " + current.Target?.Name;
            return DiText.T("DI_INTEL_CANNOT_GO_2", "cannot go");
        }

        // ================= helpers =================

        private SpyNetwork SelectedNetwork()
            => _selectedTarget == null ? null : SpyNetworks.Get(CoreBehavior.State, Clan.PlayerClan, _selectedTarget);

        private void ChangeBudget(int delta)
        {
            var state = CoreBehavior.State;
            var network = SelectedNetwork();
            if (state == null || network == null) return;
            var next = Math.Max(0, network.WeeklyBudget + delta);
            SpyNetworks.SetBudget(state, Clan.PlayerClan, network.Target, next);
            Rebuild();
        }

        private static string MarkSuffix(SpyMission m)
            => m.TargetHero != null ? " · " + m.TargetHero.Name : m.TargetSettlement != null ? " · " + m.TargetSettlement.Name : "";

        private static string MarkNeed(MissionSpec spec)
            => spec.NeedsSettlement ? "a town or castle of theirs"
             : spec.NeedsHouseHead ? "the head of a sworn house"
             : spec.NeedsHero ? "a lord of theirs"
             : "no mark to choose";

        internal static string TitleOf(SpyMissionType type)
        {
            switch (type)
            {
                case SpyMissionType.ScoutArmies: return DiText.T("DI_INTEL_SCOUT_THEIR_ARMIES_2", "Scout their armies");
                case SpyMissionType.ReadCourt: return DiText.T("DI_INTEL_READ_THEIR_COURT_2", "Read their court");
                case SpyMissionType.SabotageGarrison: return DiText.T("DI_INTEL_SABOTAGE_GARRISON_2", "Sabotage a garrison");
                case SpyMissionType.SpreadDissent: return DiText.T("DI_INTEL_SPREAD_DISSENT_2", "Spread dissent");
                case SpyMissionType.StealTreasury: return DiText.T("DI_INTEL_ROB_THE_TREASURY_2", "Rob the treasury");
                case SpyMissionType.BribeLord: return DiText.T("DI_INTEL_BRIBE_LORD_2", "Bribe a lord");
                case SpyMissionType.ForgeLetters: return DiText.T("DI_INTEL_FORGE_LETTERS_2", "Forge letters");
                case SpyMissionType.Assassinate: return DiText.T("DI_INTEL_ASSASSINATE_2", "Assassinate");
                default: return type.ToString();
            }
        }

        private static string EffectOf(SpyMissionType type)
        {
            switch (type)
            {
                case SpyMissionType.ScoutArmies:
                    return DiText.T("DI_INTEL_WHERE_THEIR_ARMIES_ARE_AND_HOW_SCOUTARMIESREVEALDAYS_2",
                        "Where their armies are and how strong, reported to us for {SCOUTARMIESREVEALDAYS} days.",
                        ("SCOUTARMIESREVEALDAYS", EspionageConstants.ScoutArmiesRevealDays));
                case SpyMissionType.ReadCourt:
                    return DiText.T("DI_INTEL_EXACT_LEGITIMACY_LOYALTY_GRIEVANCES_AND_COUNCIL_2",
                        "Exact legitimacy, loyalty, grievances and council business on their Encyclopedia page for {READCOURTREVEALDAYS} days.",
                        ("READCOURTREVEALDAYS", EspionageConstants.ReadCourtRevealDays));
                case SpyMissionType.SabotageGarrison:
                    return DiText.T("DI_INTEL_THE_GARRISON_LOSES_OF_ITS_MEN_PERCENT_2",
                        "The garrison loses {PERCENT} of its men; siege engines being built there are burned.",
                        ("PERCENT", Percent(EspionageConstants.SabotageGarrisonShare)));
                case SpyMissionType.SpreadDissent:
                    return DiText.T("DI_INTEL_THE_FIEF_LOSES_LOYALTY_DISSENTLOYALTYLOSS_2",
                        "The fief loses {DISSENTLOYALTYLOSS} loyalty.",
                        ("DISSENTLOYALTYLOSS", EspionageConstants.DissentLoyaltyLoss.ToString("0")));
                case SpyMissionType.StealTreasury:
                    return DiText.T("DI_INTEL_OF_THEIR_RULER_PURSE_AT_MOST_CAPITALISE_DENARS_2",
                        "{CAPITALISE} of their ruler's purse, at most {DENARS}, comes to us.",
                        ("CAPITALISE", Capitalise(Percent(EspionageConstants.StealTreasuryShare))),
                        ("DENARS", Denars(EspionageConstants.StealTreasuryCap)));
                case SpyMissionType.BribeLord:
                    return DiText.T("DI_INTEL_THE_LORD_KEEPS_THE_GOLD_THE_BRIBELOYALTYLOSS_2",
                        "The lord keeps the gold; the house loses {BRIBELOYALTYLOSS} loyalty and joins any rising in the next two years.",
                        ("BRIBELOYALTYLOSS", EspionageConstants.BribeLoyaltyLoss.ToString("0")));
                case SpyMissionType.ForgeLetters:
                    return DiText.T("DI_INTEL_THE_HOUSE_HOLDS_LETTERS_IN_THE_GRIEVANCEFORGEDLETTERS_2",
                        "The house holds letters in the ruler's hand against the crown: a grievance of {GRIEVANCEFORGEDLETTERS}.",
                        ("GRIEVANCEFORGEDLETTERS", IntrigueConstants.GrievanceForgedLetters.ToString("0")));
                case SpyMissionType.Assassinate:
                    return DiText.T("DI_INTEL_THE_LORD_DIES_2", "The lord dies.");
                default: return string.Empty;
            }
        }

        private static string SuccessText(SpyMissionType type, Kingdom target, string mark)
        {
            switch (type)
            {
                case SpyMissionType.SabotageGarrison:
                    return mark + " loses " + Percent(EspionageConstants.SabotageGarrisonShare) + " of its garrison; siege engines being built there are burned.";
                case SpyMissionType.SpreadDissent:
                    return mark + " loses " + EspionageConstants.DissentLoyaltyLoss.ToString("0") + " loyalty.";
                case SpyMissionType.BribeLord:
                    return DiText.T("DI_INTEL_KEEPS_THE_GOLD_THE_HOUSE_LOSES_MARK_BRIBELOYALTYLOSS_NAME_2",
                        "{MARK} keeps the gold; the house loses {BRIBELOYALTYLOSS} loyalty and joins any rising in {NAME} in the next two years.",
                        ("MARK", mark),
                        ("BRIBELOYALTYLOSS", EspionageConstants.BribeLoyaltyLoss.ToString("0")),
                        ("NAME", target.Name));
                case SpyMissionType.ForgeLetters:
                    return DiText.T("DI_INTEL_HOUSE_HOLDS_FORGED_LETTERS_AGAINST_THE_MARK_2",
                        "{MARK}'s house holds forged letters against the crown: a grievance of {GRIEVANCEFORGEDLETTERS}.",
                        ("MARK", mark),
                        ("GRIEVANCEFORGEDLETTERS", IntrigueConstants.GrievanceForgedLetters.ToString("0")));
                case SpyMissionType.Assassinate:
                    return mark + " dies.";
                default:
                    return EffectOf(type);
            }
        }

        /// <summary>What being caught costs (design 03 §5), from the constants exposure applies.</summary>
        private static string CaughtText(SpyMissionType type, Kingdom target)
        {
            var text = DiText.T("DI_INTEL_THE_NETWORK_IN_IS_LOST_GAINS_NAME_NAME_2_LEGITIMACY_2",
                "The network in {NAME} is lost, {NAME_2} gains a casus belli against us (Espionage exposed, {LEGITIMACY}) and trusts us {EXPOSUREVICTIMTRUST} less.",
                ("NAME", target.Name),
                ("NAME_2", target.Name),
                ("LEGITIMACY", DiplomacyIntrigue.Diplomacy.CasusBelli.Legitimacy(CasusBelliType.EspionageExposed).ToString("0.00")),
                ("EXPOSUREVICTIMTRUST", (-EspionageConstants.ExposureVictimTrust).ToString("0")));
            if (type == SpyMissionType.Assassinate)
                text += DiText.T("DI_INTEL_EVERY_OTHER_REALM_TRUSTS_US_LESS_2",
                    " Every other realm trusts us {EXPOSUREASSASSINATIONOBSERVERTRUST} less as well.",
                    ("EXPOSUREASSASSINATIONOBSERVERTRUST", (-EspionageConstants.ExposureAssassinationObserverTrust).ToString("0")));
            return text;
        }

        internal static Color KingdomColor(Kingdom k)
            => k == null ? MutedColor : Color.FromUint(k.Color);

        // ASCII hyphen, not U+2212: the game's Fira Sans has no minus sign and draws it as an underscore.
        internal static string Signed(float x) => (x >= 0f ? "+" : "-") + Math.Abs(x).ToString("0.00");

        internal static string Denars(int n) => n.ToString("N0");

        private static string Days(int n) => n + (n == 1 ? " day" : " days");

        private static string More(int n) => n + (n == 1 ? " more day" : " more days");

        private static string Percent(float share) => (share * 100f).ToString("0") + "%";

        private static string Capitalise(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Every command runs from the UI, outside any campaign handler's try: each catches its own.</summary>
        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error("UI", what + " failed.", ex);
            }
        }

        private void SetText(ref string field, string value, string name)
        {
            value = value ?? string.Empty;
            if (value == field) return;
            field = value;
            OnPropertyChangedWithValue(value, name);
        }

        private void SetBool(ref bool field, bool value, string name)
        {
            if (value == field) return;
            field = value;
            OnPropertyChangedWithValue(value, name);
        }

        private void SetInt(ref int field, int value, string name)
        {
            if (value == field) return;
            field = value;
            OnPropertyChangedWithValue(value, name);
        }

        private void SetColor(ref Color field, Color value, string name)
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChangedWithValue(value, name);
        }

        private void SetList<T>(ref MBBindingList<T> field, MBBindingList<T> value, string name) where T : ViewModel
        {
            if (value == field) return;
            field = value;
            OnPropertyChangedWithValue(value, name);
        }
    }

    // ================= rows =================

    internal sealed class DiNetworkRowVM : ViewModel
    {
        private readonly Action _onSelect;

        public DiNetworkRowVM(string name, Color accent, string strengthText, string trendText, Color trendColor,
            string handlerText, int strengthAmount, int ceilingAmount, bool hasCeiling, bool isSelected, Action onSelect)
        {
            Name = name; AccentColor = accent; StrengthText = strengthText; TrendText = trendText; TrendColor = trendColor;
            HandlerText = handlerText; StrengthAmount = strengthAmount; CeilingAmount = ceilingAmount; HasCeiling = hasCeiling;
            IsSelected = isSelected; _onSelect = onSelect;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public string StrengthText { get; }
        [DataSourceProperty] public string TrendText { get; }
        [DataSourceProperty] public Color TrendColor { get; }
        [DataSourceProperty] public string HandlerText { get; }
        [DataSourceProperty] public int StrengthAmount { get; }
        [DataSourceProperty] public int CeilingAmount { get; }
        [DataSourceProperty] public bool HasCeiling { get; }
        [DataSourceProperty] public bool IsSelected { get; }

        public void OnSelect() => _onSelect?.Invoke();
    }

    internal sealed class DiTermVM : ViewModel
    {
        public DiTermVM(string label, string value, Color color) { Label = label; Value = value; ValueColor = color; }

        [DataSourceProperty] public string Label { get; }
        [DataSourceProperty] public string Value { get; }
        [DataSourceProperty] public Color ValueColor { get; }
    }

    internal sealed class DiMissionRowVM : ViewModel
    {
        private readonly Action _onSelect;

        public DiMissionRowVM(string label, string costText, string daysText, string oddsText, Color oddsColor,
            Color nameColor, bool isSelected, Action onSelect)
        {
            Label = label; CostText = costText; DaysText = daysText; OddsText = oddsText; OddsColor = oddsColor;
            NameColor = nameColor; IsSelected = isSelected; _onSelect = onSelect;
        }

        [DataSourceProperty] public string Label { get; }
        [DataSourceProperty] public string CostText { get; }
        [DataSourceProperty] public string DaysText { get; }
        [DataSourceProperty] public string OddsText { get; }
        [DataSourceProperty] public Color OddsColor { get; }
        [DataSourceProperty] public Color NameColor { get; }
        [DataSourceProperty] public bool IsSelected { get; }

        public void OnSelect() => _onSelect?.Invoke();
    }

    internal sealed class DiOperationVM : ViewModel
    {
        private readonly Action _onCancel;

        public DiOperationVM(string title, string line, string cancelText, Action onCancel)
        {
            Title = title; Line = line; CancelText = cancelText; _onCancel = onCancel;
        }

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string Line { get; }
        [DataSourceProperty] public string CancelText { get; }

        public void ExecuteCancel() => _onCancel?.Invoke();
    }

    internal sealed class DiReportVM : ViewModel
    {
        public DiReportVM(string title, string line, string body) { Title = title; Line = line; Body = body; }

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string Line { get; }
        [DataSourceProperty] public string Body { get; }
    }

    internal sealed class DiMarkVM : ViewModel
    {
        private readonly Action _onSelect;

        public DiMarkVM(string name, string line, bool isSelected, Action onSelect)
        {
            Name = name; Line = line; IsSelected = isSelected; _onSelect = onSelect;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Line { get; }
        [DataSourceProperty] public bool IsSelected { get; }

        public void OnSelect() => _onSelect?.Invoke();
    }

    internal sealed class DiPickRealmVM : ViewModel
    {
        private readonly Action _onSelect;

        public DiPickRealmVM(string name, string line, Color accent, bool isSelected, Action onSelect)
        {
            Name = name; Line = line; AccentColor = accent; IsSelected = isSelected; _onSelect = onSelect;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Line { get; }
        [DataSourceProperty] public Color AccentColor { get; }
        [DataSourceProperty] public bool IsSelected { get; }

        public void OnSelect() => _onSelect?.Invoke();
    }

    internal sealed class DiPickMemberVM : ViewModel
    {
        private readonly Action _onSelect;

        public DiPickMemberVM(string name, string roguery, string charm, string ceilingText, Color color, bool isSelected, Action onSelect)
        {
            Name = name; RogueryText = roguery; CharmText = charm; CeilingText = ceilingText; NameColor = color;
            IsSelected = isSelected; _onSelect = onSelect;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string RogueryText { get; }
        [DataSourceProperty] public string CharmText { get; }
        [DataSourceProperty] public string CeilingText { get; }
        [DataSourceProperty] public Color NameColor { get; }
        [DataSourceProperty] public bool IsSelected { get; }

        public void OnSelect() => _onSelect?.Invoke();
    }
}
