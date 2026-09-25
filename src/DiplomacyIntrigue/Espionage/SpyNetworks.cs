using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace DiplomacyIntrigue.Espionage
{
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
        /// leading a party and not governing a town. A companion in the player's party qualifies.
        /// </summary>
        public static bool CanHandle(ModState state, Hero hero, Clan owner, Kingdom target, out string reason)
        {
            reason = null;
            if (hero == null || owner == null || target == null) { reason = "A hero, a clan and a realm are needed."; return false; }
            if (!hero.IsAlive || hero.IsDead) { reason = hero.Name + " is dead."; return false; }
            if (hero.Clan != owner) { reason = hero.Name + " is not of " + owner.Name + "."; return false; }
            if (hero.IsChild) { reason = hero.Name + " is a child."; return false; }
            if (hero.IsPrisoner) { reason = hero.Name + " is a prisoner."; return false; }
            if (hero == owner.Leader) { reason = hero.Name + " leads the clan and cannot go abroad as a handler."; return false; }
            if (hero.IsPartyLeader) { reason = hero.Name + " leads a party."; return false; }
            if (hero.GovernorOf != null) { reason = hero.Name + " governs " + hero.GovernorOf.Name + "."; return false; }
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
            if (network.Handler != null) Release(state, network, "replaced by " + hero.Name);

            var station = StationFor(target);
            TeleportHeroAction.ApplyImmediateTeleportToSettlement(hero, station);
            network.SetHandler(hero);

            Log.Info("Espionage", owner.Name + " put " + hero.Name + " in charge of its network in " + target.Name
                                  + ", stationed at " + station.Name + " (ceiling " + CeilingOf(hero).ToString("0") + ").");
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
        /// Takes the handler off a network. The hero stays where they were stationed, as a
        /// companion sent to a town does in vanilla; the owner fetches them. The network keeps its
        /// strength and wears down until someone else takes it on.
        /// </summary>
        public static void Release(ModState state, SpyNetwork network, string why)
        {
            if (network?.Handler == null) return;
            var hero = network.Handler;
            network.SetHandler(null);
            Log.Info("Espionage", hero.Name + " no longer runs " + network.Owner?.Name + "'s network in "
                                  + network.Target?.Name + " (" + why + ").");
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

            t.FromGold = t.Spend / EspionageConstants.NetworkGoldPerPoint
                         * (1f + t.Roguery / EspionageConstants.NetworkRogueryScale);
            t.AtWar = owner?.Kingdom != null && owner.Kingdom.IsAtWarWith(target);
            t.Investment = t.AtWar ? t.FromGold * EspionageConstants.NetworkWartimeGrowth : t.FromGold;
            return t;
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
                    if (handler != null && !StillHandles(handler, network, out var why))
                        Release(state, network, why);

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

        private static bool StillHandles(Hero hero, SpyNetwork network, out string why)
        {
            why = null;
            if (!hero.IsAlive || hero.IsDead) { why = "died"; return false; }
            if (hero.Clan != network.Owner) { why = "left " + network.Owner.Name; return false; }
            if (hero.IsPrisoner) { why = "taken prisoner"; return false; }
            if (hero == network.Owner.Leader) { why = "became head of the clan"; return false; }
            if (hero.IsPartyLeader) { why = "took command of a party"; return false; }
            if (hero.GovernorOf != null) { why = "became governor of " + hero.GovernorOf.Name; return false; }
            return true;
        }

        /// <summary>
        /// Where a handler is sent: the target realm's most prosperous town, which is where a
        /// court's business - and its gossip - collects. Null for a realm with no town.
        /// </summary>
        private static Settlement StationFor(Kingdom target)
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
