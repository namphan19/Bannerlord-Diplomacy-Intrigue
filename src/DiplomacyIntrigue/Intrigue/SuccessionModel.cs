using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// What a succession costs the crown, and who is left holding a claim.
    ///
    /// **Who takes the throne is still vanilla's decision**, and that is deliberate. Design 02
    /// §9.3 - the lead's call on 2026-09-23 - is to extend `KingdomDecision` rather than
    /// replace it, and `KingSelectionKingdomDecision` already runs a real election with real
    /// clan support. Overriding its outcome would mean owning succession end to end, including
    /// every path that reaches it: a ruler killed in battle, a clan destroyed, a kingdom
    /// absorbed. What this file owns instead is the *politics afterwards*, which vanilla has
    /// none of: how divided the court was, what that division cost the new ruler's standing,
    /// and who walks away still calling themselves the rightful heir.
    ///
    /// That split keeps the risky half in the engine's hands and puts the interesting half in
    /// ours. If the lead later wants the throne itself decided here, this is the file to widen
    /// and `KingSelectionKingdomDecision` is the class to extend.
    /// </summary>
    public static class SuccessionModel
    {
        /// <summary>
        /// Who was on each throne when we last looked. Not saved: it is rebuilt silently on
        /// the first daily tick after a load, which costs one missed succession only if a
        /// ruler dies while the game is not running - which cannot happen.
        /// </summary>
        private static readonly Dictionary<Kingdom, Hero> LastKnownRulers = new Dictionary<Kingdom, Hero>();

        /// <summary>
        /// Drops the watch list and records who sits on every throne now. Session start, where
        /// the kingdoms belong to another campaign.
        ///
        /// Seeded here rather than on the first daily tick, which is what the first version did.
        /// A ruler who died between the load and that tick was then recorded as the *first*
        /// sighting of their heir, and the succession passed without politics: found live on
        /// 2026-09-23, when a queen killed straight after a load left no trace in the log.
        /// </summary>
        public static void Reset()
        {
            LastKnownRulers.Clear();
            BranchedAtSuccession.Clear();
            foreach (var kingdom in Kingdom.All)
                if (kingdom.IsRealm() && kingdom.Leader != null) LastKnownRulers[kingdom] = kingdom.Leader;
        }

        /// <summary>
        /// Set while an internal war puts its claimant on the throne. `ChangeRulingClanAction`
        /// raises `RulingClanChanged` synchronously, and without this the succession politics
        /// below would run a second time on a change the war has already priced: the contested
        /// succession's -15 on top of the war's own -15, and grievances for "backing the loser"
        /// handed to the loyalists who just lost a war.
        /// </summary>
        private static Kingdom _installingByArms;

        /// <summary>
        /// Heirs who walked out of the late ruler's own house at this succession and founded a
        /// cadet branch (2.6b, <see cref="ClanSuccession"/>). They were members of the ruling
        /// house when the ruler died, which is the blood claim's first rule, but by the time the
        /// court is tallied they lead a different clan and the rule no longer sees them.
        ///
        /// Found by running it: the first live test assumed a runner-up is always the late
        /// ruler's child or sibling. Vanilla's heirs include nephews and in-laws, and Patyr - who
        /// split from Southern Empire's ruling house when its queen died - was neither, so the
        /// tally left him out. Not saved: it is read by the very next resolution, which in the
        /// engine's own order comes after the split and before the next save.
        /// </summary>
        private static readonly Dictionary<Kingdom, List<Hero>> BranchedAtSuccession =
            new Dictionary<Kingdom, List<Hero>>();

        /// <summary>Records a founder of a cadet branch of the ruling house. Called by <see cref="ClanSuccession"/>.</summary>
        public static void NoteBranchedHeir(Kingdom kingdom, Hero founder)
        {
            if (kingdom == null || founder == null) return;
            if (!BranchedAtSuccession.TryGetValue(kingdom, out var list))
                BranchedAtSuccession[kingdom] = list = new List<Hero>();
            if (!list.Contains(founder)) list.Add(founder);
        }

        /// <summary>
        /// Crowns a clan that won an internal war (design 07 §3a Q1). The throne changes hands
        /// through vanilla's own action; this only keeps the succession watch from treating it
        /// as an ordinary succession.
        /// </summary>
        public static void InstallByArms(ModState state, Kingdom kingdom, Clan clan)
        {
            if (kingdom == null || clan == null) return;

            _installingByArms = kingdom;
            try
            {
                ChangeRulingClanAction.Apply(kingdom, clan);
            }
            finally
            {
                _installingByArms = null;
            }

            if (kingdom.Leader != null) LastKnownRulers[kingdom] = kingdom.Leader;
            RetireSpentClaims(state);
        }

        /// <summary>
        /// Drops a claim that was fought for and lost (design 07 §3a Q1). A claim otherwise
        /// only ends when its holder dies, takes the throne or leaves - losing a war over it is
        /// the one ending that is a decision rather than a fact.
        /// </summary>
        public static void RetireClaim(ModState state, Kingdom kingdom, Hero claimant, string reason)
        {
            if (state == null || kingdom == null || claimant == null) return;

            var removed = state.Pretenders.RemoveAll(p => p.Kingdom == kingdom && p.Claimant == claimant);
            if (removed == 0) return;

            BlocModel.Invalidate();
            Log.Info("Succession", kingdom.Name + ": " + claimant.Name + "'s claim is retired - " + reason + ".");
        }

        /// <summary>
        /// Notices that a throne changed hands, by watching who sits on it rather than by
        /// listening for an event.
        ///
        /// **`RulingClanChanged` is not enough, measured rather than assumed.** It fires when
        /// the ruling *clan* changes and not when an heir inherits inside it - which is the
        /// ordinary case. Killing Battania's ruler produced a new king and no event at all,
        /// while killing Sturgia's produced one, and the difference is whether the clan itself
        /// changed. Hooking the event alone would have meant most successions in the game
        /// silently skipping this system.
        ///
        /// Polling also sidesteps event ordering, which CLAUDE.md §1 records as unreliable
        /// here: by the time the daily tick runs, whoever vanilla chose is already on the
        /// throne, so there is no race to lose.
        /// </summary>
        public static void DailyWatch(ModState state)
        {
            if (state == null) return;

            foreach (var kingdom in Kingdom.All)
            {
                if (!kingdom.IsRealm()) continue;

                var ruler = kingdom.Leader;
                if (ruler == null) continue;

                if (!LastKnownRulers.TryGetValue(kingdom, out var previous))
                {
                    // First sighting: seed it, and say nothing. A load is not a succession.
                    LastKnownRulers[kingdom] = ruler;
                    continue;
                }

                if (previous == ruler) continue;

                LastKnownRulers[kingdom] = ruler;
                Resolve(state, kingdom, previous);
            }
        }

        /// <summary>
        /// The event path, kept alongside the daily watch rather than replaced by it.
        ///
        /// `RulingClanChanged` was observed firing for real - Vlandia and Sturgia both
        /// produced a succession line through it - and it arrives immediately rather than up
        /// to a day late. What it misses is an heir inheriting inside the ruling clan, which
        /// is the ordinary case and which the watch catches. Using only the watch would have
        /// meant discarding a path already seen working for one only reasoned about.
        ///
        /// They cannot both fire for the same succession: whichever arrives first records the
        /// new ruler in <see cref="LastKnownRulers"/>, and the other then sees no change.
        /// </summary>
        public static void OnRulingClanChanged(ModState state, Kingdom kingdom, Clan oldRulingClan)
        {
            if (state == null || kingdom == null || kingdom.IsEliminated) return;

            var ruler = kingdom.Leader;
            if (ruler == null) return;

            if (kingdom == _installingByArms)
            {
                // An internal war's outcome, already priced by the war. Record the new ruler
                // so the daily watch does not see a change tomorrow either.
                LastKnownRulers[kingdom] = ruler;
                return;
            }

            if (LastKnownRulers.TryGetValue(kingdom, out var previous))
            {
                if (previous == ruler) return;   // the watch already handled it
                LastKnownRulers[kingdom] = ruler;
                Resolve(state, kingdom, previous);
                return;
            }

            // Never seen this kingdom before: the late ruler is the best we can name.
            LastKnownRulers[kingdom] = ruler;
            Resolve(state, kingdom, oldRulingClan?.Leader);
        }

        /// <summary>
        /// Work out how contested a succession was and apply the consequences in design 02 §5:
        /// a contested succession drains the crown's standing, hands every backer of a losing
        /// claimant a grievance, and leaves a strong loser as a pretender.
        /// </summary>
        private static void Resolve(ModState state, Kingdom kingdom, Hero lateRuler)
        {
            var oldRulingClan = lateRuler?.Clan;
            if (state == null || kingdom == null || kingdom.IsEliminated) return;

            var incumbent = kingdom.Leader;
            if (incumbent == null || incumbent == lateRuler) return;

            // A claim against a throne nobody holds any more is not a claim.
            RetireSpentClaims(state);

            // A ruler who dies mid-civil-war is succeeded by vanilla's heir without a second
            // contest (design 07 §3a Q3): the war already is the contest, and running the
            // succession politics beside it would mint pretenders and grievances for a court
            // whose sides are already drawn. If the heir is a rebel, the war sees that tomorrow.
            if (InternalWars.OngoingIn(state, kingdom) != null)
            {
                Log.Info("Succession", kingdom.Name + ": " + incumbent.Name
                                       + " took the throne during a civil war - the war decides the rest.");
                return;
            }

            var claimants = Claimants(state, kingdom, incumbent, oldRulingClan, lateRuler);
            if (claimants.Count < 2)
            {
                Log.Info("Succession", kingdom.Name + ": " + incumbent.Name
                                       + " took the throne unopposed - no rival claimant stood.");
                return;
            }

            // Who backs whom is decided ONCE and both the tally and the grievances read it.
            // The first version decided it twice - Support() weighed loyalty and self-backing,
            // GrieveBackers() compared bare relations - and on the very first live contested
            // succession they already disagreed: the tally put 4 clans behind the loser and 5
            // clans were handed a grievance for backing him. The fifth, fen Penraic, sat at
            // loyalty 66, which counted it firmly for the new king in the vote; it was then
            // punished for not supporting him. This was first written up as the two "happening
            // to agree" - they did not, it was simply not checked. Re-run on this code: 4 and 4.
            // CLAUDE.md §3: a value derived in two places is the bug.
            var backing = Backing(state, kingdom, claimants);
            var support = Support(kingdom, claimants, backing);
            var total = 0f;
            foreach (var pair in support) total += pair.Value;
            if (total <= 0f) return;

            var incumbentShare = support.TryGetValue(incumbent, out var own) ? own / total : 0f;

            // One line with the whole division, so a contested succession can be checked from
            // the log: the grievances that follow must go to exactly the clans counted here
            // behind each loser, since both read the same backing map.
            var division = new System.Text.StringBuilder();
            foreach (var pair in support)
            {
                var clans = 0;
                foreach (var b in backing) if (b.Value == pair.Key) clans++;
                if (division.Length > 0) division.Append(", ");
                division.Append(pair.Key.Name).Append(' ')
                        .Append((pair.Value / total * 100f).ToString("0")).Append("% (")
                        .Append(clans).Append(clans == 1 ? " clan)" : " clans)");
            }
            Log.Info("Succession", kingdom.Name + " divides: " + division);

            if (incumbentShare >= IntrigueConstants.SuccessionClearMajority)
            {
                Log.Info("Succession", kingdom.Name + ": " + incumbent.Name + " took the throne with "
                                       + (incumbentShare * 100f).ToString("0") + "% of the court - orderly.");
                return;
            }

            // Contested. The crown starts weaker than it would have.
            LegitimacyRegistry.Adjust(state, kingdom, -IntrigueConstants.SuccessionContestedLegitimacy,
                "a contested succession at " + (incumbentShare * 100f).ToString("0") + "% support");

            foreach (var pair in support)
            {
                var claimant = pair.Key;
                if (claimant == incumbent) continue;

                var share = pair.Value / total;

                // Everyone who backed a loser is left with a grievance against the winner.
                GrieveBackers(state, kingdom, claimant, backing);

                if (share < IntrigueConstants.SuccessionPretenderShare) continue;
                if (claimant.Clan == null || claimant.Clan == kingdom.RulingClan) continue;

                state.Pretenders.Add(new Pretender(kingdom, claimant, share));
                BlocModel.Invalidate();

                Log.Info("Succession", kingdom.Name + ": " + claimant.Name
                                       + " kept " + (share * 100f).ToString("0")
                                       + "% of the court and remains a pretender.");
            }
        }

        // ----- Reading --------------------------------------------------------

        /// <summary>Standing claims against a throne. The Pretenders bloc and design 07 read this.</summary>
        public static List<Pretender> PretendersTo(ModState state, Kingdom kingdom)
        {
            var list = new List<Pretender>();
            if (state == null || kingdom == null) return list;

            for (var i = 0; i < state.Pretenders.Count; i++)
            {
                var p = state.Pretenders[i];
                if (p.Kingdom == kingdom && p.IsStillStanding) list.Add(p);
            }
            return list;
        }

        /// <summary>The claim this clan's own leader holds, if any.</summary>
        public static Pretender ClaimOf(ModState state, Clan clan)
        {
            if (state == null || clan?.Leader == null) return null;

            for (var i = 0; i < state.Pretenders.Count; i++)
            {
                var p = state.Pretenders[i];
                if (p.Claimant == clan.Leader && p.IsStillStanding) return p;
            }
            return null;
        }

        /// <summary>Drops claims whose claimant died, took the throne, or left the realm.</summary>
        public static void RetireSpentClaims(ModState state)
        {
            if (state == null || state.Pretenders.Count == 0) return;

            var removed = state.Pretenders.RemoveAll(p => !p.IsStillStanding);
            if (removed > 0)
            {
                BlocModel.Invalidate();
                Log.Info("Succession", removed + " claim(s) lapsed - the claimant died, took the"
                                       + " throne, or left the kingdom.");
            }
        }

        // ----- The arithmetic -------------------------------------------------

        /// <summary>
        /// Who could plausibly have taken the throne. Design 02 §5: "the late ruler's heir,
        /// plus any clan leader with a blood claim."
        ///
        /// **The blood rule alone is too narrow, measured rather than guessed.** Shipped
        /// spec-faithful first and tested: killing Vlandia's ruler produced "took the throne
        /// unopposed - no rival claimant stood", because Bannerlord's kingdom clans are
        /// separate families and the heir inherits inside the ruling clan. Every succession on
        /// a fresh map would have been uncontested, which makes the Pretenders bloc and design
        /// 07's armed contest dead code.
        ///
        /// **So a second route was added, and it is a change beyond design 02 §5.** A clan
        /// leader is also a claimant when the clan is strong enough at court to press one and
        /// disaffected enough to want to - influence at least
        /// <see cref="IntrigueConstants.SuccessionClaimantInfluenceRatio"/> times the court's
        /// average and loyalty below the transactional band. That is the magnate pressing his claim, and it is built out
        /// of numbers this pillar already has rather than a new invented fact. Setting the
        /// ratio constant very high turns it off and restores the spec's rule exactly.
        /// </summary>
        private static List<Hero> Claimants(ModState state, Kingdom kingdom, Hero incumbent,
            Clan oldRulingClan, Hero lateRuler)
        {
            var claimants = new List<Hero> { incumbent };

            for (var i = 0; i < kingdom.Clans.Count; i++)
            {
                var clan = kingdom.Clans[i];
                var leader = clan?.Leader;
                if (leader == null || !Court.IsMember(clan) || leader == incumbent) continue;
                if (claimants.Contains(leader)) continue;

                if (HasBloodClaim(leader, lateRuler, oldRulingClan)
                    || BranchedFromRulingHouse(kingdom, leader)
                    || HasPowerClaim(state, clan, kingdom))
                    claimants.Add(leader);
            }

            // Used once: the note describes this succession, not the house forever after.
            BranchedAtSuccession.Remove(kingdom);
            return claimants;
        }

        /// <summary>
        /// Blood ties the game actually models: parents, children, siblings, and membership of
        /// the late ruling clan itself. Deliberately not "related somewhere up the tree" -
        /// Bannerlord's genealogy is shallow and a looser rule would make half a kingdom a
        /// claimant, which is a different design rather than a more generous one.
        /// </summary>
        private static bool BranchedFromRulingHouse(Kingdom kingdom, Hero candidate)
            => BranchedAtSuccession.TryGetValue(kingdom, out var list) && list.Contains(candidate);

        private static bool HasBloodClaim(Hero candidate, Hero lateRuler, Clan oldRulingClan)
        {
            if (candidate == null) return false;
            if (oldRulingClan != null && candidate.Clan == oldRulingClan) return true;
            if (lateRuler == null) return false;

            if (candidate.Father == lateRuler || candidate.Mother == lateRuler) return true;
            if (lateRuler.Father == candidate || lateRuler.Mother == candidate) return true;

            foreach (var sibling in candidate.Siblings)
                if (sibling == lateRuler) return true;

            return false;
        }

        /// <summary>
        /// Influence of this clan as a multiple of its court's average - the one place that
        /// average is computed. Sworn clans with positive influence make up the average; a
        /// mercenary is not at court and a clan at or below zero carries no weight in it.
        /// </summary>
        public static float InfluenceRatio(Clan clan, Kingdom kingdom)
        {
            if (clan == null || kingdom?.Clans == null) return 0f;

            var total = 0f;
            var counted = 0;
            for (var i = 0; i < kingdom.Clans.Count; i++)
            {
                var other = kingdom.Clans[i];
                if (!Court.IsMember(other) || other.Influence <= 0f) continue;
                total += other.Influence;
                counted++;
            }
            if (counted == 0 || total <= 0f) return 0f;
            return clan.Influence / (total / counted);
        }

        /// <summary>
        /// The magnate's claim: strong enough at court to press one, disaffected enough to
        /// want to. **Not in design 02 §5** - see the note on <see cref="Claimants"/> for why
        /// it exists and how to turn it off.
        ///
        /// Both halves are required. Influence alone would make every great clan a claimant at
        /// every succession, which is a different game; disaffection alone would let a clan
        /// with no standing at court declare for the throne and be ignored.
        /// </summary>
        public static bool HasPowerClaim(ModState state, Clan clan, Kingdom kingdom)
        {
            if (clan?.Leader == null) return false;
            if (LoyaltyModel.Of(state, clan) >= IntrigueConstants.LoyaltyTransactional) return false;
            return IsMagnate(clan, kingdom);
        }

        /// <summary>
        /// Strong enough at court to press a claim: influence at least
        /// <see cref="IntrigueConstants.SuccessionClaimantInfluenceRatio"/> times the court's
        /// average. The influence half of <see cref="HasPowerClaim"/>, on its own because a
        /// rival court's Encyclopedia band ("among the great houses") is this same edge -
        /// before 2.7 the average was computed twice, here and in <see cref="InfluenceRatio"/>.
        ///
        /// Measured against the court's average rather than an absolute share of it. The
        /// first version used a flat 15% and nobody in the game ever qualified: a nine-clan
        /// court averages 11% each and its strongest clan held 14%, so the bar sat above
        /// the top of the field. A share threshold silently encodes an assumption about how
        /// many clans a kingdom has; a multiple of the average does not.
        /// </summary>
        public static bool IsMagnate(Clan clan, Kingdom kingdom)
            => InfluenceRatio(clan, kingdom) >= IntrigueConstants.SuccessionClaimantInfluenceRatio;

        /// <summary>
        /// How the court divides: which claimant each clan backs. The single answer to that
        /// question - the tally and the grievances both read this map.
        ///
        /// Backing follows design 02 §5 - "loyalty, bloc agenda, and relation to the claimant".
        /// Relation carries it, with the incumbent given the clan's loyalty as a bonus, because
        /// loyalty to the crown is exactly the disposition to accept whoever now wears it.
        /// Bloc agenda enters through loyalty rather than as a separate term: a clan's bloc is
        /// already a function of the same pressures. A claimant's own clan backs its claimant.
        /// </summary>
        private static Dictionary<Clan, Hero> Backing(ModState state, Kingdom kingdom, List<Hero> claimants)
        {
            var backing = new Dictionary<Clan, Hero>();
            var incumbent = kingdom.Leader;

            for (var c = 0; c < kingdom.Clans.Count; c++)
            {
                var clan = kingdom.Clans[c];
                if (clan?.Leader == null || !Court.IsMember(clan)) continue;

                Hero backed = null;
                var best = float.MinValue;

                for (var i = 0; i < claimants.Count; i++)
                {
                    var claimant = claimants[i];
                    var score = clan.Leader == claimant
                        ? IntrigueConstants.SuccessionSelfBacking
                        : clan.Leader.GetRelation(claimant);

                    if (claimant == incumbent)
                        score += LoyaltyModel.Of(state, clan) * IntrigueConstants.SuccessionLoyaltyWeight;

                    // Design 08 S-8: the claimant's own Charm, never delegated. It is added to
                    // every house's score for that claimant, so what counts is how the claimants
                    // compare, and the realms' median cancels out.
                    score += Statecraft.StatecraftTerms.Backing(claimant);

                    if (score <= best) continue;
                    best = score;
                    backed = claimant;
                }

                if (backed != null) backing[clan] = backed;
            }
            return backing;
        }

        /// <summary>Each claimant's weight at court: the influence of the clans backing them.</summary>
        private static Dictionary<Hero, float> Support(Kingdom kingdom, List<Hero> claimants,
            Dictionary<Clan, Hero> backing)
        {
            var support = new Dictionary<Hero, float>();
            for (var i = 0; i < claimants.Count; i++) support[claimants[i]] = 0f;

            foreach (var pair in backing)
            {
                var influence = pair.Key.Influence > 0f ? pair.Key.Influence : 0f;
                if (influence <= 0f) continue;
                if (support.ContainsKey(pair.Value)) support[pair.Value] += influence;
            }
            return support;
        }

        /// <summary>
        /// Every clan that backed a losing claimant resents the winner for it. Design 02 §5
        /// puts this at weight 6; the grievance is against the new ruling clan, which is the
        /// crown now.
        /// </summary>
        private static void GrieveBackers(ModState state, Kingdom kingdom, Hero loser,
            Dictionary<Clan, Hero> backing)
        {
            var ruling = kingdom.RulingClan;
            if (ruling == null) return;

            foreach (var pair in backing)
            {
                var clan = pair.Key;
                if (clan == ruling || pair.Value != loser) continue;

                GrievanceRegistry.Add(state, clan, ruling, GrievanceType.SuccessionPassedOver,
                    reason: "backed " + loser.Name + " for the throne");
            }
        }
    }
}
