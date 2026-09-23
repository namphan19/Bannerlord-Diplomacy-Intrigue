using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// A house divided by its own succession (Phase 2.6b, design 07 §5).
    ///
    /// **Who leads the clan is still vanilla's decision**, for the same reason the throne is
    /// (see <see cref="SuccessionModel"/>): `ChangeClanLeaderAction` already scores every heir
    /// through `HeirSelectionCalculationModel` and picks the highest. What vanilla has none of is
    /// the politics afterwards - an heir who came within a hair of the headship, never liked the
    /// one who got it, and will not serve under them.
    ///
    /// **When that happens the house divides.** The runner-up leaves with their spouse and minor
    /// children and founds a cadet branch in the same realm, by vanilla's own recipe for making a
    /// lord's clan out of a hero (`Clan.CreateCompanionToLordClan`, minus the fief). Nothing new
    /// is saved: the cadet branch is an ordinary clan, and everything downstream already exists.
    ///
    /// **The ruling house is the case that matters most, and needs nothing extra.** A runner-up
    /// who splits from the ruling clan is now a clan leader and a child or sibling of the late
    /// ruler, which is exactly who <see cref="SuccessionModel"/>'s blood claim admits. The next
    /// daily succession watch counts them as a claimant, tallies the court, and makes them a
    /// standing pretender if they hold 30% - after which the Pretenders bloc and the internal war
    /// (2.6) follow by their own rules. A second claimant path here would be a second resolver
    /// for the same question, which is the thing CLAUDE.md §3 forbids.
    ///
    /// **The player's house is under the same rule.** Vanilla lets the player pick their heir
    /// rather than scoring one, so there the successor is a choice - and passing over an heir
    /// who scored higher, and who dislikes the choice, can split the player's own house. That is
    /// the project's rule that the AI and the player play by the same rules (design 02 §9.2),
    /// and it gives the player's choice of heir a consequence it never had.
    ///
    /// **One resolver.** <see cref="Assess"/> decides whether a succession was contested; the
    /// event handler and `diplomacy.heirs` both print what it returns.
    /// </summary>
    public static class ClanSuccession
    {
        public sealed class Heir
        {
            public Hero Hero;
            public int Points;
        }

        public sealed class Assessment
        {
            public Clan Clan;
            public Hero Successor;
            public int SuccessorPoints;
            public Heir RunnerUp;
            public int Relation;
            public readonly List<Heir> Heirs = new List<Heir>();
            public bool Divides;
            public string Reason;
        }

        /// <summary>
        /// How contested a clan's succession from <paramref name="deadLeader"/> to
        /// <paramref name="successor"/> was.
        ///
        /// The heirs are scored exactly as `Clan.GetHeirApparents` scores them - same filter,
        /// same model, the same bonus for the most skilled - so "close" means close by vanilla's
        /// own measure, not ours.
        /// </summary>
        public static Assessment Assess(ModState state, Clan clan, Hero deadLeader, Hero successor)
        {
            var a = new Assessment { Clan = clan, Successor = successor };
            if (clan == null || deadLeader == null || successor == null)
            {
                a.Reason = "no succession";
                return a;
            }

            var model = Campaign.Current?.Models?.HeirSelectionCalculationModel;
            if (model == null)
            {
                a.Reason = "no heir model";
                return a;
            }

            var comesOfAge = Campaign.Current.Models.AgeModel.HeroComesOfAge;
            Hero maxSkillHero = null;
            var points = new Dictionary<Hero, int>();
            for (var i = 0; i < clan.Heroes.Count; i++)
            {
                var hero = clan.Heroes[i];
                if (!IsEligible(hero, deadLeader, comesOfAge)) continue;
                points[hero] = model.CalculateHeirSelectionPoint(hero, deadLeader, ref maxSkillHero);
            }
            if (maxSkillHero != null && points.ContainsKey(maxSkillHero))
                points[maxSkillHero] += model.HighestSkillPoint;

            foreach (var pair in points)
                a.Heirs.Add(new Heir { Hero = pair.Key, Points = pair.Value });
            a.Heirs.Sort((x, y) => y.Points.CompareTo(x.Points));

            a.SuccessorPoints = points.TryGetValue(successor, out var own) ? own : int.MinValue;

            for (var i = 0; i < a.Heirs.Count; i++)
            {
                var heir = a.Heirs[i].Hero;
                if (heir == successor || heir == successor.Spouse) continue;
                a.RunnerUp = a.Heirs[i];
                break;
            }

            if (a.RunnerUp == null) { a.Reason = "no rival heir"; return a; }

            a.Relation = a.RunnerUp.Hero.GetRelation(successor);

            if (IsAtWarWithItself(state, clan))
            {
                a.Reason = "the house is fighting an internal war (design 07 §3a Q3)";
                return a;
            }
            if (a.SuccessorPoints - a.RunnerUp.Points > IntrigueConstants.ClanSuccessionContestMargin)
            {
                a.Reason = "not close: " + a.SuccessorPoints + " against " + a.RunnerUp.Points;
                return a;
            }
            if (a.Relation >= IntrigueConstants.ClanSuccessionDisputeRelation)
            {
                a.Reason = "close, but " + a.RunnerUp.Hero.Name + " is on good terms with the new head ("
                           + a.Relation + ")";
                return a;
            }
            if (a.RunnerUp.Hero.IsPrisoner || a.RunnerUp.Hero.PartyBelongedTo?.MapEvent != null)
            {
                a.Reason = a.RunnerUp.Hero.Name + " would leave, but is held or in battle";
                return a;
            }

            a.Divides = true;
            a.Reason = "close (" + a.SuccessorPoints + " against " + a.RunnerUp.Points + ") and no love lost ("
                       + a.Relation + ")";
            return a;
        }

        /// <summary>
        /// What would happen if the clan's head died today: the heir vanilla would pick (the
        /// highest score; vanilla breaks a tie at random, this takes the first) and whether the
        /// house would divide over it. For `diplomacy.heirs`, so a live test can find a house
        /// that is one death away from splitting.
        /// </summary>
        public static Assessment Predict(ModState state, Clan clan)
        {
            var probe = Assess(state, clan, clan?.Leader, clan?.Leader);
            if (probe.Heirs.Count == 0) return probe;
            return Assess(state, clan, clan.Leader, probe.Heirs[0].Hero);
        }

        /// <summary>Vanilla's own filter in `Clan.GetHeirApparents`, read from IL.</summary>
        private static bool IsEligible(Hero hero, Hero deadLeader, float comesOfAge)
            => hero != null && hero != deadLeader && hero.IsAlive
               && hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None && !hero.IsNotSpawned
               && !hero.IsDisabled && !hero.IsWanderer && !hero.IsNotable && hero.Age >= comesOfAge;

        private static bool IsAtWarWithItself(ModState state, Clan clan)
        {
            if (InternalWars.RebellionOf(state, clan) != null) return true;
            var kingdom = clan.Kingdom;
            return kingdom != null && kingdom.RulingClan == clan && InternalWars.OngoingIn(state, kingdom) != null;
        }

        /// <summary>
        /// `OnClanLeaderChanged`. Only a death divides a house: a retirement is the old head's
        /// own choice of successor, and there is no one to have been passed over by it.
        /// </summary>
        public static void OnClanLeaderChanged(ModState state, Hero oldLeader, Hero newLeader)
        {
            if (state == null || oldLeader == null || newLeader == null) return;
            if (oldLeader.IsAlive) return;

            var clan = newLeader.Clan;
            if (clan == null || clan.IsEliminated || !clan.IsNoble || clan.IsMinorFaction) return;
            if (!Court.IsMember(clan)) return;

            var a = Assess(state, clan, oldLeader, newLeader);
            Log.Info("ClanSuccession", clan.Name + ": " + newLeader.Name + " succeeds " + oldLeader.Name
                                       + (a.RunnerUp == null ? "" : "; runner-up " + a.RunnerUp.Hero.Name)
                                       + " - " + (a.Divides ? "the house divides" : "no division") + " (" + a.Reason + ").");
            if (!a.Divides) return;

            Divide(state, a);
        }

        /// <summary>
        /// The runner-up founds a cadet branch. Public for the test command.
        ///
        /// Vanilla's recipe from `Clan.CreateCompanionToLordClan`, in its order: create, name,
        /// culture, banner, kingdom, home, move the hero in, make them leader, mark noble, raise
        /// `OnClanCreated`. Two steps differ, both on purpose. No fief is granted - a cadet
        /// branch starts landless, which is also what makes it hungry at court (`FiefStanding`).
        /// Its tier comes from a share of the parent's renown rather than the companion tier,
        /// because a younger son of a great house is not a freed companion.
        /// </summary>
        public static Clan Divide(ModState state, Assessment a)
        {
            var parent = a.Clan;
            var founder = a.RunnerUp.Hero;
            var home = parent.HomeSettlement ?? parent.InitialHomeSettlement;
            if (home == null) return null;

            // Out of any party first: a hero changing clan does not take their party along
            // (Hero.set_Clan only moves them between lord lists), and a party of the old house
            // led by a lord of the new one is a state vanilla never produces.
            var household = Household(founder, parent);
            for (var i = 0; i < household.Count; i++)
            {
                var member = household[i];
                if (member.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(member);
                if (member.PartyBelongedTo != null || member.CurrentSettlement == null)
                    TeleportHeroAction.ApplyImmediateTeleportToSettlement(member, home);
            }

            var cadet = Clan.CreateClan("di_cadet_" + parent.StringId);
            var name = new TextObject(parent.Name + " of " + founder.FirstName);
            cadet.ChangeClanName(name, name);
            cadet.Culture = parent.Culture;
            cadet.Banner = Banner.CreateOneColoredBannerWithOneIcon(
                parent.Banner.GetFirstIconColor(), parent.Banner.GetPrimaryColor(), IconOf(parent));
            cadet.Kingdom = parent.Kingdom;
            cadet.SetInitialHomeSettlement(home);

            for (var i = 0; i < household.Count; i++) household[i].Clan = cadet;
            cadet.SetLeader(founder);
            cadet.IsNoble = true;
            cadet.AddRenown(parent.Renown * IntrigueConstants.ClanSuccessionCadetRenownShare, false);

            CampaignEventDispatcher.Instance.OnClanCreated(cadet, false);

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(founder, a.Successor,
                -IntrigueConstants.ClanSuccessionRelationPenalty, false);

            BlocModel.Invalidate();

            var names = new System.Text.StringBuilder();
            for (var i = 0; i < household.Count; i++)
            {
                if (names.Length > 0) names.Append(", ");
                names.Append(household[i].Name);
            }
            Log.Info("ClanSuccession", parent.Name + " divides: " + founder.Name + " founds " + cadet.Name
                                       + " (" + cadet.StringId + ", tier " + cadet.Tier + ") with " + names
                                       + " in " + parent.Kingdom?.Name + "."
                                       + (parent.Kingdom?.RulingClan == parent
                                           ? " The ruling house has split; the succession watch will weigh the claim."
                                           : ""));
            Log.Notify(founder.Name + " would not serve " + a.Successor.Name + " and has left "
                       + parent.Name + " to found " + cadet.Name + ".", Colors.Yellow);
            return cadet;
        }

        /// <summary>
        /// Who leaves with the founder: their spouse, if in the same house, and their children
        /// who have not come of age. Grown children are lords in their own right and stay where
        /// their own interests are - a design choice, not a limitation.
        /// </summary>
        private static List<Hero> Household(Hero founder, Clan parent)
        {
            var list = new List<Hero> { founder };
            var spouse = founder.Spouse;
            if (spouse != null && spouse.IsAlive && spouse.Clan == parent) list.Add(spouse);

            for (var i = 0; i < founder.Children.Count; i++)
            {
                var child = founder.Children[i];
                if (child != null && child.IsAlive && child.IsChild && child.Clan == parent) list.Add(child);
            }
            return list;
        }

        /// <summary>The parent house's own icon, so the cadet banner reads as a branch of it.</summary>
        private static int IconOf(Clan parent)
        {
            try
            {
                var data = parent.Banner?.BannerDataList;
                if (data != null && data.Count > Banner.BannerIconDataIndex)
                    return data[Banner.BannerIconDataIndex].MeshId;
            }
            catch
            {
                // A malformed banner costs the cadet its parent's icon, nothing more.
            }
            return 0;
        }
    }
}
