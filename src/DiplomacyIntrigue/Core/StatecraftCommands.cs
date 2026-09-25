using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Statecraft;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// The statecraft layer's diagnostics and levers (design 08 §11, S0). Kept in its own file
    /// of the same class so the console namespace stays "diplomacy" and the helpers are shared.
    /// </summary>
    public static partial class DebugCommands
    {
        private static readonly Portfolio[] AllPortfolios =
        {
            Portfolio.Ruler, Portfolio.Envoy, Portfolio.Steward,
            Portfolio.Treasurer, Portfolio.Spymaster, Portfolio.Watch
        };

        /// <summary>
        /// Who holds each portfolio for every realm, the realms' medians, and - for one realm - every
        /// term those actors feed. Usage: diplomacy.statecraft [kingdom]
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("statecraft", "diplomacy")]
        public static string StatecraftReport(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var sb = new StringBuilder();
            sb.AppendLine("Statecraft (design 08): " + (StatecraftModel.Enabled ? "ON" : "OFF - every term neutral, no XP"));

            sb.Append("Medians:");
            foreach (var p in AllPortfolios)
            {
                var skill = StatecraftModel.SkillOf(p);
                sb.Append("  " + StatecraftModel.TitleOf(p) + " " + StatecraftModel.SkillName(skill)
                          + " " + StatecraftModel.Pivot(skill).ToString("0"));
            }
            sb.AppendLine();

            Kingdom only = null;
            if (args != null && args.Count > 0)
            {
                only = FindKingdom(string.Join(" ", args));
                if (only == null) return "No kingdom matching \"" + string.Join(" ", args) + "\".";
            }

            foreach (var kingdom in Kingdom.All)
            {
                if (!kingdom.IsRealm()) continue;
                if (only != null && kingdom != only) continue;

                sb.AppendLine(kingdom.Name + ":");
                foreach (var p in AllPortfolios)
                {
                    var skill = StatecraftModel.SkillOf(p);
                    var actor = StatecraftModel.Actor(kingdom, p);
                    sb.AppendLine("  " + StatecraftModel.TitleOf(p).PadRight(10)
                                  + (actor == null ? "nobody" : actor.Name + " (" + actor.Clan?.Name + ")").PadRight(34)
                                  + StatecraftModel.SkillName(skill) + " " + (actor == null ? 0 : actor.GetSkillValue(skill))
                                  + "  level " + StatecraftModel.Level(actor, skill).ToString("+0.00;-0.00;0.00")
                                  + (actor != null && !StatecraftModel.CanAct(actor) ? "  (cannot act - fallback)" : ""));
                }
            }

            if (only == null) return sb.ToString();

            var ruler = only.Leader;
            sb.AppendLine("Terms for " + only.Name + ":");
            sb.AppendLine("  S-1 " + StatecraftTerms.ResolveLine(ruler));
            sb.AppendLine("  S-3 " + StatecraftTerms.PersuasionLine(only) + " on any court it asks");
            sb.AppendLine("  S-4 authority on every Hold it is patron of: "
                          + StatecraftModel.Signed(StatecraftTerms.Authority(only)));
            sb.AppendLine("  S-6 " + StatecraftTerms.RecoveryLine(only.RulingClan)
                          + " -> grievances against the crown fade "
                          + GrievanceRegistry.FadePerDay(only.RulingClan).ToString("0.000") + "/day, peace dividend "
                          + LegitimacyRegistry.PeaceDividendOf(only).ToString("0.00") + "/year");
            sb.AppendLine("  S-7 presence on every loyalty in its court: "
                          + StatecraftModel.Signed(StatecraftTerms.Presence(only)));
            sb.AppendLine("  A-2 Firebrand on the ruler: "
                          + (StatecraftTerms.FirebrandFactor(ruler) < 1f ? "yes, x0.75 on wars and treaties" : "no"));
            foreach (var other in Kingdom.All)
            {
                if (other == only || !other.IsRealm()) continue;
                sb.AppendLine("  vs " + other.Name + ": S-2 budget " + StatecraftModel.Factor(StatecraftTerms.NegotiationFactor(only, other))
                              + " as winner; S-5 exposure " + (StatecraftTerms.ExposureChance(only, other) * 100f).ToString("0.0")
                              + "% fabricating on them; war cost "
                              + AiDiplomacy.WarDeclarationCostAgainst(state, only, other));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Sets a hero's skill, for predicting a term by hand and reading it back.
        /// Usage: diplomacy.test_set_skill hero | skill | value (skill: charm, leadership, steward, trade, roguery, scouting)
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_set_skill", "diplomacy")]
        public static string TestSetSkill(List<string> args)
        {
            if (CoreBehavior.State == null) return NoCampaign;
            var parts = SplitOnPipe(args);
            if (parts.Count < 3) return "Usage: diplomacy.test_set_skill <hero> | <skill> | <value>";

            var hero = FindHero(parts[0]);
            if (hero == null) return "No hero matching \"" + parts[0] + "\".";
            var skill = FindPoliticalSkill(parts[1]);
            if (skill == null) return "Skill must be one of charm, leadership, steward, trade, roguery, scouting.";
            if (!int.TryParse(parts[2], out var value) || value < 0 || value > 330) return "Value must be 0-330.";

            var before = hero.GetSkillValue(skill);
            hero.SetSkillValue(skill, value);
            BlocModel.Invalidate();
            return hero.Name + " (" + hero.Clan?.Name + "): " + StatecraftModel.SkillName(skill) + " " + before + " -> "
                   + hero.GetSkillValue(skill) + ". Level now "
                   + StatecraftModel.Level(hero, skill).ToString("+0.00;-0.00;0.00") + " against the median "
                   + StatecraftModel.Pivot(skill).ToString("0") + ".";
        }

        /// <summary>
        /// Gives a hero a perk, for A-2 (Firebrand) and A-3 (Silver Tongue).
        /// Usage: diplomacy.test_add_perk hero | perk (by name or id, e.g. Firebrand)
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_add_perk", "diplomacy")]
        public static string TestAddPerk(List<string> args)
        {
            if (CoreBehavior.State == null) return NoCampaign;
            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.test_add_perk <hero> | <perk>";

            var hero = FindHero(parts[0]);
            if (hero == null) return "No hero matching \"" + parts[0] + "\".";

            PerkObject perk = null;
            foreach (var candidate in PerkObject.All)
            {
                var name = candidate.Name?.ToString();
                if (string.Equals(candidate.StringId, parts[1], StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, parts[1], StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name?.Replace(" ", ""), parts[1].Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                {
                    perk = candidate;
                    break;
                }
            }
            if (perk == null) return "No perk matching \"" + parts[1] + "\".";
            if (hero.GetPerkValue(perk)) return hero.Name + " already has " + perk.Name + ".";

            hero.HeroDeveloper.AddPerk(perk);
            return hero.Name + " now has " + perk.Name + ": " + hero.GetPerkValue(perk) + ".";
        }

        /// <summary>
        /// Flips the Statecraft setting for this session, the same property MCM writes, so the
        /// regression check (off prints exactly what the build before design 08 printed) and S3's
        /// control run can be driven from the bridge. Not saved into the campaign; MCM persists it
        /// only if the player also changes it there.
        /// Usage: diplomacy.test_statecraft on|off
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_statecraft", "diplomacy")]
        public static string TestStatecraft(List<string> args)
        {
            var settings = Settings.Current;
            var arg = args == null || args.Count == 0 ? "" : args[0].Trim().ToLowerInvariant();
            if (arg == "on") settings.EnableStatecraft = true;
            else if (arg == "off") settings.EnableStatecraft = false;
            else return "Usage: diplomacy.test_statecraft on|off   (now " + (StatecraftModel.Enabled ? "on" : "off") + ")";
            BlocModel.Invalidate();
            return "Statecraft is now " + (StatecraftModel.Enabled ? "ON" : "OFF") + ".";
        }

        /// <summary>
        /// The player's house joins a kingdom as a sworn vassal, for the civil-war prompts that
        /// need the player inside a realm before its war begins (the side-choice prompt). Refused
        /// while the player's house rules a kingdom. Test saves only.
        /// Usage: diplomacy.test_player_join Battania
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_player_join", "diplomacy")]
        public static string TestPlayerJoin(List<string> args)
        {
            if (CoreBehavior.State == null) return NoCampaign;
            if (args == null || args.Count == 0) return "Usage: diplomacy.test_player_join <kingdom>";
            var kingdom = FindKingdom(string.Join(" ", args));
            if (kingdom == null) return "No kingdom matching \"" + string.Join(" ", args) + "\".";
            if (!kingdom.IsRealm()) return kingdom.Name + " is not a realm.";

            var player = Clan.PlayerClan;
            if (player == null) return "No player clan.";
            if (player.Kingdom == kingdom) return "The player's house is already in " + kingdom.Name + ".";
            if (player.Kingdom != null && player.Kingdom.RulingClan == player)
                return "The player's house rules " + player.Kingdom.Name + "; it cannot leave that realm without a crown.";

            if (player.Kingdom == null)
                TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction.ApplyByJoinToKingdom(player, kingdom, default(CampaignTime), true);
            else
                TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction.ApplyByJoinToKingdomByDefection(player, player.Kingdom, kingdom, default(CampaignTime), true);
            return player.Kingdom == kingdom
                ? "The player's house is now a vassal of " + kingdom.Name + "."
                : "The player's house could not join " + kingdom.Name + ".";
        }

        /// <summary>
        /// A hero taken prisoner by another hero's party, for the civil war's captivity rule (a
        /// leader held 30 days by the other side ends the war). Test saves only.
        /// Usage: diplomacy.test_imprison prisoner | captor
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_imprison", "diplomacy")]
        public static string TestImprison(List<string> args)
        {
            if (CoreBehavior.State == null) return NoCampaign;
            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.test_imprison <prisoner> | <captor>";
            var prisoner = FindHero(parts[0]);
            var captor = FindHero(parts[1]);
            if (prisoner == null || captor == null) return "Hero not found.";
            if (prisoner.IsPrisoner) return prisoner.Name + " is already a prisoner.";
            var party = captor.PartyBelongedTo?.Party;
            if (party == null) return captor.Name + " is not in a party.";

            TaleWorlds.CampaignSystem.Actions.TakePrisonerAction.Apply(party, prisoner);
            return prisoner.Name + (prisoner.IsPrisoner
                ? " is held by " + captor.Name + "'s party (" + party.MapFaction?.Name + ")."
                : " could not be taken.");
        }

        /// <summary>
        /// Moves a crown's legitimacy to a value, through the registry's own Adjust (so the change
        /// is recorded with a reason and the bloc memo is invalidated as for any real event), for
        /// reaching the internal-war trigger on a test save. Test saves only.
        /// Usage: diplomacy.test_set_legitimacy Battania | 20
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_set_legitimacy", "diplomacy")]
        public static string TestSetLegitimacy(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.test_set_legitimacy <kingdom> | <0-100>";
            var kingdom = FindKingdom(parts[0]);
            if (kingdom == null) return "No kingdom matching \"" + parts[0] + "\".";
            if (!float.TryParse(parts[1], out var target) || target < 0f || target > 100f) return "Value must be 0-100.";

            var before = LegitimacyRegistry.Of(state, kingdom);
            LegitimacyRegistry.Adjust(state, kingdom, target - before, "test_set_legitimacy");
            return kingdom.Name + ": legitimacy " + before.ToString("0.0") + " -> "
                   + LegitimacyRegistry.Of(state, kingdom).ToString("0.0") + ".";
        }

        private static SkillObject FindPoliticalSkill(string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "charm": return DefaultSkills.Charm;
                case "leadership": return DefaultSkills.Leadership;
                case "steward": return DefaultSkills.Steward;
                case "trade": return DefaultSkills.Trade;
                case "roguery": return DefaultSkills.Roguery;
                case "scouting": return DefaultSkills.Scouting;
                default: return null;
            }
        }
    }
}
