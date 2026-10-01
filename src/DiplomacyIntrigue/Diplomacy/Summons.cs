using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using DiplomacyIntrigue.Statecraft;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// The hegemon calls up its vassals' parties: the call to arms carried one step further
    /// (design 04 §5.2a, story 1.10c, D5 2026-10-01).
    ///
    /// A vassal already answers its patron's wars by fighting *beside* it, in parallel, with its
    /// own army and its own marshal. This puts some of its lords *under* the patron's command, and
    /// Hold decides whether they come.
    ///
    /// **The scope is deliberately narrow, and the narrowness is the design.** A summons names one
    /// war: the obligation war the vassal is already serving in (R3). Not a second war, not a
    /// peacetime retinue, not the vassal's ruler's own party. Everything wider would make the
    /// summons a second foreign policy for the patron, which is what the call to arms already is.
    ///
    /// **Enemies in common only** is the load-bearing constraint (R3). Bannerlord resolves
    /// hostility by each party's own map faction, so a foreign party inside the patron's army
    /// cannot be made to attack a kingdom its own realm is at peace with - but it could be made to
    /// *stand there* while the army did, which reads as breaking the peace by proxy. So the parties
    /// are released before the army reaches anything the vassal is not fighting, which
    /// <see cref="DailyTick"/> checks every day.
    ///
    /// **One resolver.** The player's button, the AI's weekly decision and the console diagnostics
    /// all ask <see cref="QuoteFor"/> and nothing else; the price shown is the price charged
    /// (CLAUDE.md §3).
    /// </summary>
    public static class Summons
    {
        // Marker on an excuse, matching CallToArms so one refusal path reads both kinds correctly.
        private const string Excused = "excused: ";

        // =====================================================================
        // The quote: one resolver for every gate and the price
        // =====================================================================

        public sealed class Quote
        {
            public Kingdom Patron;
            public Kingdom Vassal;
            public Hero Summoner;
            public Treaty Link;

            /// <summary>The obligation war this summons is for, or null when there is none.</summary>
            public WarRecord War;

            /// <summary>The other side of that war.</summary>
            public Kingdom Enemy;

            /// <summary>The army the parties would march in. Null when the summoner has none.</summary>
            public Army Army;

            public bool Eligible;
            public string Reason;

            /// <summary>
            /// Set when the refusal is an excuse rather than defiance: a realm with nothing left,
            /// or one its patron left to fight alone. No mark, no charge (R5, D7).
            /// </summary>
            public bool Excused;

            /// <summary>How many parties would be taken, at the cap (R2).</summary>
            public int PartyCount;

            /// <summary>How many are actually available before the cap, for the diagnostic.</summary>
            public int Available;

            public float Hold;

            /// <summary>Whether the vassal would serve, by the same rule the call to arms reads (R4).</summary>
            public bool WillServe;
            public string Refusal;

            public float InfluenceFactor = 1f;
            public float GoldFactor = 1f;
            public int Influence;
            public int Gold;

            /// <summary>
            /// Whether the quote got as far as asking the vassal (R4) and pricing the order (R6). A quote
            /// refused earlier - no war, no army, no party, on cooldown - leaves every figure below at
            /// its default, and a reader that printed them would show a price of 0 that nobody was ever
            /// quoted, and "would refuse" with no reason.
            /// </summary>
            public bool Asked;
            public bool Priced;

            public bool Affordable;
            public string Short;

            /// <summary>The payer's own purse and ruling house, named so the diagnostic can quote them.</summary>
            public float InfluenceHeld;
            public int GoldHeld;

            /// <summary>Days left on the cooldown (R7).</summary>
            public float CooldownLeft;

            /// <summary>Days the parties would march under the patron (R7).</summary>
            public float DurationDays = DiplomacyConstants.SummonsDurationDays;
        }

        /// <summary>
        /// Whether <paramref name="patron"/> can call up <paramref name="vassal"/>'s parties right
        /// now, for which war, and what it costs. Every gate in R1, R2, R3, R5 and R7 is asked here
        /// and nowhere else, so a button, an AI and a console line cannot disagree about it.
        /// </summary>
        public static Quote QuoteFor(ModState state, Kingdom patron, Kingdom vassal)
            => QuoteFor(state, patron, vassal, answering: false);

        /// <summary>
        /// <paramref name="answering"/> is the vassal answering an order already given and paid for:
        /// R7's two gates (a live summons, the cooldown) are skipped, because the order itself has
        /// just stamped the cooldown on the link. Re-asking them at the answer refused every order
        /// that was ever given - the first build charged the price and then found "summoned 0 days
        /// ago" and marched nobody. Every gate about the world (the war, the army, the parties, an
        /// excuse) is still asked again, because that is what can have moved since the order.
        /// </summary>
        private static Quote QuoteFor(ModState state, Kingdom patron, Kingdom vassal, bool answering)
        {
            var q = new Quote { Patron = patron, Vassal = vassal };

            if (state == null) { q.Reason = "no campaign"; return q; }
            if (!Settings.Current.EnableDiplomacy) { q.Reason = "diplomacy is switched off"; return q; }
            if (patron == null || vassal == null || patron == vassal)
            {
                q.Reason = "no such pair of realms";
                return q;
            }
            if (patron.IsEliminated || vassal.IsEliminated) { q.Reason = "one of them no longer exists"; return q; }

            // R1: hegemony is derived, never stored. The link is the only authority on who may
            // summon whom, and it also carries the Hold the answer is read from.
            q.Link = Hegemony.VassalageOf(state, vassal);
            if (q.Link == null || q.Link.DominantParty != patron)
            {
                q.Reason = vassal.Name + " is not a vassal of " + patron.Name;
                return q;
            }
            q.Hold = Hegemony.HoldOf(q.Link);

            q.Summoner = patron.Leader;
            if (q.Summoner == null) { q.Reason = patron.Name + " has nobody on the throne"; return q; }
            q.InfluenceHeld = patron.RulingClan != null ? patron.RulingClan.Influence : 0;
            q.GoldHeld = q.Summoner.Gold;

            // R3: the summons names its war. A vassal serves one obligation war at a time
            // (CallToArms.AlreadyServing), so this is that war or nothing.
            var war = ServiceWar(state, patron, vassal);
            if (war == null)
            {
                q.Reason = vassal.Name + " is serving " + patron.Name + " in no war right now"
                           + (ObligationFighter(state, vassal) != null
                               ? " - it is fighting one for " + ObligationFighter(state, vassal).Name.ToString()
                               : "");
                return q;
            }
            q.War = war;
            q.Enemy = war.Other(vassal);

            // The army they would march in. Required rather than created: a summons that raised an
            // army for a ruler who had not chosen to would be the mod deciding where a king stands,
            // and the Realm tab says so on the button instead.
            var party = q.Summoner.PartyBelongedTo;
            q.Army = party != null ? party.Army : null;
            if (q.Army == null)
            {
                q.Reason = q.Summoner.Name + " commands no army to march them under";
                return q;
            }
            if (q.Army.Kingdom != patron)
            {
                q.Reason = q.Summoner.Name + " is in an army of "
                           + (q.Army.Kingdom == null ? "no realm" : q.Army.Kingdom.Name.ToString());
                return q;
            }

            // R2: at most half the eligible war parties, nearest the summoner first.
            q.Available = EligibleParties(state, vassal, party, null).Count;
            q.PartyCount = Hegemony.MaxVassalsToCall(q.Available);
            if (q.PartyCount <= 0)
            {
                q.Reason = vassal.Name + " has no war party that could march";
                return q;
            }

            // R7: one live summons per vassal, and a cooldown between them.
            var live = answering ? null : Find(state, patron, vassal);
            if (live != null) { q.Reason = "they are already marching under " + patron.Name; return q; }
            if (!answering && q.Link.LastSummonedOn != CampaignTime.Never)
            {
                q.CooldownLeft = DiplomacyConstants.SummonsCooldownDays
                                  - (float)(CampaignTime.Now - q.Link.LastSummonedOn).ToDays;
                if (q.CooldownLeft > 0f)
                {
                    q.Reason = "summoned " + q.CooldownLeft.ToString("0") + " days ago; " + vassal.Name
                               + " is available again in " + q.CooldownLeft.ToString("0") + " days";
                    return q;
                }
            }

            // R4: asked here so the button can say what the answer will be, and re-asked at issue
            // (R4) - Hold may have moved between the quote and the click.
            Hegemony.WouldServe(state, q.Link, out var refusal);
            q.Asked = true;
            q.WillServe = refusal == null;
            q.Refusal = refusal;

            // R5: an excuse is not defiance, and costs nobody anything.
            var excuse = Excuse(state, q);
            if (excuse != null)
            {
                q.Excused = true;
                q.Reason = Excused + excuse;
                return q;
            }

            // R6: influence and gold, each scaled by the skill that does the work.
            Price(q);

            q.Affordable = q.InfluenceHeld >= q.Influence && q.GoldHeld >= q.Gold;
            if (!q.Affordable)
                q.Short = q.InfluenceHeld < q.Influence && q.GoldHeld < q.Gold
                    ? "the crown holds neither the influence nor the denars"
                    : q.InfluenceHeld < q.Influence
                        ? "the crown holds " + q.InfluenceHeld.ToString("0") + " influence of " + q.Influence
                        : "the purse holds " + q.GoldHeld.ToString("N0") + " of " + q.Gold.ToString("N0") + " denars";

            q.Eligible = true;
            return q;
        }

        /// <summary>
        /// The price, from R6. Influence is a contest between the two rulers in Leadership - the
        /// skill design 08 already reads as "how firmly its vassals hold" - so the patron that is
        /// the better ruler pays less for its own command. Gold is the patron's treasurer's Trade
        /// against the median, and it comes out of the ruler's own purse, which is the purse that
        /// also feeds the army.
        /// </summary>
        private static void Price(Quote q)
        {
            var patronRuler = q.Summoner;
            var vassalRuler = q.Vassal.Leader;
            if (vassalRuler != null)
                q.InfluenceFactor = StatecraftTerms.PriceFactor(patronRuler, vassalRuler, DefaultSkills.Leadership);

            var treasurer = q.Patron.RulingClan != null
                ? StatecraftModel.Actor(q.Patron, Portfolio.Treasurer)
                : null;
            if (treasurer != null) q.GoldFactor = StatecraftTerms.PriceFactor(treasurer, DefaultSkills.Trade);

            var influence = DiplomacyConstants.SummonsInfluenceBase
                            + DiplomacyConstants.SummonsInfluencePerParty * q.PartyCount;
            var gold = DiplomacyConstants.SummonsGoldBase
                       + DiplomacyConstants.SummonsGoldPerParty * q.PartyCount;

            q.Influence = (int)Math.Ceiling(influence * q.InfluenceFactor);
            q.Gold = (int)(Math.Round(gold * q.GoldFactor / 100f) * 100f);
            q.Priced = true;
        }

        /// <summary>
        /// The one obligation war <paramref name="vassal"/> is serving <paramref name="patron"/> in,
        /// or null. <see cref="WarRecord.IsObligationWar"/> is <c>CalledBy != null</c> and
        /// <c>CallToArms.Answer</c> stamps the caller on the *vassal's own* war record, so this is
        /// exactly "it answered our call", not a copy of the rule.
        /// </summary>
        public static WarRecord ServiceWar(ModState state, Kingdom patron, Kingdom vassal)
        {
            if (state == null || patron == null || vassal == null) return null;
            foreach (var war in state.OngoingWarsOf(vassal))
                if (war.IsObligationWar && war.CalledBy == patron) return war;
            return null;
        }

        /// <summary>Whoever this vassal is fighting somebody else's war for, for the diagnostic's wording.</summary>
        private static Kingdom ObligationFighter(ModState state, Kingdom vassal)
        {
            foreach (var war in state.OngoingWarsOf(vassal))
                if (war.IsObligationWar) return war.CalledBy;
            return null;
        }

        /// <summary>
        /// The ways a vassal is excused rather than defiant (R5): spent beyond fighting for its
        /// life, at war with itself, with its own territory besieged, or - D7, 2026-10-01 - left to
        /// fight a war alone because its patron stayed out of it. That last one is read through
        /// <see cref="Hegemony.AnswerTo"/>, the same classification the Hold protection term uses,
        /// so "legal neglect" means one thing in this file and in the drift.
        /// </summary>
        private static string Excuse(ModState state, Quote q)
        {
            var exhaustion = WarExhaustion.Worst(state, q.Vassal);
            if (exhaustion > DiplomacyConstants.VassalExcusedAboveExhaustion)
                return "spent (exhaustion " + exhaustion.ToString("0.0") + ")";

            if (InternalWars.OngoingIn(state, q.Vassal) != null) return "at war with itself";

            var settlements = q.Vassal.Settlements;
            for (var i = 0; i < settlements.Count; i++)
                if (settlements[i].SiegeEvent != null) return "its own territory is besieged";

            // D7 is about the vassal's *other* wars, never the service war: that one the patron is
            // in by definition, and the vassal declared it (`ApplyByCallToWarAgreement`), so
            // AnswerTo reads it as None. The first build asked about q.War and the excuse could
            // never fire. A vassal defending itself in any war its patron stayed out of is excused.
            foreach (var war in state.OngoingWarsOf(q.Vassal))
            {
                var answer = Hegemony.AnswerTo(state, war, q.Vassal, q.Patron);
                var attacker = war.Other(q.Vassal);
                if (answer == Hegemony.Answer.Ignored)
                    return "left to fight " + attacker.Name + " alone: " + q.Patron.Name + " never answered";
                if (answer == Hegemony.Answer.Bound)
                    return "fighting " + attacker.Name + " alone: " + q.Patron.Name + " is bound by treaty to stand out of it";
            }

            return null;
        }

        // =====================================================================
        // The parties
        // =====================================================================

        /// <summary>
        /// The vassal's war parties that could march today (R2): not the vassal's own ruler, not
        /// already in an army, not besieged, not captive, not disbanding. Read from the kingdom's
        /// own war-party list rather than from <c>MobileParty.All</c>, so a party that is not a
        /// realm's war party is never taken.
        ///
        /// <paramref name="skip"/> is the patron's summoner, passed so a caller that also wants to
        /// exclude it does not have to re-derive the rule.
        /// </summary>
        private static List<MobileParty> EligibleParties(ModState state, Kingdom vassal,
            MobileParty summoner, List<MobileParty> into)
        {
            var list = into ?? new List<MobileParty>();
            if (vassal == null) return list;

            var components = vassal.WarPartyComponents;
            for (var i = 0; i < components.Count; i++)
            {
                var party = components[i].MobileParty;
                if (party == null || !party.IsActive || party.IsDisbanding || party.IsGarrison) continue;
                if (party.Army != null) continue;
                if (party.SiegeEvent != null || party.MapEvent != null) continue;
                if (party == summoner) continue;

                var leader = party.LeaderHero;
                // The vassal's ruler keeps their own party, always (scope, "Out").
                if (leader != null && leader == vassal.Leader) continue;
                if (leader != null && leader.IsPrisoner) continue;

                // R9: the player's house is never taken without the player's word. A player who
                // rules the vassal gives it through the inquiry; a player who is only a lord of an
                // AI-ruled vassal is never asked, so their parties are never on this list.
                if (party.ActualClan == Clan.PlayerClan && vassal.Leader != Hero.MainHero) continue;

                list.Add(party);
            }

            // Nearest the summoner first, the same reading as the §5.4 cascade cap: a patron
            // marching west calls the lords whose borders face west.
            if (summoner != null)
            {
                var here = summoner.GetPosition2D;
                list.Sort((x, y) => DistanceTo(here, x.GetPosition2D).CompareTo(DistanceTo(here, y.GetPosition2D)));
            }
            return list;
        }

        private static float DistanceTo(Vec2 from, Vec2 to)
        {
            var dx = to.x - from.x;
            var dy = to.y - from.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // =====================================================================
        // The act
        // =====================================================================

        /// <summary>
        /// The patron's ruler gives the order: the price is charged at issue (R6) and the vassal
        /// answers (R4). A refused summons refunds nothing (D3) - the patron has spent the
        /// influence and the denars to make the demand, and that is what it costs to be answered
        /// with a mark.
        ///
        /// Re-asks <see cref="QuoteFor"/> rather than trusting the caller, so the same act reached
        /// from the Realm tab, from the AI and from the console is one act (CLAUDE.md §3).
        /// </summary>
        public static bool Issue(ModState state, Kingdom patron, Kingdom vassal, out string failed)
        {
            failed = null;
            var q = QuoteFor(state, patron, vassal);
            if (!q.Eligible)
            {
                failed = q.Reason;
                // AC5: an order given to an excused vassal is logged with its reason. It is not
                // charged and not marked, which is what the excuse means.
                if (q.Excused) Refuse(state, q, q.Reason);
                return false;
            }
            if (!q.Affordable) { failed = "cannot pay: " + q.Short; return false; }

            // Charged first, answered second - which is what lets a player deliberately summon a
            // vassal it knows will refuse (D4), which is a legitimate lever and costs the full price.
            Charge(q);

            // The cooldown runs from the order, not from the outcome: a refused summons was still
            // an order, and letting a patron re-ask every day would turn one mark into several.
            q.Link.MarkSummoned();

            Log.Info("Summons", "[SUMMONS] issued patron=" + q.Patron.Name + " vassal=" + q.Vassal.Name
                                + " war=" + q.Enemy.Name + " n=" + q.PartyCount
                                + " price=" + q.Influence + "inf/" + q.Gold + "gold");
            Telemetry.Event("summons_issued", "patron", q.Patron, "vassal", q.Vassal, "enemy", q.Enemy,
                "n", q.PartyCount, "influence", q.Influence, "gold", q.Gold,
                "influenceFactor", q.InfluenceFactor, "goldFactor", q.GoldFactor, "hold", q.Hold);

            if (q.Vassal.Leader == Hero.MainHero) { AskPlayer(state, q); return true; }

            if (q.WillServe) { Serve(state, q); return true; }

            Refuse(state, q, q.Refusal ?? "it will not march");
            return true;
        }

        private static void Charge(Quote q)
        {
            try
            {
                if (q.Influence > 0 && q.Patron.RulingClan != null)
                    ChangeClanInfluenceAction.Apply(q.Patron.RulingClan, -q.Influence);
                if (q.Gold > 0 && q.Summoner != null) q.Summoner.ChangeHeroGold(-q.Gold);
            }
            catch (Exception ex)
            {
                // The order stands even if the purse could not be opened: the price is the price,
                // and a fail here must not silently make the summons free.
                Log.Error("Summons", "Charging " + q.Patron.Name + " for a summons failed.", ex);
            }
        }

        /// <summary>
        /// The vassal serves: the parties go into the patron's army. Re-checked against the world
        /// at the moment of service rather than at issue, because for a player vassal the inquiry
        /// can sit open through days of drift.
        /// </summary>
        private static bool Serve(ModState state, Quote q)
        {
            var now = QuoteFor(state, q.Patron, q.Vassal, answering: true);
            if (!now.Eligible)
            {
                Log.Info("Summons", "[SUMMONS] served n=0 patron=" + q.Patron.Name + " vassal=" + q.Vassal.Name
                                    + " - the order could not be carried out: " + now.Reason);
                return false;
            }

            var taken = Attach(state, now);
            if (taken <= 0)
            {
                Log.Info("Summons", "[SUMMONS] served n=0 patron=" + now.Patron.Name + " vassal=" + now.Vassal.Name
                                    + " - no party could be taken");
                return false;
            }

            var record = new SummonsRecord(now.Patron, now.Vassal, now.Summoner, now.Enemy, taken);
            record.SetEnd(CampaignTime.DaysFromNow(DiplomacyConstants.SummonsDurationDays));
            state.Summons.Add(record);

            Log.Info("Summons", "[SUMMONS] served n=" + taken + " patron=" + now.Patron.Name
                                + " vassal=" + now.Vassal.Name + " until " + record.EndsOn);
            Telemetry.Event("summons_served", "patron", now.Patron, "vassal", now.Vassal, "enemy", now.Enemy,
                "n", taken, "endsOn", record.EndsOn.ToString(), "hold", now.Hold);
            Announce(now.Vassal.Name + " sends " + taken + " of its parties to serve under "
                     + now.Patron.Name + " against " + now.Enemy.Name + ".", Colors.Green);
            return true;
        }

        /// <summary>
        /// Puts the parties in the army, and returns how many actually went.
        ///
        /// <c>party.Army = army</c> is what <c>Army.Gather</c> itself does for the list it is given,
        /// with no faction test anywhere on the way (design 04 §5.2a, read by IL): the engine adds
        /// the party, raises <c>OnPartyJoinedArmy</c> and charges the *leader's* clan its own
        /// per-party cost. That last charge is on top of our price and is not ours to skip.
        /// </summary>
        private static int Attach(ModState state, Quote q)
        {
            var summoner = q.Summoner.PartyBelongedTo;
            if (summoner == null || summoner.Army == null) return 0;

            var parties = EligibleParties(state, q.Vassal, summoner, null);
            var army = summoner.Army;
            var attempted = 0;
            for (var i = 0; i < parties.Count && attempted < q.PartyCount; i++)
            {
                try
                {
                    parties[i].Army = army;
                    attempted++;
                }
                catch (Exception ex)
                {
                    Log.Error("Summons", "Taking " + parties[i] + " into the army failed.", ex);
                }
            }

            // Counted by looking, not by the assignments that did not throw: the engine may refuse or
            // undo one, and a record that claims 19 parties while the army holds none is the first
            // build's "served n=19, released n=0" (run 10, 2026-10-01) that could not be explained.
            var inside = 0;
            for (var i = 0; i < parties.Count; i++)
                if (parties[i].Army == army && parties[i].MapFaction == q.Vassal) inside++;
            if (inside != attempted)
                Log.Info("Summons", "[SUMMONS] attached " + attempted + " but " + inside + " are in the army");
            return inside;
        }

        /// <summary>
        /// How many of the vassal's own parties are in the summoner's army right now. The only honest
        /// answer to "are they still marching", since the engine disbands armies on its own.
        /// </summary>
        private static int InArmy(SummonsRecord record)
        {
            var army = SummonerArmy(record);
            if (army == null) return 0;
            var count = 0;
            var parties = army.Parties;
            for (var i = 0; i < parties.Count; i++)
                if (parties[i] != null && parties[i].MapFaction == record.Vassal) count++;
            return count;
        }

        // Last count logged per record, in memory only: it exists so a change is logged once, not so a
        // save can remember it.
        private static readonly Dictionary<SummonsRecord, int> LastSeen = new Dictionary<SummonsRecord, int>();

        /// <summary>
        /// The vassal refuses. Handled by <see cref="CallToArms.Refuse"/> itself - the same call the
        /// call to arms makes - so the mark, the trust (<see cref="DiplomacyConstants.TrustCallToArmsRefused"/>) and the renunciation on a second mark
        /// inside the memory window are the existing machinery and not a copy of it (AC4).
        /// </summary>
        private static void Refuse(ModState state, Quote q, string why)
        {
            var excuse = q.Excused || (why != null && why.StartsWith(Excused));

            if (excuse)
            {
                var reason = why == null ? "no reason given" : why.StartsWith(Excused) ? why.Substring(Excused.Length) : why;
                Log.Info("Summons", "[SUMMONS] excused patron=" + q.Patron.Name + " vassal=" + q.Vassal.Name
                                    + " reason=" + reason);
                Telemetry.Event("summons_excused", "patron", q.Patron, "vassal", q.Vassal,
                    "enemy", q.Enemy, "reason", reason);
                return;
            }

            Log.Info("Summons", "[SUMMONS] refused hold=" + q.Hold.ToString("0") + " patron=" + q.Patron.Name
                                + " vassal=" + q.Vassal.Name);
            Telemetry.Event("summons_refused", "patron", q.Patron, "vassal", q.Vassal, "enemy", q.Enemy,
                "hold", q.Hold, "reason", why ?? "none");

            CallToArms.Refuse(state, q.Link, q.Patron, q.Vassal, why ?? "it will not march");
        }

        /// <summary>
        /// The player who rules the vassal is asked (R9), with the price of each answer named.
        ///
        /// No timeout, deliberately: the call to arms treats silence as a refusal, which is fair for
        /// a war that is about to start either way but not for an order that would earn a mark of
        /// defiance. The clock stops while the question is open, and R9 accepts that, because the
        /// question is the player's own decision.
        /// </summary>
        private static void AskPlayer(ModState state, Quote q)
        {
            var refusedTrust = (-DiplomacyConstants.TrustCallToArmsRefused).ToString("0");
            var parties = q.PartyCount;
            var body = q.Patron.Name + " calls " + parties + " of your war parties up to serve under "
                       + q.Summoner.Name + " against " + q.Enemy.Name + ", the war you are already "
                       + "fighting for it." + Environment.NewLine + Environment.NewLine
                       + "Send: they march until the war ends or "
                       + DiplomacyConstants.SummonsDurationDays.ToString("0") + " days pass. Your ruler's "
                       + "own party is never taken." + Environment.NewLine
                       + "Refuse: defiance. It costs " + refusedTrust + " trust with " + q.Patron.Name
                       + ", earns a mark, and " + CallToArms.RenounceClause(q.Link)
                       + Environment.NewLine + Environment.NewLine
                       + q.Patron.Name + " has already paid " + q.Influence + " influence and "
                       + q.Gold.ToString("N0") + " denars for the order. Neither answer refunds it.";

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "A summons from " + q.Patron.Name,
                    body,
                    true, true,
                    "Send " + parties + " parties", "Refuse",
                    () => { try { Serve(state, q); } catch (Exception ex) { Log.Error("Summons", "Answering a summons failed.", ex); } },
                    () => { try { Refuse(state, q, "declined by the ruler"); } catch (Exception ex) { Log.Error("Summons", "Refusing a summons failed.", ex); } },
                    ""), true);
            }
            catch (Exception ex)
            {
                Log.Error("Summons", "Could not show the summons prompt.", ex);
                Refuse(state, q, "prompt unavailable");
            }
        }

        // =====================================================================
        // The daily tick: when the parties go home
        // =====================================================================

        /// <summary>
        /// Ends a summons that has run its course, and releases the parties. Every reason is named,
        /// because a diagnostic that says "released" without saying why is how a broken mechanic
        /// looks like a design choice (CLAUDE.md §1).
        /// </summary>
        public static void DailyTick(ModState state)
        {
            if (state == null || state.Summons == null || state.Summons.Count == 0) return;

            for (var i = state.Summons.Count - 1; i >= 0; i--)
            {
                var record = state.Summons[i];
                try
                {
                    var reason = WhyEnded(state, record);
                    if (reason == null) continue;
                    Release(state, record, reason);
                }
                catch (Exception ex)
                {
                    Log.Error("Summons", "The daily upkeep of a summons failed.", ex);
                }
            }
        }

        /// <summary>
        /// Why this summons is over, or null while it stands. R7's expiry and R8's five early ends
        /// in one place, plus the R3 guard.
        /// </summary>
        private static string WhyEnded(ModState state, SummonsRecord record)
        {
            if (record.EndsOn <= CampaignTime.Now) return "expired";

            // The engine ends an army on its own - its purpose done, its cohesion spent - and every
            // member goes home with it. A summons whose army is gone has nobody marching, so it ends
            // that day instead of standing, blocking a new order, until its date (run 10: "expired,
            // n=0" nineteen days after nineteen parties were taken).
            var inArmy = InArmy(record);
            int before;
            if (!LastSeen.TryGetValue(record, out before) || before != inArmy)
            {
                Log.Info("Summons", "[SUMMONS] under command now n=" + inArmy
                                    + (LastSeen.ContainsKey(record) ? " (was " + before + ")" : " (first look)")
                                    + " patron=" + record.Patron?.Name + " vassal=" + record.Vassal?.Name);
                LastSeen[record] = inArmy;
            }
            if (inArmy == 0) return "army-gone";

            var patron = record.Patron;
            var vassal = record.Vassal;
            if (patron == null || vassal == null) return "realm-gone";
            if (patron.IsEliminated || vassal.IsEliminated) return "realm-gone";

            var link = Hegemony.VassalageOf(state, vassal);
            if (link == null || link.DominantParty != patron) return "link-ended";

            // The summoner keeps being its ruler's right; a king who has been deposed does not
            // keep commanding somebody else's lords (R8).
            if (record.Summoner == null || patron.Leader != record.Summoner) return "summoner-deposed";

            var war = state.OngoingWarBetween(vassal, record.Enemy);
            if (war == null || !war.IsOngoing) return "war-ended";
            if (vassal.IsAtWarWith(patron)) return "at-war-with-its-patron";

            // R3: the war is only the vassal's to serve in while the patron is in it too.
            if (!patron.IsAtWarWith(record.Enemy)) return "enemy-not-shared";

            if (UnsafeFor(record)) return "would-fight-a-peace";

            return null;
        }

        /// <summary>
        /// Whether the patron's army is about to take something the vassal is not fighting, in which
        /// case the summoned parties come out before it does (R3).
        ///
        /// Read from what the army is actually doing - the settlement it has targeted, or the one it
        /// is besieging - and tested against the vassal's own wars. Anything less would be a guess,
        /// and a guess here either lets a foreign party stand in a battle it has no quarrel with or
        /// pulls it out of a war it was sent to fight.
        /// </summary>
        private static bool UnsafeFor(SummonsRecord record)
        {
            var army = SummonerArmy(record);
            if (army == null) return false;

            var target = army.LeaderParty != null ? army.LeaderParty.TargetSettlement : null;
            if (target == null) target = army.AiBehaviorObject as Settlement;
            if (target == null) return false;

            var owner = target.MapFaction;
            if (owner == null || owner == record.Patron || owner == record.Vassal) return false;

            // Only a settlement the army is actually fighting is our business, and only when the
            // vassal has no quarrel of its own with whoever holds it.
            if (!record.Patron.IsAtWarWith(owner)) return false;
            return !record.Vassal.IsAtWarWith(owner);
        }

        private static Army SummonerArmy(SummonsRecord record)
        {
            var party = record.Summoner != null ? record.Summoner.PartyBelongedTo : null;
            return party != null ? party.Army : null;
        }

        /// <summary>
        /// Sends the parties home. Up to <see cref="SummonsRecord.PartyCount"/> of the vassal's own
        /// parties leave the army, which is what keeps a lord who joined the same army by another
        /// road from being marched out of it by somebody else's order.
        /// </summary>
        private static void Release(ModState state, SummonsRecord record, string reason)
        {
            var army = SummonerArmy(record);
            var freed = 0;

            if (army != null)
            {
                // A copy: `party.Army = null` takes the party out of army.Parties, so walking the live
                // list skipped every other party - live, 2026-10-01, R3 released 8 of 16 and left
                // the other 8 marching at a realm their own was at peace with.
                var parties = new List<MobileParty>(army.Parties);
                for (var i = 0; i < parties.Count && freed < record.PartyCount; i++)
                {
                    var party = parties[i];
                    if (party == null || party.MapFaction != record.Vassal) continue;
                    try
                    {
                        party.Army = null;
                        freed++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Summons", "Releasing " + party + " from the army failed.", ex);
                    }
                }
            }

            state.Summons.Remove(record);
            LastSeen.Remove(record);

            Log.Info("Summons", "[SUMMONS] released reason=" + reason + " n=" + freed
                                + " patron=" + record.Patron.Name + " vassal=" + record.Vassal.Name);
            Telemetry.Event("summons_released", "patron", record.Patron, "vassal", record.Vassal,
                "enemy", record.Enemy, "n", freed, "reason", reason);
            Announce(record.Vassal.Name + "'s parties return to their own command"
                     + (reason == "expired" ? "." : " - " + reason + "."), Colors.Yellow);
        }

        /// <summary>The live summons between two realms, or null.</summary>
        public static SummonsRecord Find(ModState state, Kingdom patron, Kingdom vassal)
        {
            if (state == null || state.Summons == null) return null;
            for (var i = 0; i < state.Summons.Count; i++)
                if (state.Summons[i].Is(patron, vassal)) return state.Summons[i];
            return null;
        }

        /// <summary>The live summons against this vassal from any patron. One at a time (R7).</summary>
        public static SummonsRecord FindFor(ModState state, Kingdom vassal)
        {
            if (state == null || state.Summons == null) return null;
            for (var i = 0; i < state.Summons.Count; i++)
                if (state.Summons[i].Vassal == vassal) return state.Summons[i];
            return null;
        }

        // =====================================================================
        // The AI hegemon (R10)
        // =====================================================================

        /// <summary>
        /// What an AI hegemon would do about its serving vassals this week, and why.
        /// One object, printed by the diagnostic and executed by the weekly run, so the two cannot
        /// describe different decisions (the shape of <c>AiEspionage.Plan</c>).
        /// </summary>
        public sealed class AiChoice
        {
            public Kingdom Realm;

            /// <summary>Why the realm does not summon at all this week, if it does not.</summary>
            public string Skip;

            public Kingdom Vassal;
            public Quote Quote;
            public string Why;
        }

        /// <summary>
        /// The AI's summons, on exactly the player's terms (R10): a war it shares with the vassal, a
        /// vassal whose Hold says it will serve, an army already raised, and a price under a share
        /// of its own purse and influence. It has no discount the player does not get, and no cap
        /// the player does not have.
        /// </summary>
        public static AiChoice PlanFor(ModState state, Kingdom realm)
        {
            var choice = new AiChoice { Realm = realm };
            if (state == null || realm == null || !realm.IsRealm()) { choice.Skip = "not a realm"; return choice; }
            if (realm.Leader == Hero.MainHero) { choice.Skip = "the player rules it"; return choice; }
            if (!Hegemony.IsHegemon(state, realm)) { choice.Skip = "it holds no vassals"; return choice; }

            var links = new List<Treaty>();
            Hegemony.CollectVassalages(state, realm, links);
            if (links.Count == 0) { choice.Skip = "it holds no vassals"; return choice; }

            var best = new AiChoice { Realm = realm };
            var bestParties = -1;

            for (var i = 0; i < links.Count; i++)
            {
                var q = QuoteFor(state, realm, links[i].SubordinateParty);
                if (!q.Eligible || !q.Affordable || !q.WillServe) continue;
                if (!WithinBudget(q)) continue;
                // One summons a week, so when two would serve, the one that brings more men.
                if (q.PartyCount <= bestParties) continue;
                bestParties = q.PartyCount;
                best.Vassal = q.Vassal;
                best.Quote = q;
            }

            if (best.Quote == null)
            {
                choice.Skip = "no vassal is serving it in a war it could call up, at a price it would pay";
                return choice;
            }

            best.Why = best.Vassal.Name + " serves it in the war against " + best.Quote.Enemy.Name
                       + " at hold " + best.Quote.Hold.ToString("0") + " - " + best.Quote.PartyCount
                       + " of its parties for " + best.Quote.Influence + " influence and "
                       + best.Quote.Gold.ToString("N0") + " denars";
            return best;
        }

        /// <summary>
        /// R10's budget: no AI spends more than <see cref="DiplomacyConstants.SummonsAiBudgetShare"/>
        /// of either currency on one summons.
        /// </summary>
        private static bool WithinBudget(Quote q)
        {
            if (q.InfluenceHeld > 0 && q.Influence > q.InfluenceHeld * DiplomacyConstants.SummonsAiBudgetShare) return false;
            if (q.GoldHeld > 0 && q.Gold > q.GoldHeld * DiplomacyConstants.SummonsAiBudgetShare) return false;
            return true;
        }

        /// <summary>The weekly act. Returns whether it summoned.</summary>
        public static bool TryAi(ModState state, Kingdom realm)
        {
            var choice = PlanFor(state, realm);
            if (choice.Quote == null) return false;
            if (!Issue(state, realm, choice.Vassal, out var failed))
                Log.Info("Summons", realm.Name + " meant to summon " + choice.Vassal.Name + " and could not: " + failed);
            return true;
        }

        // =====================================================================
        // The diagnostics
        // =====================================================================

        /// <summary>Every live summons, every eligible one, and every cooldown. For <c>diplomacy.summons</c>.</summary>
        public static string DescribeAll(ModState state, Kingdom only)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("Live summons: " + state.Summons.Count);
            for (var i = 0; i < state.Summons.Count; i++)
            {
                var record = state.Summons[i];
                if (only != null && record.Patron != only && record.Vassal != only) continue;
                sb.AppendLine("  " + record + (record.Summoner != null
                    ? " (given by " + record.Summoner.Name + ")" : ""));
            }

            sb.AppendLine();
            foreach (var patron in Kingdom.All)
            {
                if (!patron.IsRealm() || (only != null && patron != only)) continue;
                if (!Hegemony.IsHegemon(state, patron)) continue;

                var links = new List<Treaty>();
                Hegemony.CollectVassalages(state, patron, links);
                sb.AppendLine(patron.Name + ":");
                for (var i = 0; i < links.Count; i++)
                {
                    var vassal = links[i].SubordinateParty;
                    var q = QuoteFor(state, patron, vassal);
                    sb.Append("  ").Append(vassal.Name).Append("  hold ")
                      .Append(q.Hold.ToString("0.0")).Append("  ");
                    sb.Append(q.Eligible
                        ? q.PartyCount + " party(ies) for " + q.Influence + " influence and "
                          + q.Gold.ToString("N0") + " denars"
                        : (q.Excused ? "excused - " : "no - ") + Strip(q.Reason));
                    if (q.Eligible && !q.Affordable) sb.Append("  (cannot pay: ").Append(q.Short).Append(")");
                    if (q.Eligible && !q.WillServe) sb.Append("  (would refuse: ").Append(q.Refusal).Append(")");
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// The price term by term, for <c>diplomacy.summons_value</c>. The same resolver the button
        /// and the AI read, so what it prints is what would be charged (AC2).
        /// </summary>
        public static string ExplainValue(ModState state, Kingdom patron, Kingdom vassal)
        {
            var q = QuoteFor(state, patron, vassal);
            var sb = new System.Text.StringBuilder();
            sb.Append(patron.Name).Append(" -> ").Append(vassal.Name).AppendLine(":");

            if (q.War == null)
            {
                sb.Append("  ").Append(q.Reason).AppendLine();
                return sb.ToString();
            }

            sb.Append("  war: ").Append(q.Enemy.Name)
              .AppendLine(q.War.IsObligationWar ? " (an obligation war it answered)" : "");

            // A quote refused before it was priced has nothing more to say than its reason. The
            // arithmetic below would print a price of 0 that nobody was quoted.
            if (!q.Priced)
            {
                sb.Append("  hold ").Append(q.Hold.ToString("0.0")).AppendLine();
                sb.Append(q.Excused ? "  EXCUSED: " : "  not possible: ").AppendLine(Strip(q.Reason));
                return sb.ToString();
            }

            sb.Append("  parties: ").Append(q.PartyCount).Append(" of ").Append(q.Available)
              .AppendLine(" eligible, half the cap, nearest the summoner first");
            sb.Append("  hold ").Append(q.Hold.ToString("0.0"))
              .Append(q.WillServe ? " - it would serve" : " - it would refuse: " + q.Refusal).AppendLine();

            sb.Append("  influence ").Append(DiplomacyConstants.SummonsInfluenceBase).Append(" + ")
              .Append(DiplomacyConstants.SummonsInfluencePerParty).Append(" x ").Append(q.PartyCount)
              .Append(" = ").Append(DiplomacyConstants.SummonsInfluenceBase + DiplomacyConstants.SummonsInfluencePerParty * q.PartyCount)
              .Append(" x ").Append(q.InfluenceFactor.ToString("0.00"))
              .Append(" (Leadership, ").Append(q.Summoner != null ? q.Summoner.Name.ToString() : "?").Append(")")
              .Append(" = ").Append(q.Influence).AppendLine();
            sb.Append("  gold ").Append(DiplomacyConstants.SummonsGoldBase).Append(" + ")
              .Append(DiplomacyConstants.SummonsGoldPerParty).Append(" x ").Append(q.PartyCount)
              .Append(" = ").Append(DiplomacyConstants.SummonsGoldBase + DiplomacyConstants.SummonsGoldPerParty * q.PartyCount)
              .Append(" x ").Append(q.GoldFactor.ToString("0.00"))
              .AppendLine(" (Trade, the realm's treasurer) = " + q.Gold.ToString("N0"));

            sb.Append("  the army charges ").Append(q.PartyCount)
              .Append(" of its own per-party cost to ").Append(q.Summoner != null ? q.Summoner.Name.ToString() : "?")
              .AppendLine(" on top of this (design 04 §5.2a)");
            sb.Append("  duration ").Append(DiplomacyConstants.SummonsDurationDays)
              .Append(" days, or until the war ends; cooldown ").Append(DiplomacyConstants.SummonsCooldownDays)
              .AppendLine(" days");

            if (q.Excused) sb.Append("  EXCUSED: ").AppendLine(Strip(q.Reason));
            else if (!q.Eligible) sb.Append("  not possible: ").AppendLine(q.Reason);
            else if (!q.Affordable) sb.Append("  cannot pay: ").AppendLine(q.Short);
            return sb.ToString();
        }

        private static string Strip(string reason)
            => reason != null && reason.StartsWith(Excused) ? reason.Substring(Excused.Length) : reason;

        private static void Announce(string text, Color color)
        {
            if (!Settings.Current.AnnounceAiDecisions) return;
            Log.Notify(text, color);
        }
    }
}