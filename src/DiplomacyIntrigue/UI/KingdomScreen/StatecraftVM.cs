using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Statecraft;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// The Realm tab's Statecraft strip (design 08 §10, S1): who speaks for the realm in each of
    /// the six portfolios, their skill against the realms' median, and what that moves. Every
    /// figure comes from <see cref="StatecraftTerms"/>, the functions the formulas themselves call,
    /// so the strip cannot describe a number the game did not use.
    ///
    /// The effects are stated against a median counterpart - "budgets x1.04 against a median
    /// envoy" - because a contest has two sides and the other side is not known until a war or a
    /// deal names it. The breakdown where the contest happens (the peace table, the price column)
    /// names both heroes.
    /// </summary>
    internal sealed class DiStatecraftVM : ViewModel
    {
        private bool _show;
        private string _note = string.Empty;
        private MBBindingList<DiStatecraftRowVM> _rows = new MBBindingList<DiStatecraftRowVM>();

        // ----- static labels, moved out of the prefab (story 4.1 §9) ---------------------

        [DataSourceProperty] public string StatecraftTitleText => DiText.T("DI_STATECRAFT_STATECRAFT_TITLE", "Statecraft");
        [DataSourceProperty] public string StatecraftNoteText => DiText.T("DI_STATECRAFT_WHO_SPEAKS_FOR_THE_REALM", "Who speaks for the realm, and what their skill moves.");

        [DataSourceProperty] public bool Show { get => _show; set { if (value == _show) return; _show = value; OnPropertyChangedWithValue(value, nameof(Show)); } }
        [DataSourceProperty] public string Note { get => _note; set { value = value ?? string.Empty; if (value == _note) return; _note = value; OnPropertyChangedWithValue(value, nameof(Note)); } }
        [DataSourceProperty] public MBBindingList<DiStatecraftRowVM> Rows { get => _rows; set { if (value == _rows) return; _rows = value; OnPropertyChangedWithValue(value, nameof(Rows)); } }

        internal void Rebuild()
        {
            try
            {
                Compose();
            }
            catch (Exception ex)
            {
                Show = false;
                Log.Error("UI", "The Statecraft strip could not be rebuilt.", ex);
            }
        }

        private void Compose()
        {
            var realm = Clan.PlayerClan?.Kingdom;
            Show = SubModule.Healthy && StatecraftModel.Enabled && realm != null && realm.IsRealm();
            if (!Show) return;

            var rows = new MBBindingList<DiStatecraftRowVM>();

            var ruler = StatecraftModel.Actor(realm, Portfolio.Ruler);
            rows.Add(Row(realm, Portfolio.Ruler,
                DiText.T("DI_STATECRAFT_WAR_EXHAUSTION_EVERY_VASSAL_HOLD_EVERY_FACTOR_SIGNED_2",
                    "War exhaustion {FACTOR} · every vassal's Hold {SIGNED} · every house's loyalty {SIGNED_2}",
                    ("FACTOR", StatecraftModel.Factor(StatecraftTerms.ResolveFactor(realm))),
                    ("SIGNED", Signed(StatecraftTerms.Authority(realm))),
                    ("SIGNED_2", Signed(StatecraftTerms.Presence(realm))))));

            var envoyLevel = StatecraftModel.Level(realm, Portfolio.Envoy);
            rows.Add(Row(realm, Portfolio.Envoy,
                DiText.T("DI_STATECRAFT_PEACE_BUDGETS_AGAINST_MEDIAN_ENVOY_ON_FACTOR_SIGNED_2",
                    "Peace budgets {FACTOR} against a median envoy · {SIGNED} on any court we ask for a pact",
                    ("FACTOR", StatecraftModel.Factor(1f + StatecraftConstants.NegotiationWeight * envoyLevel)),
                    ("SIGNED", Signed(StatecraftTerms.Persuasion(realm))))));

            rows.Add(Row(realm, Portfolio.Steward,
                DiText.T("DI_STATECRAFT_GRIEVANCES_AGAINST_THE_CROWN_FADE_DAY_FADEPERDAY_2",
                    "Grievances against the crown fade {FADEPERDAY} a day · a year of peace restores {PEACEDIVIDENDOF} legitimacy",
                    ("FADEPERDAY", GrievanceRegistry.FadePerDay(realm.RulingClan).ToString("0.000")),
                    ("PEACEDIVIDENDOF", LegitimacyRegistry.PeaceDividendOf(realm).ToString("0.0")))));

            var treasurerLevel = StatecraftModel.Level(realm, Portfolio.Treasurer);
            rows.Add(Row(realm, Portfolio.Treasurer,
                DiText.T("DI_STATECRAFT_HOUSE_WE_BUY_IN_CIVIL_WAR_FACTOR_2",
                    "A house we buy in a civil war costs {FACTOR} against a median house",
                    ("FACTOR", StatecraftModel.Factor(1f - StatecraftConstants.HagglingWeight * treasurerLevel)))));

            var spyLevel = StatecraftModel.Level(realm, Portfolio.Spymaster);
            rows.Add(Row(realm, Portfolio.Spymaster,
                DiText.T("DI_STATECRAFT_CLAIM_WE_FABRICATE_IS_CAUGHT_OF_PCT_2",
                    "A claim we fabricate is caught {PCT} of the time against a median watch",
                    ("PCT", Pct(DiplomacyConstants.FabricateClaimExposureChance
                      * (1f - StatecraftConstants.SubterfugeWeight * StatecraftModel.Contest(spyLevel, 0f)))))));

            var watchLevel = StatecraftModel.Level(realm, Portfolio.Watch);
            rows.Add(Row(realm, Portfolio.Watch,
                DiText.T("DI_STATECRAFT_MEDIAN_SPYMASTER_FABRICATING_ON_US_IS_PCT_2",
                    "A median spymaster fabricating on us is caught {PCT} of the time",
                    ("PCT", Pct(DiplomacyConstants.FabricateClaimExposureChance
                      * (1f - StatecraftConstants.SubterfugeWeight * StatecraftModel.Contest(0f, watchLevel)))))));

            Rows = rows;
            Note = ruler == Hero.MainHero ? DiText.T("DI_STATECRAFT_EACH_SKILL_IS_MEASURED_AGAINST_THE_2",
                "Each skill is measured against the median of the same office across every realm, so a ruler as good as their peers moves nothing. You hold Leadership in person; the other five go to the best hero of the ruling house, family or companion. A captured hero hands the office to the next best. Doing the work trains the skill.") : DiText.T("DI_STATECRAFT_EACH_SKILL_IS_MEASURED_AGAINST_THE_3",
                "Each skill is measured against the median of the same office across every realm, so a ruler as good as their peers moves nothing. The ruler holds Leadership in person; the other five go to the best hero of the ruling house, family or companion. A captured hero hands the office to the next best. Doing the work trains the skill.");
        }

        private static DiStatecraftRowVM Row(Kingdom realm, Portfolio portfolio, string moves)
        {
            var skill = StatecraftModel.SkillOf(portfolio);
            var actor = StatecraftModel.Actor(realm, portfolio);
            var level = StatecraftModel.Level(actor, skill);

            // Who would hold it if everyone were free - so a captured chancellor is visible as such.
            string stand = string.Empty;
            if (portfolio != Portfolio.Ruler)
            {
                var absent = BestAbsent(realm.RulingClan, skill);
                if (absent != null && actor != null && absent.GetSkillValue(skill) > actor.GetSkillValue(skill))
                    stand = absent.IsPrisoner ? DiText.T("DI_STATECRAFT_STANDING_IN_FOR_PRISONER_NAME_2",
                        "Standing in for {NAME}, a prisoner",
                        ("NAME", absent.Name)) : DiText.T("DI_STATECRAFT_STANDING_IN_FOR_WHO_CANNOT_ACT_NAME_2",
                        "Standing in for {NAME}, who cannot act",
                        ("NAME", absent.Name));
            }

            var who = DiText.T("DI_STATECRAFT_ACTOR_2",
                "{ACTOR}",
                ("ACTOR", actor == null ? "nobody" : StatecraftModel.NameOf(actor)));
            if (who == "you") who = DiText.T("DI_STATECRAFT_YOU_2", "You");
            return new DiStatecraftRowVM(
                StatecraftModel.TitleOf(portfolio),
                who,
                StatecraftModel.SkillName(skill) + " " + (actor == null ? 0 : actor.GetSkillValue(skill))
                + "  ·  median " + StatecraftModel.Pivot(skill).ToString("0"),
                level > 0.005f ? DiRealmVM.PositiveColor : level < -0.005f ? DiRealmVM.NegativeColor : DiRealmVM.MutedColor,
                moves,
                stand);
        }

        /// <summary>The house's best in a skill among heroes who cannot act now, or null.</summary>
        private static Hero BestAbsent(Clan clan, SkillObject skill)
        {
            if (clan == null) return null;
            Hero best = null;
            foreach (var hero in clan.AliveLords)
                if (hero != null && !hero.IsChild && !StatecraftModel.CanAct(hero)
                    && (best == null || hero.GetSkillValue(skill) > best.GetSkillValue(skill))) best = hero;
            foreach (var hero in clan.Companions)
                if (hero != null && hero.IsAlive && !StatecraftModel.CanAct(hero)
                    && (best == null || hero.GetSkillValue(skill) > best.GetSkillValue(skill))) best = hero;
            return best;
        }

        // ASCII hyphen: the game's Fira Sans has no U+2212 and draws it as an underscore.
        private static string Signed(float x) => (x >= 0f ? "+" : "-") + Math.Abs(x).ToString("0.0");

        private static string Pct(float share) => (share * 100f).ToString("0") + "%";
    }

    /// <summary>One portfolio: who holds it, their skill against the median, and what it moves.</summary>
    internal sealed class DiStatecraftRowVM : ViewModel
    {
        public DiStatecraftRowVM(string office, string hero, string skillText, Color skillColor, string moves, string stand)
        {
            Office = office;
            Hero = hero;
            SkillText = skillText;
            SkillColor = skillColor;
            Moves = moves;
            Stand = stand;
            HasStand = !string.IsNullOrEmpty(stand);
        }

        [DataSourceProperty] public string Office { get; }
        [DataSourceProperty] public string Hero { get; }
        [DataSourceProperty] public string SkillText { get; }
        [DataSourceProperty] public Color SkillColor { get; }
        [DataSourceProperty] public string Moves { get; }
        [DataSourceProperty] public string Stand { get; }
        [DataSourceProperty] public bool HasStand { get; }
    }
}
