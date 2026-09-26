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
    /// Making amends (design 09 C1): the ruler answers one grievance a sworn house holds against
    /// the crown, pays influence and gold for it, and the grievance's weight goes to nothing. The
    /// verb Phase 2's acceptance line needed - "the player can survive it by managing grievances" -
    /// and, by CLAUDE.md §3, the same verb an AI ruler uses.
    ///
    /// **One price for everybody.** <see cref="QuoteFor"/> is the only place a price is made; the
    /// Court tab, the AI's daily choice, the diagnostic and <see cref="Execute"/> all read it, so
    /// the number on the button is the number the AI weighed. The price follows the lead's rule of
    /// 2026-09-26 (design 09 §0): influence and gold together, each scaled by its skill through
    /// <see cref="StatecraftTerms.PriceFactor(Hero, Hero, SkillObject)"/> - the crown's envoy against
    /// the house's best Charm for the influence, the two treasurers' Trade for the gold.
    ///
    /// **Where the gold goes.** To the head of the house: reparations, not a fee to nobody. Gold
    /// does not vanish from the world, and a bribe's gold reaches its lord the same way (design 03).
    /// The influence is spent.
    ///
    /// Nothing here takes an "is this the player" argument. The one player branch is who decides
    /// for a realm: an AI ruler's daily choice is made here, the player's on the Court tab.
    /// </summary>
    public static class Amends
    {
        /// <summary>A priced answer to one grievance, term by term, and whether it can be paid.</summary>
        public sealed class Quote
        {
            public Grievance Grievance;
            public Kingdom Kingdom;
            public Clan House;
            public Hero Ruler;

            /// <summary>False when the grievance cannot be answered at all; <see cref="Reason"/> says why.</summary>
            public bool Eligible;
            public string Reason;

            public float Weight;
            public float Standing;
            public float Memory;

            public Hero Envoy;
            public Hero TheirCharm;
            public float CharmFactor;
            public Hero Treasurer;
            public Hero TheirTrade;
            public float TradeFactor;

            public int Influence;
            public int Gold;

            public float LoyaltyBefore;
            public float LoyaltyAfter;

            /// <summary>The ruler holds both parts of the price.</summary>
            public bool Affordable;
            /// <summary>What the ruler is short of, when not affordable.</summary>
            public string Short;
        }

        // ----- The price ---------------------------------------------------------------

        /// <summary>
        /// What answering <paramref name="grievance"/> would cost its crown now, and what it would
        /// do. The one resolver of the price (design 09 §1).
        /// </summary>
        public static Quote QuoteFor(ModState state, Grievance grievance)
        {
            var q = new Quote { Grievance = grievance };
            var house = grievance?.Holder;
            var kingdom = house?.Kingdom;
            q.House = house;
            q.Kingdom = kingdom;
            q.Ruler = kingdom?.Leader;

            if (state == null || grievance == null || house == null) { q.Reason = "nothing to answer"; return q; }
            if (!Settings.Current.EnableIntrigue) { q.Reason = "court intrigue is switched off"; return q; }
            if (grievance.Weight <= 0f) { q.Reason = "nothing left to answer"; return q; }
            if (kingdom == null || !kingdom.IsRealm()) { q.Reason = house.Name + " is not sworn to a realm"; return q; }
            if (!Court.IsMember(house)) { q.Reason = house.Name + " has no seat at court"; return q; }
            if (grievance.Target != kingdom.RulingClan) { q.Reason = "it is held against another house, not the crown"; return q; }
            if (q.Ruler == null || !q.Ruler.IsAlive) { q.Reason = "the throne is empty"; return q; }
            if (InternalWars.TryFaction(house, out _))
            {
                // A civil war is settled by buying a house back (design 07 §6), which has its own
                // price. Amends to a house in arms would be a second, cheaper road to the same end.
                q.Reason = house.Name + " is in arms against the crown; a house in the rising is bought back, not appeased";
                return q;
            }

            q.Eligible = true;
            q.Weight = grievance.Weight;
            q.Standing = StatecraftModel.Clamp(SuccessionModel.InfluenceRatio(house, kingdom, peersOnly: true),
                IntrigueConstants.AmendsStandingMin, IntrigueConstants.AmendsStandingMax);
            q.Memory = GrievanceRegistry.AnsweredRecently(state, house, kingdom.RulingClan)
                ? IntrigueConstants.AmendsRepeatPriceFactor : 1f;

            q.Envoy = StatecraftModel.Actor(kingdom, Portfolio.Envoy);
            q.TheirCharm = StatecraftModel.Actor(house, Portfolio.Envoy);
            q.CharmFactor = StatecraftTerms.PriceFactor(q.Envoy, q.TheirCharm, DefaultSkills.Charm);
            q.Treasurer = StatecraftModel.Actor(kingdom, Portfolio.Treasurer);
            q.TheirTrade = StatecraftModel.Actor(house, Portfolio.Treasurer);
            q.TradeFactor = StatecraftTerms.PriceFactor(q.Treasurer, q.TheirTrade, DefaultSkills.Trade);

            var common = q.Weight * q.Standing * q.Memory;
            q.Influence = (int)Math.Ceiling(common * IntrigueConstants.AmendsInfluencePerPoint * q.CharmFactor);
            q.Gold = (int)(Math.Round(common * IntrigueConstants.AmendsGoldPerPoint * q.TradeFactor / 100f) * 100f);

            // The loyalty it buys is the grievance term it removes, through the same factor that
            // put it there - read off the breakdown, not recomputed.
            var explained = LoyaltyModel.Explain(state, house);
            q.LoyaltyBefore = explained.Total;
            var raw = explained.Raw + q.Weight * IntrigueConstants.LoyaltyGrievanceFactor;
            q.LoyaltyAfter = raw < 0f ? 0f : (raw > 100f ? 100f : raw);

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

        // ----- The act -----------------------------------------------------------------

        /// <summary>
        /// The crown answers <paramref name="grievance"/>: re-priced now, paid in full, recorded.
        /// Everything is checked again here, whoever asked, so a stale button cannot buy at an old
        /// price. Returns false with the reason when it cannot be done.
        /// </summary>
        public static bool Execute(ModState state, Grievance grievance, out string failed)
        {
            failed = null;
            var q = QuoteFor(state, grievance);
            if (!q.Eligible) { failed = q.Reason; return false; }
            if (!q.Affordable) { failed = q.Short; return false; }

            var kingdom = q.Kingdom;
            var house = q.House;
            var ruler = q.Ruler;

            ChangeClanInfluenceAction.Apply(kingdom.RulingClan, -q.Influence);

            var head = house.Leader;
            if (head != null && head.IsAlive && head != ruler)
                GiveGoldAction.ApplyBetweenCharacters(ruler, head, q.Gold,
                    disableNotification: ruler != Hero.MainHero && head != Hero.MainHero);
            else
                ruler.ChangeHeroGold(-q.Gold);

            var type = grievance.Type;
            GrievanceRegistry.Answer(grievance);
            SkillXp.AmendsMade(q);

            Log.Info("Amends", kingdom.Name + " (" + ruler.Name + ") made amends to " + house.Name
                               + " for " + type + " " + q.Weight.ToString("0.0") + ": " + q.Influence
                               + " influence, " + q.Gold + " denars (standing x" + q.Standing.ToString("0.00")
                               + ", memory x" + q.Memory.ToString("0.#") + ", Charm x" + q.CharmFactor.ToString("0.00")
                               + ", Trade x" + q.TradeFactor.ToString("0.00") + "). Loyalty "
                               + q.LoyaltyBefore.ToString("0.0") + " -> " + LoyaltyModel.Of(state, house).ToString("0.0") + ".");

            Telemetry.Event("amends", "kingdom", kingdom, "house", house, "type", type.ToString(),
                "weight", q.Weight, "influence", q.Influence, "gold", q.Gold, "standing", q.Standing,
                "memory", q.Memory, "charmFactor", q.CharmFactor, "tradeFactor", q.TradeFactor,
                "loyaltyBefore", q.LoyaltyBefore, "loyaltyAfter", LoyaltyModel.Of(state, house));

            // Told to the player when it touches them: their own act, or their own house answered.
            if (ruler == Hero.MainHero)
                Log.Notify("You made amends to " + house.Name + ": " + q.Influence + " influence and "
                           + q.Gold.ToString("N0") + " denars. They set the grievance aside.", Colors.Green);
            else if (house == Clan.PlayerClan)
                Log.Notify(ruler.Name + " has made amends to your house for "
                           + GrievanceRegistry.TitleOf(type).ToLowerInvariant()
                           + ". Your grievance (" + q.Weight.ToString("0.0") + ") is set aside.", Colors.Cyan);
            return true;
        }

        // ----- The AI (design 09 §1, D6) --------------------------------------------------

        /// <summary>What an AI ruler would answer today, and why it answers nothing if it would not.</summary>
        public sealed class AiChoice
        {
            public Quote Pick;
            public string Why;
            public float Score;
            public int Candidates;
        }

        /// <summary>
        /// An AI ruler whose court is under threat - a Pretenders bloc has formed, or a sworn house
        /// is below the defection line - answers one grievance a day: the one that moves the most
        /// court weight out of danger per point of influence, from what is left above its reserves.
        /// A dry run: nothing is paid.
        /// </summary>
        public static AiChoice PlanFor(ModState state, Kingdom kingdom)
        {
            var choice = new AiChoice();
            if (state == null || kingdom == null || !kingdom.IsRealm()) { choice.Why = "not a realm"; return choice; }
            var ruling = kingdom.RulingClan;
            var ruler = kingdom.Leader;
            if (ruling == null || ruler == null) { choice.Why = "no ruler"; return choice; }

            // Who is in danger, and what the crown keeps back: one answer for both court acts.
            var danger = CourtThreat.DangerHouses(state, kingdom);
            if (danger.Count == 0) { choice.Why = "the court is not under threat"; return choice; }

            var influenceReserve = CourtThreat.InfluenceReserve(state, kingdom);
            var spendableInfluence = ruling.Influence - influenceReserve;
            var spendableGold = ruler.Gold - DiplomacyConstants.AiGoldReserve;

            foreach (var clan in danger)
            {
                foreach (var g in GrievanceRegistry.Of(state, clan))
                {
                    if (g.Target != ruling) continue;
                    var q = QuoteFor(state, g);
                    if (!q.Eligible) continue;
                    choice.Candidates++;
                    if (q.Influence > spendableInfluence || q.Gold > spendableGold) continue;

                    // What the answer really moves: loyalty is clamped at 0, so a house deep below
                    // it gains nothing visible from one amends (live 2026-09-26: fen Uvain, 0.0 ->
                    // 0.0). Scoring the grievance's weight instead would pay for nothing.
                    var gain = q.LoyaltyAfter - q.LoyaltyBefore;
                    if (gain <= 0f) continue;
                    var weight = Math.Max(SuccessionModel.InfluenceRatio(clan, kingdom, peersOnly: true), 0.1f);
                    var score = gain * weight / Math.Max(q.Influence, 1);
                    if (choice.Pick != null && score <= choice.Score) continue;
                    choice.Pick = q;
                    choice.Score = score;
                }
            }

            if (choice.Pick != null) choice.Why = "answers " + choice.Pick.House.Name;
            else if (choice.Candidates == 0) choice.Why = "no grievance held against the crown by a house in danger";
            else choice.Why = "cannot afford it above its reserves (" + influenceReserve.ToString("0")
                              + " influence, " + DiplomacyConstants.AiGoldReserve.ToString("N0") + " denars)";
            return choice;
        }

        /// <summary>
        /// The AI ruler of <paramref name="kingdom"/> makes today's amends, if its court is under threat
        /// and it can pay. Called by <see cref="IntrigueUpkeep.AiCourtDaily"/>, which gives each AI realm
        /// one court act a day, before the internal-war check (design 09 D17). Returns whether it acted.
        /// </summary>
        public static bool TryAi(ModState state, Kingdom kingdom)
        {
            var choice = PlanFor(state, kingdom);
            if (choice.Pick == null) return false;
            if (Execute(state, choice.Pick.Grievance, out var failed)) return true;
            Log.Info("Amends", kingdom.Name + " meant to answer " + choice.Pick.House.Name + " and could not: " + failed);
            return false;
        }

        // ----- Words ---------------------------------------------------------------------

        /// <summary>
        /// The price, term by term, as label and value: the Court tab's lines and the diagnostic's.
        /// One set of words for both, so the screen and the log cannot describe the price
        /// differently.
        /// </summary>
        public static List<KeyValuePair<string, string>> PriceTerms(Quote q)
        {
            var terms = new List<KeyValuePair<string, string>>();
            if (q == null || !q.Eligible) return terms;
            terms.Add(Term("Weight " + q.Weight.ToString("0.0") + " at " + IntrigueConstants.AmendsInfluencePerPoint.ToString("0")
                           + " influence and " + IntrigueConstants.AmendsGoldPerPoint.ToString("N0") + " denars a point",
                           (q.Weight * IntrigueConstants.AmendsInfluencePerPoint).ToString("0") + " / "
                           + (q.Weight * IntrigueConstants.AmendsGoldPerPoint).ToString("N0")));
            terms.Add(Term("Their standing at court", "x" + q.Standing.ToString("0.00")));
            if (q.Memory > 1f)
                terms.Add(Term("Amends to this house within " + IntrigueConstants.AmendsMemoryYears.ToString("0") + " years",
                               "x" + q.Memory.ToString("0.#")));
            terms.Add(Term("Envoy " + Skilled(q.Envoy, DefaultSkills.Charm) + " against " + Skilled(q.TheirCharm, DefaultSkills.Charm),
                           "influence x" + q.CharmFactor.ToString("0.00")));
            terms.Add(Term("Treasurer " + Skilled(q.Treasurer, DefaultSkills.Trade) + " against " + Skilled(q.TheirTrade, DefaultSkills.Trade),
                           "denars x" + q.TradeFactor.ToString("0.00")));
            return terms;
        }

        public static List<string> PriceLines(Quote q)
        {
            var lines = new List<string>();
            foreach (var t in PriceTerms(q)) lines.Add(t.Key + ": " + t.Value);
            return lines;
        }

        private static KeyValuePair<string, string> Term(string label, string value)
            => new KeyValuePair<string, string>(label, value);

        /// <summary>
        /// What answering every open grievance against this crown would cost at today's prices,
        /// or null when nothing is open. The Court tab's line under the roster (design 09 §4).
        /// </summary>
        public static string AllOpenCost(ModState state, Kingdom kingdom)
        {
            if (state == null || kingdom?.RulingClan == null) return null;
            var influence = 0;
            var gold = 0;
            var houses = 0;
            var clans = kingdom.Clans;
            for (var i = 0; i < clans.Count; i++)
            {
                var clan = clans[i];
                if (clan == kingdom.RulingClan || !Court.IsMember(clan)) continue;
                var counted = false;
                foreach (var g in GrievanceRegistry.Of(state, clan))
                {
                    if (g.Target != kingdom.RulingClan) continue;
                    var q = QuoteFor(state, g);
                    if (!q.Eligible) continue;
                    influence += q.Influence;
                    gold += q.Gold;
                    counted = true;
                }
                if (counted) houses++;
            }
            if (houses == 0) return null;
            return "Answering all " + houses + (houses == 1 ? " house" : " houses") + " would cost " + influence.ToString("N0")
                   + " influence and " + gold.ToString("N0") + " denars; the crown holds "
                   + kingdom.RulingClan.Influence.ToString("N0") + " and " + (kingdom.Leader == null ? 0 : kingdom.Leader.Gold).ToString("N0") + ".";
        }

        private static string Skilled(Hero hero, SkillObject skill)
            => hero == null ? "nobody" : StatecraftModel.NameOf(hero) + " (" + StatecraftModel.SkillName(skill) + " " + hero.GetSkillValue(skill) + ")";

        /// <summary><c>diplomacy.amends</c>: every open grievance against one crown, priced, and the AI's pick.</summary>
        public static string Describe(ModState state, Kingdom kingdom, Clan only = null)
        {
            var sb = new StringBuilder();
            if (state == null || kingdom == null) return "No such realm.";
            var ruling = kingdom.RulingClan;
            sb.AppendLine(kingdom.Name + " - ruled by " + StatecraftModel.NameOf(kingdom.Leader)
                          + "; influence " + (ruling == null ? 0f : ruling.Influence).ToString("0")
                          + ", treasury " + (kingdom.Leader == null ? 0 : kingdom.Leader.Gold).ToString("N0"));

            var any = false;
            var clans = kingdom.Clans;
            for (var i = 0; i < clans.Count; i++)
            {
                var clan = clans[i];
                if (clan == ruling || !Court.IsMember(clan)) continue;
                if (only != null && clan != only) continue;
                foreach (var g in GrievanceRegistry.Of(state, clan))
                {
                    if (g.Target != ruling) continue;
                    any = true;
                    var q = QuoteFor(state, g);
                    sb.AppendLine("  " + clan.Name + ": " + g.Type + " " + g.Weight.ToString("0.0"));
                    if (!q.Eligible) { sb.AppendLine("      cannot be answered: " + q.Reason); continue; }
                    foreach (var line in PriceLines(q)) sb.AppendLine("      " + line);
                    sb.AppendLine("      = " + q.Influence + " influence, " + q.Gold.ToString("N0") + " denars; loyalty "
                                  + q.LoyaltyBefore.ToString("0.0") + " -> " + q.LoyaltyAfter.ToString("0.0")
                                  + (q.Affordable ? "" : " (cannot pay: " + q.Short + ")"));
                }
                foreach (var g in GrievanceRegistry.AnsweredOf(state, clan, ruling))
                    sb.AppendLine("  " + clan.Name + ": " + g.Type + " answered " + g.AnsweredOn.ElapsedDaysUntilNow.ToString("0")
                                  + " days ago (x" + g.Answers + "), remembered");
            }
            if (!any) sb.AppendLine("  No grievance is held against this crown" + (only == null ? "." : " by " + only.Name + "."));

            if (kingdom.Leader == Hero.MainHero)
                sb.AppendLine("  The AI does not choose here: you rule.");
            else
            {
                var plan = PlanFor(state, kingdom);
                sb.AppendLine("  AI today: " + plan.Why
                              + (plan.Pick == null ? "" : " (" + plan.Pick.Grievance.Type + ", " + plan.Pick.Influence
                                 + " influence, " + plan.Pick.Gold.ToString("N0") + " denars)"));
            }
            return sb.ToString().TrimEnd();
        }
    }
}
