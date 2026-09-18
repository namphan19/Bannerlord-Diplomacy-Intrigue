using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// Writes the numbers a balance pass needs, on its own, into the log.
    ///
    /// Why this exists: the acceptance test for the diplomacy pillar is a statement about a
    /// decade of campaign - average war length, whether alliances form and hold, whether
    /// anyone sits at permanent war. Measuring that by hand means someone watching a screen
    /// for hours and typing console commands, and the retail game has no console unless a
    /// separate mod provides one. So the mod reports on itself instead: leave a campaign
    /// running and the log is the dataset.
    ///
    /// Lines are prefixed and key=value so they can be grepped and parsed without a tool.
    /// </summary>
    public static class Telemetry
    {
        private const string SnapshotPrefix = "[SNAPSHOT]";
        private const string WarEndedPrefix = "[WAR-ENDED]";

        // Written for runs nobody watches: the lead leaves the game running and the log is the
        // whole dataset. Every record is one line, `[KIND] day=<absolute day> date=<Season_D_Year>`
        // then key=value pairs with no spaces inside a value, so the analyser never has to read
        // prose. The prose lines stay beside them for people.
        private const string EventPrefix = "[EVENT]";
        private const string KingdomPrefix = "[KINGDOM]";
        private const string LinkPrefix = "[LINK]";
        private const string WarPrefix = "[WAR]";
        private const string RunPrefix = "[RUN]";
        private const string ConfigPrefix = "[CONFIG]";

        /// <summary>How a war ended, from the point of view of whose code ended it.</summary>
        public enum PeaceCause
        {
            /// <summary>Nothing of ours did it, so the vanilla peace AI or the player did.</summary>
            External = 0,
            /// <summary>Our peace table: exhaustion crossed the threshold and terms were agreed.</summary>
            PeaceTable = 1,
            /// <summary>An ally was let out of a war it had only been called into.</summary>
            FollowerRelease = 2,
            /// <summary>
            /// A war nobody was fighting, lapsed by mutual indifference. Separate from
            /// PeaceTable on purpose: a balance run needs to tell a settled war from a war
            /// that was never really a war, and both end on white terms.
            /// </summary>
            Dormant = 3,
            /// <summary>
            /// One side no longer exists: conquered down to its last settlement and destroyed by
            /// the engine, which ends its wars without a peace.
            /// </summary>
            Eliminated = 4,
        }

        /// <summary>
        /// Set immediately before our code calls MakePeaceAction, read by the war ledger
        /// when the resulting event fires, then cleared.
        ///
        /// A static is not elegant. The alternative is threading a cause through the engine
        /// event, which is not possible, or storing it on the war record, which would put a
        /// transient reporting detail into the save format. The scope is one synchronous
        /// call, and run 01 showed that not knowing this costs an hour of cross-referencing.
        /// </summary>
        public static PeaceCause PendingPeaceCause { get; private set; } = PeaceCause.External;

        /// <summary>Describes what was conceded, or null for a peace we did not broker.</summary>
        public static string PendingPeaceTerms { get; private set; }

        /// <summary>
        /// What the pending cause was before this one, to be handed back to
        /// <see cref="RestorePeaceCause"/>.
        /// </summary>
        public struct PeaceCauseToken
        {
            internal PeaceCause Cause;
            internal string Terms;
        }

        /// <summary>
        /// Declares why the peace about to be made is happening, and returns the previous
        /// value so the caller can put it back.
        ///
        /// **Why save and restore rather than clear.** Peaces nest. Our war ledger handles the
        /// engine's MakePeace event, and one of the things it does there is release allies
        /// who were only in the war because they were called into it - which makes a second,
        /// inner MakePeaceAction call while the outer one is still on the stack. When the
        /// inner call finished by *clearing* the cause, the outer war was then reported as
        /// `endedBy=External`.
        ///
        /// Run 03 shows exactly what that cost: two wars settled by our own peace table with
        /// real terms - `Battania / Northern Empire, tributary pact at 500` and
        /// `Khuzait / Sturgia, tributary pact at 500` - were both filed as ended by nobody,
        /// which is the one conclusion the whole takeover was being measured against. The
        /// mechanism worked and the instrument lied about it.
        /// </summary>
        public static PeaceCauseToken NotePeaceCause(PeaceCause cause, string terms = null)
        {
            var previous = new PeaceCauseToken { Cause = PendingPeaceCause, Terms = PendingPeaceTerms };
            PendingPeaceCause = cause;
            PendingPeaceTerms = terms;
            return previous;
        }

        /// <summary>Puts back whatever was pending before, including nothing.</summary>
        public static void RestorePeaceCause(PeaceCauseToken previous)
        {
            PendingPeaceCause = previous.Cause;
            PendingPeaceTerms = previous.Terms;
        }

        /// <summary>
        /// One line per week: the state of the world in numbers. Cheap enough to leave on.
        /// </summary>
        public static void WriteSnapshot(ModState state)
        {
            try
            {
                var ongoing = 0;
                var chosen = 0;
                var obligation = 0;
                var exhaustionTotal = 0f;
                var exhaustionSamples = 0;
                var longestWarDays = 0f;
                var dayTotal = 0f;

                for (var i = 0; i < state.Wars.Count; i++)
                {
                    var war = state.Wars[i];
                    if (!war.IsOngoing) continue;

                    ongoing++;

                    // Counted apart on purpose. A war a kingdom chose and a war it was
                    // dragged into by an ally are different things, and averaging them is
                    // what made run 01 look healthier than it was.
                    if (war.IsObligationWar) obligation++; else chosen++;

                    exhaustionTotal += war.AggressorExhaustion + war.DefenderExhaustion;
                    exhaustionSamples += 2;
                    dayTotal += war.DaysElapsed;
                    if (war.DaysElapsed > longestWarDays) longestWarDays = war.DaysElapsed;
                }

                var byType = new Dictionary<TreatyType, int>();
                for (var i = 0; i < state.Treaties.Count; i++)
                {
                    var treaty = state.Treaties[i];
                    if (!treaty.IsActive) continue;
                    byType.TryGetValue(treaty.Type, out var n);
                    byType[treaty.Type] = n + 1;
                }

                var kingdoms = 0;
                var atWar = 0;
                foreach (var kingdom in Kingdom.All)
                {
                    if (kingdom.IsEliminated) continue;
                    kingdoms++;

                    var fighting = false;
                    foreach (var _ in state.OngoingWarsOf(kingdom)) { fighting = true; break; }
                    if (fighting) atWar++;
                }

                var line = new StringBuilder(SnapshotPrefix);
                line.Append(" date=").Append(CampaignTime.Now.ToString());
                line.Append(" kingdoms=").Append(kingdoms);
                line.Append(" atWar=").Append(atWar);
                line.Append(" wars=").Append(ongoing);
                line.Append(" warsChosen=").Append(chosen);
                line.Append(" warsObligation=").Append(obligation);
                line.Append(" avgWarDays=").Append(
                    ongoing == 0 ? "0" : (dayTotal / ongoing).ToString("0"));
                line.Append(" longestWarDays=").Append(longestWarDays.ToString("0"));
                line.Append(" avgExhaustion=").Append(
                    exhaustionSamples == 0 ? "0.0" : (exhaustionTotal / exhaustionSamples).ToString("0.0"));
                line.Append(" claims=").Append(CountLiveClaims(state));
                line.Append(" trustRecords=").Append(state.Trust.Count);
                line.Append(" fabrications=").Append(state.Fabrications.Count);

                // Cumulative for the session: how often vanilla tried to start a war and
                // was told no. The headline measure of whether taking initiation over
                // actually took effect.
                line.Append(" vanillaWarsRefused=").Append(TreatyEnforcement.VanillaWarProposalsRefused);

                // The takeover's own acceptance measure. Zero counters and a working takeover
                // look identical from outside - vanilla might simply never have wanted any of
                // these - so the only honest way to know the overrides are installed and
                // being reached is to count what they refuse.
                // Hegemony is derived from treaties rather than stored, so it is derived here
                // too. `hegemons` is the headline: the number of kingdoms holding at least
                // one vassal, which is the only definition the mod has.
                var links = new List<Treaty>();
                Hegemony.CollectLinks(state, links);
                var holdTotal = 0f;
                var marks = 0;
                for (var i = 0; i < links.Count; i++)
                {
                    holdTotal += Hegemony.HoldOf(links[i]);
                    marks += links[i].DefianceMarks;
                }

                line.Append(" hegemons=").Append(Hegemony.CountHegemons(state));
                line.Append(" vassalLinks=").Append(links.Count);
                line.Append(" avgHold=")
                    .Append((links.Count == 0 ? 0f : holdTotal / links.Count).ToString("0.0"));
                line.Append(" defianceMarks=").Append(marks);

                // Cumulative for the session. Run 04 could not say whether tribute ever
                // arrived, because a payment that succeeded left no trace.
                line.Append(" tributePaid=").Append(TreatyRegistry.TributePaidThisSession);
                line.Append(" tributeWithheld=").Append(TreatyRegistry.TributeWithheldThisSession);

                // Power (docs/design/06-power.md): who is on top, by how much, and whether it has
                // turned greedy. Live dominance for the leader; greed reads the smoothed figure.
                var topDominance = 0f;
                var topName = "none";
                var greedy = 0;
                foreach (var kingdom in Kingdom.All)
                {
                    if (kingdom.IsEliminated) continue;
                    var dominance = Power.Dominance(kingdom);
                    if (dominance > topDominance)
                    {
                        topDominance = dominance;
                        topName = kingdom.Name.ToString().Replace(' ', '_');
                    }
                    if (!AiDiplomacy.WouldTakeVassals(state, kingdom)) greedy++;
                }
                line.Append(" topKingdom=").Append(topName);
                line.Append(" topDominance=").Append(topDominance.ToString("0.00"));
                line.Append(" greedy=").Append(greedy);
                line.Append(" annexationWars=").Append(Hegemony.AnnexationWarsThisSession);

                line.Append(" vanillaPeaceRefused=").Append(VanillaDiplomacy.PeaceRefused);
                line.Append(" vanillaAlliancesRefused=").Append(VanillaDiplomacy.AllianceRefused);
                line.Append(" vanillaTradeRefused=").Append(VanillaDiplomacy.TradeAgreementRefused);
                line.Append(" vanillaCallToWarRefused=").Append(VanillaDiplomacy.CallToWarRefused);

                foreach (TreatyType type in Enum.GetValues(typeof(TreatyType)))
                {
                    byType.TryGetValue(type, out var n);
                    line.Append(' ').Append(char.ToLowerInvariant(type.ToString()[0]))
                        .Append(type.ToString().Substring(1)).Append('=').Append(n);
                }

                Log.Info("Telemetry", line.ToString());
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Snapshot failed.", ex);
            }

            WriteWorldState(state);
        }

        // ================= Structured records ==================================

        /// <summary>
        /// One structured event line: <c>[EVENT] day= date= kind= key=value...</c>. Pairs are
        /// passed flat - name, value, name, value. Never throws: telemetry must not be able to
        /// break the decision it is recording.
        /// </summary>
        public static void Event(string kind, params object[] pairs)
        {
            try
            {
                if (!Settings.Current.EnableTelemetry) return;

                var line = new StringBuilder(EventPrefix);
                AppendWhen(line);
                line.Append(" kind=").Append(kind);
                for (var i = 0; i + 1 < pairs.Length; i += 2)
                    line.Append(' ').Append(pairs[i]).Append('=').Append(Value(pairs[i + 1]));
                Log.Info("Telemetry", line.ToString());
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Event record '" + kind + "' failed.", ex);
            }
        }

        /// <summary>
        /// The state of every kingdom, every vassal link and every war, one line each. Written
        /// with the weekly snapshot, so a run can be replayed week by week from the file alone:
        /// who was how strong, who held whom and why, what each war had cost.
        /// </summary>
        public static void WriteWorldState(ModState state)
        {
            if (state == null) return;

            try
            {
                foreach (var kingdom in Kingdom.All)
                {
                    if (kingdom.IsEliminated) continue;
                    Log.Info("Telemetry", KingdomLine(state, kingdom));
                }

                var links = new List<Treaty>();
                Hegemony.CollectLinks(state, links);
                for (var i = 0; i < links.Count; i++) Log.Info("Telemetry", LinkLine(state, links[i]));

                for (var i = 0; i < state.Wars.Count; i++)
                {
                    var war = state.Wars[i];
                    if (!war.IsOngoing) continue;
                    Log.Info("Telemetry", WarLine(war));
                }
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "World state record failed.", ex);
            }
        }

        private static string KingdomLine(ModState state, Kingdom k)
        {
            int towns = 0, castles = 0, villages = 0;
            var settlements = k.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var s = settlements[i];
                if (s.IsTown) towns++;
                else if (s.IsCastle) castles++;
                else if (s.IsVillage) villages++;
            }

            int alliances = 0, defensive = 0, nonAggression = 0, truces = 0, paysTribute = 0, receivesTribute = 0;
            foreach (var t in state.ActiveTreatiesOf(k))
            {
                switch (t.Type)
                {
                    case TreatyType.Alliance: alliances++; break;
                    case TreatyType.DefensivePact: defensive++; break;
                    case TreatyType.NonAggressionPact: nonAggression++; break;
                    case TreatyType.Truce: truces++; break;
                }
                if (t.TributeAmount > 0)
                {
                    if (t.TributePayer == k) paysTribute++;
                    else receivesTribute++;
                }
            }

            int chosen = 0, obligation = 0;
            var worstExhaustion = 0f;
            foreach (var war in state.OngoingWarsOf(k))
            {
                if (war.IsObligationWar) obligation++; else chosen++;
                var e = war.ExhaustionOf(k);
                if (e > worstExhaustion) worstExhaustion = e;
            }

            float trustIn = 0f, trustOut = 0f;
            var others = 0;
            foreach (var other in Kingdom.All)
            {
                if (other == k || other.IsEliminated) continue;
                trustIn += TrustRegistry.Get(state, other, k);
                trustOut += TrustRegistry.Get(state, k, other);
                others++;
            }

            var line = new StringBuilder(KingdomPrefix);
            AppendWhen(line);
            Pair(line, "kingdom", k);
            Pair(line, "strength", Power.Strength(k));
            Pair(line, "smoothed", Power.Smoothed(state, k));
            Pair(line, "dominance", Power.Dominance(k));
            Pair(line, "smoothedDominance", Power.SmoothedDominance(state, k));
            Pair(line, "ambition", Power.Ambition(k));
            Pair(line, "greed", Power.Greed(state, k));
            Pair(line, "towns", towns);
            Pair(line, "castles", castles);
            Pair(line, "villages", villages);
            Pair(line, "clans", k.Clans.Count);
            Pair(line, "ruler", k.Leader);
            Pair(line, "rulerInfluence", k.RulingClan == null ? 0f : k.RulingClan.Influence);
            Pair(line, "rulerGold", k.Leader == null ? 0 : k.Leader.Gold);
            Pair(line, "weariness", state.WearinessOf(k));
            Pair(line, "warsChosen", chosen);
            Pair(line, "warsObligation", obligation);
            Pair(line, "worstExhaustion", worstExhaustion);
            Pair(line, "patron", Hegemony.PatronOf(state, k));
            Pair(line, "vassals", Hegemony.VassalCount(state, k));
            Pair(line, "alliances", alliances);
            Pair(line, "defensivePacts", defensive);
            Pair(line, "nonAggressionPacts", nonAggression);
            Pair(line, "truces", truces);
            Pair(line, "paysTribute", paysTribute);
            Pair(line, "receivesTribute", receivesTribute);
            Pair(line, "trustIn", others == 0 ? 0f : trustIn / others);
            Pair(line, "trustOut", others == 0 ? 0f : trustOut / others);
            Pair(line, "lastMove", AiDiplomacy.LastMove(k));
            return line.ToString();
        }

        private static string LinkLine(ModState state, Treaty link)
        {
            var terms = Hegemony.HoldTermsOf(state, link);
            var critical = link.CriticalSince == CampaignTime.Never
                ? 0f
                : (float)(CampaignTime.Now - link.CriticalSince).ToDays;

            var line = new StringBuilder(LinkPrefix);
            AppendWhen(line);
            Pair(line, "patron", link.DominantParty);
            Pair(line, "vassal", link.SubordinateParty);
            Pair(line, "hold", Hegemony.HoldOf(link));
            Pair(line, "target", terms.Target);
            Pair(line, "fear", terms.Fear);
            Pair(line, "protection", terms.Protection);
            Pair(line, "trust", terms.Trust);
            Pair(line, "tribute", -terms.Tribute);
            Pair(line, "wars", -terms.Wars);
            Pair(line, "rival", -terms.Rival);
            Pair(line, "culture", -terms.Culture);
            Pair(line, "dread", -terms.Dread);
            Pair(line, "marks", link.DefianceMarks);
            Pair(line, "revoltLine", Hegemony.SecessionThreshold(state, link));
            Pair(line, "criticalDays", critical);
            Pair(line, "tributeAmount", link.TributeAmount);
            Pair(line, "signedDay", (int)link.SignedOn.ToDays);
            Pair(line, "expiresDay", (int)link.ExpiresOn.ToDays);
            return line.ToString();
        }

        private static string WarLine(WarRecord war)
        {
            var line = new StringBuilder(WarPrefix);
            AppendWhen(line);
            Pair(line, "aggressor", war.Aggressor);
            Pair(line, "defender", war.Defender);
            Pair(line, "days", war.DaysElapsed);
            Pair(line, "casusBelli", war.Justification);
            Pair(line, "aggressorExhaustion", war.AggressorExhaustion);
            Pair(line, "defenderExhaustion", war.DefenderExhaustion);
            Pair(line, "score", war.WarScore);
            Pair(line, "aggressorCasualties", war.AggressorCasualties);
            Pair(line, "defenderCasualties", war.DefenderCasualties);
            Pair(line, "fiefsTakenByAggressor", war.FiefsTakenByAggressor);
            Pair(line, "fiefsTakenByDefender", war.FiefsTakenByDefender);
            Pair(line, "calledBy", war.CalledBy);
            return line.ToString();
        }

        /// <summary>
        /// What this session is running: the build, the settings, and every constant in
        /// DiplomacyConstants. Written once when a session launches, so a log can always say
        /// which numbers produced it - the question every balance run ends up asking.
        /// </summary>
        public static void WriteRunHeader(ModState state)
        {
            try
            {
                if (!Settings.Current.EnableTelemetry) return;

                var assembly = typeof(Telemetry).Assembly;
                var built = "unknown";
                try { built = File.GetLastWriteTime(assembly.Location).ToString("yyyy-MM-dd_HH:mm:ss"); }
                catch { /* the build time is a courtesy */ }

                var names = new List<string>();
                foreach (var kingdom in Kingdom.All)
                    if (!kingdom.IsEliminated) names.Add(Value(kingdom));

                var line = new StringBuilder(RunPrefix);
                AppendWhen(line);
                Pair(line, "mod", SubModule.ModuleVersion);
                Pair(line, "built", built);
                Pair(line, "schema", state == null ? 0 : state.SchemaVersion);
                Pair(line, "aggressiveness", Settings.Current.AiAggressiveness);
                Pair(line, "exhaustionRate", Settings.Current.WarExhaustionRate);
                Pair(line, "player", Hero.MainHero);
                Pair(line, "playerKingdom", Clan.PlayerClan?.Kingdom);
                Pair(line, "kingdoms", string.Join(",", names.ToArray()));
                Log.Info("Telemetry", line.ToString());

                var fields = typeof(DiplomacyConstants).GetFields(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                for (var i = 0; i < fields.Length; i++)
                {
                    if (!fields[i].IsLiteral) continue;
                    Log.Info("Telemetry", ConfigPrefix + " " + fields[i].Name + "=" + Value(fields[i].GetValue(null)));
                }
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Run header failed.", ex);
            }
        }

        private static void AppendWhen(StringBuilder line)
        {
            line.Append(" day=").Append((int)CampaignTime.Now.ToDays);
            line.Append(" date=").Append(Value(CampaignTime.Now.ToString()));
        }

        private static void Pair(StringBuilder line, string key, object value)
            => line.Append(' ').Append(key).Append('=').Append(Value(value));

        /// <summary>One token: invariant numbers, names with underscores, never a space or an equals sign.</summary>
        private static string Value(object value)
        {
            switch (value)
            {
                case null: return "none";
                case Kingdom kingdom: return Sanitise(kingdom);
                case Hero hero: return Token(hero.Name?.ToString());
                case Clan clan: return Token(clan.Name?.ToString());
                case float f: return f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                case double d: return d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                case bool b: return b ? "true" : "false";
                default: return Token(value.ToString());
            }
        }

        private static string Token(string text)
            => string.IsNullOrEmpty(text) ? "none" : text.Replace(' ', '_').Replace('=', ':').Replace(',', ';');

        /// <summary>
        /// One line per war that ends, carrying the duration. This is the raw material for
        /// "wars average under three years" - the acceptance criterion for the pillar.
        /// </summary>
        public static void WriteWarEnded(WarRecord war)
        {
            try
            {
                var when = new StringBuilder();
                AppendWhen(when);
                Log.Info("Telemetry", WarEndedPrefix + when
                                      + " endedBy=" + PendingPeaceCause
                                      + " terms=" + (PendingPeaceTerms == null
                                          ? "unknown"
                                          : PendingPeaceTerms.Replace(' ', '_').Replace(';', ','))
                                      + " aggressor=" + Sanitise(war.Aggressor)
                                      + " defender=" + Sanitise(war.Defender)
                                      + " days=" + war.DaysElapsed.ToString("0")
                                      + " casusBelli=" + war.Justification
                                      + " finalScore=" + war.WarScore.ToString("0.0")
                                      + " exhaustion=" + war.AggressorExhaustion.ToString("0.0")
                                      + "/" + war.DefenderExhaustion.ToString("0.0")
                                      + " fiefsTaken=" + war.FiefsTakenByAggressor
                                      + "/" + war.FiefsTakenByDefender
                                      + " calledBy=" + Sanitise(war.CalledBy));
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "War-ended record failed.", ex);
            }
        }

        /// <summary>
        /// Writes a full, human-readable snapshot to its own file and returns the path.
        /// This is what the diplomacy menu offers, so the whole world state can be handed
        /// over without a console.
        /// </summary>
        public static string WriteReport(ModState state)
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "DiplomacyIntrigue", "Reports");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory,
                "report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Token(CampaignTime.Now.ToString()) + ".txt");

            var sb = new StringBuilder();
            sb.AppendLine("Diplomacy & Intrigue " + SubModule.ModuleVersion + " report");
            sb.AppendLine("Campaign date: " + CampaignTime.Now);
            sb.AppendLine("Schema version: " + state.SchemaVersion);
            sb.AppendLine();

            sb.AppendLine("== Ongoing wars ==");
            var anyWar = false;
            for (var i = 0; i < state.Wars.Count; i++)
            {
                var war = state.Wars[i];
                if (!war.IsOngoing) continue;
                anyWar = true;
                sb.AppendLine("  " + war + " days=" + war.DaysElapsed.ToString("0"));
            }
            if (!anyWar) sb.AppendLine("  (none - the world is at peace)");
            sb.AppendLine();

            sb.AppendLine("== Concluded wars ==");
            var anyEnded = false;
            for (var i = 0; i < state.Wars.Count; i++)
            {
                var war = state.Wars[i];
                if (war.IsOngoing) continue;
                anyEnded = true;
                sb.AppendLine("  " + war + " lasted " + war.DaysElapsed.ToString("0") + " days");
            }
            if (!anyEnded) sb.AppendLine("  (none yet)");
            sb.AppendLine();

            sb.AppendLine("== Active treaties ==");
            var anyTreaty = false;
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive) continue;
                anyTreaty = true;
                sb.Append("  ").Append(treaty).Append(" until ").Append(treaty.ExpiresOn);
                if (treaty.SubordinateParty != null)
                    sb.Append("  [").Append(treaty.SubordinateParty.Name)
                      .Append(" answers to ").Append(treaty.DominantParty?.Name).Append(']');
                if (treaty.TributeAmount > 0)
                    sb.Append("  [tribute ").Append(treaty.TributeAmount).Append(']');
                sb.AppendLine();
            }
            if (!anyTreaty) sb.AppendLine("  (none)");
            sb.AppendLine();

            sb.AppendLine("== Live claims ==");
            var anyClaim = false;
            for (var i = 0; i < state.Claims.Count; i++)
            {
                var claim = state.Claims[i];
                if (!claim.IsLive) continue;
                anyClaim = true;
                sb.AppendLine("  " + claim);
            }
            if (!anyClaim) sb.AppendLine("  (none)");
            sb.AppendLine();

            sb.AppendLine("== Trust ledger (directional) ==");
            if (state.Trust.Count == 0) sb.AppendLine("  (empty)");
            for (var i = 0; i < state.Trust.Count; i++) sb.AppendLine("  " + state.Trust[i]);
            sb.AppendLine();

            sb.AppendLine("== War weariness ==");
            if (state.Weariness.Count == 0) sb.AppendLine("  (none)");
            for (var i = 0; i < state.Weariness.Count; i++) sb.AppendLine("  " + state.Weariness[i]);
            sb.AppendLine();

            // The same text the console commands print, so a report and a live session agree.
            sb.AppendLine("== Power ==");
            sb.AppendLine(DebugCommands.StrengthCommand(null));
            sb.AppendLine("== Hegemony ==");
            sb.AppendLine(DebugCommands.HegemonyCommand(null));

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Log.Info("Telemetry", "Report written to " + path);
            return path;
        }

        private static int CountLiveClaims(ModState state)
        {
            var n = 0;
            for (var i = 0; i < state.Claims.Count; i++) if (state.Claims[i].IsLive) n++;
            return n;
        }

        private static string Sanitise(Kingdom kingdom)
            => kingdom == null ? "none" : kingdom.Name.ToString().Replace(' ', '_');
    }
}
