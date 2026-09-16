using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
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

        private static void WriteReport(ModState state)
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
                sb.AppendLine("  our hold to them: " + hold.ToString("0.0") + " / 100  -  " + HoldMeaning(hold));
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
                    sb.AppendLine("      " + HoldMeaning(hold));
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
                if (other == kingdom || other.IsEliminated) continue;
                if (!Hegemony.IsHegemon(state, other)) continue;

                var held = new List<Treaty>();
                Hegemony.CollectVassalages(state, other, held);
                if (held.Count == 0) continue;

                otherSpheres++;
                sb.AppendLine(other.Name + " holds " + held.Count + " vassal(s):");
                for (var i = 0; i < held.Count; i++)
                {
                    var link = held[i];
                    sb.AppendLine("  " + link.SubordinateParty.Name + " - " + HoldMeaning(Hegemony.HoldOf(link)));
                }
                sb.AppendLine();
            }

            if (ourPatron == null && ours.Count == 0 && otherSpheres == 0)
            {
                sb.AppendLine("Nobody on this map holds a vassal.");
                sb.AppendLine();
                sb.AppendLine("Submission is imposed at a peace table once a war has earned "
                              + DiplomacyConstants.PeaceCostVassalage.ToString("0")
                              + " points of war score, or offered by a kingdom that cannot survive"
                              + " alone. One vassal is all it takes to be a hegemon.");
            }

            ShowText("Hegemony", sb.ToString());
        }

        /// <summary>
        /// What a hold figure means in behaviour, which is the only part of it a player can
        /// act on. Shown for rivals too - watching a bond fail needs no spies.
        /// </summary>
        private static string HoldMeaning(float hold)
        {
            if (hold >= DiplomacyConstants.HoldRenewThreshold) return "loyal; will renew when the term ends";
            if (hold >= DiplomacyConstants.HoldPassiveResistanceThreshold) return "serving, but will let the term lapse";
            if (hold >= DiplomacyConstants.HoldDefianceThreshold) return "resisting; refuses summons and withholds tribute";
            if (hold >= DiplomacyConstants.HoldSecessionThreshold) return "defiant; treats with outsiders";
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
                if (other == kingdom || other.IsEliminated) continue;
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
                if (other == kingdom || other.IsEliminated) continue;

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
                if (atWar)
                {
                    elements.Add(Element("peace", "Negotiate peace",
                        "See what this war has earned and offer terms."));
                }
                else
                {
                    AddPactOption(state, us, them, TreatyType.NonAggressionPact, elements);
                    AddPactOption(state, us, them, TreatyType.DefensivePact, elements);
                    AddPactOption(state, us, them, TreatyType.Alliance, elements);
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
                    + (DiplomacyConstants.FabricateClaimExposureChance * 100f).ToString("0")
                    + "% chance of being caught."));
            }

            Show(them.Name.ToString(), Summary(state, us, them), elements, selected =>
            {
                switch (selected as string)
                {
                    case "report": ShowText(them.Name.ToString(), Summary(state, us, them)); return;
                    case "peace": ShowPeace(state, us, them); return;
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
            var cost = DiplomacyConstants.TreatyInfluenceCost(type);

            // Their own valuation, shown honestly: this is the number the AI uses to decide.
            var theirValue = AiDiplomacy.PactValue(state, them, us);
            var hint = allowed
                ? cost + " influence. " + them.Name + " values it at " + theirValue.ToString("0")
                  + " (they need " + ThresholdFor(type).ToString("0") + ")."
                : reason;

            into.Add(new InquiryElement(type, "Propose " + type, null, allowed, hint));
        }

        private static float ThresholdFor(TreatyType type)
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

        private static void ProposePact(ModState state, Kingdom us, Kingdom them, TreatyType type)
        {
            // The other side has to want it too, judged by the same function the AI uses.
            var theirValue = AiDiplomacy.PactValue(state, them, us);
            if (theirValue < ThresholdFor(type))
            {
                Notify(them.Name + " declines: they value a " + type + " at only "
                       + theirValue.ToString("0") + ".");
                return;
            }

            var cost = DiplomacyConstants.TreatyInfluenceCost(type);
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
            Notify(us.Name + " and " + them.Name + " sign a " + type + ".", Colors.Green);
        }

        private static void ShowPeace(ModState state, Kingdom us, Kingdom them)
        {
            var war = state.OngoingWarBetween(us, them);
            if (war == null) { Notify("We are not at war with " + them.Name + "."); return; }

            var elements = new List<InquiryElement>
            {
                Element("white", "Offer a white peace", "Nothing changes hands."),
                Element("allowance", "What has this war earned?",
                    "The demand budget, the price of each term, and how close they are to signing."),
            };

            // Suing for peace. Necessary, not optional: vanilla's peace paths are ours now
            // ([design 05](../../docs/design/05-vanilla-override.md)), so without this a
            // losing player would have no way at all to end a war they cannot win.
            var theirBudget = PeaceTable.BudgetFor(war, them);
            if (theirBudget > 0f)
                elements.Add(Element("offer", "Offer terms to end this war",
                    them.Name + " has earned " + theirBudget.ToString("0")
                    + " at our expense and will not simply walk away."));

            var budget = PeaceTable.BudgetFor(war, us);
            if (budget > 0f)
            {
                elements.Add(Element("prisoners", "Demand our captives back",
                    "Costs " + DiplomacyConstants.PeaceCostPrisoners.ToString("0")
                    + " of a budget of " + budget.ToString("0") + "."));

                if (budget >= DiplomacyConstants.PeaceCostTributaryPact)
                    elements.Add(Element("tribute", "Demand tribute",
                        "Costs " + DiplomacyConstants.PeaceCostTributaryPact.ToString("0")
                        + " of a budget of " + budget.ToString("0") + "."));

                if (budget >= DiplomacyConstants.PeaceCostVassalage)
                    elements.Add(Element("vassalage", "Demand their submission",
                        "Costs " + DiplomacyConstants.PeaceCostVassalage.ToString("0")
                        + " of a budget of " + budget.ToString("0")
                        + ". They keep their ruler and their lands, and owe us troops, tribute"
                        + " and their foreign policy - and we owe them protection."));

                if (ClaimRegistry.HasTerritorialClaim(state, us, them))
                    elements.Add(Element("land", "Demand land", "Choose a fief to annex."));
                else
                    elements.Add(new InquiryElement("land", "Demand land", null, false,
                        "We hold no territorial claim against " + them.Name
                        + ", so no land can be demanded whatever this war has earned."));
            }

            Show("Peace with " + them.Name, PeaceTable.DescribeAllowance(state, war, us), elements, selected =>
            {
                var terms = new PeaceTerms(us, them);
                switch ((string)selected)
                {
                    case "allowance":
                        ShowText("Peace with " + them.Name, PeaceTable.DescribeAllowance(state, war, us));
                        return;
                    case "white":
                        break;
                    case "offer":
                        ShowSueForPeace(state, war, us, them);
                        return;
                    case "prisoners":
                        terms.ReleasePrisoners = true;
                        break;
                    case "tribute":
                        terms.ImposeTributaryPact = true;
                        terms.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                        break;
                    case "vassalage":
                        terms.ImposeVassalage = true;
                        terms.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                        break;
                    case "land":
                        ShowLandTargets(state, war, us, them);
                        return;
                }

                TryPeace(state, war, terms, us);
            });
        }

        /// <summary>
        /// What we can put on the table to end a war we are losing. The same ladder the AI
        /// walks, priced against what their victory has earned: below
        /// <see cref="DiplomacyConstants.PeaceWinnerMinimumShare"/> of it they will not
        /// bother, above their full score they are being offered more than the war justifies
        /// and <c>IsDemandable</c> refuses it.
        /// </summary>
        private static void ShowSueForPeace(ModState state, WarRecord war, Kingdom us, Kingdom them)
        {
            var budget = PeaceTable.BudgetFor(war, them);
            var wanted = budget * DiplomacyConstants.PeaceWinnerMinimumShare;

            var elements = new List<InquiryElement>
            {
                Element("prisoners", "Release their captives",
                    "Worth " + DiplomacyConstants.PeaceCostPrisoners.ToString("0")
                    + ". They want at least " + wanted.ToString("0") + ".")
            };

            var gold = us.Leader?.Gold ?? 0;
            var affordable = gold / 2 / 1000 * 1000;
            if (affordable >= 1000)
                elements.Add(Element("gold", "Pay an indemnity of " + affordable + " denars",
                    "Worth " + (affordable / 1000f * DiplomacyConstants.PeaceCostPerThousandIndemnity)
                        .ToString("0") + " to them, plus their captives."));

            elements.Add(Element("tribute", "Agree to pay tribute",
                "Worth " + DiplomacyConstants.PeaceCostTributaryPact.ToString("0")
                + ". A tributary pays for peace and keeps everything else."));

            if (budget >= DiplomacyConstants.PeaceCostVassalage)
                elements.Add(Element("submit", "Submit as their vassal",
                    "Worth " + DiplomacyConstants.PeaceCostVassalage.ToString("0")
                    + ", the most this war can be worth to them. We keep our ruler, our lands"
                    + " and our court, and owe troops, tribute and our foreign policy - and"
                    + " they owe us protection. A patron who fails to protect loses the vassal."));

            if (ClaimRegistry.HasTerritorialClaim(state, them, us))
                elements.Add(Element("land", "Cede a holding", "Choose what to give up."));
            else
                elements.Add(new InquiryElement("land", "Cede a holding", null, false,
                    them.Name + " holds no territorial claim against us, so land cannot change"
                    + " hands however badly this war goes."));

            Show("Sue for peace with " + them.Name,
                "Their war score is " + budget.ToString("0") + ". They will not settle for less than "
                + wanted.ToString("0") + " while they are winning, and cannot take more than "
                + budget.ToString("0") + " - that is what the war has earned them.",
                elements, selected =>
                {
                    var terms = new PeaceTerms(them, us) { ReleasePrisoners = true };
                    switch ((string)selected)
                    {
                        case "prisoners":
                            break;
                        case "gold":
                            terms.IndemnityGold = affordable;
                            break;
                        case "tribute":
                            terms.ImposeTributaryPact = true;
                            terms.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                            break;
                        case "submit":
                            terms.ImposeVassalage = true;
                            terms.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
                            break;
                        case "land":
                            ShowCedeTargets(state, war, us, them);
                            return;
                    }

                    TryPeace(state, war, terms, us);
                });
        }

        /// <summary>Our own fiefs, as things we could give up to end a war.</summary>
        private static void ShowCedeTargets(ModState state, WarRecord war, Kingdom us, Kingdom them)
        {
            var budget = PeaceTable.BudgetFor(war, them);
            var elements = new List<InquiryElement>();

            var settlements = us.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                if (!settlement.IsFortification) continue;

                var price = settlement.IsTown
                    ? DiplomacyConstants.PeaceCostTown
                    : DiplomacyConstants.PeaceCostCastle;
                var withinWhatTheyEarned = price <= budget;

                elements.Add(new InquiryElement(settlement,
                    settlement.Name + (settlement.IsTown ? " (town)" : " (castle)"), null,
                    withinWhatTheyEarned,
                    withinWhatTheyEarned
                        ? "Worth " + price.ToString("0") + " of their score of " + budget.ToString("0") + "."
                        : "Worth " + price.ToString("0") + ", more than the " + budget.ToString("0")
                          + " this war has earned them - they cannot take it at a peace table."));
            }

            if (elements.Count == 0) { Notify("We hold nothing that could be ceded."); return; }

            Show("Cede a holding", "Their war score: " + budget.ToString("0") + ".", elements, selected =>
            {
                var terms = new PeaceTerms(them, us) { ReleasePrisoners = true };
                terms.FiefsCeded.Add((Settlement)selected);
                TryPeace(state, war, terms, us);
            });
        }

        private static void ShowLandTargets(ModState state, WarRecord war, Kingdom us, Kingdom them)
        {
            var budget = PeaceTable.BudgetFor(war, us);
            var elements = new List<InquiryElement>();

            var settlements = them.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                if (!settlement.IsFortification) continue;

                var price = settlement.IsTown
                    ? DiplomacyConstants.PeaceCostTown
                    : DiplomacyConstants.PeaceCostCastle;
                var affordable = price <= budget;

                elements.Add(new InquiryElement(settlement,
                    settlement.Name + (settlement.IsTown ? " (town)" : " (castle)"), null, affordable,
                    affordable
                        ? "Costs " + price.ToString("0") + " of a budget of " + budget.ToString("0") + "."
                        : "Costs " + price.ToString("0") + ", but this war has only earned "
                          + budget.ToString("0") + "."));
            }

            if (elements.Count == 0) { Notify(them.Name + " holds nothing we could annex."); return; }

            Show("Demand land", "Budget: " + budget.ToString("0") + ".", elements, selected =>
            {
                var terms = new PeaceTerms(us, them);
                terms.FiefsCeded.Add((Settlement)selected);
                TryPeace(state, war, terms, us);
            });
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

        private static void BreakTreaty(ModState state, Kingdom us, Kingdom them)
        {
            var treaty = FirstBreakableTreaty(state, us, them);
            if (treaty == null) { Notify("We hold nothing with " + them.Name + " to renounce."); return; }

            TreatyRegistry.Break(state, treaty, us);
            Notify("We renounce our " + treaty.Type + " with " + them.Name
                   + ". Every court has taken note.", Colors.Red);
        }

        private static void ShowFabricationTargets(ModState state, Kingdom us, Kingdom them)
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

        private static Treaty FirstBreakableTreaty(ModState state, Kingdom us, Kingdom them)
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
