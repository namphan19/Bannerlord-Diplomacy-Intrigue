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
                Notify(DiText.T("DI_MENU_DIPLOMACY_IS_ONLY_AVAILABLE_IN_CAMPAIGN_2",
                    "Diplomacy is only available in a campaign."));
                return;
            }

            var kingdom = Clan.PlayerClan?.Kingdom;
            if (kingdom == null)
            {
                Notify(DiText.T("DI_MENU_YOU_BELONG_TO_NO_KINGDOM_SO_2",
                    "You belong to no kingdom, so you have no foreign policy."));
                return;
            }

            var isRuler = kingdom.Leader == Hero.MainHero;
            var elements = new List<InquiryElement>
            {
                Element("wars", DiText.T("DI_MENU_OUR_WARS_COUNTWARS_2",
                    "Our wars ({COUNTWARS})",
                    ("COUNTWARS", CountWars(state, kingdom))),
                    DiText.T("DI_MENU_EXHAUSTION_WAR_SCORE_AND_WHAT_EACH_2",
                        "Exhaustion, war score, and what each war has earned.")),
                Element("treaties", DiText.T("DI_MENU_OUR_AGREEMENTS_COUNTTREATIES_2",
                    "Our agreements ({COUNTTREATIES})",
                    ("COUNTTREATIES", CountTreaties(state, kingdom))),
                    DiText.T("DI_MENU_EVERY_TREATY_WE_HOLD_AND_WHAT_2",
                        "Every treaty we hold, and what it obliges.")),
                Element("claims", DiText.T("DI_MENU_OUR_CLAIMS_2", "Our claims"),
                    DiText.T("DI_MENU_STANDING_JUSTIFICATIONS_FOR_WAR_AND_WHAT_2",
                        "Standing justifications for war, and what they allow.")),
                Element("kingdoms", DiText.T("DI_MENU_OTHER_KINGDOMS_2", "Other kingdoms"),
                    DiText.T("DI_MENU_RELATIONS_TRUST_AND_WHAT_WE_CAN_2",
                        "Relations, trust, and what we can propose.")),
                Element("hegemony", DescribeOurStanding(state, kingdom),
                    DiText.T("DI_MENU_WHO_ANSWERS_TO_WHOM_KINGDOM_HOLDING_2",
                        "Who answers to whom. A kingdom holding one vassal is a hegemon.")),
                Element("report", DiText.T("DI_MENU_WRITE_REPORT_TO_FILE_2", "Write a report to file"),
                    DiText.T("DI_MENU_SAVES_THE_WHOLE_WORLD_STATE_TO_2",
                        "Saves the whole world state to Documents/Mount and Blade II Bannerlord/DiplomacyIntrigue/Reports, for sharing or for balance work.")),
            };

            var header = isRuler ? DiText.T("DI_MENU_YOU_RULE_HERE_NAME_2",
                "{NAME} - you rule here.",
                ("NAME", kingdom.Name)) : DiText.T("DI_MENU_YOU_ARE_VASSAL_HERE_SO_THIS_NAME_2",
                "{NAME} - you are a vassal here, so this is a view only.",
                ("NAME", kingdom.Name));

            Show(DiText.T("DI_MENU_DIPLOMACY_2", "Diplomacy"), header, elements, selected =>
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
                ShowText(DiText.T("DI_MENU_REPORT_WRITTEN_2", "Report written"), DiText.T("DI_MENU_SAVED_TO_THE_WEEKLY_TELEMETRY_LINES_PATH_2",
                    "Saved to:\n{PATH}\n\nThe weekly telemetry lines are in the log beside it.",
                    ("PATH", path)));
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Writing the report failed.", ex);
                Notify(DiText.T("DI_MENU_COULD_NOT_WRITE_THE_REPORT_SEE_2",
                    "Could not write the report - see the log."));
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
                sb.AppendLine("  war score:        " + WarScore.For(war, kingdom).ToString("0.0")
                              + (WarScore.For(war, kingdom) > 0 ? " in our favour" : ""));
                sb.AppendLine("    " + WarScore.Describe(war, kingdom));
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
            if (patron != null) return DiText.T("DI_MENU_OUR_STANDING_VASSAL_OF_NAME_2",
                "Our standing - vassal of {NAME}",
                ("NAME", patron.Name));

            var vassals = Hegemony.VassalCount(state, kingdom);
            if (vassals > 0) return DiText.T("DI_MENU_OUR_STANDING_HEGEMON_OVER_KINGDOM_VASSALS_2",
                "Our standing - hegemon over {VASSALS} kingdom(s)",
                ("VASSALS", vassals));

            return DiText.T("DI_MENU_OUR_STANDING_INDEPENDENT_2", "Our standing - independent");
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
            if (hold >= DiplomacyConstants.HoldRenewThreshold) return DiText.T("DI_MENU_LOYAL_WILL_RENEW_WHEN_THE_TERM_2",
                "loyal; will renew when the term ends");
            if (hold >= DiplomacyConstants.HoldPassiveResistanceThreshold) return DiText.T("DI_MENU_SERVING_BUT_WILL_LET_THE_TERM_2",
                "serving, but will let the term lapse");
            if (hold >= DiplomacyConstants.HoldDefianceThreshold) return DiText.T("DI_MENU_RESISTING_REFUSES_SUMMONS_AND_WITHHOLDS_TRIBUTE_2",
                "resisting; refuses summons and withholds tribute");
            // The revolt line moves with the balance of strength, so it is read per link.
            if (!Hegemony.IsAtBreakingPoint(state, link)) return DiText.T("DI_MENU_DEFIANT_TREATS_WITH_OUTSIDERS_2", "defiant; treats with outsiders");
            return DiText.T("DI_MENU_AT_BREAKING_POINT_COUNTING_DOWN_TO_2",
                "at breaking point; counting down to revolt");
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
                Notify(DiText.T("DI_MENU_THERE_ARE_NO_OTHER_KINGDOMS_LEFT_2",
                    "There are no other kingdoms left."));
                return;
            }

            Show(DiText.T("DI_MENU_OTHER_KINGDOMS", "Other kingdoms"), DiText.T("DI_MENU_CHOOSE_KINGDOM_2", "Choose a kingdom."), elements,
                selected => ShowKingdom(state, kingdom, (Kingdom)selected, isRuler));
        }

        private static void ShowKingdom(ModState state, Kingdom us, Kingdom them, bool isRuler)
        {
            var atWar = us.IsAtWarWith(them);
            var elements = new List<InquiryElement>
            {
                Element("report", DiText.T("DI_MENU_WHAT_WE_KNOW_2", "What we know"), DiText.T("DI_MENU_RELATIONS_TRUST_AND_STANDING_AGREEMENTS_2",
                    "Relations, trust and standing agreements.")),
            };

            if (isRuler)
            {
                var ourLink = Hegemony.VassalageOf(state, us);
                if (atWar)
                {
                    elements.Add(Element("peace", DiText.T("DI_MENU_NEGOTIATE_PEACE_2", "Negotiate peace"),
                        DiText.T("DI_MENU_SEE_WHAT_THIS_WAR_HAS_EARNED_2",
                            "See what this war has earned and offer terms.")));

                    // Kneeling is the other way out: a free kingdom submits outright, a
                    // vassal's version is a defection (design/04 §12.4.4 and F3).
                    if (ourLink == null)
                    {
                        var can = AiDiplomacy.CanSubmitTo(state, us, them, out var why, out _);
                        elements.Add(new InquiryElement("submit", DiText.T("DI_MENU_KNEEL_TO_THEM_2", "Kneel to them"), null, can,
                            DiText.T("DI_MENU_WHY_2",
                                "{WHY}",
                                ("WHY", can
                                ? "The oath is the peace: the war ends and we answer to them."
                                : why))));
                    }
                    else
                    {
                        var can = AiDiplomacy.CanDefectToAttacker(state, us, them, out var why, out _);
                        elements.Add(new InquiryElement("defect", DiText.T("DI_MENU_BEG_THEIR_MERCY_2", "Beg their mercy"), null, can,
                            DiText.T("DI_MENU_WHY",
                                "{WHY}",
                                ("WHY", can
                                ? "End this war as their vassal - " + ourLink.DominantParty.Name
                                  + ", which would not defend us, is named the oathbreaker."
                                : why))));
                    }
                }
                else
                {
                    var block = TreatyEnforcement.WhyWarBlocked(state, us, them);
                    elements.Add(new InquiryElement("war", DiText.T("DI_MENU_DECLARE_WAR_2", "Declare war"), null,
                        block == TreatyEnforcement.Block.None,
                        DiText.T("DI_MENU_BLOCK_2",
                            "{BLOCK}",
                            ("BLOCK", block == TreatyEnforcement.Block.None
                            ? "Puts the question to the court, which votes on it - the same"
                              + " proposal the Decisions tab offers."
                            : TreatyEnforcement.Explain(state, us, them, block) + "."))));

                    AddPactOption(state, us, them, TreatyType.NonAggressionPact, elements);
                    AddPactOption(state, us, them, TreatyType.DefensivePact, elements);
                    AddPactOption(state, us, them, TreatyType.Alliance, elements);

                    var canTribute = AiDiplomacy.CanDemandTribute(state, us, them, out var whyTribute);
                    elements.Add(new InquiryElement("tribute", DiText.T("DI_MENU_DEMAND_TRIBUTE_2", "Demand tribute"), null, canTribute,
                        DiText.T("DI_MENU_WHYTRIBUTE_2",
                            "{WHYTRIBUTE}",
                            ("WHYTRIBUTE", canTribute
                            ? DiplomacyConstants.AiDefaultTributePerPeriod
                              + " per period. Coercion, not negotiation: the claim makes the"
                              + " pretext and our strength makes the argument."
                            : whyTribute))));

                    if (ourLink == null)
                    {
                        var can = AiDiplomacy.CanSubmitTo(state, us, them, out var why, out _);
                        elements.Add(new InquiryElement("submit", DiText.T("DI_MENU_KNEEL_TO_THEM", "Kneel to them"), null, can,
                            DiText.T("DI_MENU_WHY",
                                "{WHY}",
                                ("WHY", can
                                ? "Their oath for our foreign policy: tribute, troops in their"
                                  + " wars, protection owed to us."
                                : why))));
                    }

                    // Courting somebody else's neglected vassal means war with its patron.
                    var theirLink = Hegemony.VassalageOf(state, them);
                    if (theirLink != null && theirLink.DominantParty != us)
                    {
                        var can = Hegemony.CanPoach(state, us, theirLink, out var value, out var why);
                        elements.Add(new InquiryElement("court",
                            DiText.T("DI_MENU_COURT_THEM_AWAY_FROM_NAME_2",
                                "Court them away from {NAME}",
                                ("NAME", theirLink.DominantParty.Name)), null, can,
                            DiText.T("DI_MENU_WHY",
                                "{WHY}",
                                ("WHY", can
                                ? "They would kneel to us (valued at " + value.ToString("0")
                                  + "). Taking them means war with " + theirLink.DominantParty.Name + "."
                                : why))));
                    }

                    // A greedy patron may tear up a vassal's oath and take its lands.
                    if (Hegemony.CouldAnnex(state, us, them))
                        elements.Add(Element("annex", DiText.T("DI_MENU_TEAR_UP_THEIR_OATH_AND_MAKE_2",
                            "Tear up their oath and make war"),
                            DiText.T("DI_MENU_THE_FULL_PRICE_OF_BREACH_THEN_2",
                                "The full price of a breach, then conquest - the same move a greedy AI patron makes.")));
                }

                // Independence is the one foreign-policy act a vassal keeps for itself,
                // so it shows on the patron's row in war or in peace.
                if (ourLink != null && ourLink.DominantParty == them)
                {
                    var breaking = Hegemony.IsAtBreakingPoint(state, ourLink);
                    elements.Add(new InquiryElement("secede", DiText.T("DI_MENU_DECLARE_INDEPENDENCE_2", "Declare independence"), null, breaking,
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
                        DiText.T("DI_MENU_ALWAYS_POSSIBLE_NEVER_FREE_TRUST_WITH_TRUSTTREATYBROKENVICTIM_2",
                            "Always possible, never free: -{TRUSTTREATYBROKENVICTIM} trust with them, -{TRUSTTREATYBROKENOBSERVER} with every other court, and they gain a reason for war.",
                            ("TRUSTTREATYBROKENVICTIM", (-DiplomacyConstants.TrustTreatyBrokenVictim).ToString("0")),
                            ("TRUSTTREATYBROKENOBSERVER", (-DiplomacyConstants.TrustTreatyBrokenObserver).ToString("0")))));

                elements.Add(Element("fabricate", DiText.T("DI_MENU_FABRICATE_CLAIM_2", "Fabricate a claim"),
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
                Notify(DiText.T("DI_MENU_DECLINES_THEY_VALUE_AT_ONLY_NAME_TYPE_THEIRVALUE_2",
                    "{NAME} declines: they value a {TYPE} at only {THEIRVALUE}.",
                    ("NAME", them.Name),
                    ("TYPE", type),
                    ("THEIRVALUE", (int)theirValue)));
                return;
            }

            var cost = StatecraftTerms.TreatyInfluenceCost(us, type);
            if (us.RulingClan == null || us.RulingClan.Influence < cost)
            {
                Notify(DiText.T("DI_MENU_NOT_ENOUGH_INFLUENCE_COSTS_TYPE_COST_2",
                    "Not enough influence: {TYPE} costs {COST}.",
                    ("TYPE", type),
                    ("COST", cost)));
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
                Notify(DiText.T("DI_MENU_CANNOT_DECLARE_WAR_EXPLAIN_2",
                    "Cannot declare war: {EXPLAIN}.",
                    ("EXPLAIN", TreatyEnforcement.Explain(state, us, them, block))));
                return;
            }

            var proposer = Clan.PlayerClan;
            var decision = new DeclareWarDecision(proposer, them);
            if (!decision.IsAllowed())
            {
                Notify(DiText.T("DI_MENU_THE_COURT_WILL_NOT_ENTERTAIN_WAR_NAME_2",
                    "The court will not entertain a war against {NAME} right now.",
                    ("NAME", them.Name)));
                return;
            }

            var cost = AiDiplomacy.WarDeclarationCostAgainst(state, us, them, proposer.Leader);
            if (proposer.Influence < cost)
            {
                Notify(DiText.T("DI_MENU_NOT_ENOUGH_INFLUENCE_PROPOSING_THIS_WAR_COST_2",
                    "Not enough influence: proposing this war costs {COST}.",
                    ("COST", cost)));
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
                Notify(DiText.T("DI_MENU_WE_CANNOT_KNEEL_TO_NAME_REASON_2",
                    "We cannot kneel to {NAME}: {REASON}",
                    ("NAME", them.Name),
                    ("REASON", reason)));
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

            Confirm("Submission to " + them.Name, body, DiText.T("DI_MENU_KNEEL_2", "Kneel"), () =>
            {
                // Re-asked rather than trusted: the inquiry can sit open through a whole
                // day of drift, so the world may have moved since the button was clicked.
                if (!AiDiplomacy.CanSubmitTo(state, us, them, out var blocked,
                        out var stillSettlesWar))
                {
                    Notify(DiText.T("DI_MENU_COULD_NOT_SUBMIT_BLOCKED_2",
                        "Could not submit: {BLOCKED}",
                        ("BLOCKED", blocked)));
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
                    Notify(DiText.T("DI_MENU_COULD_NOT_SUBMIT_FAILED_2",
                        "Could not submit: {FAILED}",
                        ("FAILED", failed)));
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
            // EvaluateTribute rather than CanDemandTribute (its wrapper): the telemetry below
            // wants the court's share and which gate answered, not only yes or no.
            var terms = AiDiplomacy.EvaluateTribute(state, us, them);
            if (!terms.Allowed)
            {
                // The button is disabled with this reason, so this is reached only when the
                // world moved between drawing it and the click. Recorded like the AI's demand:
                // only when the target's court, the cap or the cooldown answered it.
                var gate = AiDiplomacy.AnsweringGate(terms);
                if (gate != null) AiDiplomacy.NoteTributeAnswer(terms, gate);
                Notify(DiText.T("DI_MENU_CANNOT_DEMAND_TRIBUTE_BLOCKED_2",
                    "Cannot demand tribute: {BLOCKED}",
                    ("BLOCKED", terms.Blocked)));
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
            AiDiplomacy.NoteTributeAnswer(terms, null);
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
                DiText.T("DI_MENU_KNEEL", "Kneel"), () =>
                {
                    if (!AiDiplomacy.CanDefectToAttacker(state, us, them, out var lapsed,
                            out var current))
                    {
                        Notify(DiText.T("DI_MENU_THE_OFFER_HAS_LAPSED_LAPSED_2",
                            "The offer has lapsed: {LAPSED}",
                            ("LAPSED", lapsed)));
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
                Notify(DiText.T("DI_MENU_CANNOT_COURT_THEM_REASON_2",
                    "Cannot court them: {REASON}",
                    ("REASON", reason)));
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

            Confirm("Court " + them.Name, body, DiText.T("DI_MENU_COURT_THEM_2", "Court them"), () =>
            {
                if (!Hegemony.CanPoach(state, us, link, out var currentValue, out var lapsed))
                {
                    Notify(DiText.T("DI_MENU_THE_MOMENT_HAS_PASSED_LAPSED_2",
                        "The moment has passed: {LAPSED}",
                        ("LAPSED", lapsed)));
                    return;
                }

                // ExecutePoach announces a success itself; only a failure needs telling.
                if (!Hegemony.ExecutePoach(state, us, link, currentValue))
                    Notify(DiText.T("DI_MENU_THE_COURTSHIP_FAILED_SEE_THE_DIPLOMACY_2",
                        "The courtship failed - see the Diplomacy & Intrigue log."));
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
                Notify(DiText.T("DI_MENU_CANNOT_TURN_ON_NAME_2",
                    "Cannot turn on {NAME}.",
                    ("NAME", them.Name)));
                return;
            }

            var terms = AiDiplomacy.EvaluateWar(state, us, them, asAnnexation: true);
            var cost = AiDiplomacy.WarDeclarationCost(state, us,
                CasusBelli.Legitimacy(terms.Casus));

            Confirm("Turn on " + them.Name,
                DiText.T("DI_MENU_WE_TEAR_UP_OATH_AND_TAKE_NAME_COST_2",
                    "We tear up {NAME}'s oath and take their lands ourselves: the full price of a breach in every court, every vassal we hold takes the lesson in hold, and war opens at once.\n\nDeclaring costs {COST} influence.",
                    ("NAME", them.Name),
                    ("COST", cost)),
                DiText.T("DI_MENU_TO_WAR_2", "To war"), () =>
                {
                    if (!Hegemony.CouldAnnex(state, us, them))
                    {
                        Notify(DiText.T("DI_MENU_THE_MOMENT_HAS_PASSED_THE_OATH_2",
                            "The moment has passed - the oath no longer stands between us."));
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
                Notify(DiText.T("DI_MENU_WE_DO_NOT_ANSWER_TO_NAME_2",
                    "We do not answer to {NAME}.",
                    ("NAME", them.Name)));
                return;
            }

            if (!Hegemony.IsAtBreakingPoint(state, link))
            {
                Notify(DiText.T("DI_MENU_THE_REALM_IS_NOT_BREAKING_HOLD_HOLDOF_SECESSIONTHRESHOLD_2",
                    "The realm is not breaking - hold {HOLDOF} against a secession line of {SECESSIONTHRESHOLD}. Renouncing the oath is always possible; rebellion is not.",
                    ("HOLDOF", Hegemony.HoldOf(link).ToString("0")),
                    ("SECESSIONTHRESHOLD", Hegemony.SecessionThreshold(state, link).ToString("0"))));
                return;
            }

            Confirm("Secede from " + them.Name,
                DiText.T("DI_MENU_WE_RENOUNCE_OUR_OATH_TO_AND_NAME_2",
                    "We renounce our oath to {NAME} and fight for our independence: every court counts the breach, their loyal vassals answer their call to arms, and their resentful ones may rise with us.\n\nThe war of independence opens at once.",
                    ("NAME", them.Name)),
                DiText.T("DI_MENU_SECEDE_2", "Secede"), () =>
                {
                    // Re-asked rather than trusted: the bond may have moved while the
                    // inquiry sat open.
                    var current = Hegemony.VassalageOf(state, us);
                    if (current == null || current.DominantParty != them
                        || !Hegemony.IsAtBreakingPoint(state, current))
                    {
                        Notify(DiText.T("DI_MENU_THE_MOMENT_HAS_PASSED_THE_BOND_2",
                            "The moment has passed - the bond no longer stands as it did."));
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
            if (ourBudget > 0f) return DiText.T("DI_MENU_NEGOTIATE_PEACE", "Negotiate peace");
            if (theirBudget > 0f) return DiText.T("DI_MENU_SUE_FOR_PEACE_2", "Sue for peace");
            return DiText.T("DI_MENU_WHITE_PEACE_ONLY_2", "White peace only");
        }

        /// <summary>The line under that button, on the same branch.</summary>
        internal static string PeaceButtonSub(float ourBudget, float theirBudget, Kingdom them)
        {
            if (ourBudget > 0f)
                return "Budget " + ourBudget.ToString("0") + " - see hint for the price list.";
            if (theirBudget > 0f)
                return DiText.T("DI_MENU_THE_WAR_HAS_EARNED_OFFER_WHAT_NAME_THEIRBUDGET_2",
                    "The war has earned {NAME} {THEIRBUDGET}. Offer what it takes.",
                    ("NAME", them.Name),
                    ("THEIRBUDGET", theirBudget.ToString("0")));
            return DiText.T("DI_MENU_WHITE_PEACE_ONLY_THIS_WAR_HAS_2",
                "White peace only - this war has earned nothing yet.");
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
            if (war == null) { Notify(DiText.T("DI_MENU_WE_ARE_NOT_AT_WAR_WITH_NAME_2",
                "We are not at war with {NAME}.",
                ("NAME", them.Name))); return; }

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
                    DiText.T("DI_MENU_NEITHER_SIDE_HAS_EARNED_ENOUGH_TO_2",
                        "Neither side has earned enough to ask for anything - a white peace is all this war can produce. Propose it and they sign if the war has worn them too."),
                    DiText.T("DI_MENU_PROPOSE_WHITE_PEACE_2", "Propose white peace"),
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
                    DiText.T("DI_MENU_NEITHER_SIDE_HAS_EARNED_ENOUGH_TO",
                        "Neither side has earned enough to ask for anything - a white peace is all this war can produce. Propose it and they sign if the war has worn them too."),
                    DiText.T("DI_MENU_PROPOSE_WHITE_PEACE", "Propose white peace"),
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
                    DiText.T("DI_MENU_DEMAND_OUR_CAPTIVES_BACK_PRICED_2",
                        "Demand our captives back   -   {PRICED}",
                        ("PRICED", Priced(DiplomacyConstants.PeaceCostPrisoners, budget))),
                    DiText.T("DI_MENU_THEY_FREE_EVERY_HERO_OF_OURS_2",
                        "They free every hero of ours they hold."))
            };

            // Sized by what the war earned, the same way the AI ladder sizes it, so a small
            // win buys a small indemnity rather than none.
            var indemnity = PeaceTable.LargestIndemnity(war, us, them);
            if (indemnity >= 1000)
                elements.Add(Element("indemnity",
                    DiText.T("DI_MENU_DEMAND_AN_INDEMNITY_OF_DENARS_INDEMNITY_PRICED_2",
                        "Demand an indemnity of {INDEMNITY} denars   -   {PRICED}",
                        ("INDEMNITY", indemnity),
                        ("PRICED", Priced(PeaceTable.IndemnityPoints(indemnity, them), budget))),
                    DiText.T("DI_MENU_GOLD_NOW_RATHER_THAN_LAND_LATER_DESCRIBEINDEMNITY_2",
                        "Gold now rather than land later: {DESCRIBEINDEMNITY}.",
                        ("DESCRIBEINDEMNITY", PeaceTable.DescribeIndemnity(indemnity, them)))));

            // Asked of the CanSign that PeaceTable.IsDemandable asks at signing, so a loser that
            // cannot be made a tributary - already paying the most tributes a realm can
            // (MaxTributeObligations), or answering to a patron - shows the rung closed with
            // its reason, rather than offering it and refusing the whole package at the end.
            var tributeOpen = TreatyRegistry.CanSign(state, us, them, TreatyType.TributaryPact,
                out var noTribute, settlesWar: true);
            elements.Add(new InquiryElement("tribute",
                DiText.T("DI_MENU_IMPOSE_TRIBUTE_OF_PER_PERIOD_AIDEFAULTTRIBUTEPERPERIOD_PRICED_2",
                    "Impose tribute of {AIDEFAULTTRIBUTEPERPERIOD} per period   -   {PRICED}",
                    ("AIDEFAULTTRIBUTEPERPERIOD", DiplomacyConstants.AiDefaultTributePerPeriod),
                    ("PRICED", Priced(DiplomacyConstants.PeaceCostTributaryPact, budget))),
                null, tributeOpen,
                DiText.T("DI_MENU_NOTRIBUTE_2",
                    "{NOTRIBUTE}",
                    ("NOTRIBUTE", tributeOpen ? "They pay for peace and keep everything else." : noTribute))));

            // One rung with two faces at one price: a hegemon cannot be made a vassal while it
            // still holds vassals (hegemony is flat), so against one the demand is its sphere.
            if (Hegemony.IsHegemon(state, them))
                elements.Add(Element("subjugation",
                    DiText.T("DI_MENU_DEMAND_THEY_RELEASE_THEIR_VASSALS_PRICED_2",
                        "Demand they release their vassals   -   {PRICED}",
                        ("PRICED", Priced(DiplomacyConstants.PeaceCostSubjugation, budget))),
                    DiText.T("DI_MENU_THEY_KEEP_THEIR_THRONE_EVERY_KINGDOM_2",
                        "They keep their throne; every kingdom sworn to them goes free. We inherit none of them, and they stay in their own wars.")));
            else
                elements.Add(Element("subjugation",
                    DiText.T("DI_MENU_DEMAND_THEIR_SUBMISSION_PRICED_2",
                        "Demand their submission   -   {PRICED}",
                        ("PRICED", Priced(DiplomacyConstants.PeaceCostSubjugation, budget))),
                    DiText.T("DI_MENU_THEY_KEEP_THEIR_RULER_AND_THEIR_2",
                        "They keep their ruler and their lands, and owe us troops, tribute and their foreign policy - and we owe them protection.")));

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
                DiText.T("DI_MENU_AT_WAR_FOR_DAYS_OVER_THEY_DAYSELAPSED_JUSTIFICATION_DESCRIBE_2",
                    "At war for {DAYSELAPSED} days over {JUSTIFICATION}. They are {DESCRIBE}. This war has earned us {BUDGET} - the most our terms may cost. Choose nothing for a white peace.",
                    ("DAYSELAPSED", war.DaysElapsed.ToString("0")),
                    ("JUSTIFICATION", war.Justification),
                    ("DESCRIBE", ExhaustionBands.Describe(war.ExhaustionOf(them))),
                    ("BUDGET", budget.ToString("0"))),
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

            // The same CanSign question as the demand table's tribute row, from the loser's side.
            var tributeOpen = TreatyRegistry.CanSign(state, them, us, TreatyType.TributaryPact,
                out var noTribute, settlesWar: true);
            var elements = new List<InquiryElement>
            {
                Element("prisoners",
                    DiText.T("DI_MENU_RELEASE_THEIR_CAPTIVES_WORTH_PEACECOSTPRISONERS_2",
                        "Release their captives   -   worth {PEACECOSTPRISONERS}",
                        ("PEACECOSTPRISONERS", DiplomacyConstants.PeaceCostPrisoners.ToString("0"))),
                    DiText.T("DI_MENU_WE_FREE_EVERY_HERO_OF_THEIRS_2",
                        "We free every hero of theirs we hold.")),
                new InquiryElement("tribute",
                    DiText.T("DI_MENU_AGREE_TO_PAY_TRIBUTE_WORTH_PEACECOSTTRIBUTARYPACT_2",
                        "Agree to pay tribute   -   worth {PEACECOSTTRIBUTARYPACT}",
                        ("PEACECOSTTRIBUTARYPACT", DiplomacyConstants.PeaceCostTributaryPact.ToString("0"))),
                    null, tributeOpen,
                    DiText.T("DI_MENU_NOTRIBUTE",
                        "{NOTRIBUTE}",
                        ("NOTRIBUTE", tributeOpen ? "A tributary pays for peace and keeps everything else." : noTribute)))
            };

            var indemnity = PeaceTable.LargestIndemnity(war, them, us);
            if (indemnity >= 1000)
                elements.Add(Element("indemnity",
                    DiText.T("DI_MENU_PAY_AN_INDEMNITY_OF_DENARS_WORTH_INDEMNITY_INDEMNITYPOINTS_2",
                        "Pay an indemnity of {INDEMNITY} denars   -   worth {INDEMNITYPOINTS}",
                        ("INDEMNITY", indemnity),
                        ("INDEMNITYPOINTS", PeaceTable.IndemnityPoints(indemnity, us).ToString("0"))),
                    DiText.T("DI_MENU_SIZED_BY_WHAT_THIS_WAR_EARNED_DESCRIBEINDEMNITY_2",
                        "Sized by what this war earned them: {DESCRIBEINDEMNITY}.",
                        ("DESCRIBEINDEMNITY", PeaceTable.DescribeIndemnity(indemnity, us)))));

            // The top rung, in whichever form we still have to give: while we hold vassals we
            // cannot submit at all, so for a hegemon the sphere is the offer.
            if (Hegemony.IsHegemon(state, us))
                elements.Add(Element("subjugation",
                    DiText.T("DI_MENU_RELEASE_OUR_VASSALS_WORTH_PEACECOSTSUBJUGATION_2",
                        "Release our vassals   -   worth {PEACECOSTSUBJUGATION}",
                        ("PEACECOSTSUBJUGATION", DiplomacyConstants.PeaceCostSubjugation.ToString("0"))),
                    DiText.T("DI_MENU_WE_KEEP_OUR_THRONE_OUR_LANDS_NAME_2",
                        "We keep our throne, our lands and our court; every kingdom sworn to us becomes independent. They do not pass to {NAME}.",
                        ("NAME", them.Name))));
            else
                elements.Add(Element("subjugation",
                    DiText.T("DI_MENU_SUBMIT_AS_THEIR_VASSAL_WORTH_PEACECOSTSUBJUGATION_2",
                        "Submit as their vassal   -   worth {PEACECOSTSUBJUGATION}",
                        ("PEACECOSTSUBJUGATION", DiplomacyConstants.PeaceCostSubjugation.ToString("0"))),
                    DiText.T("DI_MENU_WE_KEEP_OUR_RULER_OUR_LANDS_2",
                        "We keep our ruler, our lands and our court, and owe troops, tribute and our foreign policy - and they owe us protection.")));

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

            ShowTerms(DiText.T("DI_MENU_SUE_FOR_PEACE_WITH_NAME_2",
                "Sue for peace with {NAME}",
                ("NAME", them.Name)),
                DiText.T("DI_MENU_AT_WAR_FOR_DAYS_OVER_THEIR_DAYSELAPSED_JUSTIFICATION_BUDGET_2",
                    "At war for {DAYSELAPSED} days over {JUSTIFICATION}. Their war score is {BUDGET}: they will not settle for less than {WANTED} and cannot take more than {BUDGET_2}. Choose nothing to offer a white peace.",
                    ("DAYSELAPSED", war.DaysElapsed.ToString("0")),
                    ("JUSTIFICATION", war.Justification),
                    ("BUDGET", budget.ToString("0")),
                    ("WANTED", wanted.ToString("0")),
                    ("BUDGET_2", budget.ToString("0"))),
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
                    DiText.T("DI_MENU_OFFER_THESE_TERMS_2", "Offer these terms"), DiText.T("DI_MENU_BACK_2", "Back"),
                    chosen =>
                    {
                        try
                        {
                            onChosen(chosen ?? new List<InquiryElement>());
                        }
                        catch (Exception ex)
                        {
                            Log.Error("UI", "Peace-table action failed.", ex);
                            Notify(DiText.T("DI_MENU_SOMETHING_WENT_WRONG_SEE_THE_DIPLOMACY_2",
                                "Something went wrong - see the Diplomacy & Intrigue log."));
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
                Notify(terms.Winner == us ? DiText.T("DI_MENU_CANNOT_DEMAND_THAT_NOTALLOWED_2",
                    "Cannot demand that: {NOTALLOWED}",
                    ("NOTALLOWED", notAllowed)) : DiText.T("DI_MENU_CANNOT_OFFER_THAT_NOTALLOWED_2",
                    "Cannot offer that: {NOTALLOWED}",
                    ("NOTALLOWED", notAllowed)));
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
            if (treaty == null) { Notify(DiText.T("DI_MENU_WE_HOLD_NOTHING_WITH_TO_RENOUNCE_NAME_2",
                "We hold nothing with {NAME} to renounce.",
                ("NAME", them.Name))); return; }

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
                    settlement.IsTown ? DiText.T("DI_MENU_TOWN_2", "A town.") : DiText.T("DI_MENU_CASTLE_2", "A castle.")));
            }

            if (elements.Count == 0) { Notify(DiText.T("DI_MENU_HOLDS_NOTHING_TO_CLAIM_NAME_2",
                "{NAME} holds nothing to claim.",
                ("NAME", them.Name))); return; }

            Show(DiText.T("DI_MENU_FABRICATE_CLAIM", "Fabricate a claim"),
                DiText.T("DI_MENU_HERALDS_WILL_PRODUCE_GENEALOGY_IT_TAKES_2",
                    "Heralds will produce a genealogy. It takes {FABRICATECLAIMDURATIONDAYS} days and can be exposed.",
                    ("FABRICATECLAIMDURATIONDAYS", DiplomacyConstants.FabricateClaimDurationDays)),
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
                    title, description, elements, true, 1, 1, DiText.T("DI_MENU_CHOOSE_2", "Choose"), DiText.T("DI_MENU_BACK", "Back"),
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
                            Notify(DiText.T("DI_MENU_SOMETHING_WENT_WRONG_SEE_THE_DIPLOMACY",
                                "Something went wrong - see the Diplomacy & Intrigue log."));
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
                    title, body, true, false, DiText.T("DI_MENU_CLOSE_2", "Close"), "", () => { }, () => { },
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
                    title, body, true, true, yesText, DiText.T("DI_MENU_BACK", "Back"),
                    () =>
                    {
                        try
                        {
                            onYes();
                        }
                        catch (Exception ex)
                        {
                            Log.Error("UI", "Diplomacy action failed.", ex);
                            Notify(DiText.T("DI_MENU_SOMETHING_WENT_WRONG_SEE_THE_DIPLOMACY",
                                "Something went wrong - see the Diplomacy & Intrigue log."));
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
