using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Espionage
{
    /// <summary>One row of design 03 §2's table.</summary>
    public sealed class MissionSpec
    {
        public SpyMissionType Type;
        public float Required;
        public int Gold;
        public int Days;
        public bool NeedsHero;
        public bool NeedsSettlement;

        /// <summary>
        /// The lord must head a sworn house of the target's court other than the crown's own: what
        /// the mission changes is that house's standing with its crown (step 3.5), and a mercenary,
        /// the ruling house or a house's younger son has no such standing to change.
        /// </summary>
        public bool NeedsHouseHead;

        /// <summary>Null when the mission can be run; otherwise why not yet.</summary>
        public string NotYet;

        /// <summary>(requirement - 15) / 200 - see <see cref="EspionageConstants.MissionDifficultyFloor"/>.</summary>
        public float Difficulty
            => Math.Max(0f, (Required - EspionageConstants.MissionDifficultyFloor) * EspionageConstants.MissionDifficultyPerRequirement);
    }

    /// <summary>A mission's odds, term by term (design 03 §4). What resolution rolls, and what every screen shows.</summary>
    public sealed class MissionOdds
    {
        public float Network;
        public float HandlerSkill;
        public float CounterIntelligence;
        public float Difficulty;

        public float FromNetwork;
        public float FromHandler;
        public float FromCounterIntelligence;

        /// <summary>Before the 0.05-0.95 clamp.</summary>
        public float RawSuccess;
        public float Success;

        /// <summary>Chance of exposure if it fails.</summary>
        public float ExposureOnFailure;

        /// <summary>Chance of exposure at launch: (1 - success) x exposure on failure.</summary>
        public float Exposure => (1f - Success) * ExposureOnFailure;
    }

    /// <summary>
    /// Covert operations: launching, resolving, and what each one does (design 03 §2, §4, step 3.2).
    ///
    /// **One resolver for the odds.** <see cref="OddsOf"/> is what the roll uses, what the
    /// diagnostic prints, and what the mission board (3.7) and the AI (3.6) will read - design 03
    /// §7: "the AI reads the same numbers; nothing is hidden from the player that the AI gets to use".
    ///
    /// No "is this the player" argument decides anything here. The player appears only in who is
    /// told: an operation against the player's realm is announced when it lands or is caught (the
    /// lead's decision 2, "clearly telegraphed"), and the player's own operations report back.
    ///
    /// BribeLord and ForgeLetters are step 3.5, the two whose effects land in Phase 2 state: a
    /// bribe is read by <see cref="Bribes"/> into loyalty and the internal war, and forged letters
    /// are an ordinary grievance in <see cref="GrievanceRegistry"/>. Espionage keeps no state of
    /// its own for either (design 03 §6).
    /// </summary>
    public static class Missions
    {
        // ----- The table ------------------------------------------------------

        private static readonly Dictionary<SpyMissionType, MissionSpec> Specs = new Dictionary<SpyMissionType, MissionSpec>
        {
            { SpyMissionType.ScoutArmies, new MissionSpec { Type = SpyMissionType.ScoutArmies, Required = 15, Gold = 1000, Days = 3 } },
            { SpyMissionType.ReadCourt, new MissionSpec { Type = SpyMissionType.ReadCourt, Required = 25, Gold = 2000, Days = 5 } },
            { SpyMissionType.SabotageGarrison, new MissionSpec { Type = SpyMissionType.SabotageGarrison, Required = 35, Gold = 5000, Days = 7, NeedsSettlement = true } },
            { SpyMissionType.SpreadDissent, new MissionSpec { Type = SpyMissionType.SpreadDissent, Required = 30, Gold = 4000, Days = 10, NeedsSettlement = true } },
            { SpyMissionType.BribeLord, new MissionSpec { Type = SpyMissionType.BribeLord, Required = 45, Gold = 25000, Days = 14, NeedsHero = true, NeedsHouseHead = true } },
            // §2 gives ForgeLetters no target. It takes one, the lead's call of 2026-09-25: the
            // letters are shown to a lord the player chooses, as a bribe is paid to one.
            { SpyMissionType.ForgeLetters, new MissionSpec { Type = SpyMissionType.ForgeLetters, Required = 50, Gold = 15000, Days = 14, NeedsHero = true, NeedsHouseHead = true } },
            { SpyMissionType.StealTreasury, new MissionSpec { Type = SpyMissionType.StealTreasury, Required = 40, Gold = 3000, Days = 7 } },
            { SpyMissionType.Assassinate, new MissionSpec { Type = SpyMissionType.Assassinate, Required = 70, Gold = 60000, Days = 21, NeedsHero = true } },
        };

        public static MissionSpec SpecOf(SpyMissionType type) => Specs.TryGetValue(type, out var spec) ? spec : null;

        public static IEnumerable<MissionSpec> AllSpecs => Specs.Values;

        // ----- Odds -----------------------------------------------------------

        /// <summary>
        /// The odds of <paramref name="type"/> for this network now. With no handler the handler
        /// term is zero; launching needs one anyway.
        /// </summary>
        public static MissionOdds OddsOf(ModState state, SpyNetwork network, SpyMissionType type)
        {
            var o = new MissionOdds();
            var spec = SpecOf(type);
            if (network == null || spec == null) return o;

            var handler = network.Handler;
            o.Network = network.Strength;
            o.HandlerSkill = handler == null ? 0f
                : (handler.GetSkillValue(DefaultSkills.Roguery) + handler.GetSkillValue(DefaultSkills.Charm)) / 2f;
            o.CounterIntelligence = CounterIntelligence.Of(state, network.Target);
            o.Difficulty = spec.Difficulty;

            o.FromNetwork = o.Network * EspionageConstants.MissionChancePerNetwork;
            o.FromHandler = o.HandlerSkill * EspionageConstants.MissionChancePerHandlerSkill;
            o.FromCounterIntelligence = o.CounterIntelligence * EspionageConstants.MissionChancePerCounterIntelligence;

            o.RawSuccess = EspionageConstants.MissionBaseChance + o.FromNetwork + o.FromHandler
                           - o.FromCounterIntelligence - o.Difficulty;
            o.Success = Clamp(o.RawSuccess, EspionageConstants.MissionChanceMin, EspionageConstants.MissionChanceMax);

            o.ExposureOnFailure = Clamp(EspionageConstants.ExposureBase
                                        + EspionageConstants.ExposurePerCounterIntelligence * o.CounterIntelligence
                                        - EspionageConstants.ExposurePerNetwork * o.Network,
                                        EspionageConstants.ExposureMin, EspionageConstants.ExposureMax);
            return o;
        }

        // ----- Reading --------------------------------------------------------

        /// <summary>The operation this network is running, if any. A handler runs one at a time.</summary>
        public static SpyMission PendingOn(ModState state, Clan owner, Kingdom target)
        {
            if (state == null) return null;
            for (var i = 0; i < state.SpyMissions.Count; i++)
            {
                var m = state.SpyMissions[i];
                if (m.IsPending && m.Owner == owner && m.Target == target) return m;
            }
            return null;
        }

        /// <summary>
        /// Whether <paramref name="owner"/> currently sees <paramref name="target"/> through a
        /// successful ScoutArmies or ReadCourt: derived from the mission's own record, so a reveal
        /// can never outlive or disagree with the operation that bought it.
        /// </summary>
        public static bool IsRevealed(ModState state, Clan owner, Kingdom target, SpyMissionType type)
            => RevealDaysLeft(state, owner, target, type) > 0f;

        /// <summary>
        /// Days the reveal has left, from the latest success of that kind; 0 when there is none.
        /// The same reading as <see cref="IsRevealed"/>, so a screen that says "for 3 more days"
        /// cannot disagree with whether it shows anything at all.
        /// </summary>
        public static float RevealDaysLeft(ModState state, Clan owner, Kingdom target, SpyMissionType type)
        {
            if (state == null || owner == null || target == null) return 0f;
            var days = type == SpyMissionType.ScoutArmies ? EspionageConstants.ScoutArmiesRevealDays
                     : type == SpyMissionType.ReadCourt ? EspionageConstants.ReadCourtRevealDays : 0;
            if (days == 0) return 0f;

            var best = 0f;
            for (var i = 0; i < state.SpyMissions.Count; i++)
            {
                var m = state.SpyMissions[i];
                if (m.Type != type || m.Outcome != MissionOutcome.Success) continue;
                if (m.Owner != owner || m.Target != target) continue;
                var left = days - (float)(CampaignTime.Now - m.ResolvedOn).ToDays;
                if (left > best) best = left;
            }
            return best;
        }

        // ----- Launching ------------------------------------------------------

        public static bool CanLaunch(ModState state, Clan owner, Kingdom target, SpyMissionType type,
            Hero targetHero, Settlement targetSettlement, out string reason)
        {
            if (!CanPlan(state, owner, target, type, out reason)) return false;
            var spec = SpecOf(type);

            if (spec.NeedsSettlement)
            {
                if (targetSettlement == null || targetSettlement.Town == null) { reason = type + " needs a town or castle."; return false; }
                if (targetSettlement.OwnerClan?.Kingdom != target) { reason = targetSettlement.Name + " is not held by " + target.Name + "."; return false; }
            }

            if (spec.NeedsHero)
            {
                if (targetHero == null || !targetHero.IsAlive) { reason = type + " needs a living lord."; return false; }
                if (targetHero.Clan?.Kingdom != target) { reason = targetHero.Name + " is not of " + target.Name + "."; return false; }
                if (targetHero.Clan == owner) { reason = targetHero.Name + " is of our own house."; return false; }
                if (spec.NeedsHouseHead && !IsHouseHead(targetHero, target, out var why))
                {
                    reason = type + " needs the head of a sworn house: " + why;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Every gate of <see cref="CanLaunch"/> that does not depend on the mark: the network, its
        /// handler, an operation already running, the strength and the purse. The Intelligence tab's
        /// board reads this before a mark is chosen, so the board and the launch are one set of rules
        /// rather than two that could drift apart.
        /// </summary>
        public static bool CanPlan(ModState state, Clan owner, Kingdom target, SpyMissionType type, out string reason)
        {
            reason = null;
            var spec = SpecOf(type);
            if (spec == null) { reason = "No such mission."; return false; }
            if (spec.NotYet != null) { reason = type + " cannot be run yet: " + spec.NotYet + "."; return false; }

            var network = SpyNetworks.Get(state, owner, target);
            if (network == null) { reason = owner?.Name + " has no network in " + target?.Name + "."; return false; }

            var week = SpyNetworks.Explain(state, network);
            if (week.Idle != null) { reason = "The network is idle: " + week.Idle + "."; return false; }

            if (PendingOn(state, owner, target) != null) { reason = "The handler is already running an operation there."; return false; }

            if (network.Strength < spec.Required)
            {
                reason = type + " needs a network of " + spec.Required.ToString("0") + "; this one is "
                         + network.Strength.ToString("0.0") + ".";
                return false;
            }

            var purse = owner.Leader == null ? 0 : owner.Leader.Gold;
            if (purse < spec.Gold) { reason = type + " costs " + spec.Gold + "; " + owner.Leader?.Name + " holds " + purse + "."; return false; }
            return true;
        }

        /// <summary>
        /// Whether <paramref name="hero"/> leads a house sworn to <paramref name="target"/> that is
        /// not the crown's. Checked at launch and again before the roll, since fourteen days is
        /// long enough for a lord to die, be succeeded, or lead the house to another crown.
        /// </summary>
        private static bool IsHouseHead(Hero hero, Kingdom target, out string why)
        {
            why = null;
            if (hero == null || !hero.IsAlive) { why = (hero == null ? "the lord" : hero.Name.ToString()) + " is dead."; return false; }
            var clan = hero.Clan;
            if (clan == null || clan.Leader != hero) { why = hero?.Name + " is not the head of a house."; return false; }
            if (clan.Kingdom != target) { why = clan.Name + " is not of " + target?.Name + "."; return false; }
            if (clan == target.RulingClan) { why = clan.Name + " is the crown's own house."; return false; }
            if (!Court.IsMember(clan)) { why = clan.Name + " serves for pay and has no place at court."; return false; }
            return true;
        }

        /// <summary>Pays for and starts an operation. Returns it, or null with the reason.</summary>
        public static SpyMission Launch(ModState state, Clan owner, Kingdom target, SpyMissionType type,
            Hero targetHero, Settlement targetSettlement, out string reason)
        {
            if (!CanLaunch(state, owner, target, type, targetHero, targetSettlement, out reason)) return null;

            var spec = SpecOf(type);
            var network = SpyNetworks.Get(state, owner, target);
            owner.Leader.ChangeHeroGold(-spec.Gold);

            var mission = new SpyMission(owner, target, type, network.Handler,
                spec.NeedsHero ? targetHero : null, spec.NeedsSettlement ? targetSettlement : null, spec.Gold, spec.Days);
            state.SpyMissions.Add(mission);

            var odds = OddsOf(state, network, type);
            Log.Info("Espionage", owner.Name + " launched " + mission + ": " + spec.Gold + " paid, resolves in "
                                  + spec.Days + " days, success " + Pct(odds.Success) + ", exposure if it fails "
                                  + Pct(odds.ExposureOnFailure) + ".");
            if (owner == Clan.PlayerClan)
                Log.Notify(network.Handler?.Name + " begins " + Describe(type) + " in " + target.Name + ": "
                           + spec.Days + " days, " + Pct(odds.Success) + " to succeed.", Colors.Cyan);
            return mission;
        }

        /// <summary>
        /// Calls an operation off before it resolves. The payment is gone (design 03 §7); the
        /// network is not charged, since nothing was attempted.
        /// </summary>
        public static bool Cancel(ModState state, SpyMission mission)
        {
            if (state == null || mission == null || !mission.IsPending) return false;
            state.SpyMissions.Remove(mission);
            Log.Info("Espionage", "Called off: " + mission + " (" + mission.GoldPaid + " lost).");
            return true;
        }

        // ----- Resolving ------------------------------------------------------

        /// <summary>
        /// Once a day: resolves every operation whose time has come, and drops resolved ones that
        /// no reader needs any more.
        /// </summary>
        public static void DailyTick(ModState state)
        {
            if (state == null) return;

            var due = new List<SpyMission>();
            for (var i = 0; i < state.SpyMissions.Count; i++)
                if (state.SpyMissions[i].IsPending && state.SpyMissions[i].ResolvesOn.IsPast) due.Add(state.SpyMissions[i]);

            for (var i = 0; i < due.Count; i++)
            {
                try
                {
                    Resolve(state, due[i], null);
                }
                catch (Exception ex)
                {
                    Log.Error("Espionage", "Resolving " + due[i] + " failed.", ex);
                }
            }

            state.SpyMissions.RemoveAll(m => !m.IsPending && (CampaignTime.Now - m.ResolvedOn).ToDays > KeepDays(m));
        }

        /// <summary>
        /// How long a resolved operation stays on the record. Long enough for every reader of it:
        /// a successful bribe is read by <see cref="Bribes"/> for its whole window, the rest only
        /// by the reveals and the operations list.
        /// </summary>
        internal static int KeepDays(SpyMission mission)
            => mission.Type == SpyMissionType.BribeLord && mission.Outcome == MissionOutcome.Success
                ? EspionageConstants.BribeWindowDays
                : EspionageConstants.ResolvedMissionKeepDays;

        /// <summary>
        /// Rolls an operation (design 03 §4) and applies what came of it. <paramref name="forced"/>
        /// skips the roll, for <c>diplomacy.test_resolve_mission</c> only: everything after the roll
        /// is the same code whichever way the outcome was reached.
        /// </summary>
        public static MissionOutcome Resolve(ModState state, SpyMission mission, MissionOutcome? forced)
        {
            if (state == null || mission == null || !mission.IsPending) return MissionOutcome.Pending;

            var network = SpyNetworks.Get(state, mission.Owner, mission.Target);

            // The handler who ran it is gone - dead, captured, called home. Nobody is left to bring
            // it off, and nobody is left to be caught: it simply fails.
            if (network == null || network.Handler == null || network.Handler != mission.Handler)
            {
                mission.Resolve(MissionOutcome.Failure);
                Log.Info("Espionage", "Failed for want of a handler: " + mission + ".");
                TellOwner(mission, Describe(mission.Type) + " in " + mission.Target.Name + " came to nothing: nobody was left to run it.");
                return MissionOutcome.Failure;
            }

            // The lord a bribe or a forgery was meant for no longer heads a house of that court -
            // dead, succeeded, gone to another crown. Like a lost handler, it fails before any
            // roll: nobody was approached, so nobody can be caught, and a Success on the record
            // always means the effect landed.
            var spec = SpecOf(mission.Type);
            if (spec != null && spec.NeedsHouseHead && !IsHouseHead(mission.TargetHero, mission.Target, out var gone))
            {
                mission.Resolve(MissionOutcome.Failure);
                Log.Info("Espionage", "Failed for want of a mark: " + mission + " - " + gone);
                TellOwner(mission, Describe(mission.Type) + " in " + mission.Target.Name + " came to nothing: " + gone);
                return MissionOutcome.Failure;
            }

            var odds = OddsOf(state, network, mission.Type);
            MissionOutcome outcome;
            if (forced.HasValue) outcome = forced.Value;
            else if (MBRandom.RandomFloat < odds.Success) outcome = MissionOutcome.Success;
            else outcome = MBRandom.RandomFloat < odds.ExposureOnFailure ? MissionOutcome.Exposed : MissionOutcome.Failure;

            var note = " (success was " + Pct(odds.Success) + ", exposure on failure " + Pct(odds.ExposureOnFailure)
                       + (forced.HasValue ? ", outcome forced by a test lever" : "") + ")";

            // A bribe that reaches the player's own house is the player's to take or refuse (the
            // lead's call for 3.6, design 03 §9 decision 12). For an AI lord the successful roll is
            // the lord taking the gold; for the player, the roll only gets the offer to them. It
            // stays pending until they answer: an inquiry stops the clock, and a save made while it
            // is open reloads with the operation still due, so it is simply asked again.
            if (outcome == MissionOutcome.Success && mission.Type == SpyMissionType.BribeLord
                && mission.TargetHero != null && mission.TargetHero == Hero.MainHero)
            {
                if (!_askingPlayer) OfferBribeToPlayer(state, mission, note);
                return MissionOutcome.Pending;
            }

            Conclude(state, mission, network, outcome, note);
            return outcome;
        }

        /// <summary>Everything after the roll: the record, the network's cost, and the effect.</summary>
        private static void Conclude(ModState state, SpyMission mission, SpyNetwork network, MissionOutcome outcome, string note)
        {
            mission.Resolve(outcome);
            Log.Info("Espionage", "Resolved: " + mission + note + ".");

            switch (outcome)
            {
                case MissionOutcome.Success:
                    SpyNetworks.Spend(network, EspionageConstants.MissionSuccessNetworkCost);
                    ApplyEffect(state, mission);
                    break;
                case MissionOutcome.Failure:
                    SpyNetworks.Spend(network, EspionageConstants.MissionFailureNetworkCost);
                    TellOwner(mission, Describe(mission.Type) + " in " + mission.Target.Name + " failed. The network paid for it.");
                    break;
                case MissionOutcome.Exposed:
                    SpyNetworks.Spend(network, network.Strength);
                    Exposure.Apply(state, mission, network);
                    break;
            }
        }

        // ----- A bribe offered to the player ---------------------------------------

        private static bool _askingPlayer;

        /// <summary>Clears the one-at-a-time guard; a new campaign or a load must not inherit an unanswered question.</summary>
        public static void Reset() => _askingPlayer = false;

        /// <summary>
        /// A foreign network's agents reach the player's house with gold. Taking it is the same
        /// bargain an AI lord makes: the gold, and while it holds, loyalty -20 and the rising's side
        /// if the realm goes to war with itself. Refusing turns the agents away, and the operation
        /// is a failure like any other.
        /// </summary>
        private static void OfferBribeToPlayer(ModState state, SpyMission mission, string note)
        {
            _askingPlayer = true;
            var realm = mission.Target;
            var buyer = mission.Owner;
            var buyerRealm = buyer.Kingdom;
            var body = "Agents of " + buyer.Name + (buyerRealm != null ? " of " + buyerRealm.Name : "")
                       + " offer you " + mission.GoldPaid.ToString("N0") + " denars."
                       + Environment.NewLine + Environment.NewLine
                       + "In return, if " + realm.Name + " goes to war with itself in the next two years, your house stands with "
                       + "the rising against " + realm.Leader?.Name + ". While the bargain holds, your house's loyalty to the crown "
                       + "is " + EspionageConstants.BribeLoyaltyLoss.ToString("0") + " lower, where your court can see it - though "
                       + "not whose gold it was.";

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "Foreign gold",
                    body,
                    true, true,
                    "Take " + mission.GoldPaid.ToString("N0"), "Turn them away",
                    () => AnswerBribe(state, mission, true, note),
                    () => AnswerBribe(state, mission, false, note)), true);
            }
            catch (Exception ex)
            {
                _askingPlayer = false;
                Log.Error("Espionage", "Could not show the bribe offer to the player.", ex);
            }
        }

        /// <summary>Runs from the UI, outside any campaign handler's try, so it catches its own.</summary>
        private static void AnswerBribe(ModState state, SpyMission mission, bool accepted, string note)
        {
            try
            {
                _askingPlayer = false;
                if (!mission.IsPending) return;

                // The answer can come after the world moved: the same checks the roll made.
                var network = SpyNetworks.Get(state, mission.Owner, mission.Target);
                if (network == null || network.Handler == null || network.Handler != mission.Handler
                    || !IsHouseHead(mission.TargetHero, mission.Target, out _))
                {
                    mission.Resolve(MissionOutcome.Failure);
                    Log.Info("Espionage", "The bribe offered to the player lapsed before the answer: " + mission + ".");
                    if (accepted) Log.Notify("The agents are gone - the offer no longer stands.", Colors.Red);
                    return;
                }

                if (!accepted) Log.Info("Espionage", "The player turned away " + mission.Owner.Name + "'s agents.");
                Conclude(state, mission, network, accepted ? MissionOutcome.Success : MissionOutcome.Failure,
                         note + (accepted ? ", taken by the player" : ", refused by the player"));
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Answering the bribe offer failed.", ex);
            }
        }

        // ----- Effects --------------------------------------------------------

        private static void ApplyEffect(ModState state, SpyMission mission)
        {
            var target = mission.Target;
            switch (mission.Type)
            {
                case SpyMissionType.ScoutArmies:
                {
                    var report = ArmiesReport(target);
                    Log.Info("Espionage", mission.Owner.Name + " has " + target.Name + "'s armies for "
                                          + EspionageConstants.ScoutArmiesRevealDays + " days: " + report);
                    TellOwner(mission, "Our agents report " + target.Name + "'s forces: " + report);
                    break;
                }
                case SpyMissionType.ReadCourt:
                {
                    var report = CourtReport(state, target);
                    Log.Info("Espionage", mission.Owner.Name + " reads " + target.Name + "'s court for "
                                          + EspionageConstants.ReadCourtRevealDays + " days: " + report);
                    TellOwner(mission, "Our agents read " + target.Name + "'s court: " + report);
                    break;
                }
                case SpyMissionType.SabotageGarrison:
                {
                    var removed = Sabotage(mission.TargetSettlement);
                    Log.Info("Espionage", "Sabotage at " + mission.TargetSettlement.Name + ": " + removed + " of the garrison gone.");
                    TellOwner(mission, "Sabotage at " + mission.TargetSettlement.Name + ": " + removed + " of its garrison are gone.");
                    TellVictim(mission, "Saboteurs struck the garrison of " + mission.TargetSettlement.Name + ": " + removed + " men lost.");
                    break;
                }
                case SpyMissionType.SpreadDissent:
                {
                    var town = mission.TargetSettlement.Town;
                    var before = town.Loyalty;
                    town.Loyalty = Math.Max(0f, before - EspionageConstants.DissentLoyaltyLoss);
                    Log.Info("Espionage", "Dissent in " + mission.TargetSettlement.Name + ": loyalty "
                                          + before.ToString("0") + " -> " + town.Loyalty.ToString("0") + ".");
                    TellOwner(mission, "Dissent spreads in " + mission.TargetSettlement.Name + ": loyalty "
                                       + before.ToString("0") + " -> " + town.Loyalty.ToString("0") + ".");
                    TellVictim(mission, "Agitators have been stirring " + mission.TargetSettlement.Name + ": loyalty "
                                        + before.ToString("0") + " -> " + town.Loyalty.ToString("0") + ".");
                    break;
                }
                case SpyMissionType.StealTreasury:
                {
                    var ruler = target.Leader;
                    var thief = mission.Owner.Leader;
                    var take = ruler == null ? 0 : Math.Min((int)(ruler.Gold * EspionageConstants.StealTreasuryShare),
                                                            EspionageConstants.StealTreasuryCap);
                    if (take > 0 && thief != null)
                        GiveGoldAction.ApplyBetweenCharacters(ruler, thief, take, mission.Owner != Clan.PlayerClan);
                    Log.Info("Espionage", mission.Owner.Name + " stole " + take + " from " + ruler?.Name + ".");
                    TellOwner(mission, "Our agents lifted " + take + " denars from " + ruler?.Name + "'s treasury.");
                    TellVictim(mission, take + " denars are missing from the treasury.");
                    break;
                }
                case SpyMissionType.BribeLord:
                {
                    // Resolve has already checked that the lord still heads a house of this court.
                    var lord = mission.TargetHero;
                    var clan = lord.Clan;

                    // The gold paid at launch reaches the lord who took it. Revised in 3.6: 3.5 left
                    // it spent on nobody, which made a bribe an offer with nothing in it the moment
                    // the player was the one asked (design 03 §10).
                    lord.ChangeHeroGold(mission.GoldPaid);
                    Log.Info("Espionage", clan.Name + " of " + target.Name + " is bought by " + mission.Owner.Name
                                          + " for " + EspionageConstants.BribeWindowDays + " days: loyalty -"
                                          + EspionageConstants.BribeLoyaltyLoss.ToString("0") + " (now "
                                          + LoyaltyModel.Of(state, clan).ToString("0.0")
                                          + "), and it takes the rising's side if the realm goes to war with itself.");
                    TellOwner(mission, lord.Name + " has taken our gold. " + clan.Name + " will stand against "
                                       + target.Leader?.Name + " if " + target.Name + " goes to war with itself in the next two years.");
                    if (lord == Hero.MainHero)
                        Log.Notify("You took " + mission.GoldPaid.ToString("N0") + " denars from agents of " + mission.Owner.Name
                                   + ". For two years your house stands with any rising against " + target.Leader?.Name + ".", Colors.Yellow);
                    else
                        TellVictim(mission, "Foreign gold has reached " + clan.Name + ". Its loyalty to the crown has fallen, "
                                            + "and nobody can say whose gold it was.");
                    break;
                }
                case SpyMissionType.ForgeLetters:
                {
                    var lord = mission.TargetHero;
                    var clan = lord.Clan;
                    var ruling = target.RulingClan;
                    GrievanceRegistry.Add(state, clan, ruling, GrievanceType.ForgedLetters, -1f,
                        "letters forged by agents of " + mission.Owner.Name);
                    TellOwner(mission, lord.Name + " has read our letters in " + target.Leader?.Name
                                       + "'s hand. " + clan.Name + " now holds them against the crown.");
                    TellVictim(mission, "Letters under " + target.Leader?.Name + "'s seal, never written by "
                                        + target.Leader?.Name + ", have reached " + clan.Name
                                        + ". The house holds them against the crown.");
                    break;
                }
                case SpyMissionType.Assassinate:
                {
                    var victim = mission.TargetHero;
                    if (victim == null || !victim.IsAlive)
                    {
                        Log.Info("Espionage", "The assassination found its mark already dead.");
                        break;
                    }
                    // No killer named: an assassination that is not exposed is not traced to anyone.
                    KillCharacterAction.ApplyByMurder(victim, null, true);
                    Log.Info("Espionage", victim.Name + " was assassinated by agents of " + mission.Owner.Name + ".");
                    TellOwner(mission, victim.Name + " is dead. Nobody knows who ordered it.");
                    break;
                }
            }
        }

        /// <summary>Removes a quarter of each regular troop line from the garrison. Returns how many men.</summary>
        private static int Sabotage(Settlement settlement)
        {
            var garrison = settlement?.Town?.GarrisonParty;
            if (garrison == null) return 0;

            var roster = garrison.MemberRoster;
            var removed = 0;
            var lines = roster.GetTroopRoster();
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line.Character == null || line.Character.IsHero) continue;
                var cut = (int)(line.Number * EspionageConstants.SabotageGarrisonShare);
                if (cut <= 0) continue;
                roster.AddToCounts(line.Character, -cut, false, 0, 0, true, -1);
                removed += cut;
            }
            return removed;
        }

        internal static string ArmiesReport(Kingdom target)
        {
            var sb = new StringBuilder();
            sb.Append("strength ").Append(target.CurrentTotalStrength.ToString("0"));
            var armies = 0;
            foreach (var army in target.Armies)
            {
                if (army?.LeaderParty == null) continue;
                armies++;
                sb.Append("; army of ").Append(army.LeaderParty.LeaderHero?.Name)
                  .Append(", ").Append(army.Parties.Count).Append(" parties, ")
                  .Append(army.TotalManCount).Append(" men, near ")
                  .Append(army.LeaderParty.LastVisitedSettlement?.Name?.ToString() ?? "the field");
            }
            if (armies == 0) sb.Append("; no army in the field");
            return sb.ToString();
        }

        private static string CourtReport(ModState state, Kingdom target)
        {
            var sb = new StringBuilder();
            sb.Append("legitimacy ").Append(LegitimacyRegistry.Of(state, target).ToString("0.0"));
            sb.Append(", worst war exhaustion ").Append(WarExhaustion.Worst(state, target).ToString("0.0"));
            foreach (var bloc in BlocModel.BlocsOf(state, target))
                sb.Append("; ").Append(bloc.Agenda).Append(" ").Append(bloc.Members.Count)
                  .Append(" houses, power ").Append(bloc.EffectivePower.ToString("0"));
            return sb.ToString();
        }

        // ----- Telling people -------------------------------------------------

        private static void TellOwner(SpyMission mission, string text)
        {
            if (mission.Owner == Clan.PlayerClan) Log.Notify(text, Colors.Cyan);
        }

        /// <summary>
        /// The lead's decision 2: an operation against the player's realm leaves a visible trace.
        /// A harm the player can see - a garrison thinned, a town stirred, a treasury lighter - is
        /// announced as it lands, though who did it stays unknown unless it is exposed.
        /// </summary>
        private static void TellVictim(SpyMission mission, string text)
        {
            if (Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom == mission.Target)
                Log.Notify(text, Colors.Red);
        }

        public static string Describe(SpyMissionType type)
        {
            switch (type)
            {
                case SpyMissionType.ScoutArmies: return "scouting the armies";
                case SpyMissionType.ReadCourt: return "reading the court";
                case SpyMissionType.SabotageGarrison: return "sabotaging a garrison";
                case SpyMissionType.SpreadDissent: return "spreading dissent";
                case SpyMissionType.BribeLord: return "bribing a lord";
                case SpyMissionType.ForgeLetters: return "forging letters";
                case SpyMissionType.StealTreasury: return "robbing the treasury";
                case SpyMissionType.Assassinate: return "an assassination";
                default: return type.ToString();
            }
        }

        internal static string Pct(float chance) => (chance * 100f).ToString("0") + "%";

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
