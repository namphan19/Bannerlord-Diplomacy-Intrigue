using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// A house changing sides in a civil war, for gold (design 07 §6, Phase 2.6c).
    ///
    /// **One price, whichever direction.** The side that gains the house pays its head, from
    /// the purse of that side's leader (the ruler, or the claimant). What the crown pays to win
    /// a house back and what the claimant pays to take one are the same formula - there is no
    /// "buying back" price and no "defecting" price. The Court tab, the AI and the offer made to
    /// the player all read <see cref="QuoteFor"/>; nothing else computes a price.
    ///
    /// **The same rule for the player's house.** A player's house can be bought like any other
    /// (the lead, 2026-09-24). Where an AI house simply accepts - the price is its asking price -
    /// the player is asked, which is the UI-layer difference design 02 §9.2 allows. No argument
    /// here says "is this the player".
    /// </summary>
    public static class SideChange
    {
        /// <summary>A house's price to change sides, the parts it is made of, and whether it can.</summary>
        public sealed class Quote
        {
            public InternalWar War;
            public Clan Clan;

            /// <summary>True when the house would go over to the rising, false when back to the crown.</summary>
            public bool ToRising;

            /// <summary>The leader of the side the house would join, who pays.</summary>
            public Hero Buyer;

            public float Strength;
            public int Towns;
            public int Castles;
            public float RelationFactor = 1f;
            public float BondFactor = 1f;
            public float MomentumFactor = 1f;

            /// <summary>Design 08 S-10: the two treasurers' haggling. 1 with statecraft off.</summary>
            public float HagglingFactor = 1f;

            /// <summary>Design 08 A-3: the buyer's treasurer holds Silver Tongue.</summary>
            public float SilverTongueFactor = 1f;

            public int Price;

            /// <summary>
            /// The price as the lines the Court tab shows, summing exactly to <see cref="Price"/>:
            /// the two base parts, then each factor as the gold it adds or removes.
            /// </summary>
            public readonly List<KeyValuePair<string, int>> Lines = new List<KeyValuePair<string, int>>();

            /// <summary>How many of <see cref="Lines"/>, from the top, are base parts rather than factors.</summary>
            public int BaseLineCount;

            public bool Eligible;

            /// <summary>Why it cannot change sides, when <see cref="Eligible"/> is false.</summary>
            public string Reason;
        }

        // ----- The price ----------------------------------------------------------

        /// <summary>
        /// What the other side's leader would pay for this house today, and whether the house
        /// can change sides at all. Design 07 §6:
        /// <code>
        /// (base + gold x strength + gold per town and castle) x relation x bond x momentum
        /// </code>
        /// Returns null for a clan that is not in this war's kingdom at all.
        /// </summary>
        public static Quote QuoteFor(ModState state, InternalWar war, Clan clan)
        {
            if (war?.Kingdom == null || clan == null) return null;

            var q = new Quote { War = war, Clan = clan, ToRising = !war.IsRebel(clan) };
            q.Buyer = InternalWars.LeaderOf(war, q.ToRising);
            q.Strength = Math.Max(0f, clan.CurrentTotalStrength);
            for (var i = 0; i < clan.Fiefs.Count; i++)
            {
                var fief = clan.Fiefs[i];
                if (fief == null) continue;
                if (fief.IsTown) q.Towns++;
                else if (fief.IsCastle) q.Castles++;
            }

            var head = clan.Leader;

            // Relation with whoever pays: a house that likes them comes cheaper.
            if (head != null && q.Buyer != null)
                q.RelationFactor = Clamp(1f - head.GetRelation(q.Buyer) / 200f, 0.5f, 1.5f);

            // How firmly the house holds to the side it is on now. On the crown's side that is
            // its loyalty - the court's own resolver - and on the rising's its head's standing
            // with the claimant, put on the same 0-100 scale.
            q.BondFactor = 0.5f + BondOf(state, war, clan) / 100f;

            // Joining the side that is losing costs more: the buyer's exhaustion against the other side's.
            var buyerEx = q.ToRising ? war.RebelExhaustion : war.CrownExhaustion;
            var otherEx = q.ToRising ? war.CrownExhaustion : war.RebelExhaustion;
            q.MomentumFactor = Clamp(1f + (buyerEx - otherEx) / 100f,
                IntrigueConstants.SideChangeMomentumMin, IntrigueConstants.SideChangeMomentumMax);

            var men = IntrigueConstants.SideChangeBaseGold + IntrigueConstants.SideChangeGoldPerStrength * q.Strength;
            var land = q.Towns * IntrigueConstants.SideChangeGoldPerTown + q.Castles * IntrigueConstants.SideChangeGoldPerCastle;
            var raw = men + land;
            var afterRelation = raw * q.RelationFactor;
            var afterBond = afterRelation * q.BondFactor;
            var afterMomentum = afterBond * q.MomentumFactor;

            // Design 08 S-10 and A-3: the two treasurers haggle, then the buyer's bargains with
            // Silver Tongue if it has it - vanilla's own discount on persuading a lord to defect,
            // which is exactly this act.
            q.HagglingFactor = Statecraft.StatecraftTerms.HagglingFactor(q.Buyer, clan);
            var afterHaggling = afterMomentum * q.HagglingFactor;
            var silverTongue = Statecraft.StatecraftTerms.SilverTongueHolder(q.Buyer);
            q.SilverTongueFactor = silverTongue == null ? 1f : Statecraft.StatecraftConstants.SilverTongueFactor;
            var afterPerk = afterHaggling * q.SilverTongueFactor;

            q.Price = Math.Max(IntrigueConstants.SideChangeMinimumPrice, (int)(Math.Round(afterPerk / 100f) * 100f));

            // Worded from where the player stands: "you" for the player's hero, "your" for the
            // player's own house, the way the rest of the Court tab speaks.
            var ours = clan == Clan.PlayerClan;
            AddLine(q, ours ? "Your house and its men" : "Their house and its men", men);
            if (land > 0f) AddLine(q, ours ? "The fiefs you would bring" : "The fiefs they would bring", land);
            q.BaseLineCount = q.Lines.Count;
            AddLine(q, (ours ? "How you feel about " : "How they feel about ")
                       + (q.Buyer == null ? "their new leader" : NameOf(q.Buyer)), afterRelation - raw);
            AddLine(q, q.ToRising
                ? (ours ? "Your loyalty to the crown" : "Their loyalty to the crown")
                : (ours ? "What you owe " : "What they owe ")
                  + (war.Claimant == null ? "the claimant" : NameOf(war.Claimant)),
                afterBond - afterRelation);
            AddLine(q, "How the war goes for " + SideName(war, q.ToRising), afterMomentum - afterBond);
            if (q.HagglingFactor != 1f)
                AddLine(q, Statecraft.StatecraftTerms.HagglingLabel(q.Buyer, clan), afterHaggling - afterMomentum);
            if (silverTongue != null)
                AddLine(q, "Silver Tongue (" + NameOf(silverTongue) + ")", afterPerk - afterHaggling);

            // The lines are rounded one by one; whatever that leaves over, and the floor when it
            // applies, are shown rather than hidden, so the column always adds up.
            var sum = 0;
            for (var i = 0; i < q.Lines.Count; i++) sum += q.Lines[i].Value;
            var rest = q.Price - sum;
            if (afterPerk < IntrigueConstants.SideChangeMinimumPrice)
                q.Lines.Add(new KeyValuePair<string, int>("The least any house takes", rest));
            else if (rest != 0 && q.Lines.Count > 0)
            {
                var last = q.Lines[q.Lines.Count - 1];
                q.Lines[q.Lines.Count - 1] = new KeyValuePair<string, int>(last.Key, last.Value + rest);
            }

            q.Eligible = CanChange(war, clan, q, out q.Reason);
            return q;
        }

        /// <summary>
        /// The house's tie to the side it is on, 0-100: loyalty on the crown's side, 50 plus
        /// half its head's relation with the claimant on the rising's.
        /// </summary>
        public static float BondOf(ModState state, InternalWar war, Clan clan)
        {
            if (!war.IsRebel(clan)) return Clamp(LoyaltyModel.Of(state, clan), 0f, 100f);
            var head = clan.Leader;
            var claimant = war.Claimant;
            if (head == null || claimant == null) return 50f;
            return Clamp(50f + head.GetRelation(claimant) / 2f, 0f, 100f);
        }

        private static string NameOf(Hero hero) => Statecraft.StatecraftModel.NameOf(hero);

        public static string SideName(InternalWar war, bool rising)
            => rising ? (war.Faction == null ? "the rising" : war.Faction.Name.ToString()) : "the crown";

        private static void AddLine(Quote q, string label, float gold)
            => q.Lines.Add(new KeyValuePair<string, int>(label, (int)Math.Round(gold)));

        // ----- Who can change -------------------------------------------------------

        /// <summary>
        /// Whether this house can change sides now. Design 07 §6: a sworn house of the kingdom,
        /// not either leader's own, not already turned once in this war, its head free - and
        /// nothing of it in the middle of the fighting, since its parties' map faction flips
        /// with it and a battle or a siege with a side changing under it is a state vanilla
        /// never produces.
        /// </summary>
        private static bool CanChange(InternalWar war, Clan clan, Quote q, out string reason)
        {
            reason = null;
            var kingdom = war.Kingdom;

            if (!war.IsOngoing) { reason = "The war is over."; return false; }
            if (!Court.IsMember(clan) || clan.Kingdom != kingdom)
            {
                reason = "Not a sworn house of " + kingdom.Name + ".";
                return false;
            }
            if (clan == kingdom.RulingClan) { reason = "The crown's own house."; return false; }
            if (clan == war.Banner) { reason = "The claimant's own house."; return false; }
            if (war.HasChangedSides(clan)) { reason = "Has already changed sides once in this war."; return false; }

            var head = clan.Leader;
            if (head == null || !head.IsAlive) { reason = "Has no head to make the choice."; return false; }
            if (head.IsPrisoner) { reason = head.Name + " is a prisoner."; return false; }

            if (q.Buyer == null || !q.Buyer.IsAlive)
            {
                reason = SideName(war, q.ToRising) + " has no leader to pay.";
                return false;
            }
            if (q.Buyer.IsPrisoner) { reason = q.Buyer.Name + " is a prisoner and cannot pay."; return false; }

            for (var i = 0; i < clan.Fiefs.Count; i++)
            {
                var fief = clan.Fiefs[i];
                if (fief?.Settlement != null && fief.Settlement.IsUnderSiege)
                {
                    reason = fief.Settlement.Name + " is under siege.";
                    return false;
                }
            }

            foreach (var party in PartiesOf(clan))
            {
                if (party.MapEvent != null) { reason = "One of their parties is in battle."; return false; }
                if (party.SiegeEvent != null || party.BesiegedSettlement != null)
                {
                    reason = "One of their parties is at a siege.";
                    return false;
                }

                // A party inside a town of the side it is leaving would wake up inside an
                // enemy town. Its own fiefs go with it, and a town of the side it joins stays friendly.
                var inside = party.CurrentSettlement;
                var owner = inside?.OwnerClan;
                if (owner != null && owner != clan && owner.Kingdom == kingdom && war.IsRebel(owner) != q.ToRising)
                {
                    reason = (party.LeaderHero == null ? "One of their parties" : party.LeaderHero.Name.ToString())
                             + " is inside " + inside.Name + ", held by the side they would leave.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>Every party of the house: its war parties, and any party one of its heroes is in.</summary>
        private static IEnumerable<MobileParty> PartiesOf(Clan clan)
        {
            var seen = new HashSet<MobileParty>();
            for (var i = 0; i < clan.WarPartyComponents.Count; i++)
            {
                var party = clan.WarPartyComponents[i]?.MobileParty;
                if (party != null && seen.Add(party)) yield return party;
            }
            for (var i = 0; i < clan.Heroes.Count; i++)
            {
                var party = clan.Heroes[i]?.PartyBelongedTo;
                if (party != null && seen.Add(party)) yield return party;
            }
        }

        // ----- Who pays -------------------------------------------------------------

        /// <summary>
        /// An AI leader's rule for paying a house's price: its own side is not ahead in the war,
        /// and the price is at most <see cref="IntrigueConstants.AiSideChangeBudgetShare"/> of
        /// its purse. The same test decides whether an AI leader takes the player's house when
        /// the player offers it, and whether it makes the player an offer.
        ///
        /// "Not ahead" is ours, added before any code ran (design 07 §6): without it a rich
        /// ruler who is already winning buys a house a week at the winning side's discount, and
        /// a civil war ends by purse rather than by arms.
        /// </summary>
        public static bool AiWouldPay(Quote q, out string reason)
        {
            reason = null;
            if (q?.Buyer == null) { reason = "Nobody would pay."; return false; }

            var war = q.War;
            var buyerEx = q.ToRising ? war.RebelExhaustion : war.CrownExhaustion;
            var otherEx = q.ToRising ? war.CrownExhaustion : war.RebelExhaustion;
            if (buyerEx < otherEx)
            {
                reason = q.Buyer.Name + " is winning, and pays nobody to join.";
                return false;
            }

            var budget = q.Buyer.Gold * IntrigueConstants.AiSideChangeBudgetShare;
            if (q.Price > budget)
            {
                reason = q.Buyer.Name + " holds " + q.Buyer.Gold.ToString("N0") + " and spends at most half of it on one house.";
                return false;
            }
            return true;
        }

        // ----- Changing -------------------------------------------------------------

        /// <summary>
        /// The house goes over: re-quoted and re-checked first (the price shown may be a day
        /// old), then paid, moved, and the leader it walked out on remembers it.
        /// <paramref name="paid"/> false is for the test command only.
        /// </summary>
        public static bool Execute(ModState state, InternalWar war, Clan clan, bool paid, out string failed)
        {
            failed = null;
            var q = QuoteFor(state, war, clan);
            if (q == null) { failed = "Not a house of this war."; return false; }
            if (!q.Eligible) { failed = q.Reason; return false; }
            if (paid && q.Buyer.Gold < q.Price)
            {
                failed = q.Buyer.Name + " cannot pay " + q.Price.ToString("N0") + ".";
                return false;
            }

            var head = clan.Leader;
            var walkedOutOn = InternalWars.LeaderOf(war, !q.ToRising);
            var playerInvolved = q.Buyer == Hero.MainHero || head == Hero.MainHero;

            if (paid)
            {
                GiveGoldAction.ApplyBetweenCharacters(q.Buyer, head, q.Price, !playerInvolved);
                Statecraft.SkillXp.HouseBought(q.Buyer, clan, q.Price);
            }
            InternalWars.ChangeSide(state, war, clan, q.ToRising);
            if (walkedOutOn != null && walkedOutOn != head)
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(head, walkedOutOn,
                    IntrigueConstants.SideChangeRelationPenalty, playerInvolved);

            var side = SideName(war, q.ToRising);
            Log.Info("InternalWar", war.Kingdom.Name + ": " + clan.Name + " went over to " + side
                                    + (paid ? " for " + q.Price.ToString("N0") + " paid by " + q.Buyer.Name : " (forced, unpaid)")
                                    + ". Exhaustion rebels " + war.RebelExhaustion.ToString("0.0")
                                    + " / crown " + war.CrownExhaustion.ToString("0.0") + ".");
            Log.Notify(clan.Name + " has gone over to " + side
                       + (paid ? " for " + q.Price.ToString("N0") + " denars." : "."),
                       q.ToRising ? Colors.Red : Colors.Cyan);
            return true;
        }

        // ----- The AI's week ----------------------------------------------------------

        private static readonly Dictionary<Hero, CampaignTime> PlayerRefusedUntil = new Dictionary<Hero, CampaignTime>();
        private static bool _askingPlayer;

        public static void Reset()
        {
            PlayerRefusedUntil.Clear();
            _askingPlayer = false;
        }

        /// <summary>
        /// Once a week, each AI leader of each civil war considers the houses on the other side
        /// and buys the one with the most strength per denar that it would pay for
        /// (<see cref="AiWouldPay"/>). One house per leader per week. When that house is the
        /// player's, the player is asked instead.
        /// </summary>
        public static void WeeklyTick(ModState state)
        {
            if (state == null) return;

            var wars = new List<InternalWar>();
            for (var i = 0; i < state.InternalWars.Count; i++)
                if (state.InternalWars[i].IsOngoing) wars.Add(state.InternalWars[i]);

            for (var w = 0; w < wars.Count; w++)
            {
                var war = wars[w];
                try
                {
                    BuyFor(state, war, risingSide: false);
                    if (war.IsOngoing) BuyFor(state, war, risingSide: true);
                }
                catch (Exception ex)
                {
                    Log.Error("InternalWar", "The weekly side changes of " + war + " failed.", ex);
                }
            }
        }

        private static void BuyFor(ModState state, InternalWar war, bool risingSide)
        {
            var buyer = InternalWars.LeaderOf(war, risingSide);
            // A player leader buys from the Court tab, when they choose to.
            if (buyer == null || buyer == Hero.MainHero) return;

            Quote best = null;
            var bestValue = -1f;
            foreach (var clan in new List<Clan>(Court.MembersOf(war.Kingdom)))
            {
                // Houses on the other side only: the ones this leader would be buying.
                if (war.IsRebel(clan) == risingSide) continue;
                if (clan == Clan.PlayerClan && (_askingPlayer || RefusedRecently(buyer))) continue;

                var q = QuoteFor(state, war, clan);
                if (q == null || !q.Eligible || !AiWouldPay(q, out _)) continue;

                var value = q.Strength / Math.Max(1, q.Price);
                if (value > bestValue)
                {
                    bestValue = value;
                    best = q;
                }
            }
            if (best == null) return;

            if (best.Clan == Clan.PlayerClan) OfferToPlayer(state, best);
            else if (!Execute(state, war, best.Clan, paid: true, out var failed))
                Log.Warn("InternalWar", buyer.Name + " could not buy " + best.Clan.Name + ": " + failed);
        }

        private static bool RefusedRecently(Hero buyer)
            => PlayerRefusedUntil.TryGetValue(buyer, out var until) && !until.IsPast;

        /// <summary>
        /// An AI leader wants the player's house. Asked, not decided: accepting re-checks both
        /// the price and the leader's willingness, since the answer can come a while later.
        /// </summary>
        private static void OfferToPlayer(ModState state, Quote q)
        {
            _askingPlayer = true;
            var war = q.War;
            var buyer = q.Buyer;
            var side = SideName(war, q.ToRising);

            var body = buyer.Name + " offers " + q.Price.ToString("N0") + " denars for your house to go over to "
                       + side + " in the civil war in " + war.Kingdom.Name + "."
                       + Environment.NewLine + Environment.NewLine
                       + "Your fiefs and your parties would fight for " + side + " until the war is decided,"
                       + " and you would stay a sworn house of " + war.Kingdom.Name + ". "
                       + InternalWars.LeaderOf(war, !q.ToRising)?.Name + " would think less of you for it ("
                       + IntrigueConstants.SideChangeRelationPenalty + "). A house changes sides only once in a war.";

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "An offer from " + buyer.Name,
                    body,
                    true, true,
                    "Go over - receive " + q.Price.ToString("N0"), "Stay where you are",
                    () =>
                    {
                        // Runs from the UI, outside any campaign handler's try.
                        try
                        {
                            _askingPlayer = false;
                            var fresh = QuoteFor(state, war, Clan.PlayerClan);
                            if (fresh == null || !fresh.Eligible || fresh.Buyer != buyer || !AiWouldPay(fresh, out _))
                            {
                                Log.Notify("The moment has passed - " + (fresh?.Reason ?? "the offer no longer stands") + ".", Colors.Red);
                                return;
                            }
                            if (!Execute(state, war, Clan.PlayerClan, paid: true, out var failed))
                                Log.Notify("Could not go over: " + failed, Colors.Red);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("InternalWar", "Accepting the offer to change sides failed.", ex);
                        }
                    },
                    () =>
                    {
                        _askingPlayer = false;
                        PlayerRefusedUntil[buyer] = CampaignTime.DaysFromNow(IntrigueConstants.SideChangeOfferAgainDays);
                        Log.Info("InternalWar", "The player refused " + buyer.Name + "'s offer of " + q.Price.ToString("N0") + ".");
                    }), true);
            }
            catch (Exception ex)
            {
                _askingPlayer = false;
                Log.Error("InternalWar", "Could not show the offer to change sides.", ex);
            }
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
