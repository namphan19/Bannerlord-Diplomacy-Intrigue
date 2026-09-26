using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using DiplomacyIntrigue.Statecraft;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// The seats at a realm's court (design 09 C2, absorbing design 08 §8). A seat makes its holder
    /// the realm's voice in one political skill - <see cref="StatecraftModel.Actor(Kingdom, Portfolio)"/>
    /// reads <see cref="Speaker"/> first - and puts the holder's house in the crown's favour:
    /// <see cref="Patronage"/> on its loyalty and a pull toward the Centralists.
    ///
    /// **One price for everybody**, <see cref="QuoteAppointment"/>, under the lead's rule of
    /// 2026-09-26: 200 influence scaled by the ruler's Leadership, 25,000 denars by the treasurer's
    /// Trade. Taking a seat back is free and costs the house's goodwill. The gold goes to the
    /// appointee: a stipend, not a fee to nobody.
    ///
    /// The one player branch is who decides: an AI ruler chooses in <see cref="TryAi"/>, the player
    /// on the Court tab, and a seat an AI ruler would give the player's own hero is asked, not given.
    /// </summary>
    public static class Offices
    {
        /// <summary>The five seats; Leadership stays the ruler's own.</summary>
        public static readonly Portfolio[] Seats =
            { Portfolio.Envoy, Portfolio.Steward, Portfolio.Treasurer, Portfolio.Spymaster, Portfolio.Watch };

        // ----- Reading -----------------------------------------------------------------

        public static CourtOffice RecordOf(ModState state, Kingdom kingdom, Portfolio seat)
        {
            if (state == null || kingdom == null) return null;
            for (var i = 0; i < state.Offices.Count; i++)
            {
                var o = state.Offices[i];
                if (o.Kingdom == kingdom && o.Seat == seat) return o;
            }
            return null;
        }

        /// <summary>
        /// Whether a record still stands: the holder alive, grown, of a house sworn to that realm
        /// and not in arms against it. A record that fails this is vacated by <see cref="DailyTick"/>,
        /// with no grievance - a death or a departure is not a dismissal.
        /// </summary>
        public static bool Stands(CourtOffice o)
        {
            if (o?.Kingdom == null || o.Holder == null || !o.Kingdom.IsRealm()) return false;
            var hero = o.Holder;
            if (!hero.IsAlive || hero.IsChild) return false;
            var clan = hero.Clan;
            if (clan == null || clan.Kingdom != o.Kingdom || !Court.IsMember(clan)) return false;
            return !InternalWars.TryFaction(clan, out _);
        }

        /// <summary>
        /// Who speaks for <paramref name="kingdom"/> in <paramref name="seat"/> because they were
        /// appointed, or null for an empty seat or one whose holder cannot act today (a captive):
        /// the ruling house's best speaks then, as before C2. Read by
        /// <see cref="StatecraftModel.Actor(Kingdom, Portfolio)"/>, with no state to hand it.
        /// </summary>
        public static Hero Speaker(Kingdom kingdom, Portfolio seat)
        {
            if (kingdom == null || seat == Portfolio.Ruler) return null;
            try
            {
                if (!Settings.Current.EnableIntrigue) return null;
                var o = RecordOf(Behaviors.CoreBehavior.State, kingdom, seat);
                return o != null && Stands(o) && StatecraftModel.CanAct(o.Holder) ? o.Holder : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>How many seats the house's own hold in its realm's court.</summary>
        public static int SeatsHeldBy(ModState state, Clan clan)
        {
            if (state == null || clan?.Kingdom == null) return 0;
            var n = 0;
            for (var i = 0; i < state.Offices.Count; i++)
            {
                var o = state.Offices[i];
                if (o.Kingdom == clan.Kingdom && o.Holder?.Clan == clan && Stands(o)) n++;
            }
            return n;
        }

        /// <summary>The loyalty a house draws from its seats: +8 for one, +4 more for a second (D9).</summary>
        public static float Patronage(ModState state, Clan clan) => PatronageFor(SeatsHeldBy(state, clan));

        private static float PatronageFor(int seats)
            => seats <= 0 ? 0f
                : IntrigueConstants.OfficePatronageFirst + (seats >= 2 ? IntrigueConstants.OfficePatronageSecond : 0f);

        /// <summary>The seat a hero holds in their realm, or null.</summary>
        public static CourtOffice SeatOf(ModState state, Hero hero)
        {
            if (state == null || hero == null) return null;
            for (var i = 0; i < state.Offices.Count; i++)
            {
                var o = state.Offices[i];
                if (o.Holder == hero && Stands(o)) return o;
            }
            return null;
        }

        /// <summary>
        /// The house's best hero for a seat who could take it today: alive, grown, free, not the
        /// ruler and not holding another seat of the same realm. Null when there is nobody.
        /// </summary>
        public static Hero CandidateFrom(ModState state, Clan house, Portfolio seat)
        {
            if (house == null) return null;
            var skill = StatecraftModel.SkillOf(seat);
            Hero best = null;
            var bestSkill = int.MinValue;
            Consider(state, house, house.AliveLords, skill, ref best, ref bestSkill);
            Consider(state, house, house.Companions, skill, ref best, ref bestSkill);
            return best;
        }

        private static void Consider(ModState state, Clan house, IReadOnlyList<Hero> heroes, SkillObject skill,
                                     ref Hero best, ref int bestSkill)
        {
            if (heroes == null) return;
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                if (!StatecraftModel.CanAct(hero) || hero == house.Kingdom?.Leader) continue;
                if (SeatOf(state, hero) != null) continue;
                var value = hero.GetSkillValue(skill);
                if (value <= bestSkill) continue;
                best = hero;
                bestSkill = value;
            }
        }

        // ----- The price -----------------------------------------------------------------

        public sealed class AppointQuote
        {
            public Kingdom Kingdom;
            public Portfolio Seat;
            public Hero Candidate;
            public Hero Ruler;
            public CourtOffice Current;

            public bool Eligible;
            public string Reason;

            public Hero Treasurer;
            public float LeadershipFactor;
            public float TradeFactor;
            public int Influence;
            public int Gold;

            /// <summary>Who speaks for the seat now, and the skill values before and after.</summary>
            public Hero SpeakerNow;
            public int SkillNow;
            public int SkillAfter;

            /// <summary>The candidate's house, when appointing puts it in the crown's favour.</summary>
            public Clan FavouredHouse;
            public float LoyaltyBefore;
            public float LoyaltyAfter;

            /// <summary>The house that loses the seat, when it is not the crown's own - it takes a grievance.</summary>
            public Clan DisplacedHouse;

            public bool Affordable;
            public string Short;
        }

        /// <summary>
        /// What giving <paramref name="seat"/> to <paramref name="candidate"/> would cost the crown now,
        /// and what it would do. The one resolver of an appointment's price (design 09 §2).
        /// </summary>
        public static AppointQuote QuoteAppointment(ModState state, Kingdom kingdom, Portfolio seat, Hero candidate)
        {
            var q = new AppointQuote { Kingdom = kingdom, Seat = seat, Candidate = candidate, Ruler = kingdom?.Leader };
            if (state == null || kingdom == null || candidate == null) { q.Reason = "nobody to appoint"; return q; }
            if (!Settings.Current.EnableIntrigue) { q.Reason = "court intrigue is switched off"; return q; }
            if (!kingdom.IsRealm()) { q.Reason = kingdom.Name + " is not a realm"; return q; }
            if (seat == Portfolio.Ruler) { q.Reason = "Leadership is the ruler's own"; return q; }
            if (q.Ruler == null || !q.Ruler.IsAlive) { q.Reason = "the throne is empty"; return q; }
            if (candidate == q.Ruler) { q.Reason = "the ruler holds no seat of their own court"; return q; }
            if (!StatecraftModel.CanAct(candidate)) { q.Reason = candidate.Name + " cannot serve today"; return q; }
            var house = candidate.Clan;
            if (house == null || house.Kingdom != kingdom || !Court.IsMember(house))
            {
                q.Reason = candidate.Name + " is not of a house sworn to " + kingdom.Name;
                return q;
            }
            if (InternalWars.TryFaction(house, out _)) { q.Reason = house.Name + " is in arms against the crown"; return q; }
            var held = SeatOf(state, candidate);
            if (held != null)
            {
                q.Reason = candidate.Name + " already holds the " + StatecraftModel.TitleOf(held.Seat) + "'s seat";
                return q;
            }

            q.Eligible = true;
            q.Current = RecordOf(state, kingdom, seat);
            if (q.Current != null && !Stands(q.Current)) q.Current = null;

            var skill = StatecraftModel.SkillOf(seat);
            q.SpeakerNow = StatecraftModel.Actor(kingdom, seat);
            q.SkillNow = q.SpeakerNow == null ? 0 : q.SpeakerNow.GetSkillValue(skill);
            q.SkillAfter = candidate.GetSkillValue(skill);

            q.LeadershipFactor = StatecraftTerms.PriceFactor(q.Ruler, DefaultSkills.Leadership);
            q.Treasurer = StatecraftModel.Actor(kingdom, Portfolio.Treasurer);
            q.TradeFactor = StatecraftTerms.PriceFactor(q.Treasurer, DefaultSkills.Trade);
            q.Influence = (int)Math.Ceiling(IntrigueConstants.OfficeInfluencePrice * q.LeadershipFactor);
            q.Gold = (int)(Math.Round(IntrigueConstants.OfficeGoldPrice * q.TradeFactor / 100f) * 100f);

            // The favour it buys, read off the loyalty breakdown the court already shows. The crown's
            // own house has no loyalty to buy.
            if (house != kingdom.RulingClan)
            {
                q.FavouredHouse = house;
                var explained = LoyaltyModel.Explain(state, house);
                q.LoyaltyBefore = explained.Total;
                // A seat taken from one of the house's own leaves its count where it was (live
                // 2026-09-26: replacing Koltit's envoy with another of Koltit was priced as a second seat).
                var seats = SeatsHeldBy(state, house);
                var sameHouse = q.Current?.Holder?.Clan == house;
                var raw = explained.Raw + PatronageFor(sameHouse ? seats : seats + 1) - PatronageFor(seats);
                q.LoyaltyAfter = raw < 0f ? 0f : (raw > 100f ? 100f : raw);
            }

            var displaced = q.Current?.Holder?.Clan;
            if (displaced != null && displaced != kingdom.RulingClan && displaced != house) q.DisplacedHouse = displaced;

            var influence = kingdom.RulingClan.Influence;
            var gold = q.Ruler.Gold;
            q.Affordable = influence >= q.Influence && gold >= q.Gold;
            if (!q.Affordable)
                q.Short = influence < q.Influence && gold < q.Gold
                    ? "the crown holds neither the influence nor the denars"
                    : influence < q.Influence
                        ? "the crown holds " + influence.ToString("0") + " influence of " + q.Influence
                        : "the treasury holds " + gold.ToString("N0") + " of " + q.Gold.ToString("N0") + " denars";
            return q;
        }

        // ----- The acts ------------------------------------------------------------------

        /// <summary>
        /// The crown gives <paramref name="seat"/> to <paramref name="candidate"/>: re-priced now, paid,
        /// recorded. Whoever held it loses it - their house takes a grievance unless it is the
        /// crown's own. Returns false with the reason when it cannot be done.
        /// </summary>
        public static bool Appoint(ModState state, Kingdom kingdom, Portfolio seat, Hero candidate, out string failed)
        {
            failed = null;
            var q = QuoteAppointment(state, kingdom, seat, candidate);
            if (!q.Eligible) { failed = q.Reason; return false; }
            if (!q.Affordable) { failed = q.Short; return false; }

            var ruler = q.Ruler;
            ChangeClanInfluenceAction.Apply(kingdom.RulingClan, -q.Influence);
            if (candidate.Clan == ruler.Clan && candidate.Clan?.Leader == ruler)
                ruler.ChangeHeroGold(-q.Gold);   // the stipend stays in the crown's own purse
            else
                GiveGoldAction.ApplyBetweenCharacters(ruler, candidate, q.Gold,
                    disableNotification: ruler != Hero.MainHero && candidate != Hero.MainHero);

            var old = RecordOf(state, kingdom, seat);
            if (old != null) Remove(state, old, dismissed: old.Holder != candidate, reason: "gave the seat to " + candidate.Name);

            state.Offices.Add(new CourtOffice(kingdom, seat, candidate));
            BlocModel.Invalidate();
            SkillXp.AppointmentMade(q);

            Log.Info("Offices", kingdom.Name + " (" + ruler.Name + ") appointed " + candidate.Name + " of " + candidate.Clan?.Name
                                + " " + StatecraftModel.TitleOf(seat) + ": " + q.Influence + " influence, " + q.Gold
                                + " denars (Leadership x" + q.LeadershipFactor.ToString("0.00") + ", Trade x"
                                + q.TradeFactor.ToString("0.00") + "). " + StatecraftModel.SkillName(StatecraftModel.SkillOf(seat))
                                + " " + q.SkillNow + " -> " + q.SkillAfter
                                + (q.FavouredHouse == null ? "" : "; " + q.FavouredHouse.Name + " loyalty "
                                   + q.LoyaltyBefore.ToString("0.0") + " -> " + LoyaltyModel.Of(state, q.FavouredHouse).ToString("0.0"))
                                + ".");
            Telemetry.Event("office_appointed", "kingdom", kingdom, "seat", seat.ToString(), "holder", candidate,
                "house", candidate.Clan, "influence", q.Influence, "gold", q.Gold, "skillBefore", q.SkillNow,
                "skillAfter", q.SkillAfter, "loyaltyBefore", q.LoyaltyBefore,
                "loyaltyAfter", q.FavouredHouse == null ? 0f : LoyaltyModel.Of(state, q.FavouredHouse));

            if (ruler == Hero.MainHero)
                Log.Notify(candidate.Name + " is your " + StatecraftModel.TitleOf(seat) + ": " + q.Influence + " influence and "
                           + q.Gold.ToString("N0") + " denars.", Colors.Green);
            else if (candidate == Hero.MainHero || candidate.Clan == Clan.PlayerClan)
                Log.Notify(ruler.Name + " has named " + (candidate == Hero.MainHero ? "you" : candidate.Name.ToString())
                           + " " + StatecraftModel.TitleOf(seat) + " of " + kingdom.Name + ".", Colors.Cyan);
            return true;
        }

        /// <summary>The crown takes <paramref name="seat"/> back. Free; the holder's house remembers it.</summary>
        public static bool Dismiss(ModState state, Kingdom kingdom, Portfolio seat, out string failed)
        {
            failed = null;
            var o = RecordOf(state, kingdom, seat);
            if (o == null) { failed = "nobody holds the " + StatecraftModel.TitleOf(seat) + "'s seat"; return false; }
            Remove(state, o, dismissed: true, reason: "dismissed");
            return true;
        }

        private static void Remove(ModState state, CourtOffice o, bool dismissed, string reason)
        {
            state.Offices.Remove(o);
            BlocModel.Invalidate();

            var house = o.Holder?.Clan;
            var ruling = o.Kingdom?.RulingClan;
            var grieved = dismissed && house != null && ruling != null && house != ruling && house.Kingdom == o.Kingdom;
            if (grieved)
                GrievanceRegistry.Add(state, house, ruling, GrievanceType.DismissedFromOffice, -1f,
                    reason: o.Holder.Name + " lost the " + StatecraftModel.TitleOf(o.Seat) + "'s seat");

            Log.Info("Offices", o + " " + (dismissed ? "taken back" : "vacated") + " (" + reason + ")"
                                + (grieved ? "; " + house.Name + " takes a grievance" : "") + ".");
            Telemetry.Event(dismissed ? "office_dismissed" : "office_vacated", "kingdom", o.Kingdom,
                "seat", o.Seat.ToString(), "holder", o.Holder, "house", house, "reason", reason);

            if (o.Holder == Hero.MainHero)
                Log.Notify("You no longer hold the " + StatecraftModel.TitleOf(o.Seat) + "'s seat in " + o.Kingdom?.Name
                           + (dismissed ? "; your house will remember it." : "."), dismissed ? Colors.Red : Colors.Yellow);
        }

        /// <summary>
        /// Seats whose holder died, grew away from the realm or took up arms against it fall empty,
        /// with no grievance. Part of the daily intrigue upkeep, before the AI's court acts.
        /// </summary>
        public static void DailyTick(ModState state)
        {
            if (state == null || state.Offices.Count == 0) return;
            for (var i = state.Offices.Count - 1; i >= 0; i--)
            {
                var o = state.Offices[i];
                if (Stands(o)) continue;
                Remove(state, o, dismissed: false, reason: WhyFallen(o));
            }
        }

        private static string WhyFallen(CourtOffice o)
        {
            var hero = o.Holder;
            if (hero == null || !hero.IsAlive) return "the holder is dead";
            if (o.Kingdom == null || !o.Kingdom.IsRealm()) return "the realm is gone";
            if (hero.Clan == null || hero.Clan.Kingdom != o.Kingdom) return "the holder's house left the realm";
            if (InternalWars.TryFaction(hero.Clan, out _)) return "the holder's house took up arms against the crown";
            return "the holder can no longer serve";
        }

        // ----- The AI (design 09 D12, daily by D17) ------------------------------------------

        public sealed class AiChoice
        {
            public Portfolio Seat;
            public Hero Candidate;
            public AppointQuote Quote;
            public string Why;
        }

        /// <summary>
        /// Days an AI ruler waits before offering a seat again to a player who declined one. Session
        /// state, like the other "asked recently" memories: a reload may ask again, which is harmless.
        /// </summary>
        private static readonly Dictionary<Kingdom, CampaignTime> PlayerDeclinedUntil = new Dictionary<Kingdom, CampaignTime>();
        private static bool _askingPlayer;

        public static void Reset()
        {
            PlayerDeclinedUntil.Clear();
            _askingPlayer = false;
        }

        /// <summary>
        /// What an AI ruler would do with a seat today. Only under threat, as for amends: it gives a
        /// seat to the house in danger that weighs most at court and holds none, choosing the seat
        /// where that house's candidate costs the realm least skill - an empty seat or one held by
        /// the crown's own house first, since those take a seat from nobody who would resent it. Not
        /// under threat, it does nothing: an empty seat already speaks through the ruling house's best,
        /// which is what run 08 measured, and paying to seat its own would buy nothing. A dry run.
        /// </summary>
        public static AiChoice PlanFor(ModState state, Kingdom kingdom)
        {
            var choice = new AiChoice();
            if (state == null || kingdom == null || !kingdom.IsRealm()) { choice.Why = "not a realm"; return choice; }
            var ruling = kingdom.RulingClan;
            if (ruling == null || kingdom.Leader == null) { choice.Why = "no ruler"; return choice; }

            var danger = CourtThreat.DangerHouses(state, kingdom);
            if (danger.Count == 0) { choice.Why = "the court is not under threat"; return choice; }

            // The heaviest house in danger that holds no seat yet.
            Clan house = null;
            var heaviest = float.MinValue;
            foreach (var clan in danger)
            {
                if (SeatsHeldBy(state, clan) > 0) continue;
                if (clan == Clan.PlayerClan && DeclinedRecently(kingdom)) continue;
                var weight = SuccessionModel.InfluenceRatio(clan, kingdom, peersOnly: true);
                if (weight <= heaviest) continue;
                heaviest = weight;
                house = clan;
            }
            if (house == null) { choice.Why = "every house in danger already holds a seat, or declined one"; return choice; }

            // The seat: never one held by a house in danger; among the rest, the smallest loss of skill,
            // an unresented seat (empty or the crown's own) before one another house would lose.
            AppointQuote best = null;
            var bestScore = float.MinValue;
            foreach (var seat in Seats)
            {
                var candidate = CandidateFrom(state, house, seat);
                if (candidate == null) continue;
                var current = RecordOf(state, kingdom, seat);
                if (current != null && Stands(current) && danger.Contains(current.Holder.Clan)) continue;

                var q = QuoteAppointment(state, kingdom, seat, candidate);
                if (!q.Eligible || q.LoyaltyAfter <= q.LoyaltyBefore) continue;
                var score = (q.SkillAfter - q.SkillNow) - (q.DisplacedHouse != null ? 1000f : 0f);
                if (best != null && score <= bestScore) continue;
                best = q;
                bestScore = score;
            }
            if (best == null) { choice.Why = house.Name + " has nobody who could take a seat"; return choice; }
            if (!CourtThreat.CanSpend(state, kingdom, best.Influence, best.Gold))
            {
                choice.Why = "cannot afford a seat above its reserves (" + CourtThreat.Reserves(state, kingdom) + ")";
                return choice;
            }

            choice.Seat = best.Seat;
            choice.Candidate = best.Candidate;
            choice.Quote = best;
            choice.Why = "seats " + best.Candidate.Name + " of " + house.Name + " as " + StatecraftModel.TitleOf(best.Seat);
            return choice;
        }

        private static bool DeclinedRecently(Kingdom kingdom)
            => PlayerDeclinedUntil.TryGetValue(kingdom, out var until) && !until.IsPast;

        /// <summary>
        /// The AI ruler of <paramref name="kingdom"/> gives today's seat, if its court is under threat.
        /// A seat for the player's own house is offered to the player instead. Returns whether it acted.
        /// </summary>
        public static bool TryAi(ModState state, Kingdom kingdom)
        {
            var choice = PlanFor(state, kingdom);
            if (choice.Quote == null) return false;

            if (choice.Candidate.Clan == Clan.PlayerClan)
            {
                if (_askingPlayer) return false;
                OfferToPlayer(state, choice);
                return true;
            }

            if (Appoint(state, kingdom, choice.Seat, choice.Candidate, out var failed)) return true;
            Log.Info("Offices", kingdom.Name + " meant to seat " + choice.Candidate.Name + " and could not: " + failed);
            return false;
        }

        /// <summary>An AI king offers a seat to the player's house (D13). Asked, not decided.</summary>
        private static void OfferToPlayer(ModState state, AiChoice choice)
        {
            var kingdom = choice.Quote.Kingdom;
            var q = choice.Quote;
            var title = StatecraftModel.TitleOf(choice.Seat);
            var skill = StatecraftModel.SkillOf(choice.Seat);
            var you = choice.Candidate == Hero.MainHero ? "you" : choice.Candidate.Name.ToString();
            var body = q.Ruler.Name + " asks " + you + " to serve " + kingdom.Name + " as its " + title + ". "
                       + (choice.Candidate == Hero.MainHero ? "Your" : choice.Candidate.Name + "'s") + " "
                       + StatecraftModel.SkillName(skill) + " (" + q.SkillAfter + "; the realms' median "
                       + StatecraftModel.Pivot(skill).ToString("0") + ") would speak for the realm."
                       + Environment.NewLine + Environment.NewLine
                       + "While the seat is held your house stands in the crown's favour (+"
                       + IntrigueConstants.OfficePatronageFirst.ToString("0") + " loyalty to the crown), and "
                       + q.Gold.ToString("N0") + " denars come with it. If the seat is taken back, your house will remember it.";

            _askingPlayer = true;
            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "A seat at " + q.Ruler.Name + "'s table", body, true, true,
                    "Accept the seat", "Decline",
                    () =>
                    {
                        // Runs from the UI, outside any campaign handler's try.
                        try
                        {
                            _askingPlayer = false;
                            if (!Appoint(state, kingdom, choice.Seat, choice.Candidate, out var failed))
                                Log.Notify("The offer no longer stands - " + failed + ".", Colors.Red);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Offices", "Accepting a seat failed.", ex);
                        }
                    },
                    () =>
                    {
                        _askingPlayer = false;
                        PlayerDeclinedUntil[kingdom] = CampaignTime.DaysFromNow(IntrigueConstants.OfficeOfferAgainDays);
                        Log.Info("Offices", "The player declined the " + title + "'s seat in " + kingdom.Name + ".");
                    }), true);
            }
            catch (Exception ex)
            {
                _askingPlayer = false;
                Log.Error("Offices", "Could not show the offer of a seat.", ex);
            }
        }

        // ----- Words ---------------------------------------------------------------------

        /// <summary>The price and effect of an appointment, term by term, for the Court tab and the diagnostic.</summary>
        public static List<KeyValuePair<string, string>> PriceTerms(AppointQuote q)
        {
            var terms = new List<KeyValuePair<string, string>>();
            if (q == null || !q.Eligible) return terms;
            var skill = StatecraftModel.SkillOf(q.Seat);
            terms.Add(Term("The realm's " + StatecraftModel.TitleOf(q.Seat) + ": " + StatecraftModel.NameOf(q.SpeakerNow)
                           + " -> " + StatecraftModel.NameOf(q.Candidate),
                           StatecraftModel.SkillName(skill) + " " + q.SkillNow + " -> " + q.SkillAfter));
            terms.Add(Term(IntrigueConstants.OfficeInfluencePrice.ToString("0") + " influence, by "
                           + StatecraftModel.Who(q.Ruler, DefaultSkills.Leadership), "x" + q.LeadershipFactor.ToString("0.00")));
            terms.Add(Term(IntrigueConstants.OfficeGoldPrice.ToString("N0") + " denars, by "
                           + StatecraftModel.Who(q.Treasurer, DefaultSkills.Trade), "x" + q.TradeFactor.ToString("0.00")));
            if (q.DisplacedHouse != null)
                terms.Add(Term("Taken from " + StatecraftModel.NameOf(q.Current?.Holder) + ": " + q.DisplacedHouse.Name
                               + " takes a grievance", "-" + (IntrigueConstants.GrievanceDismissedFromOffice
                                                             * IntrigueConstants.LoyaltyGrievanceFactor).ToString("0.0") + " loyalty"));
            return terms;
        }

        private static KeyValuePair<string, string> Term(string label, string value)
            => new KeyValuePair<string, string>(label, value);

        /// <summary><c>diplomacy.offices</c>: a realm's seats, who speaks for each, and the AI's plan.</summary>
        public static string Describe(ModState state, Kingdom kingdom)
        {
            var sb = new StringBuilder();
            if (state == null || kingdom == null) return "No such realm.";
            sb.AppendLine(kingdom.Name + " - ruled by " + StatecraftModel.NameOf(kingdom.Leader));
            foreach (var seat in Seats)
            {
                var o = RecordOf(state, kingdom, seat);
                var skill = StatecraftModel.SkillOf(seat);
                var speaker = StatecraftModel.Actor(kingdom, seat);
                sb.AppendLine("  " + StatecraftModel.TitleOf(seat).PadRight(10)
                              + (o == null ? "empty - " : "held by " + o.Holder.Name + " of " + o.Holder.Clan?.Name
                                 + " since " + o.Since.ElapsedDaysUntilNow.ToString("0") + "d"
                                 + (Stands(o) ? "" : " (falls empty at the next daily tick)") + " - ")
                              + "speaks: " + StatecraftModel.Who(speaker, skill));
            }
            foreach (var clan in Court.MembersOf(kingdom))
            {
                var n = SeatsHeldBy(state, clan);
                if (n > 0) sb.AppendLine("  favour: " + clan.Name + " holds " + n + " seat(s), loyalty +" + Patronage(state, clan).ToString("0"));
            }
            if (kingdom.Leader == Hero.MainHero)
                sb.AppendLine("  The AI does not choose here: you rule.");
            else
            {
                var plan = PlanFor(state, kingdom);
                sb.AppendLine("  AI today: " + plan.Why + (plan.Quote == null ? ""
                              : " (" + plan.Quote.Influence + " influence, " + plan.Quote.Gold.ToString("N0") + " denars)"));
            }
            return sb.ToString().TrimEnd();
        }
    }
}
