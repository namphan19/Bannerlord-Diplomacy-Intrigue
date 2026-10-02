using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace DiplomacyIntrigue.Espionage
{
    /// <summary>
    /// Why a hero is no longer a handler. A closed set, not a free-text reason: 3.12 counts
    /// handler losses per cause per year, and a reason read out of a sentence drifts the moment
    /// the sentence is reworded (CLAUDE.md §5). Every <see cref="SpyNetworks.Release"/> names one.
    ///
    /// Two of the eleven are not losses, and are kept in the same record rather than left to
    /// prose or dropped, because either would under-report the count AC4 is judged on
    /// (lead's call, 2026-10-01): <see cref="Exposed"/>, an operation traced back to the owner,
    /// and <see cref="Replaced"/>, the owner choosing a different hero - a decision, not a loss.
    /// </summary>
    public enum HandlerLossCause
    {
        /// <summary>The hero is still posted. Never passed to <see cref="SpyNetworks.Release"/>.</summary>
        None = 0,

        /// <summary>Vanilla gave them a party to lead. Should not happen: the veto in HandlerPostBehavior.</summary>
        Party,

        /// <summary>Vanilla made them governor of a town. Should not happen: ModClanPoliticsModel refuses.</summary>
        Governor,

        /// <summary>Taken prisoner.</summary>
        Prisoner,

        /// <summary>Died.</summary>
        Died,

        /// <summary>Left the clan that owned the network.</summary>
        LeftClan,

        /// <summary>Inherited the clan, and leads it, and a leader cannot go abroad.</summary>
        BecameHead,

        /// <summary>The owner recalled them on purpose.</summary>
        Recalled,

        /// <summary>Forced onto a party despite the veto - vanilla's second pass, which ignores it (story 3.8 R5).</summary>
        ForcedParty,

        /// <summary>Found outside the target realm and sent back to the station, rather than released (story 3.8 R4).</summary>
        LeftRealm,

        /// <summary>An operation of theirs was exposed; the network burned with the handler on it.</summary>
        Exposed,

        /// <summary>The owner put a different hero on the network. The old one is still alive and still posted abroad.</summary>
        Replaced,
    }

    /// <summary>A week of one network's upkeep, term by term. What the upkeep applies and what the diagnostic prints.</summary>
    public sealed class NetworkGrowthTerms
    {
        public SpyNetwork Network;

        /// <summary>Denars the upkeep would pay this week: the budget, or the purse if it is shorter.</summary>
        public int Spend;

        public float Roguery;
        public float Charm;

        /// <summary>(spend / 2000) x (1 + roguery / 200), before the wartime rate.</summary>
        public float FromGold;

        /// <summary>True while the owner's realm is at war with the target.</summary>
        public bool AtWar;

        /// <summary>The investment after the wartime rate.</summary>
        public float Investment;

        public float CounterIntelligence;
        public float FromCounterIntelligence;
        public float Attrition;

        /// <summary>The week's seven daily decays, shown here so the trend reads as one number.</summary>
        public float WeekOfDecay;

        /// <summary>Why the network cannot grow this week, if it cannot - no handler, or the owner now serves the target.</summary>
        public string Idle;

        public float Ceiling;

        /// <summary>The weekly sum, not counting the daily decay the daily tick applies on its own.</summary>
        public float Weekly => Investment - FromCounterIntelligence - Attrition;

        public float NetOverAWeek => Weekly - WeekOfDecay;
    }

    /// <summary>
    /// Every clan's spy networks: founding one, putting a handler on it, funding it, and the
    /// upkeep that grows and wears it (design 03 §1, step 3.1).
    ///
    /// The one place a network changes. Missions (3.2) will spend strength through here too, so
    /// a network cannot be moved by two paths that disagree about its ceiling.
    ///
    /// No "is this the player" argument anywhere, and none will be added: the lead's decision is
    /// that the AI runs operations on the player under the same rules (design 03 §9, decision 2).
    /// Which clans the AI chooses to run networks from is the AI's business at 3.6, not a rule here.
    /// </summary>
    public static class SpyNetworks
    {
        // ----- Reading --------------------------------------------------------

        public static SpyNetwork Get(ModState state, Clan owner, Kingdom target)
        {
            if (state == null) return null;
            for (var i = 0; i < state.SpyNetworks.Count; i++)
                if (state.SpyNetworks[i].Is(owner, target)) return state.SpyNetworks[i];
            return null;
        }

        public static List<SpyNetwork> OwnedBy(ModState state, Clan owner)
        {
            var list = new List<SpyNetwork>();
            if (state == null) return list;
            for (var i = 0; i < state.SpyNetworks.Count; i++)
                if (state.SpyNetworks[i].Owner == owner) list.Add(state.SpyNetworks[i]);
            return list;
        }

        public static List<SpyNetwork> In(ModState state, Kingdom target)
        {
            var list = new List<SpyNetwork>();
            if (state == null) return list;
            for (var i = 0; i < state.SpyNetworks.Count; i++)
                if (state.SpyNetworks[i].Target == target) list.Add(state.SpyNetworks[i]);
            return list;
        }

        /// <summary>The network this hero runs, if any. A hero handles one network at a time.</summary>
        public static SpyNetwork HandledBy(ModState state, Hero hero)
        {
            if (state == null || hero == null) return null;
            for (var i = 0; i < state.SpyNetworks.Count; i++)
                if (state.SpyNetworks[i].Handler == hero) return state.SpyNetworks[i];
            return null;
        }

        /// <summary>
        /// The most a handler can hold: 40 + roguery / 2 + charm / 4, never above 100. With no
        /// handler, the ceiling is what the network already has, so it can only wear down.
        /// </summary>
        public static float CeilingOf(Hero handler)
        {
            if (handler == null) return 0f;
            var ceiling = EspionageConstants.NetworkBaseCeiling
                          + handler.GetSkillValue(DefaultSkills.Roguery) * EspionageConstants.NetworkCeilingPerRoguery
                          + handler.GetSkillValue(DefaultSkills.Charm) * EspionageConstants.NetworkCeilingPerCharm;
            return ceiling > EspionageConstants.NetworkMaxStrength ? EspionageConstants.NetworkMaxStrength : ceiling;
        }

        /// <summary>
        /// Whether <paramref name="hero"/> may run a network of <paramref name="owner"/>'s in
        /// <paramref name="target"/>. A handler is stationed in the target realm, so the hero has
        /// to be free to go there: of the owning clan, grown, free, not the head of the clan, not
        /// leading a party, not governing a town, and not already on the road to some other post.
        /// A companion in the player's party qualifies.
        ///
        /// The three refusals added on 2026-10-01 (story 3.8 R1) close the hole run 09 found: a
        /// hero already travelling to take up a governorship has a null <c>GovernorOf</c> until the
        /// teleport lands 3-5 real seconds later, so the mod posted them abroad and vanilla then
        /// moved them home (Hajara, Simir). A hero in transit is going somewhere, and it is not
        /// here. <c>IsReleased</c> is read the same way vanilla reads it: it marks a hero who has
        /// just come out of captivity and is not yet back in play - <c>ClanPartiesVM</c> filters
        /// party-leader candidates on it, for the same reason.
        /// </summary>
        public static bool CanHandle(ModState state, Hero hero, Clan owner, Kingdom target, out string reason)
        {
            reason = null;
            if (hero == null || owner == null || target == null) { reason = "A hero, a clan and a realm are needed."; return false; }
            if (!IsFreeToGo(hero, owner, out reason)) return false;
            if (target.IsEliminated) { reason = target.Name + " is no more."; return false; }
            if (owner.Kingdom == target) { reason = owner.Name + " serves " + target.Name + ": a network works a foreign realm, not its own."; return false; }
            if (StationFor(target) == null) { reason = target.Name + " holds no town to station an agent in."; return false; }

            var current = HandledBy(state, hero);
            if (current != null && current.Target != target)
            {
                reason = hero.Name + " already runs " + owner.Name + "'s network in " + current.Target.Name + ".";
                return false;
            }
            return true;
        }

        // ----- Writing --------------------------------------------------------

        /// <summary>
        /// Puts <paramref name="hero"/> in charge of <paramref name="owner"/>'s network in
        /// <paramref name="target"/>, founding the network if there is none, and sends them to a
        /// town of that realm. A handler already on it is recalled first.
        /// </summary>
        public static SpyNetwork Assign(ModState state, Hero hero, Clan owner, Kingdom target, out string reason)
        {
            if (!CanHandle(state, hero, owner, target, out reason)) return null;

            var network = Get(state, owner, target);
            if (network == null)
            {
                network = new SpyNetwork(owner, target);
                state.SpyNetworks.Add(network);
            }
            if (network.Handler == hero) return network;
            if (network.Handler != null) Release(state, network, HandlerLossCause.Replaced, hero.Name.ToString());

            var station = StationFor(target);
            TeleportHeroAction.ApplyImmediateTeleportToSettlement(hero, station);
            network.SetHandler(hero);

            Log.Info("Espionage", owner.Name + " put " + hero.Name + " in charge of its network in " + target.Name
                                  + ", stationed at " + station.Name + " (ceiling " + CeilingOf(hero).ToString("0") + ").");
            return network;
        }

        /// <summary>
        /// The half of <see cref="CanHandle"/> that is about the hero alone, whatever the realm: of the
        /// owning clan, alive, grown, free, not the head of the clan, not leading a party, not
        /// governing, not on the road to another post. Split out on 2026-10-02 so the weekly
        /// telemetry can count a ruling house's free members by the very rule the AI's plan applies
        /// (story 3.12 §5: "at least half of the ruling houses that have a free member") rather than
        /// by a copy of it.
        /// </summary>
        public static bool IsFreeToGo(Hero hero, Clan owner, out string reason)
        {
            reason = null;
            if (hero == null || owner == null) { reason = "A hero and a clan are needed."; return false; }
            if (!hero.IsAlive || hero.IsDead) { reason = hero.Name + " is dead."; return false; }
            if (hero.Clan != owner) { reason = hero.Name + " is not of " + owner.Name + "."; return false; }
            if (hero.IsChild) { reason = hero.Name + " is a child."; return false; }
            if (hero.IsPrisoner) { reason = hero.Name + " is a prisoner."; return false; }
            if (hero.IsTraveling) { reason = hero.Name + " is on the way to a post."; return false; }
            if (hero.IsFugitive) { reason = hero.Name + " is a fugitive."; return false; }
            if (hero.IsReleased) { reason = hero.Name + " has just been let out of captivity."; return false; }
            if (hero == owner.Leader) { reason = hero.Name + " leads the clan and cannot go abroad as a handler."; return false; }
            if (hero.IsPartyLeader) { reason = hero.Name + " leads a party."; return false; }
            if (hero.GovernorOf != null) { reason = hero.Name + " governs " + hero.GovernorOf.Name + "."; return false; }
            return true;
        }

        /// <summary>
        /// Puts <paramref name="hero"/> on <paramref name="owner"/>'s network in
        /// <paramref name="target"/> **without** <see cref="CanHandle"/>'s "free to go abroad" rules -
        /// a clan head, a party leader, a governor, a hero on the road may all be put here - and
        /// without moving them. For <c>diplomacy.test_found_network</c> only, and nothing else may
        /// call it: the AI and the player go through <see cref="Assign"/>.
        ///
        /// Why it exists (story 3.10 §10, 2026-10-02): staging an AI operation on the player's house
        /// needs an AI ruling house with a network on the player's realm, and in every save tried
        /// each ruling house's free lords were already governing, leading a party or posted - the
        /// wall run 09 §4 found. <see cref="Assign"/> refuses there, correctly.
        ///
        /// What it does NOT bypass, because a network that broke them would make a later check lie
        /// about something other than eligibility: the hero is alive, grown, free and of the owning
        /// clan; the realm exists, is foreign to the owner and holds a town; the hero runs no other
        /// network. The hero is not teleported - moving a party leader out of their party or a
        /// governor out of their town is a state no rule produces. So the arrangement lasts until the
        /// next daily tick, which releases a busy hero through <see cref="StillHandles"/> or fetches
        /// a free one to the station: launch and force-resolve the operation before it.
        /// </summary>
        public static SpyNetwork AssignForTest(ModState state, Hero hero, Clan owner, Kingdom target, out string reason)
        {
            reason = null;
            if (state == null || hero == null || owner == null || target == null) { reason = "A hero, a clan and a realm are needed."; return null; }
            if (!hero.IsAlive || hero.IsDead) { reason = hero.Name + " is dead."; return null; }
            if (hero.Clan != owner) { reason = hero.Name + " is not of " + owner.Name + "."; return null; }
            if (hero.IsChild) { reason = hero.Name + " is a child."; return null; }
            if (hero.IsPrisoner) { reason = hero.Name + " is a prisoner."; return null; }
            if (target.IsEliminated) { reason = target.Name + " is no more."; return null; }
            if (owner.Kingdom == target) { reason = owner.Name + " serves " + target.Name + ": a network works a foreign realm, not its own."; return null; }
            if (StationFor(target) == null) { reason = target.Name + " holds no town to station an agent in."; return null; }
            var current = HandledBy(state, hero);
            if (current != null && current.Target != target)
            {
                reason = hero.Name + " already runs " + owner.Name + "'s network in " + current.Target.Name + ".";
                return null;
            }

            var network = Get(state, owner, target);
            if (network == null)
            {
                network = new SpyNetwork(owner, target);
                state.SpyNetworks.Add(network);
            }
            if (network.Handler == hero) return network;
            if (network.Handler != null) Release(state, network, HandlerLossCause.Replaced, hero.Name + " (test lever)");
            network.SetHandler(hero);

            Log.Info("Espionage", "TEST LEVER: " + hero.Name + " put on " + owner.Name + "'s network in " + target.Name
                                  + " without the handler rules and without moving them. The next daily tick releases or re-stations them.");
            return network;
        }

        /// <summary>Sets what the owner means to spend on a network each week, founding it if needed.</summary>
        public static SpyNetwork SetBudget(ModState state, Clan owner, Kingdom target, int weekly)
        {
            if (state == null || owner == null || target == null) return null;
            var network = Get(state, owner, target);
            if (network == null)
            {
                network = new SpyNetwork(owner, target);
                state.SpyNetworks.Add(network);
            }
            network.SetBudget(weekly);
            return network;
        }

        /// <summary>
        /// Takes the handler off a network, naming why in the closed set of
        /// <see cref="HandlerLossCause"/>. The hero stays where they were stationed, as a
        /// companion sent to a town does in vanilla; the owner fetches them. The network keeps its
        /// strength and wears down until someone else takes it on.
        ///
        /// Every loss writes a <c>handler_lost</c> record (story 3.8 R8). Run 09 could say 21
        /// handlers were lost and name three of the causes, because the reasons were prose: to
        /// count a cause per year, which is what 3.12's acceptance turns on, the cause has to be
        /// a field.
        /// </summary>
        public static void Release(ModState state, SpyNetwork network, HandlerLossCause cause, string detail = null)
        {
            if (network?.Handler == null) return;
            var hero = network.Handler;
            network.SetHandler(null);

            Telemetry.Event("handler_lost", "owner", network.Owner, "target", network.Target, "hero", hero,
                                  "cause", TokenOf(cause), "detail", detail);
            Log.Info("Espionage", hero.Name + " no longer runs " + network.Owner?.Name + "'s network in "
                                  + network.Target?.Name + " (" + TokenOf(cause)
                                  + (string.IsNullOrEmpty(detail) ? "" : ": " + detail) + ").");
        }

        /// <summary>The cause as the log's one token: <c>left-realm</c>, not <c>LeftRealm</c>.</summary>
        public static string TokenOf(HandlerLossCause cause)
        {
            switch (cause)
            {
                case HandlerLossCause.None: return "none";
                case HandlerLossCause.Party: return "party";
                case HandlerLossCause.Governor: return "governor";
                case HandlerLossCause.Prisoner: return "prisoner";
                case HandlerLossCause.Died: return "died";
                case HandlerLossCause.LeftClan: return "left-clan";
                case HandlerLossCause.BecameHead: return "became-head";
                case HandlerLossCause.Recalled: return "recalled";
                case HandlerLossCause.ForcedParty: return "forced-party";
                case HandlerLossCause.LeftRealm: return "left-realm";
                case HandlerLossCause.Exposed: return "exposed";
                case HandlerLossCause.Replaced: return "replaced";
                default: return "unknown";
            }
        }

        /// <summary>
        /// The network this hero is posted on, if the handler rules are live: espionage on, the
        /// module healthy, and the hero on a network. One gate for every rule that refuses
        /// something over a handler (story 3.8 R2, R4, R7), so the pillar's switch cannot be
        /// honoured by one of them and missed by another.
        /// </summary>
        public static SpyNetwork PostedNetwork(Hero hero)
        {
            if (hero == null) return null;
            var state = CoreBehavior.State;
            if (state == null || !SubModule.Healthy || !Settings.Current.EnableEspionage) return null;
            return HandledBy(state, hero);
        }

        // ----- The residual, told apart from an ordinary loss ---------------------

        // Heroes the party veto refused since the last daily tick. Vanilla's second pass for a
        // clan with no other free lord ignores the veto (story 3.8 R5, from IL), and when that
        // happens the hero leads a party anyway. The daily check cannot tell that apart from any
        // other route to a party by looking at the hero - both arrive as `IsPartyLeader` - so the
        // veto records what it refused and the check reads it back. A static, not save data: a
        // transient reporting detail, and one day of memory is all it means (CLAUDE.md §3).
        private static readonly HashSet<Hero> PartyVetoRefused = new HashSet<Hero>();

        /// <summary>Called by the party veto when it refuses, so the loss can be named (story 3.8 R5).</summary>
        public static void MarkPartyVetoRefused(Hero hero)
        {
            if (hero == null) return;
            if (PostedNetwork(hero) == null) return;
            lock (PartyVetoRefused) PartyVetoRefused.Add(hero);
        }

        private static bool WasPartyVetoRefused(Hero hero)
        {
            lock (PartyVetoRefused) return PartyVetoRefused.Contains(hero);
        }

        private static void ForgetPartyVetoRefusals()
        {
            lock (PartyVetoRefused) PartyVetoRefused.Clear();
        }

        /// <summary>
        /// Whether <paramref name="hero"/> is standing in a settlement of <paramref name="target"/>:
        /// the station, or another town of the same realm. False also for a hero on the road, who
        /// is therefore out of station (story 3.8 R4).
        ///
        /// Read through the settlement's <c>MapFaction</c> cast to <c>Kingdom</c>, which is the
        /// same read <c>ClaimsBehavior</c> makes: a clan's map faction is its kingdom whenever it
        /// is in one, and a village's is its owner's.
        /// </summary>
        public static bool InTargetRealm(Hero hero, Kingdom target)
        {
            var where = hero?.CurrentSettlement;
            if (where == null || target == null) return false;
            return where.MapFaction as Kingdom == target;
        }

        /// <summary>
        /// Uses up network strength - what a mission's success, failure or exposure costs (design
        /// 03 §4). Through here so a mission cannot move a network by a path that disagrees about
        /// its bounds.
        /// </summary>
        internal static void Spend(SpyNetwork network, float amount)
        {
            if (network == null || amount <= 0f) return;
            network.Change(-amount, EspionageConstants.NetworkMaxStrength);
        }

        // ----- Upkeep ---------------------------------------------------------

        /// <summary>
        /// A network's week, term by term, as the weekly upkeep would apply it now. Read by the
        /// upkeep itself and by <c>diplomacy.networks</c>, so the diagnostic cannot describe a
        /// formula the upkeep does not run.
        /// </summary>
        public static NetworkGrowthTerms Explain(ModState state, SpyNetwork network)
        {
            var t = new NetworkGrowthTerms
            {
                Network = network,
                Attrition = EspionageConstants.NetworkWeeklyAttrition,
                WeekOfDecay = EspionageConstants.NetworkDailyDecay * 7f,
            };
            if (network == null) return t;

            var owner = network.Owner;
            var target = network.Target;
            var handler = network.Handler;

            t.CounterIntelligence = CounterIntelligence.Of(state, target);
            t.FromCounterIntelligence = t.CounterIntelligence * EspionageConstants.NetworkCounterIntelligenceDrag;
            t.Ceiling = handler == null ? network.Strength : CeilingOf(handler);

            if (handler == null) t.Idle = "no handler";
            else if (owner?.Kingdom != null && owner.Kingdom == target) t.Idle = owner.Name + " now serves " + target.Name;

            if (t.Idle != null) return t;

            t.Roguery = handler.GetSkillValue(DefaultSkills.Roguery);
            t.Charm = handler.GetSkillValue(DefaultSkills.Charm);

            var purse = owner?.Leader == null ? 0 : owner.Leader.Gold;
            t.Spend = Math.Max(0, Math.Min(network.WeeklyBudget, purse));

            t.FromGold = FromGold(t.Spend, t.Roguery);
            t.AtWar = owner?.Kingdom != null && owner.Kingdom.IsAtWarWith(target);
            t.Investment = t.AtWar ? t.FromGold * EspionageConstants.NetworkWartimeGrowth : t.FromGold;
            return t;
        }

        /// <summary>What a week's spend buys before the wartime rate: (spend / 2000) x (1 + roguery / 200).</summary>
        public static float FromGold(int spend, float roguery)
            => spend / EspionageConstants.NetworkGoldPerPoint * (1f + roguery / EspionageConstants.NetworkRogueryScale);

        /// <summary>
        /// The week a network <i>would</i> have, net of the daily decay, if it were run at this spend
        /// by a handler of this roguery: the sum <see cref="Explain"/> applies, for a network that
        /// may not exist yet. What the AI reads to ask "could a network there grow at all" before it
        /// founds one (run 11 §8 item 1), from the same terms the tab shows the player.
        /// </summary>
        public static float ProjectedWeek(int spend, float roguery, bool atWar, float counterIntelligence)
        {
            var investment = FromGold(spend, roguery) * (atWar ? EspionageConstants.NetworkWartimeGrowth : 1f);
            return investment
                   - counterIntelligence * EspionageConstants.NetworkCounterIntelligenceDrag
                   - EspionageConstants.NetworkWeeklyAttrition
                   - EspionageConstants.NetworkDailyDecay * 7f;
        }

        /// <summary>
        /// Once a week: each network with a handler is paid for out of the owner's purse and
        /// grows by the sum in <see cref="Explain"/>. A network with no handler spends nothing
        /// and does not grow, but its attrition and counter-intelligence still apply - agents
        /// with nobody running them are found and lost.
        /// </summary>
        public static void WeeklyTick(ModState state)
        {
            if (state == null) return;
            for (var i = 0; i < state.SpyNetworks.Count; i++)
            {
                var network = state.SpyNetworks[i];
                try
                {
                    var t = Explain(state, network);
                    if (t.Spend > 0) network.Owner.Leader.ChangeHeroGold(-t.Spend);

                    var before = network.Strength;
                    network.Change(t.Weekly, t.Ceiling);
                    network.RecordWeek(network.Strength - before - t.WeekOfDecay, t.Spend);

                    // One line a network a week: the only record of what was paid, once the purse
                    // has moved on. A live check on 2026-09-25 had to reconstruct a week from
                    // strengths alone because this line did not exist.
                    Log.Info("Espionage", network.Owner.Name + " in " + network.Target.Name + ": "
                                          + before.ToString("0.0") + " -> " + network.Strength.ToString("0.0")
                                          + " (weekly " + t.Weekly.ToString("+0.00;-0.00;0.00")
                                          + (t.Idle != null ? ", idle: " + t.Idle : ", spent " + t.Spend)
                                          + (t.AtWar ? ", at war" : "") + ").");
                }
                catch (Exception ex)
                {
                    Log.Error("Espionage", "The weekly upkeep of " + network + " failed.", ex);
                }
            }
        }

        /// <summary>
        /// Once a day: every network decays, and one whose handler, owner or target no longer
        /// qualifies is put right. Checked rather than hooked on events, so nothing depends on the
        /// order the engine fires its listeners in (CLAUDE.md §1) and no event can be missed.
        /// </summary>
        public static void DailyTick(ModState state)
        {
            if (state == null) return;
            try
            {
                for (var i = state.SpyNetworks.Count - 1; i >= 0; i--)
                {
                    var network = state.SpyNetworks[i];
                    try
                    {
                        if (network.Owner == null || network.Owner.IsEliminated
                            || network.Target == null || network.Target.IsEliminated)
                        {
                            Log.Info("Espionage", "Dropped " + network + ": its owner or its target is gone.");
                            state.SpyNetworks.RemoveAt(i);
                            continue;
                        }

                        var handler = network.Handler;
                        if (handler != null)
                        {
                            var cause = StillHandles(handler, network, out var why);
                            if (cause != HandlerLossCause.None) Release(state, network, cause, why);
                            else RestationIfAbroad(state, network, handler);
                        }

                        network.Change(-EspionageConstants.NetworkDailyDecay, EspionageConstants.NetworkMaxStrength);

                        // A network worn to nothing with nobody on it is not an asset any more. Kept
                        // while it has a handler or a budget: the owner is still building it.
                        if (network.Strength <= 0f && network.Handler == null && network.WeeklyBudget <= 0)
                            state.SpyNetworks.RemoveAt(i);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Espionage", "The daily upkeep of " + network + " failed.", ex);
                    }
                }
            }
            finally
            {
                // Read once per day by StillHandles, then spent: a refusal from yesterday must not
                // name the cause of a party taken today.
                ForgetPartyVetoRefusals();
            }
        }

        private static HandlerLossCause StillHandles(Hero hero, SpyNetwork network, out string why)
        {
            why = null;
            if (!hero.IsAlive || hero.IsDead) { why = "died"; return HandlerLossCause.Died; }
            if (hero.Clan != network.Owner) { why = "left " + network.Owner.Name; return HandlerLossCause.LeftClan; }
            if (hero.IsPrisoner) { why = "taken prisoner"; return HandlerLossCause.Prisoner; }
            if (hero == network.Owner.Leader) { why = "became head of the clan"; return HandlerLossCause.BecameHead; }
            // Two different things end a post with a party, and 3.12 counts them apart (R5): the
            // veto answered no and vanilla took the hero anyway, or the hero was given a party by
            // a path that never asked. Only the first is the residual the lead accepted instead
            // of Harmony, so only the first is `forced-party`.
            if (hero.IsPartyLeader)
            {
                why = WasPartyVetoRefused(hero) ? "forced onto a party despite the veto" : "took command of a party";
                return WasPartyVetoRefused(hero) ? HandlerLossCause.ForcedParty : HandlerLossCause.Party;
            }
            if (hero.GovernorOf != null) { why = "became governor of " + hero.GovernorOf.Name; return HandlerLossCause.Governor; }
            return HandlerLossCause.None;
        }

        /// <summary>
        /// Sends a handler back to the station if they are not standing in the target realm
        /// (story 3.8 R4; the lead's D1: the station is enforced, not dropped from the fiction).
        /// Re-stationed, not released - the handler is still the right hero, they are simply in
        /// the wrong place, and losing a network's only handler to a relocation is what made AI
        /// networks look unable to grow. Run 09 saw 7 of 7 AI handlers outside their target on
        /// day 15 while their networks kept growing: vanilla moves idle AI lords home, and
        /// nothing here noticed.
        ///
        /// The station is the target's own <see cref="StationFor"/>, so a handler whose town fell
        /// to a third realm is moved to the target's new one rather than left in enemy hands.
        /// A target with no town left is left alone: there is nowhere to station anyone, and
        /// releasing the handler over it would be a loss the owner did nothing to earn.
        /// </summary>
        private static void RestationIfAbroad(ModState state, SpyNetwork network, Hero handler)
        {
            if (InTargetRealm(handler, network.Target)) return;

            var station = StationFor(network.Target);
            if (station == null) return;

            // Where the handler was found, read BEFORE anything moves them. Read after the
            // teleport it always reads the station, which is a line that cannot tell a vanilla
            // relocation from a check that is simply wrong - and a live check on 2026-10-01
            // produced exactly that for a whole run: every day said "abroad at <the station>".
            var from = handler.CurrentSettlement;
            var fromFaction = from?.MapFaction;
            var fromWhere = from == null
                ? "no settlement (on the road)"
                : from.Name.ToString() + " in " + (fromFaction?.Name?.ToString() ?? "no realm")
                               + (fromFaction is Kingdom ? "" : " [not a kingdom]");

            // A hero in a map event or a siege waits for the next day. Teleporting a hero out of
            // a battle is the kind of thing that ends a campaign, and one day of a handler being
            // abroad costs nothing that the loss of the handler would have.
            if (handler.HitPoints <= 0) return;
            var party = handler.PartyBelongedTo;
            if (party != null && (party.MapEvent != null || party.SiegeEvent != null))
            {
                Log.Info("Espionage", handler.Name + " is outside " + network.Target.Name + " - at " + fromWhere
                                      + " - but is in a battle or a siege, and stays there until the next day.");
                return;
            }

            TeleportHeroAction.ApplyImmediateTeleportToSettlement(handler, station);
            // Recorded on the same record as a loss, with the cause R4 names, because that is
            // where 3.12 counts (AC4) and because a post found abroad is the number that says
            // whether 3.8 worked. The detail says what happened - the post was kept, not lost -
            // and analyse-log.py prints `left-realm` apart from the causes that ended a post.
            Telemetry.Event("handler_lost", "owner", network.Owner, "target", network.Target, "hero", handler,
                                  "cause", TokenOf(HandlerLossCause.LeftRealm), "from", fromFaction,
                                  "detail", "sent back to " + station.Name);
            Log.Info("Espionage", handler.Name + " was found outside " + network.Target.Name + " - at "
                                  + fromWhere + " - and is sent back to the station at " + station.Name
                                  + " (left-realm: the post is kept).");
        }

        /// <summary>
        /// Where a handler is sent: the target realm's most prosperous town, which is where a
        /// court's business - and its gossip - collects. Null for a realm with no town.
        /// </summary>
        public static Settlement StationFor(Kingdom target)
        {
            Settlement best = null;
            var bestProsperity = float.MinValue;
            foreach (var fief in target.Fiefs)
            {
                if (fief == null || !fief.IsTown) continue;
                if (fief.Prosperity <= bestProsperity) continue;
                bestProsperity = fief.Prosperity;
                best = fief.Settlement;
            }
            return best;
        }
    }
}
