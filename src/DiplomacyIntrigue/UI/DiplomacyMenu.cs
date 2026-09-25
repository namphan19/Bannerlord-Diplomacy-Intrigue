using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using DiplomacyIntrigue.Statecraft;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI
{
    /// <summary>
    /// The player's way into everything the mod does, without the console.
    ///
    /// Built from the game's own selection dialogs rather than a custom Gauntlet screen.
    /// That is a deliberate trade: a hand-built screen with its own prefabs looks better and
    /// is the eventual goal, but it is also the single most fragile thing a Bannerlord mod
    /// can own - it breaks on game updates and it breaks the whole screen stack when it
    /// fails. Native dialogs cannot do that, need no prefab XML, and give the player the
    /// full feature set now.
    ///
    /// Everything shown here reads the same functions the AI uses. Where a number is hidden
    /// it is hidden on purpose - a rival's war exhaustion appears as a band, never a figure,
    /// which is what Phase 3 espionage will sell back.
    /// </summary>
    public static class DiplomacyMenu
    {
        public static void Open()
        {
            var state = CoreBehavior.State;
            if (state == null)
            {
                Notify("Diplomacy is only available in a campaign.");
                return;
            }

            var kingdom = Clan.PlayerClan?.Kingdom;
            if (kingdom == null)
            {
                Notify("You belong to no kingdom, so you have no foreign policy.");
                return;
            }

            var isRuler = kingdom.Leader == Hero.MainHero;
            var elements = new List<InquiryElement>
            {
                Element("wars", "Our wars (" + CountWars(state, kingdom) + ")",
                    "Exhaustion, war score, and what each war has earned."),
                Element("treaties", "Our agreements (" + CountTreaties(state, kingdom) + ")",
                    "Every treaty we hold, and what it obliges."),
                Element("claims", "Our claims",
                    "Standing justifications for war, and what they allow."),
                Element("kingdoms", "Other kingdoms",
                    "Relations, trust, and what we can propose."),
                Element("hegemony", DescribeOurStanding(state, kingdom),
                    "Who answers to whom. A kingdom holding one vassal is a hegemon."),
                Element("report", "Write a report to file",
                    "Saves the whole world state to Documents/Mount and Blade II Bannerlord/"
                    + "DiplomacyIntrigue/Reports, for sharing or for balance work."),
            };

            var header = kingdom.Name + (isRuler
                ? " - you rule here."
                : " - you are a vassal here, so this is a view only.");

            Show("Diplomacy", header, elements, selected =>
            {
                switch ((string)selected)
                {
                    case "wars": ShowWars(state, kingdom); break;
                    case "treaties": ShowTreaties(state, kingdom); break;
                    case "claims": ShowClaims(state, kingdom); break;
                    case "kingdoms": ShowKingdomList(state, kingdom, isRuler); break;
                    case "hegemony": ShowHegemony(state, kingdom); break;
                    case "report": WriteReport(state); break;
                }
            });
        }

        internal static void WriteReport(ModState state)
        {
            try
            {
                var path = Core.Telemetry.WriteReport(state);
                ShowText("Report written", "Saved to:" + Environment.NewLine + path
                                           + Environment.NewLine + Environment.NewLine
                                           + "The weekly telemetry lines are in the log beside it.");
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Writing the report failed.", ex);
                Notify("Could not write the report - see the log.");
            }
        }

        // ----- Read-only views -------------------------------------------------

        private static void ShowWars(ModState state, Kingdom kingdom)
        {
            var sb = new StringBuilder();
            var any = false;

            foreach (var war in state.OngoingWarsOf(kingdom))
            {
                any = true;
                var enemy = war.Other(kingdom);
                var ourExhaustion = war.ExhaustionOf(kingdom);
                var theirExhaustion = war.ExhaustionOf(enemy);

                sb.AppendLine(kingdom.Name + " against " + enemy.Name);
                sb.AppendLine("  fought for " + war.DaysElapsed.ToString("0") + " days over " + war.Justification);
                sb.AppendLine("  our exhaustion:   " + ourExhaustion.ToString("0.0") + " / 100");

                // Their figure is never shown - only what it means for their behaviour.
                sb.AppendLine("  their condition:  " + ExhaustionBands.Describe(theirExhaustion));
                sb.AppendLine("  war score:        " + war.ScoreFor(kingdom).ToString("0.0")
                              + (war.ScoreFor(kingdom) > 0 ? " in our favour" : ""));
                sb.AppendLine("  losses:           " + war.AggressorCasualties + " / " + war.DefenderCasualties);
                sb.AppendLine();
            }

            var weariness = state.WearinessOf(kingdom);
            if (weariness > 0f)
                sb.AppendLine("Lingering weariness from past wars: " + weariness.ToString("0.0"));

            ShowText("Our wars", any ? sb.ToString() : "We are at peace with the world.");
        }

        /// <summary>
        /// The root menu label, so the player can see where they stand without opening
        /// anything: patron, vassals, or neither.
        /// </summary>
        private static string DescribeOurStanding(ModState state, Kingdom kingdom)
        {
            var patron = Hegemony.PatronOf(state, kingdom);
            if (patron != null) return "Our standing - vassal of " + patron.Name;

            var vassals = Hegemony.VassalCount(state, kingdom);
            if (vassals > 0) return "Our standing - hegemon over " + vassals + " kingdom(s)";

            return "Our standing - independent";
        }

        /// <summary>
        /// Every sphere on the map, ours first. Our own links show the hold figure; other
        /// patrons show only what anyone could see - who answers to whom, and how that bond
        /// is behaving - because a rival's exact hold is the sort of thing Phase 3 espionage
        /// is meant to sell.
        /// </summary>
        private static void ShowHegemony(ModState state, Kingdom kingdom)
        {
            var sb = new StringBuilder();

            var ourPatron = Hegemony.VassalageOf(state, kingdom);
            if (ourPatron != null)
            {
                var patron = ourPatron.DominantParty;
                var hold = Hegemony.HoldOf(ourPatron);
                sb.AppendLine("We answer to " + patron.Name + ".");
                sb.AppendLine("  our hold to them: " + hold.ToString("0.0") + " / 100  -  " + HoldMeaning(state, ourPatron));
                Hegemony.HoldTarget(state, ourPatron, out var pull);
                if (pull != null) sb.AppendLine("  pulling toward: " + pull);
                sb.AppendLine("  tribute " + ourPatron.TributeAmount + " per period, term ends " + ourPatron.ExpiresOn);
                if (ourPatron.DefianceMarks > 0)
                    sb.AppendLine("  we have defied them " + ourPatron.DefianceMarks + " time(s)");
                sb.AppendLine();
            }

            var ours = new List<Treaty>();
            Hegemony.CollectVassalages(state, kingdom, ours);
            if (ours.Count > 0)
            {
                sb.AppendLine("Our vassals (" + ours.Count + "):");
                for (var i = 0; i < ours.Count; i++)
                {
                    var link = ours[i];
                    var hold = Hegemony.HoldOf(link);
                    sb.AppendLine("  " + link.SubordinateParty.Name
                                  + "  hold " + hold.ToString("0.0")
                                  + "  marks " + link.DefianceMarks
                                  + "  tribute " + link.TributeAmount);
                    sb.AppendLine("      " + HoldMeaning(state, link));
                    Hegemony.HoldTarget(state, link, out var pull);
                    if (pull != null) sb.AppendLine("      pulling toward: " + pull);
                }
                sb.AppendLine();
                sb.AppendLine("  We may call " + Hegemony.MaxVassalsToCall(ours.Count)
                              + " of them into any one war, nearest the enemy first.");
                sb.AppendLine();
            }

            var otherSpheres = 0;
            foreach (var other in Kingdom.All)
            {
                if (other == kingdom || !other.IsRealm()) continue;
                if (!Hegemony.IsHegemon(state, other)) continue;

                var held = new List<Treaty>();
                Hegemony.CollectVassalages(state, other, held);
                if (held.Count == 0) continue;

                otherSpheres++;
                sb.AppendLine(other.Name + " holds " + held.Count + " vassal(s):");
                for (var i = 0; i < held.Count; i++)
                {
                    var link = held[i];
                    sb.AppendLine("  " + link.SubordinateParty.Name + " - " + HoldMeaning(state, link));
                }
                sb.AppendLine();
            }

            if (ourPatron == null && ours.Count == 0 && otherSpheres == 0)
            {
                sb.AppendLine("Nobody on this map holds a vassal.");
                sb.AppendLine();
                sb.AppendLine("Submission is imposed at a peace table once a war has earned "
                              + PeaceTable.SubjugationCost.ToString("0")
                              + " points of war score, or offered by a kingdom that cannot survive"
                              + " alone. One vassal is all it takes to be a hegemon.");
            }

            ShowText("Hegemony", sb.ToString());
        }

        /// <summary>
        /// What a hold figure means in behaviour, which is the only part of it a player can
        /// act on. Shown for rivals too - watching a bond fail needs no spies.
        /// </summary>
        internal static string HoldMeaning(ModState state, Treaty link)
        {
            var hold = Hegemony.HoldOf(link);
            if (hold >= DiplomacyConstants.HoldRenewThreshold) return "loyal; will renew when the term ends";
            if (hold >= DiplomacyConstants.HoldPassiveResistanceThreshold) return "serving, but will let the term lapse";
            if (hold >= DiplomacyConstants.HoldDefianceThreshold) return "resisting; refuses summons and withholds tribute";
            // The revolt line moves with the balance of strength, so it is read per link.
            if (!Hegemony.IsAtBreakingPoint(state, link)) return "defiant; treats with outsiders";
            return "at breaking point; counting down to revolt";
        }

        private static void ShowTreaties(ModState state, Kingdom kingdom)
        {
            var sb = new StringBuilder();
            var any = false;

            foreach (var treaty in state.ActiveTreatiesOf(kingdom))
            {
                any = true;
                var other = treaty.Other(kingdom);
                sb.AppendLine(treaty.Type + " with " + other.Name + ", until " + treaty.ExpiresOn);

                if (treaty.CarriesCallToArms)
                    sb.AppendLine("  obliges us to join their " +
                                  (treaty.CallToArmsIsDefensiveOnly ? "defensive wars" : "wars"));
                if (treaty.SubordinateParty == kingdom)
                    sb.AppendLine("  we answer to " + other.Name + " and cannot declare war on our own account");
                else if (treaty.SubordinateParty == other)
                    sb.AppendLine("  " + other.Name + " answers to us");
                if (treaty.TributeAmount > 0)
                    sb.AppendLine("  tribute " + treaty.TributeAmount + " from " + treaty.TributePayer.Name
                                  + ", next due " + treaty.NextTributeDue);
                sb.AppendLine();
            }

            ShowText("Our agreements", any ? sb.ToString() : "We hold no agreements with anyone.");
        }

        private static void ShowClaims(ModState state, Kingdom kingdom)
        {
            var sb = new StringBuilder();
            var any = false;

            foreach (var other in Kingdom.All)
            {
                if (other == kingdom || !other.IsRealm()) continue;
                foreach (var claim in ClaimRegistry.LiveClaims(state, kingdom, other))
                {
                    any = true;
                    sb.AppendLine("Against " + other.Name + ": " + claim.Type
                                  + " (legitimacy " + claim.Legitimacy.ToString("0.00") + ")");
                    if (claim.Settlement != null) sb.AppendLine("  over " + claim.Settlement.Name);
                    sb.AppendLine(claim.AllowsFiefDemands
                        ? "  allows us to demand land at a peace table"
                        : "  justifies war, but does not entitle us to land");
                    sb.AppendLine("  expires " + claim.ExpiresOn);
                    sb.AppendLine();
                }
            }

            ShowText("Our claims", any
                ? sb.ToString()
                : "We hold no claims. Claims come from fiefs taken from us, raids on our villages,"
                  + " treaties broken against us - or from fabricating one.");
        }

        // ----- Kingdom list and actions ----------------------------------------

        private static void ShowKingdomList(ModState state, Kingdom kingdom, bool isRuler)
        {
            var elements = new List<InquiryElement>();

            foreach (var other in Kingdom.All)
            {
                if (other == kingdom || !other.IsRealm()) continue;

                var atWar = kingdom.IsAtWarWith(other);
                var trust = TrustRegistry.Get(state, kingdom, other);
                var label = other.Name + (atWar ? "  (at war)" : "");

                elements.Add(Element(other, label,
                    "Trust " + trust.ToString("0") + ". Strength " + other.CurrentTotalStrength.ToString("0") + "."));
            }

            if (elements.Count == 0)
            {
                Notify("There are no other kingdoms left.");
                return;
            }

            Show("Other kingdoms", "Choose a kingdom.", elements,
                selected => ShowKingdom(state, kingdom, (Kingdom)selected, isRuler));
        }

        private static void ShowKingdom(ModState state, Kingdom us, Kingdom them, bool isRuler)
        {
            var atWar = us.IsAtWarWith(them);
            var elements = new List<InquiryElement>
            {
                Element("report", "What we know", "Relations, trust and standing agreements."),
            };

            if (isRuler)
            {
                var ourLink = Hegemony.VassalageOf(state, us);
                if (atWar)
                {
                    elements.Add(Element("peace", "Negotiate peace",
                        "See what this war has earned and offer terms."));

                    // Kneeling is the other way out: a free kingdom submits outright, a
                    // vassal's version is a defection (design/04 §12.4.4 and F3).
                    if (ourLink == null)
                    {
                        var can = AiDiplomacy.CanSubmitTo(state, us, them, out var why, out _);
                        elements.Add(new InquiryElement("submit", "Kneel to them", null, can,
                            can
                                ? "The oath is the peace: the war ends and we answer to them."
                                : why));
                    }
                    else
                    {
                        var can = AiDiplomacy.CanDefectToAttacker(state, us, them, out var why, out _);
                        elements.Add(new InquiryElement("defect", "Beg their mercy", null, can,
                            can
                                ? "End this war as their vassal - " + ourLink.DominantParty.Name
                                  + ", which would not defend us, is named the oathbreaker."
                                : why));
                    }
                }
                else
                {
                    var block = TreatyEnforcement.WhyWarBlocked(state, us, them);
                    elements.Add(new InquiryElement("war", "Declare war", null,
                        block == TreatyEnforcement.Block.None,
                        block == TreatyEnforcement.Block.None
                            ? "Puts the question to the court, which votes on it - the same"
                              + " proposal the Decisions tab offers."
                            : TreatyEnforcement.Explain(state, us, them, block) + "."));

                    AddPactOption(state, us, them, TreatyType.NonAggressionPact, elements);
                    AddPactOption(state, us, them, TreatyType.DefensivePact, elements);
                    AddPactOption(state, us, them, TreatyType.Alliance, elements);

                    var canTribute = AiDiplomacy.CanDemandTribute(state, us, them, out var whyTribute);
                    elements.Add(new InquiryElement("tribute", "Demand tribute", null, canTribute,
                        canTribute
                            ? DiplomacyConstants.AiDefaultTributePerPeriod
                              + " per period. Coercion, not negotiation: the claim makes the"
                              + " pretext and our strength makes the argument."
                            : whyTribute));

                    if (ourLink == null)
                    {
                        var can = AiDiplomacy.CanSubmitTo(state, us, them, out var why, out _);
                        elements.Add(new InquiryElement("submit", "Kneel to them", null, can,
                            can
                                ? "Their oath for our foreign policy: tribute, troops in their"
                                  + " wars, protection owed to us."
                                : why));
                    }

                    // Courting somebody else's neglected vassal means war with its patron.
                    var theirLink = Hegemony.VassalageOf(state, them);
                    if (theirLink != null && theirLink.DominantParty != us)
                    {
                        var can = Hegemony.CanPoach(state, us, theirLink, out var value, out var why);
                        elements.Add(new InquiryElement("court",
                            "Court them away from " + theirLink.DominantParty.Name, null, can,
                            can
                                ? "They would kneel to us (valued at " + value.ToString("0")
                                  + "). Taking them means war with " + theirLink.DominantParty.Name + "."
                                : why));
                    }

                    // A greedy patron may tear up a vassal's oath and take its lands.
                    if (Hegemony.CouldAnnex(state, us, them))
                        elements.Add(Element("annex", "Tear up their oath and make war",
                            "The full price of a breach, then conquest - the same move a greedy"
                            + " AI patron makes."));
                }

                // Independence is the one foreign-policy act a vassal keeps for itself,
                // so it shows on the patron's row in war or in peace.
                if (ourLink != null && ourLink.DominantParty == them)
                {
                    var breaking = Hegemony.IsAtBreakingPoint(state, ourLink);
                    elements.Add(new InquiryElement("secede", "Declare independence", null, breaking,
                        breaking
                            ? "Hold " + Hegemony.HoldOf(ourLink).ToString("0") + ", below the"
                              + " breaking point of "
                              + Hegemony.SecessionThreshold(state, ourLink).ToString("0")
                              + " - a war of independence, and their other resentful vassals"
                              + " may rise with us."
                            : "Hold " + Hegemony.HoldOf(ourLink).ToString("0")
                              + " - above the breaking point of "
                              + Hegemony.SecessionThreshold(state, ourLink).ToString("0")
                              + ". Renouncing the oath is always possible; rebellion needs a"
                              + " realm already breaking."));
                }

                var breakable = FirstBreakableTreaty(state, us, them);
                if (breakable != null)
                    elements.Add(Element("break", "Renounce our " + breakable.Type,
                        "Always possible, never free: -" + (-DiplomacyConstants.TrustTreatyBrokenVictim).ToString("0")
                        + " trust with them, -" + (-DiplomacyConstants.TrustTreatyBrokenObserver).ToString("0")
                        + " with every other court, and they gain a reason for war."));

                elements.Add(Element("fabricate", "Fabricate a claim",
                    DiplomacyConstants.FabricateClaimInfluenceCost + " influence and "
                    + DiplomacyConstants.FabricateClaimGoldCost + " denars, "
                    + DiplomacyConstants.FabricateClaimDurationDays + " days, and a "
                    + StatecraftTerms.ExposureLine(us, them) + "."));
            }

            Show(them.Name.ToString(), Summary(state, us, them), elements, selected =>
            {
                switch (selected as string)
                {
                    case "report": ShowText(them.Name.ToString(), Summary(state, us, them)); return;
                    case "peace": ShowPeace(state, us, them); return;
                    case "war": DeclareWar(state, us, them); return;
                    case "tribute": DemandTribute(state, us, them); return;
                    case "submit": OfferSubmission(state, us, them); return;
                    case "defect": DefectToAttacker(state, us, them); return;
                    case "court": CourtVassal(state, us, them); return;
                    case "annex": AnnexVassal(state, us, them); return;
                    case "secede": SecedeFromPatron(state, us, them); return;
                    case "break": BreakTreaty(state, us, them); return;
                    case "fabricate": ShowFabricationTargets(state, us, them); return;
                }

                if (selected is TreatyType type) ProposePact(state, us, them, type);
            });
        }

        private static void AddPactOption(ModState state, Kingdom us, Kingdom them,
            TreatyType type, List<InquiryElement> into)
        {
            var allowed = TreatyRegistry.CanSign(state, us, them, type, out var reason);
            var cost = StatecraftTerms.TreatyInfluenceCost(us, type);

            // Their own valuation, shown honestly: this is the number the AI uses to decide.
            // Truncated, like the Diplomacy tab's chooser: rounding would show 34.6 as "35"
            // beside a threshold of 35 it has not cleared.
            var theirValue = AiDiplomacy.PactValueWhenAsked(state, them, us);
            var hint = allowed
                ? cost + " influence. " + them.Name + " values it at " + ((int)theirValue)
                  + " (they need " + ThresholdFor(type).ToString("0") + ")."
                : reason;

            into.Add(new InquiryElement(type, "Propose " + type, null, allowed, hint));
        }

        internal static float ThresholdFor(TreatyType type)
        {
            switch (type)
            {
                case TreatyType.Alliance: return DiplomacyConstants.AiAllianceThreshold;
                case TreatyType.DefensivePact: return DiplomacyConstants.AiDefensivePactThreshold;
                default: return DiplomacyConstants.AiNonAggressionThreshold;
            }
        }

        private static string Summary(ModState state, Kingdom us, Kingdom them)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ruler: " + (them.Leader == null ? "none" : them.Leader.Name.ToString()));
            sb.AppendLine("Strength: " + them.CurrentTotalStrength.ToString("0")
                          + "  (ours: " + us.CurrentTotalStrength.ToString("0") + ")");
            // Ambition and greed are what the whole map can see of a ruler, and the same
            // numbers every AI court reads when it decides whom to fear.
            sb.AppendLine("Power: " + them.Name + " " + Power.Describe(state, them));
            sb.AppendLine("They trust us: " + TrustRegistry.Get(state, them, us).ToString("0.0"));
            sb.AppendLine("We trust them: " + TrustRegistry.Get(state, us, them).ToString("0.0"));

            var patron = TreatyRegistry.PatronOf(state, them);
            if (patron != null) sb.AppendLine("They answer to " + patron.Name + ".");

            var war = state.OngoingWarBetween(us, them);
            if (war != null)
            {
                sb.AppendLine();
                sb.AppendLine("At war for " + war.DaysElapsed.ToString("0") + " days over " + war.Justification + ".");
                sb.AppendLine("Our exhaustion: " + war.ExhaustionOf(us).ToString("0.0"));
                sb.AppendLine("Their condition: " + ExhaustionBands.Describe(war.ExhaustionOf(them)));
            }

            var claim = ClaimRegistry.Best(state, us, them);
            if (claim != null)
            {
                sb.AppendLine();
                sb.AppendLine("Our best claim: " + claim.Type
                              + " (legitimacy " + claim.Legitimacy.ToString("0.00") + ")"
                              + (claim.AllowsFiefDemands ? ", which entitles us to land" : ""));
            }

            return sb.ToString();
        }

        // ----- Actions ---------------------------------------------------------

        internal static void ProposePact(ModState state, Kingdom us, Kingdom them, TreatyType type)
        {
            // The other side has to want it too, judged by the same function the AI uses.
            var theirValue = AiDiplomacy.PactValueWhenAsked(state, them, us);
            if (theirValue < ThresholdFor(type))
            {
                Notify(them.Name + " declines: they value a " + type + " at only "
                       + ((int)theirValue) + ".");
                return;
            }

            var cost = StatecraftTerms.TreatyInfluenceCost(us, type);
            if (us.RulingClan == null || us.RulingClan.Influence < cost)
            {
                Notify("Not enough influence: " + type + " costs " + cost + ".");
                return;
            }

            var treaty = TreatyRegistry.Sign(state, us, them, type, out var reason);
            if (treaty == null)
            {
                Notify("Refused: " + reason);
                return;
            }

            TaleWorlds.CampaignSystem.Actions.ChangeClanInfluenceAction.Apply(us.RulingClan, -cost);
            SkillXp.PactSigned(us, type);
            Notify(us.Name + " and " + them.Name + " sign a " + type + ".", Colors.Green);
        }

        /// <summary>
        /// Declaring war the way the vanilla button did before this mod took the strip over:
        /// a DeclareWarDecision is proposed by the player's clan and the realm votes on it.
        /// The mod's enforcement gates apply to the decision itself, exactly as they do to
        /// the Decisions tab - a treaty in the way greys the button out with its reason.
        ///
        /// **One price for a war** (design 08 A-1, the lead's delegated call D4). The proposal
        /// used to be charged vanilla's <c>GetInfluenceCostOfProposingWar</c> - 200, 400 under War
        /// Tax - while an AI ruler paid the mod's <see cref="AiDiplomacy.WarDeclarationCost"/>,
        /// typically 52-100. The player now pays that same figure, charged here, and the decision
        /// is added with its vanilla cost ignored so nothing is charged twice.
        /// </summary>
        internal static void DeclareWar(ModState state, Kingdom us, Kingdom them)
        {
            var block = TreatyEnforcement.WhyWarBlocked(state, us, them);
            if (block != TreatyEnforcement.Block.None)
            {
                Notify("Cannot declare war: " + TreatyEnforcement.Explain(state, us, them, block) + ".");
                return;
            }

            var proposer = Clan.PlayerClan;
            var decision = new DeclareWarDecision(proposer, them);
            if (!decision.IsAllowed())
            {
                Notify("The court will not entertain a war against " + them.Name + " right now.");
                return;
            }

            var cost = AiDiplomacy.WarDeclarationCostAgainst(state, us, them, proposer.Leader);
            if (proposer.Influence < cost)
            {
                Notify("Not enough influence: proposing this war costs " + cost + ".");
                return;
            }

            us.AddDecision(decision, true);
            TaleWorlds.CampaignSystem.Actions.ChangeClanInfluenceAction.Apply(proposer, -cost);
            Notify("The court is asked to declare war on " + them.Name + " (" + cost + " influence).", Colors.Green);
        }

        /// <summary>
        /// We kneel: the player's own kingdom offers its oath to the selected one. The gates
        /// are the AI's - <see cref="AiDiplomacy.CanSubmitTo"/> - and kneeling to an attacker
        /// signs as the peace that ends the war, the same desperate door an AI cornered the
        /// same way would take.
        /// </summary>
        internal static void OfferSubmission(ModState state, Kingdom us, Kingdom them)
        {
            if (!AiDiplomacy.CanSubmitTo(state, us, them, out var reason, out var settlesWar))
            {
                Notify("We cannot kneel to " + them.Name + ": " + reason);
                return;
            }

            var value = Hegemony.SubmissionValue(state, us, them, out var explanation);
            var body = "We offer " + them.Name + " our oath: tribute of "
                       + DiplomacyConstants.AiDefaultTributePerPeriod + " per period, troops in"
                       + " their wars, and our foreign policy answers to them."
                       + Environment.NewLine + Environment.NewLine
                       + (settlesWar
                           ? "The oath is the peace - our war with them ends the day it is sworn."
                             + Environment.NewLine + Environment.NewLine
                           : "")
                       + "In exchange they owe us protection; a patron that does not defend its"
                       + " vassals loses them. What the move is worth to a court in our position: "
                       + value.ToString("0") + " (" + explanation + ").";

            Confirm("Submission to " + them.Name, body, "Kneel", () =>
            {
                // Re-asked rather than trusted: the inquiry can sit open through a whole
                // day of drift, so the world may have moved since the button was clicked.
                if (!AiDiplomacy.CanSubmitTo(state, us, them, out var blocked,
                        out var stillSettlesWar))
                {
                    Notify("Could not submit: " + blocked);
                    return;
                }

                if (stillSettlesWar) AiDiplomacy.SettleBySubmission(us, them);

                var treaty = Hegemony.Submit(state, them, us,
                    stillSettlesWar
                        ? DiplomacyConstants.HoldOnDesperateSubmission
                        : DiplomacyConstants.HoldOnVoluntarySubmission,
                    DiplomacyConstants.AiDefaultTributePerPeriod, out var failed,
                    route: stillSettlesWar ? "player_submitted_to_attacker" : "player_voluntary",
                    value: value, detail: explanation, settlesWar: stillSettlesWar);

                if (treaty == null)
                {
                    Notify("Could not submit: " + failed);
                    return;
                }

                Notify("We now answer to " + them.Name + ".", Colors.Green);
            });
        }

        /// <summary>
        /// The demand of tribute the AI runs weekly, handed to the player for the pair they
        /// picked: claim, overwhelming strength, the target's trust and its court - and the
        /// target's consent lives in those gates, exactly as it does for the AI.
        /// </summary>
        internal static void DemandTribute(ModState state, Kingdom us, Kingdom them)
        {
            if (!AiDiplomacy.CanDemandTribute(state, us, them, out var reason))
            {
                Notify("Cannot demand tribute: " + reason);
                return;
            }

            var treaty = TreatyRegistry.Sign(state, us, them, TreatyType.TributaryPact,
                out var failed, tributePayer: them,
                tributeAmount: DiplomacyConstants.AiDefaultTributePerPeriod);
            if (treaty == null)
            {
                Notify("Refused: " + failed);
                return;
            }

            SkillXp.TributeDemandAccepted(us);
            Notify(them.Name + " agrees to pay us "
                   + DiplomacyConstants.AiDefaultTributePerPeriod + " per period.", Colors.Green);
        }

        /// <summary>
        /// The neglected-vassal's exit run in reverse: our patron will not defend us, so we
        /// kneel to the kingdom attacking us and it is named the oathbreaker for failing its
        /// duty. Same gates and same execution as the AI's defection.
        /// </summary>
        internal static void DefectToAttacker(ModState state, Kingdom us, Kingdom them)
        {
            if (!AiDiplomacy.CanDefectToAttacker(state, us, them, out var reason, out var link))
            {
                Notify("Cannot defect: " + reason);
                return;
            }

            var patron = link.DominantParty;
            Confirm("Kneel to " + them.Name,
                "We abandon " + patron.Name + ", which will not defend us, and submit to our"
                + " attacker: the war ends as our submission, and " + patron.Name
                + " is named the oathbreaker in every court for failing its duty."
                + Environment.NewLine + Environment.NewLine
                + "Tribute of " + DiplomacyConstants.AiDefaultTributePerPeriod
                + " per period, and our foreign policy answers to " + them.Name + ".",
                "Kneel", () =>
                {
                    if (!AiDiplomacy.CanDefectToAttacker(state, us, them, out var lapsed,
                            out var current))
                    {
                        Notify("The offer has lapsed: " + lapsed);
                        return;
                    }

                    AiDiplomacy.ExecuteDefection(state, us, current, current.DominantParty, them);
                    if (Hegemony.PatronOf(state, us) == them)
                        Notify("We now answer to " + them.Name + ".", Colors.Green);
                });
        }

        /// <summary>
        /// Courting a rival's neglected vassal, offered to the player for the link they
        /// selected. The consequences are the poacher's side of the same move the AI makes:
        /// the old bond repudiated, the patron's trust and a casus belli against us, and war
        /// with the patron unless we are already in one.
        /// </summary>
        internal static void CourtVassal(ModState state, Kingdom us, Kingdom them)
        {
            var link = Hegemony.VassalageOf(state, them);
            if (!Hegemony.CanPoach(state, us, link, out var value, out var reason))
            {
                Notify("Cannot court them: " + reason);
                return;
            }

            var patron = link.DominantParty;
            var body = them.Name + " answers to " + patron.Name + " at hold "
                       + Hegemony.HoldOf(link).ToString("0")
                       + ", and would kneel to us (valued at " + value.ToString("0") + ")."
                       + Environment.NewLine + Environment.NewLine
                       + "Taking them repudiates their oath at no charge to them - the blame is"
                       + " ours: " + (-DiplomacyConstants.PoachingTrustCost).ToString("0")
                       + " trust with " + patron.Name + ", a broken-treaty claim against us, and"
                       + (us.IsAtWarWith(patron)
                           ? " we are already at war with them."
                           : " war with " + patron.Name + " the moment the oath moves.");

            Confirm("Court " + them.Name, body, "Court them", () =>
            {
                if (!Hegemony.CanPoach(state, us, link, out var currentValue, out var lapsed))
                {
                    Notify("The moment has passed: " + lapsed);
                    return;
                }

                // ExecutePoach announces a success itself; only a failure needs telling.
                if (!Hegemony.ExecutePoach(state, us, link, currentValue))
                    Notify("The courtship failed - see the Diplomacy & Intrigue log.");
            });
        }

        /// <summary>
        /// The greedy patron's move, offered to a player whose kingdom has grown hungry for
        /// provinces rather than vassals: tear up the vassal's oath at the full price of a
        /// breach, then conquer it. Same gate (<see cref="Hegemony.CouldAnnex"/>), same cost
        /// formula and same execution as the AI's annexation war.
        /// </summary>
        internal static void AnnexVassal(ModState state, Kingdom us, Kingdom them)
        {
            if (!Hegemony.CouldAnnex(state, us, them))
            {
                Notify("Cannot turn on " + them.Name + ".");
                return;
            }

            var terms = AiDiplomacy.EvaluateWar(state, us, them, asAnnexation: true);
            var cost = AiDiplomacy.WarDeclarationCost(state, us,
                CasusBelli.Legitimacy(terms.Casus));

            Confirm("Turn on " + them.Name,
                "We tear up " + them.Name + "'s oath and take their lands ourselves: the full"
                + " price of a breach in every court, every vassal we hold takes the lesson"
                + " in hold, and war opens at once."
                + Environment.NewLine + Environment.NewLine
                + "Declaring costs " + cost + " influence.",
                "To war", () =>
                {
                    if (!Hegemony.CouldAnnex(state, us, them))
                    {
                        Notify("The moment has passed - the oath no longer stands between us.");
                        return;
                    }

                    var opened = AiDiplomacy.ExecuteWarDeclaration(state, us, them, terms,
                        annexation: true, value: terms.Total);
                    Notify(opened
                        ? "We tear up the oath and march on " + them.Name + "."
                        : "The declaration was refused - see the log.", opened ? Colors.Green : Colors.Red);
                });
        }

        /// <summary>
        /// The secession the daily tick asks about, pulled early by the player whose kingdom
        /// is the resentful vassal: the realm is already at breaking point, so the button is
        /// the same trigger the question fires - <see cref="Hegemony.Secede"/> runs the
        /// identical revolt an AI vassal would have run.
        /// </summary>
        internal static void SecedeFromPatron(ModState state, Kingdom us, Kingdom them)
        {
            var link = Hegemony.VassalageOf(state, us);
            if (link == null || link.DominantParty != them)
            {
                Notify("We do not answer to " + them.Name + ".");
                return;
            }

            if (!Hegemony.IsAtBreakingPoint(state, link))
            {
                Notify("The realm is not breaking - hold " + Hegemony.HoldOf(link).ToString("0")
                       + " against a secession line of "
                       + Hegemony.SecessionThreshold(state, link).ToString("0")
                       + ". Renouncing the oath is always possible; rebellion is not.");
                return;
            }

            Confirm("Secede from " + them.Name,
                "We renounce our oath to " + them.Name + " and fight for our independence:"
                + " every court counts the breach, their loyal vassals answer their call to"
                + " arms, and their resentful ones may rise with us."
                + Environment.NewLine + Environment.NewLine
                + "The war of independence opens at once.",
                "Secede", () =>
                {
                    // Re-asked rather than trusted: the bond may have moved while the
                    // inquiry sat open.
                    var current = Hegemony.VassalageOf(state, us);
                    if (current == null || current.DominantParty != them
                        || !Hegemony.IsAtBreakingPoint(state, current))
                    {
                        Notify("The moment has passed - the bond no longer stands as it did.");
                        return;
                    }

                    Hegemony.Secede(state, current);
                });
        }

        /// <summary>
        /// The label on a button that calls <see cref="ShowPeace"/>, from the same two
        /// budgets it branches on - so the button never promises a white peace and then
        /// opens the loser's table, which the first live pass caught the Realm tab doing.
        /// </summary>
        internal static string PeaceButtonLabel(float ourBudget, float theirBudget)
        {
            if (ourBudget > 0f) return "Negotiate peace";
            if (theirBudget > 0f) return "Sue for peace";
            return "White peace only";
        }

        /// <summary>The line under that button, on the same branch.</summary>
        internal static string PeaceButtonSub(float ourBudget, float theirBudget, Kingdom them)
        {
            if (ourBudget > 0f)
                return "Budget " + ourBudget.ToString("0") + " - see hint for the price list.";
            if (theirBudget > 0f)
                return "The war has earned " + them.Name + " " + theirBudget.ToString("0")
                       + ". Offer what it takes.";
            return "White peace only - this war has earned nothing yet.";
        }

        /// <summary>
        /// The peace table. The negotiation screen (<see cref="UI.Negotiation.PeaceTablePopup"/>)
        /// is the way in: every term carries its price against a live budget, and a white
        /// peace is one button. Which face shows depends on who is winning - the losing
        /// player reaches the same entry point and gets the offer face instead, because
        /// vanilla's peace paths are ours now ([design 05](../../docs/design/05-vanilla-override.md)).
        /// If the screen cannot open, the older multi-select checklists stand in.
        /// </summary>
        internal static void ShowPeace(ModState state, Kingdom us, Kingdom them)
        {
            var war = state.OngoingWarBetween(us, them);
            if (war == null) { Notify("We are not at war with " + them.Name + "."); return; }

            var ourBudget = PeaceTable.BudgetFor(war, us);
            var theirBudget = PeaceTable.BudgetFor(war, them);

            try
            {
                if (ourBudget > 0f)
                {
                    // The winner's table: we price what to take.
                    UI.Negotiation.PeaceTablePopup.ShowDemand(state, war, us, them, true,
                        terms => TryPeace(state, war, terms, us),
                        () => TryPeace(state, war, new PeaceTerms(us, them), us));
                    return;
                }
                if (theirBudget > 0f)
                {
                    // The loser's table: we price what to give.
                    UI.Negotiation.PeaceTablePopup.ShowDemand(state, war, us, them, false,
                        terms => TryPeace(state, war, terms, us),
                        () => TryPeace(state, war, new PeaceTerms(them, us), us));
                    return;
                }

                Confirm("Peace with " + them.Name,
                    "Neither side has earned enough to ask for anything - a white peace is all this"
                    + " war can produce. Propose it and they sign if the war has worn them too.",
                    "Propose white peace",
                    () => TryPeace(state, war, new PeaceTerms(us, them), us));
            }
            catch (Exception ex)
            {
                // The screen is the one piece of Gauntlet this project owns; if it cannot
                // come up, the war still has to be negotiable. The old inquiry tables are
                // the fallback, not a second implementation of the rules - they price and
                // decide through the same PeaceTable resolvers.
                Log.Error("UI", "The peace table could not open; falling back to the checklist.", ex);
                if (ourBudget > 0f) ShowDemandTable(state, war, us, them, ourBudget);
                else if (theirBudget > 0f) ShowOfferTable(state, war, us, them, theirBudget);
                else Confirm("Peace with " + them.Name,
                    "Neither side has earned enough to ask for anything - a white peace is all this"
                    + " war can produce. Propose it and they sign if the war has worn them too.",
                    "Propose white peace",
                    () => TryPeace(state, war, new PeaceTerms(us, them), us));
            }
        }

        /// <summary>
        /// The winner's checklist. Every line is a thing this war can take from them, priced
        /// in the same points <see cref="PeaceTable.CostOf"/> counts and gated by what the war
        /// actually earned: anything beyond the budget stays visible but untickable, with its
        /// reason, rather than disappearing.
        /// </summary>
        private static void ShowDemandTable(ModState state, WarRecord war, Kingdom us,
            Kingdom them, float budget)
        {
            var elements = new List<InquiryElement>
            {
                Element("prisoners",
                    "Demand our captives back   -   " + Priced(DiplomacyConstants.PeaceCostPrisoners, budget),
                    "They free every hero of ours they hold.")
            };

            // Sized by what the war earned, the same way the AI ladder sizes it, so a small
            // win buys a small indemnity rather than none.
            var indemnity = PeaceTable.LargestIndemnity(war, us, them);
            if (indemnity >= 1000)
                elements.Add(Element("indemnity",
                    "Demand an indemnity of " + indemnity + " denars   -   "
                    + Priced(indemnity / 1000f * DiplomacyConstants.PeaceCostPerThousandIndemnity, budget),
                    "Gold now rather than land later."));

            elements.Add(Element("tribute",
                "Impose tribute of " + DiplomacyConstants.AiDefaultTributePerPeriod
                + " per period   -   " + Priced(DiplomacyConstants.PeaceCostTributaryPact, budget),
                "They pay for peace and keep everything else."));

            // One rung with two faces at one price: a hegemon cannot be made a vassal while it
            // still holds vassals (hegemony is flat), so against one the demand is its sphere.
            if (Hegemony.IsHegemon(state, them))
                elements.Add(Element("subjugation",
                    "Demand they release their vassals   -   "
                    + Priced(DiplomacyConstants.PeaceCostSubjugation, budget),
                    "They keep their throne; every kingdom sworn to them goes free."
                    + " We inherit none of them, and they stay in their own wars."));
            else
                elements.Add(Element("subjugation",
                    "Demand their submission   -   " + Priced(DiplomacyConstants.PeaceCostSubjugation, budget),
                    "They keep their ruler and their lands, and owe us troops, tribute and their"
                    + " foreign policy - and we owe them protection."));

            var hasClaim = ClaimRegistry.HasTerritorialClaim(state, us, them);
            var settlements = them.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var fief = settlements[i];
                if (!fief.IsFortification) continue;
                var price = fief.IsTown ? DiplomacyConstants.PeaceCostTown : DiplomacyConstants.PeaceCostCastle;
                var affordable = price <= budget;
                elements.Add(new InquiryElement(fief,
                    "Annex " + fief.Name + (fief.IsTown ? " (town)" : " (castle)")
                    + "   -   " + Priced(price, budget),
                    null,
                    hasClaim && affordable,
                    !hasClaim
                        ? "We hold no territorial claim against " + them.Name
                          + ", so no land can be demanded whatever this war has earned."
                        : affordable
                            ? "Within what this war has earned."
                            : "Costs " + price.ToString("0") + ", more than the " + budget.ToString("0")
                              + " this war has earned."));
            }

            ShowTerms("Peace with " + them.Name,
                "At war for " + war.DaysElapsed.ToString("0") + " days over " + war.Justification
                + ". They are " + ExhaustionBands.Describe(war.ExhaustionOf(them))
                + ". This war has earned us " + budget.ToString("0")
                + " - the most our terms may cost. Choose nothing for a white peace.",
                elements, chosen =>
                {
                    var terms = new PeaceTerms(us, them);
                    foreach (var el in chosen)
                    {
                        if (el.Identifier is Settlement fief) { terms.FiefsCeded.Add(fief); continue; }
                        switch (el.Identifier as string)
                        {
                            case "prisoners": terms.ReleasePrisoners = true; break;
                            case "indemnity": terms.IndemnityGold = indemnity; break;
                            case "tribute":
                                terms.ImposeTributaryPact = true;
                                terms.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                                break;
                            case "subjugation":
                                if (Hegemony.IsHegemon(state, them)) terms.DissolveHegemony = true;
                                else
                                {
                                    terms.ImposeVassalage = true;
                                    terms.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                                }
                                break;
                        }
                    }
                    TryPeace(state, war, terms, us);
                });
        }

        /// <summary>
        /// The loser's checklist - what we can put on the table to end a war we are losing.
        /// The same ladder the AI walks, priced against what their victory has earned: below
        /// their floor they will not bother, above their full score the offer is more than the
        /// war justifies and <see cref="PeaceTable.IsDemandable"/> refuses it.
        /// </summary>
        private static void ShowOfferTable(ModState state, WarRecord war, Kingdom us,
            Kingdom them, float budget)
        {
            var wanted = PeaceTable.MinimumAcceptable(state, war, them);

            var elements = new List<InquiryElement>
            {
                Element("prisoners",
                    "Release their captives   -   worth " + DiplomacyConstants.PeaceCostPrisoners.ToString("0"),
                    "We free every hero of theirs we hold."),
                Element("tribute",
                    "Agree to pay tribute   -   worth " + DiplomacyConstants.PeaceCostTributaryPact.ToString("0"),
                    "A tributary pays for peace and keeps everything else.")
            };

            var indemnity = PeaceTable.LargestIndemnity(war, them, us);
            if (indemnity >= 1000)
                elements.Add(Element("indemnity",
                    "Pay an indemnity of " + indemnity + " denars   -   worth "
                    + (indemnity / 1000f * DiplomacyConstants.PeaceCostPerThousandIndemnity).ToString("0"),
                    "Sized by what this war earned them."));

            // The top rung, in whichever form we still have to give: while we hold vassals we
            // cannot submit at all, so for a hegemon the sphere is the offer.
            if (Hegemony.IsHegemon(state, us))
                elements.Add(Element("subjugation",
                    "Release our vassals   -   worth " + DiplomacyConstants.PeaceCostSubjugation.ToString("0"),
                    "We keep our throne, our lands and our court; every kingdom sworn to us"
                    + " becomes independent. They do not pass to " + them.Name + "."));
            else
                elements.Add(Element("subjugation",
                    "Submit as their vassal   -   worth " + DiplomacyConstants.PeaceCostSubjugation.ToString("0"),
                    "We keep our ruler, our lands and our court, and owe troops, tribute and our"
                    + " foreign policy - and they owe us protection."));

            var hasClaim = ClaimRegistry.HasTerritorialClaim(state, them, us);
            var settlements = us.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var fief = settlements[i];
                if (!fief.IsFortification) continue;
                var price = fief.IsTown ? DiplomacyConstants.PeaceCostTown : DiplomacyConstants.PeaceCostCastle;
                var affordable = price <= budget;
                elements.Add(new InquiryElement(fief,
                    "Cede " + fief.Name + (fief.IsTown ? " (town)" : " (castle)")
                    + "   -   worth " + price.ToString("0"),
                    null,
                    hasClaim && affordable,
                    !hasClaim
                        ? them.Name + " holds no territorial claim against us, so land cannot"
                          + " change hands however badly this war goes."
                        : affordable
                            ? "Within what this war has earned them."
                            : "Worth " + price.ToString("0") + ", more than the " + budget.ToString("0")
                              + " this war has earned them."));
            }

            ShowTerms("Sue for peace with " + them.Name,
                "At war for " + war.DaysElapsed.ToString("0") + " days over " + war.Justification
                + ". Their war score is " + budget.ToString("0") + ": they will not settle for"
                + " less than " + wanted.ToString("0") + " and cannot take more than "
                + budget.ToString("0") + ". Choose nothing to offer a white peace.",
                elements, chosen =>
                {
                    var terms = new PeaceTerms(them, us);
                    foreach (var el in chosen)
                    {
                        if (el.Identifier is Settlement fief) { terms.FiefsCeded.Add(fief); continue; }
                        switch (el.Identifier as string)
                        {
                            case "prisoners": terms.ReleasePrisoners = true; break;
                            case "indemnity": terms.IndemnityGold = indemnity; break;
                            case "tribute":
                                terms.ImposeTributaryPact = true;
                                terms.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                                break;
                            case "subjugation":
                                if (Hegemony.IsHegemon(state, us)) terms.DissolveHegemony = true;
                                else
                                {
                                    terms.ImposeVassalage = true;
                                    terms.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                                }
                                break;
                        }
                    }
                    TryPeace(state, war, terms, us);
                });
        }

        /// <summary>"costs 12 of 122" - a term's price against what the war earned.</summary>
        private static string Priced(float cost, float budget)
            => "costs " + cost.ToString("0") + " of " + budget.ToString("0");

        /// <summary>
        /// The multi-select wrapper both tables share. Ticking nothing is legal - that is the
        /// white peace - so the minimum selection is zero and the affirmative button is always
        /// live. The callback re-checks everything through <see cref="TryPeace"/> against the
        /// world as it stands when it fires, never as it stood when the list was built.
        /// </summary>
        private static void ShowTerms(string title, string description,
            List<InquiryElement> elements, Action<List<InquiryElement>> onChosen)
        {
            try
            {
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    title, description, elements, true, 0, elements.Count,
                    "Offer these terms", "Back",
                    chosen =>
                    {
                        try
                        {
                            onChosen(chosen ?? new List<InquiryElement>());
                        }
                        catch (Exception ex)
                        {
                            Log.Error("UI", "Peace-table action failed.", ex);
                            Notify("Something went wrong - see the Diplomacy & Intrigue log.");
                        }
                    },
                    _ => { }, "", false), true, false);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Could not show the peace table.", ex);
            }
        }

        /// <summary>
        /// Puts a package to the other side.
        ///
        /// Only *their* willingness is consulted, and which check that is depends on which
        /// side of the table we are on: demanding, the question is whether they will sign
        /// away what we asked; offering, whether they will settle for what we put up. Our own
        /// willingness is not a calculation - the player just chose it.
        ///
        /// This is also why <see cref="PeaceTable"/> has no idea who the player is. It
        /// exposes both checks symmetrically and the caller asks about whichever side is not
        /// making the choice, so the AI answers exactly the question the player answered by
        /// clicking.
        /// </summary>
        private static void TryPeace(ModState state, WarRecord war, PeaceTerms terms, Kingdom us)
        {
            if (!PeaceTable.IsDemandable(state, war, terms, out var notAllowed))
            {
                Notify((terms.Winner == us ? "Cannot demand that: " : "Cannot offer that: ") + notAllowed);
                return;
            }

            var weAreDemanding = terms.Winner == us;
            var accepted = weAreDemanding
                ? PeaceTable.WouldAccept(state, war, terms, out var refused)
                : PeaceTable.WinnerWouldAccept(state, war, terms, out refused);
            if (!accepted)
            {
                Notify(refused);
                return;
            }
            if (!PeaceTable.Apply(state, war, terms, out var failed))
            {
                Notify("Failed: " + failed);
                return;
            }

            Notify("Peace signed with " + terms.Loser.Name + ": " + terms + ".", Colors.Green);
        }

        internal static void BreakTreaty(ModState state, Kingdom us, Kingdom them)
        {
            var treaty = FirstBreakableTreaty(state, us, them);
            if (treaty == null) { Notify("We hold nothing with " + them.Name + " to renounce."); return; }

            TreatyRegistry.Break(state, treaty, us);
            Notify("We renounce our " + treaty.Type + " with " + them.Name
                   + ". Every court has taken note.", Colors.Red);
        }

        internal static void ShowFabricationTargets(ModState state, Kingdom us, Kingdom them)
        {
            var elements = new List<InquiryElement>();
            var settlements = them.Settlements;

            for (var i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                if (!settlement.IsFortification) continue;
                elements.Add(Element(settlement, settlement.Name.ToString(),
                    settlement.IsTown ? "A town." : "A castle."));
            }

            if (elements.Count == 0) { Notify(them.Name + " holds nothing to claim."); return; }

            Show("Fabricate a claim",
                "Heralds will produce a genealogy. It takes "
                + DiplomacyConstants.FabricateClaimDurationDays + " days and can be exposed.",
                elements, selected =>
                {
                    var attempt = ClaimRegistry.StartFabrication(state, us, (Settlement)selected, out var reason);
                    Notify(attempt == null
                        ? "Cannot fabricate: " + reason
                        : "Our heralds set to work on " + ((Settlement)selected).Name + ".");
                });
        }

        // ----- Dialog plumbing -------------------------------------------------

        private static InquiryElement Element(object id, string title, string hint)
            => new InquiryElement(id, title, null, true, hint);

        private static void Show(string title, string description,
            List<InquiryElement> elements, Action<object> onChosen)
        {
            try
            {
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    title, description, elements, true, 1, 1, "Choose", "Back",
                    chosen =>
                    {
                        if (chosen == null || chosen.Count == 0) return;
                        try
                        {
                            onChosen(chosen[0].Identifier);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("UI", "Diplomacy menu action failed.", ex);
                            Notify("Something went wrong - see the Diplomacy & Intrigue log.");
                        }
                    },
                    _ => { }, "", false), true, false);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Could not show the diplomacy menu.", ex);
            }
        }

        private static void ShowText(string title, string body)
        {
            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    title, body, true, false, "Close", "", () => { }, () => { },
                    "", 0f, null, null, null), true, false);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Could not show the diplomacy report.", ex);
            }
        }

        /// <summary>
        /// A yes/no question for the acts a misclick would make expensive: the terms go in
        /// the body, and the act only happens if the answer is yes. The callback is wrapped
        /// the same way <see cref="Show"/> wraps its choices - nothing thrown from an
        /// inquiry may reach the engine.
        /// </summary>
        private static void Confirm(string title, string body, string yesText, Action onYes)
        {
            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    title, body, true, true, yesText, "Back",
                    () =>
                    {
                        try
                        {
                            onYes();
                        }
                        catch (Exception ex)
                        {
                            Log.Error("UI", "Diplomacy action failed.", ex);
                            Notify("Something went wrong - see the Diplomacy & Intrigue log.");
                        }
                    },
                    () => { }), true);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Could not show the confirmation.", ex);
            }
        }

        internal static Treaty FirstBreakableTreaty(ModState state, Kingdom us, Kingdom them)
        {
            foreach (var treaty in state.ActiveTreatiesOf(us))
                if (treaty.Other(us) == them && treaty.Type != TreatyType.Truce) return treaty;
            return null;
        }

        private static int CountWars(ModState state, Kingdom kingdom)
        {
            var n = 0;
            foreach (var _ in state.OngoingWarsOf(kingdom)) n++;
            return n;
        }

        private static int CountTreaties(ModState state, Kingdom kingdom)
        {
            var n = 0;
            foreach (var _ in state.ActiveTreatiesOf(kingdom)) n++;
            return n;
        }

        private static void Notify(string text, Color? color = null) => Log.Notify(text, color);
    }
}
