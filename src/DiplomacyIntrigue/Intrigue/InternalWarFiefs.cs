using System;
using System.Collections.Generic;
using System.Reflection;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// The fiefs an internal war moved between its two sides (review R-6, decided 2026-09-27):
    /// how many each side has lost and the other still holds, which wears it down daily as an
    /// occupied fief does in a foreign war, and what a crown victory gives back.
    ///
    /// **"Who held it when the war began" comes from the game's own ownership log, and nothing is
    /// saved.** The decision was to read it from our fief ledger, `FiefHistory`. That ledger
    /// cannot answer it: it is kept per *kingdom*, and a transfer between two clans of one kingdom
    /// is deliberately not recorded (`FiefHistory.RecordTransfer`: "a grant between clans of the
    /// same kingdom does not change the holder"). Rebels keep `Clan.Kingdom` (design 07 §3b), so
    /// every capture of an internal war is exactly such a transfer and leaves no trace there.
    /// Adding the clan to that ledger, or a capture list to `InternalWar`, is new save data,
    /// which this change was not to add. Vanilla already keeps the clan-level record:
    /// `ChangeSettlementOwnerLogEntry` (settlement, previous clan, new clan, by siege, date), saved
    /// with the campaign in `Campaign.LogEntryHistory`. `FiefHistory` still answers the half it
    /// can - whether the fief stayed in the realm when it changed hands - so neither ledger is
    /// copied into the other and no third one is started.
    ///
    /// **Unverified, and why it degrades safely.** Which vanilla code writes that entry, for
    /// which kinds of transfer, and how long `LogEntryHistory.DeleteOutdatedLogs` keeps it, are
    /// method bodies the reference assemblies do not carry. If the entries are missing, this
    /// reads a war with no fief changes: nobody is worn down by occupation and nothing is given
    /// back, which is exactly what happened before. `diplomacy.internal_wars` prints how many
    /// changes it read, so the first live check shows at once whether the log is there.
    /// The capture event itself (the one-off exhaustion of losing a fief) does not depend on
    /// the log - see <c>InternalWars.OnSettlementOwnerChanged</c>.
    /// </summary>
    public static class InternalWarFiefs
    {
        /// <summary>One change of a fortification's owner, from vanilla's log.</summary>
        public sealed class Change
        {
            public Settlement Settlement;
            public Clan From;
            public Clan To;
            public CampaignTime On;

            /// <summary>Taken by siege. Read from the entry's private field; see <see cref="BySiegeField"/>.</summary>
            public bool BySiege;

            /// <summary>
            /// Taken by force across the civil war's line: by siege, from one house of the realm
            /// by another, the fief never leaving the realm (<c>FiefHistory.HeldAcross</c>). Inside
            /// one realm nothing else can be besieged - the rising is at war with the crown and
            /// nobody else - so which side each house was on that day, which the war does not
            /// record, never has to be known.
            /// </summary>
            public bool IsCapture;
        }

        /// <summary>
        /// Every fortification that changed owner while the war ran, and its changes, oldest first.
        /// </summary>
        public sealed class Ledger
        {
            public readonly Dictionary<Settlement, List<Change>> BySettlement = new Dictionary<Settlement, List<Change>>();

            /// <summary>Changes of a fortification's owner anywhere in the world while the war ran.</summary>
            public int Changes;

            /// <summary>Those that were captures across this war's line (<see cref="Change.IsCapture"/>).</summary>
            public int Captures;

            /// <summary>False when the game's log could not be read at all.</summary>
            public bool Readable;

            /// <summary>False when the siege flag could not be read, so every change counted as one.</summary>
            public bool SiegeFlagKnown;
        }

        /// <summary>A fief a crown victory gives back.</summary>
        public sealed class Restitution
        {
            public Settlement Settlement;

            /// <summary>The rebel house holding it at the end.</summary>
            public Clan Holder;

            /// <summary>Who held it when the war began: the previous owner at its first change in the war.</summary>
            public Clan HeldAtStart;

            /// <summary><see cref="HeldAtStart"/> if alive and still in the realm, else the ruling clan.</summary>
            public Clan ReturnsTo;
        }

        /// <summary>
        /// `ChangeSettlementOwnerLogEntry._bySiege`, present in v1.4.8's metadata. Private, so read
        /// by reflection; null on a game version that renamed it, in which case every change of
        /// owner inside the realm is taken for a capture (<see cref="Change.IsCapture"/>). That
        /// would count a gift or a sale between two houses as a capture, which is rare enough to
        /// be the better failure than counting no capture at all.
        /// </summary>
        private static readonly FieldInfo BySiegeField =
            typeof(ChangeSettlementOwnerLogEntry).GetField("_bySiege", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool _warnedNoSiegeFlag;

        /// <summary>
        /// The war's changes of owner, from the day it began to the day it ended (or today). One
        /// pass over the game's log, grouped by settlement.
        ///
        /// **Two ledgers, each for what only it knows.** The game's log says which clan a fief
        /// passed from and to; our <c>FiefHistory</c> says whether it stayed in the realm while
        /// it did. Neither is copied into the other, and nothing new is kept.
        /// </summary>
        public static Ledger Read(ModState state, InternalWar war)
        {
            var ledger = new Ledger { SiegeFlagKnown = BySiegeField != null };
            var history = Campaign.Current?.LogEntryHistory;
            if (state == null || war == null || history == null) return ledger;
            ledger.Readable = true;

            if (BySiegeField == null && !_warnedNoSiegeFlag)
            {
                _warnedNoSiegeFlag = true;
                Log.Warn("InternalWar", "ChangeSettlementOwnerLogEntry._bySiege not found on this game version - every"
                                        + " change of owner between the two sides of an internal war counts as a capture.");
            }

            var from = war.StartedOn;
            var to = war.IsOngoing ? CampaignTime.Now : war.EndedOn;

            foreach (var entry in history.GetGameActionLogs<ChangeSettlementOwnerLogEntry>(
                         e => e != null && e.Settlement != null && e.Settlement.IsFortification
                              && e.GameTime >= from && e.GameTime <= to))
            {
                var change = new Change
                {
                    Settlement = entry.Settlement,
                    From = entry.PreviousClan,
                    To = entry.NewClan,
                    On = entry.GameTime,
                    BySiege = BySiegeField == null || (BySiegeField.GetValue(entry) as bool? ?? false)
                };
                change.IsCapture = change.BySiege && change.From != null && change.To != null && change.From != change.To
                                   && Diplomacy.FiefHistory.HeldAcross(state, change.Settlement, war.Kingdom, change.On);

                if (!ledger.BySettlement.TryGetValue(change.Settlement, out var list))
                    ledger.BySettlement[change.Settlement] = list = new List<Change>();
                list.Add(change);
                ledger.Changes++;
                if (change.IsCapture) ledger.Captures++;
            }

            // The log is kept in date order, but nothing here should rest on that.
            foreach (var list in ledger.BySettlement.Values)
                list.Sort((x, y) => x.On < y.On ? -1 : (x.On > y.On ? 1 : 0));
            return ledger;
        }

        /// <summary>
        /// Whether a fief's changes concern this war's realm at all - for the debug listing, which
        /// would otherwise print every change of owner in the world while the war ran.
        /// </summary>
        public static bool Touches(InternalWar war, Settlement settlement, List<Change> changes)
        {
            if (war == null || changes == null) return false;
            if (settlement?.OwnerClan?.Kingdom == war.Kingdom) return true;
            for (var i = 0; i < changes.Count; i++)
                if (changes[i].From?.Kingdom == war.Kingdom || changes[i].To?.Kingdom == war.Kingdom) return true;
            return false;
        }

        private static bool FoughtOver(List<Change> changes)
        {
            for (var i = 0; i < changes.Count; i++)
                if (changes[i].IsCapture) return true;
            return false;
        }

        private static bool IsCrownSide(InternalWar war, Clan clan)
            => clan != null && !clan.IsEliminated && clan.Kingdom == war.Kingdom && !war.IsRebel(clan);

        /// <summary>
        /// Fiefs this war has cost one side that the other side still holds: the internal war's
        /// count for <c>DiplomacyConstants.ExhaustionPerDayPerOccupiedFief</c>, read daily.
        ///
        /// Lost by the crown: held now by a rebel, taken by force during the war, and not a rebel
        /// house's own when it began. Lost by the rebels: held now by a crown house, taken by force
        /// during the war, and a rebel house's own when it began. "Rebel" is the side a house is on
        /// today; a house that changed sides (2.6c) is counted where it stands now.
        ///
        /// "Still holds", as the constant's own comment says, and not the number of captures ever
        /// made: a fief retaken stops wearing its old holder down.
        /// </summary>
        public static int OccupiedFrom(InternalWar war, Ledger ledger, bool rebelSide)
        {
            if (war == null || ledger == null) return 0;

            var count = 0;
            foreach (var pair in ledger.BySettlement)
            {
                var changes = pair.Value;
                if (changes.Count == 0 || !FoughtOver(changes)) continue;

                var holder = pair.Key.OwnerClan;
                var heldAtStart = changes[0].From;
                var startedRebel = heldAtStart != null && war.IsRebel(heldAtStart);

                if (rebelSide)
                {
                    if (startedRebel && IsCrownSide(war, holder)) count++;
                }
                else
                {
                    if (!startedRebel && war.IsRebel(holder) && holder?.Kingdom == war.Kingdom) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// What a crown victory gives back (the lead's decision of 2026-09-27): every fief a rebel
        /// house holds at the end that changed hands by force during the war goes to the house that
        /// held it when the war began, if that house is alive and still in the realm; otherwise to
        /// the ruling clan. One resolver: the war's end applies it and `diplomacy.internal_wars`
        /// prints it for a war still running, as "if the crown won today".
        ///
        /// Fiefs a rebel house held before the war and never lost stay with it: the rebels lost the
        /// war, not their lands (design 07 §3a Q1). A fief a rebel house holds at the end is given
        /// back even when it was a rebel house's own when the war began (the crown took it, and
        /// another rebel took it back): the rule restores the map as it was, and that house held it
        /// then. A house the crown bought back over the war (2.6c) keeps what it brought: it is not
        /// a rebel at the end, and its price already counted its fiefs.
        /// </summary>
        public static List<Restitution> Plan(InternalWar war, Ledger ledger)
        {
            var plan = new List<Restitution>();
            var kingdom = war?.Kingdom;
            var ruling = kingdom?.RulingClan;
            if (kingdom == null || ruling == null || ledger == null) return plan;

            foreach (var pair in ledger.BySettlement)
            {
                var settlement = pair.Key;
                var changes = pair.Value;
                var holder = settlement.OwnerClan;
                if (changes.Count == 0 || holder == null || !war.IsRebel(holder) || holder.Kingdom != kingdom) continue;
                if (!FoughtOver(changes)) continue;

                var heldAtStart = changes[0].From;
                var returnsTo = heldAtStart != null && !heldAtStart.IsEliminated && heldAtStart.Kingdom == kingdom
                                && heldAtStart.Leader != null
                    ? heldAtStart
                    : ruling;
                if (returnsTo == holder) continue;

                plan.Add(new Restitution
                {
                    Settlement = settlement, Holder = holder, HeldAtStart = heldAtStart, ReturnsTo = returnsTo
                });
            }
            return plan;
        }

        /// <summary>
        /// Gives back what <see cref="Plan"/> lists. Called once, when the crown wins, after the
        /// rising is gone - so every house is plainly of the realm again and no fief list of the
        /// rising has to follow.
        ///
        /// Through `ChangeOwnerOfSettlementAction.ApplyByDefault`, the call the peace table makes
        /// for a ceded fief. **Not `ApplyByKingDecision`**, which would read as the crown granting
        /// the fief: `GrievanceSources` answers that detail with a `FiefToRival` grievance for every
        /// land-hungry house at court, and this is a war's verdict, not a grant. Under the default
        /// detail the grievance system records nothing (it reads only king's decisions and sieges),
        /// so the rebel who loses the fief gets no grievance without anything being suppressed.
        /// Vanilla's own side effects of the action are not visible in the reference assemblies.
        /// </summary>
        public static int Restore(ModState state, InternalWar war)
        {
            if (state == null || war == null) return 0;

            var ledger = Read(state, war);
            var plan = Plan(war, ledger);
            var restored = 0;
            for (var i = 0; i < plan.Count; i++)
            {
                var r = plan[i];
                try
                {
                    var receiver = r.ReturnsTo.Leader;
                    if (receiver == null) continue;

                    ChangeOwnerOfSettlementAction.ApplyByDefault(receiver, r.Settlement);
                    restored++;
                    Log.Info("InternalWar", war.Kingdom.Name + ": restitution - " + r.Settlement.Name + " passes from "
                                            + r.Holder.Name + " to " + r.ReturnsTo.Name
                                            + (r.ReturnsTo == r.HeldAtStart
                                                ? ", who held it when the war began."
                                                : " (the crown): " + (r.HeldAtStart?.Name?.ToString() ?? "nobody known")
                                                  + " held it when the war began and is not a house of the realm now."));
                }
                catch (Exception ex)
                {
                    Log.Error("InternalWar", "Restoring " + r.Settlement?.Name + " to " + r.ReturnsTo?.Name + " failed.", ex);
                }
            }

            Log.Info("InternalWar", war.Kingdom.Name + ": " + restored + " fief(s) restored after the crown's win ("
                                    + ledger.Changes + " change(s) of owner in the game's log during the war, "
                                    + ledger.Captures + " of them captures across its line" + (ledger.Readable ? "" : "; the log could not be read") + ").");
            if (restored > 0)
                Log.Notify(restored + " fief(s) the rebels took in " + war.Kingdom.Name + " have been restored.", Colors.Cyan);
            return restored;
        }
    }
}
