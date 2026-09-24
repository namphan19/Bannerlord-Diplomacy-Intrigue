using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// War inside a kingdom: a claimant's party takes up arms against the crown without the
    /// kingdom splitting (design 07, option C; Phase 2.6).
    ///
    /// **How the map is made to see two sides.** The engine resolves hostility through
    /// `MapFaction`, and a clan inside a kingdom answers with its kingdom, so two clans of one
    /// realm are the same faction before any hostility question is asked (design 07 §1.1).
    /// While a war runs, every rebel clan's `MapFaction` - and every rebel hero's, which the
    /// engine computes separately - answers with the war's <see cref="InternalWar.Faction"/>, a
    /// kingdom that exists only on the map. `Clan.Kingdom` is untouched: the rebels keep their
    /// seats, their votes and their place in the court. The two getters are patched in
    /// `Patches/`, and both read <see cref="TryFaction"/> and nothing else.
    ///
    /// **Why the map faction is a kingdom and not the claimant's clan.** The first build used
    /// the clan, and the game crashed two seconds into the first war: vanilla casts a map
    /// faction to `Kingdom` without checking at ~25 places, `KingdomDecisionProposalBehavior`
    /// first (design 07 §3c). A real kingdom makes every one of those casts correct at once. Its
    /// clan, fief, hero and war-party lists are filled by <see cref="SyncFaction"/> with the same
    /// calls vanilla makes when a clan joins a kingdom - minus the one that moves the clan - so
    /// the AI finds the rebels' castles to besiege and its own to defend.
    ///
    /// **What follows, and is design rather than accident:**
    ///   - Every clan the war leaves alone still resolves to the kingdom, and the kingdom is
    ///     the faction at war with the rebels. So "the kingdom" *is* the crown's side, and an
    ///     uninvolved clan is a loyalist (design 07 §3a, fact A).
    ///   - The rising is at war with the crown and nobody else. It inherits none of the
    ///     kingdom's foreign wars: a rebellion busy with its own crown is not also fighting the
    ///     crown's enemies.
    ///   - The rising is **not a realm**. Every system of this mod skips it
    ///     (<see cref="Realms.IsRealm"/>): no treaties, no claims, no peace table, no court.
    ///
    /// **One resolver.** Whether a kingdom is ready for war, who stands on which side and when
    /// it ends are each decided once, here. The debug command prints what <see cref="Assess"/>
    /// returns rather than recomputing it.
    /// </summary>
    public static class InternalWars
    {
        // ----- The map-faction index ------------------------------------------
        //
        // Read by the two MapFaction patches, which between them sit under more than two
        // thousand call sites, some of them in per-frame party AI. So the read path is one
        // static bool when no internal war is running, and a dictionary lookup keyed by
        // reference when one is. Written only on the campaign thread and replaced whole
        // rather than mutated, so a reader on a parallel party tick sees either the old index
        // or the new one, never half of either.

        private sealed class Side
        {
            public Kingdom Kingdom;
            public Kingdom Faction;
        }

        private sealed class ByReference<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static readonly ByReference<Clan> ClanComparer = new ByReference<Clan>();
        private static readonly ByReference<Kingdom> KingdomComparer = new ByReference<Kingdom>();

        private static Dictionary<Clan, Side> _index = new Dictionary<Clan, Side>(ClanComparer);
        private static HashSet<Kingdom> _factions = new HashSet<Kingdom>(KingdomComparer);

        /// <summary>
        /// False whenever no internal war is running anywhere - the common case, and the only
        /// thing the patches read before returning. Must stay a plain field read.
        /// </summary>
        public static bool Any { get; private set; }

        /// <summary>
        /// The kingdom a rebel clan answers to on the map, or false for everyone else.
        ///
        /// Re-checks that the clan is still in the kingdom it rebelled in, so a clan that left
        /// the realm stops being redirected at once rather than at the next daily tick.
        /// `Clan.Kingdom` is a field read and cannot re-enter a MapFaction patch.
        /// </summary>
        public static bool TryFaction(Clan clan, out Kingdom faction)
        {
            faction = null;
            if (clan == null) return false;

            var index = _index;
            if (!index.TryGetValue(clan, out var side)) return false;
            if (clan.Kingdom != side.Kingdom) return false;

            faction = side.Faction;
            return true;
        }

        /// <summary>
        /// True for a kingdom that exists only as the map faction of an internal war, running
        /// or ended. <see cref="Realms.IsRealm"/> reads it so that no system of this mod ever
        /// treats a rising as a realm.
        /// </summary>
        public static bool IsFaction(Kingdom kingdom) => kingdom != null && _factions.Contains(kingdom);

        /// <summary>Drops the index. Before a campaign loads, where the clans belong to another world.</summary>
        public static void ClearIndex()
        {
            _index = new Dictionary<Clan, Side>(ClanComparer);
            _factions = new HashSet<Kingdom>(KingdomComparer);
            Any = false;
        }

        /// <summary>
        /// Rebuilds the index from the saved wars. Called on load, before the map is drawn, and
        /// after any change to who is fighting.
        ///
        /// Must be in place before vanilla's `ClanVariablesCampaignBehavior.OnSessionLaunched`,
        /// which destroys any kingdom whose `Leader.MapFaction` is not itself - the rising's
        /// leader passes that check only through the hero patch reading this index. Rebuilt in
        /// `CoreBehavior.SyncData` for that reason.
        /// </summary>
        public static void RebuildIndex(ModState state)
        {
            var fresh = new Dictionary<Clan, Side>(ClanComparer);
            var factions = new HashSet<Kingdom>(KingdomComparer);

            if (state != null)
            {
                for (var i = 0; i < state.InternalWars.Count; i++)
                {
                    var war = state.InternalWars[i];
                    if (war.Faction != null) factions.Add(war.Faction);

                    if (!Settings.Current.EnableIntrigue) continue;
                    if (!war.IsOngoing || war.Kingdom == null || war.Faction == null) continue;

                    var side = new Side { Kingdom = war.Kingdom, Faction = war.Faction };
                    for (var r = 0; r < war.Rebels.Count; r++)
                    {
                        var clan = war.Rebels[r].Clan;
                        if (clan != null) fresh[clan] = side;
                    }
                }
            }

            _index = fresh;
            _factions = factions;
            Any = fresh.Count > 0;
        }

        // ----- The rising's lists ---------------------------------------------
        //
        // A kingdom's clan, fief, hero and war-party lists are caches the engine fills when a
        // clan joins it (Clan.EnterKingdomInternal: AddClanInternal, OnHeroAdded,
        // OnFortificationAdded, OnWarPartyAdded) and never saves ([CachedData], verified by
        // Cecil). The rising gets the same calls without the one that moves the clan, and is
        // re-synced daily, on every change of a fief's owner, and after every load.

        private static readonly MethodInfo AddClanInternal =
            typeof(Kingdom).GetMethod("AddClanInternal", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo RemoveClanInternal =
            typeof(Kingdom).GetMethod("RemoveClanInternal", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Brings a rising's lists in line with its rebels: every rebel clan, and their heroes,
        /// fiefs and war parties; nothing else. With <paramref name="empty"/> it clears them,
        /// which must happen before the rising is destroyed.
        /// </summary>
        public static void SyncFaction(InternalWar war, bool empty = false)
        {
            var faction = war?.Faction;
            if (faction == null || AddClanInternal == null || RemoveClanInternal == null) return;

            var clans = new List<Clan>();
            if (!empty)
            {
                for (var i = 0; i < war.Rebels.Count; i++)
                {
                    var clan = war.Rebels[i].Clan;
                    if (clan != null && !clan.IsEliminated && clan.Kingdom == war.Kingdom) clans.Add(clan);
                }
            }

            var heroes = new List<Hero>();
            var fiefs = new List<Town>();
            var parties = new List<WarPartyComponent>();
            for (var i = 0; i < clans.Count; i++)
            {
                heroes.AddRange(clans[i].Heroes);
                fiefs.AddRange(clans[i].Fiefs);
                parties.AddRange(clans[i].WarPartyComponents);
            }

            // Removals first, from copies, since each call edits the list being read.
            foreach (var p in new List<WarPartyComponent>(faction.WarPartyComponents))
                if (!parties.Contains(p)) faction.OnWarPartyRemoved(p);
            foreach (var f in new List<Town>(faction.Fiefs))
                if (!fiefs.Contains(f)) faction.OnFortificationRemoved(f);
            foreach (var h in new List<Hero>(faction.Heroes))
                if (!heroes.Contains(h)) faction.OnHeroRemoved(h);
            foreach (var c in new List<Clan>(faction.Clans))
                if (!clans.Contains(c)) RemoveClanInternal.Invoke(faction, new object[] { c });

            for (var i = 0; i < clans.Count; i++)
                if (!faction.Clans.Contains(clans[i])) AddClanInternal.Invoke(faction, new object[] { clans[i] });
            for (var i = 0; i < heroes.Count; i++)
                if (!faction.Heroes.Contains(heroes[i])) faction.OnHeroAdded(heroes[i]);
            for (var i = 0; i < fiefs.Count; i++)
                if (!faction.Fiefs.Contains(fiefs[i])) faction.OnFortificationAdded(fiefs[i]);
            for (var i = 0; i < parties.Count; i++)
                if (!faction.WarPartyComponents.Contains(parties[i])) faction.OnWarPartyAdded(parties[i]);
        }

        /// <summary>Re-syncs every running rising. Session start, where the engine rebuilt nothing for them.</summary>
        public static void SyncAll(ModState state)
        {
            if (state == null) return;
            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                if (!war.IsOngoing) continue;
                try
                {
                    SyncFaction(war);
                }
                catch (Exception ex)
                {
                    Log.Error("InternalWar", "Rebuilding the lists of " + war.Faction?.Name + " failed.", ex);
                }
            }
        }

        /// <summary>
        /// Creates the kingdom a rising fights under. Named for its claimant, flying the banner
        /// clan's colours, and ruled by it - the engine expects every kingdom to have a ruling
        /// clan, and `Kingdom.Leader` is read in too many places to leave it null.
        /// </summary>
        private static Kingdom CreateFaction(Kingdom kingdom, Hero claimant, Clan banner)
        {
            var home = banner.HomeSettlement;
            if (home == null && banner.Fiefs.Count > 0) home = banner.Fiefs[0].Settlement;
            if (home == null) home = kingdom.InitialHomeSettlement ?? kingdom.FactionMidSettlement;

            var name = new TextObject(claimant.Name + "'s Rising");
            var faction = Kingdom.CreateKingdom("di_rising");
            faction.InitializeKingdom(
                name, name, banner.Culture, banner.Banner, banner.Color, banner.Color2, home,
                new TextObject("The houses of " + kingdom.Name + " who took up arms to put "
                               + claimant.Name + " on its throne."),
                new TextObject("Rising"),
                new TextObject("Claimant"));
            faction.RulingClan = banner;
            return faction;
        }

        // ----- Reading --------------------------------------------------------

        public static InternalWar OngoingIn(ModState state, Kingdom kingdom)
        {
            if (state == null || kingdom == null) return null;
            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                if (war.IsOngoing && war.Kingdom == kingdom) return war;
            }
            return null;
        }

        /// <summary>The ongoing war this clan is a rebel in, if any.</summary>
        public static InternalWar RebellionOf(ModState state, Clan clan)
        {
            if (state == null || clan == null) return null;
            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                if (war.IsOngoing && war.IsRebel(clan)) return war;
            }
            return null;
        }

        /// <summary>
        /// True when these two factions are the two sides of a running internal war. The
        /// diplomacy model reads this to keep vanilla's peace offers away from it: an internal
        /// war ends by the rules in <see cref="DailyTick"/>, not by a barter.
        /// </summary>
        public static bool IsInternalWarPair(IFaction a, IFaction b)
        {
            if (!Any || a == null || b == null) return false;

            var state = Behaviors.CoreBehavior.State;
            if (state == null) return false;

            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                if (!war.IsOngoing) continue;
                if ((a == war.Faction && b == war.Kingdom) || (a == war.Kingdom && b == war.Faction))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Where a kingdom stands against design 02 §6's three conditions, and who would fight
        /// on which side if the war began today.
        /// </summary>
        public sealed class Assessment
        {
            public Kingdom Kingdom;
            public Pretender Claim;
            public CourtBloc Bloc;
            public float BlocShare;
            public float Legitimacy;
            public int DisloyalClans;
            public readonly List<Clan> Rebels = new List<Clan>();

            /// <summary>
            /// The player's clan, when the rules would put it on the rebel side and it is not
            /// the claimant's own. The resolver makes no exception for the player; the caller
            /// asks instead of deciding (design 02 §9.2, design 07 §3a Q2).
            /// </summary>
            public bool PlayerWouldRebel;

            public bool Ready;
            public string Reason;
        }

        public static Assessment Assess(ModState state, Kingdom kingdom)
        {
            var a = new Assessment { Kingdom = kingdom };
            if (state == null || !kingdom.IsRealm() || kingdom.RulingClan == null)
            {
                a.Reason = "not a realm";
                return a;
            }

            a.Legitimacy = LegitimacyRegistry.Of(state, kingdom);

            var ruling = kingdom.RulingClan;
            var total = 0f;
            foreach (var clan in Court.MembersOf(kingdom))
            {
                if (clan.Influence > 0f) total += clan.Influence;
                if (clan != ruling && LoyaltyModel.Of(state, clan) < IntrigueConstants.LoyaltyDisaffected)
                    a.DisloyalClans++;
            }

            var blocs = BlocModel.BlocsOf(state, kingdom);
            for (var i = 0; i < blocs.Count; i++)
                if (blocs[i].Agenda == CourtAgenda.Pretenders) a.Bloc = blocs[i];

            // Measured on effective power, not nominal: a member at or above the reliable band
            // follows the ruler regardless of its bloc (design 02 §3), and a member who will
            // not rebel cannot count toward a rebellion.
            a.BlocShare = a.Bloc == null || total <= 0f ? 0f : a.Bloc.EffectivePower / total;
            a.Claim = ClaimBehind(state, kingdom, a.Bloc);

            if (a.Claim != null)
            {
                var claimant = a.Claim.Claimant;
                var ruler = kingdom.Leader;

                foreach (var clan in Court.MembersOf(kingdom))
                {
                    if (clan == ruling || clan.Leader == null) continue;

                    bool rebels;
                    if (clan == claimant.Clan)
                        rebels = true;
                    else if (a.Bloc != null && a.Bloc.Members.Contains(clan))
                        rebels = LoyaltyModel.Of(state, clan) < IntrigueConstants.LoyaltyReliable;
                    else
                        // Design 02 §6: a clan below the defection line that is not in the bloc
                        // picks a side by relation.
                        rebels = LoyaltyModel.Of(state, clan) < IntrigueConstants.LoyaltyDisaffected
                                 && ruler != null
                                 && clan.Leader.GetRelation(claimant) > clan.Leader.GetRelation(ruler);

                    if (!rebels) continue;
                    if (clan == Clan.PlayerClan && clan != claimant.Clan)
                    {
                        a.PlayerWouldRebel = true;
                        continue;
                    }
                    a.Rebels.Add(clan);
                }
            }

            if (OngoingIn(state, kingdom) != null) { a.Reason = "already at war with itself"; return a; }
            if (InCooldown(state, kingdom, out var daysLeft))
            {
                a.Reason = "an internal war ended recently (" + daysLeft.ToString("0") + " days to go)";
                return a;
            }
            if (a.Claim == null) { a.Reason = "no standing claimant in a pretender bloc"; return a; }
            if (a.BlocShare < IntrigueConstants.InternalWarBlocShare)
            {
                a.Reason = "the pretender bloc holds " + (a.BlocShare * 100f).ToString("0")
                           + "% of the court's influence, needs " + (IntrigueConstants.InternalWarBlocShare * 100f).ToString("0") + "%";
                return a;
            }
            if (a.Legitimacy >= IntrigueConstants.InternalWarLegitimacy)
            {
                a.Reason = "crown legitimacy " + a.Legitimacy.ToString("0.0") + " is not below "
                           + IntrigueConstants.InternalWarLegitimacy.ToString("0");
                return a;
            }
            if (a.DisloyalClans < IntrigueConstants.InternalWarDisloyalClans)
            {
                a.Reason = a.DisloyalClans + " clan(s) below loyalty "
                           + IntrigueConstants.LoyaltyDisaffected.ToString("0") + ", needs "
                           + IntrigueConstants.InternalWarDisloyalClans;
                return a;
            }

            a.Ready = true;
            a.Reason = "all three conditions hold";
            return a;
        }

        /// <summary>
        /// The claim a rising would be fought for: a standing pretender whose own clan sits in
        /// the pretender bloc. With several, the one whose clan carries the most weight - that
        /// is who the others would rally behind.
        /// </summary>
        private static Pretender ClaimBehind(ModState state, Kingdom kingdom, CourtBloc bloc)
        {
            if (bloc == null) return null;

            Pretender best = null;
            var ruling = kingdom.RulingClan;
            var claims = SuccessionModel.PretendersTo(state, kingdom);
            for (var i = 0; i < claims.Count; i++)
            {
                var clan = claims[i].Claimant?.Clan;
                if (clan == null || clan == ruling || !Court.IsMember(clan)) continue;
                if (!bloc.Members.Contains(clan)) continue;
                if (best == null || clan.Influence > best.Claimant.Clan.Influence) best = claims[i];
            }
            return best;
        }

        private static bool InCooldown(ModState state, Kingdom kingdom, out float daysLeft)
        {
            daysLeft = 0f;
            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                if (war.IsOngoing || war.Kingdom != kingdom) continue;

                var since = (float)war.EndedOn.ElapsedDaysUntilNow;
                var left = IntrigueConstants.InternalWarCooldownDays - since;
                if (left > daysLeft) daysLeft = left;
            }
            return daysLeft > 0f;
        }

        // ----- Upkeep ---------------------------------------------------------

        /// <summary>Kingdoms whose claimant is the player and declined, and until when.</summary>
        private static readonly Dictionary<Kingdom, CampaignTime> PlayerDeclinedUntil =
            new Dictionary<Kingdom, CampaignTime>();

        private static bool _askingPlayer;

        public static void Reset()
        {
            PlayerDeclinedUntil.Clear();
            _askingPlayer = false;
        }

        /// <summary>
        /// Once a day: run every internal war one day forward and end the ones that are over,
        /// then look for a kingdom that has reached the point of one.
        /// </summary>
        public static void DailyTick(ModState state)
        {
            if (state == null) return;

            var ongoing = new List<InternalWar>();
            for (var i = 0; i < state.InternalWars.Count; i++)
                if (state.InternalWars[i].IsOngoing) ongoing.Add(state.InternalWars[i]);

            for (var i = 0; i < ongoing.Count; i++)
            {
                try
                {
                    Advance(state, ongoing[i]);
                }
                catch (Exception ex)
                {
                    Log.Error("InternalWar", "Advancing " + ongoing[i] + " failed.", ex);
                }
            }

            // A copy: starting a war creates the rising, which the engine appends to Kingdom.All
            // mid-loop. Iterating the live list threw "Collection was modified" on the first
            // war the live test started (2026-09-23), after the war itself had begun.
            foreach (var kingdom in new List<Kingdom>(Kingdom.All))
            {
                if (!kingdom.IsRealm()) continue;

                var assessment = Assess(state, kingdom);
                if (!assessment.Ready) continue;

                try
                {
                    Rise(state, assessment);
                }
                catch (Exception ex)
                {
                    Log.Error("InternalWar", "Starting an internal war in " + kingdom.Name + " failed.", ex);
                }
            }
        }

        /// <summary>
        /// A kingdom meets the conditions. An AI claimant raises the banner - the trigger rule
        /// *is* its decision. A player claimant is asked, and may decline.
        /// </summary>
        private static void Rise(ModState state, Assessment a)
        {
            var claimant = a.Claim.Claimant;
            if (claimant != Hero.MainHero)
            {
                Start(state, a, "the court's divisions came to arms");
                return;
            }

            if (_askingPlayer) return;
            if (PlayerDeclinedUntil.TryGetValue(a.Kingdom, out var until) && !until.IsPast) return;

            _askingPlayer = true;
            InformationManager.ShowInquiry(new InquiryData(
                "Raise your banner?",
                "The crown of " + a.Kingdom.Name + " stands at legitimacy " + a.Legitimacy.ToString("0")
                + ", and the clans who back your claim hold " + (a.BlocShare * 100f).ToString("0")
                + "% of the court's influence. " + a.Rebels.Count + " house(s) would take the field"
                + " with you against " + a.Kingdom.Leader?.Name + ".\n\n"
                + "If you rise, your party and theirs become enemies of the crown until the war is"
                + " decided. You stay a member of the realm throughout.",
                true, true, "Raise the banner", "Not yet",
                () =>
                {
                    // Runs from the UI, outside any campaign handler's try.
                    try
                    {
                        _askingPlayer = false;
                        var fresh = Assess(state, a.Kingdom);
                        if (!fresh.Ready || fresh.Claim?.Claimant != Hero.MainHero)
                        {
                            Log.Notify("The moment has passed - " + fresh.Reason + ".", Colors.Red);
                            return;
                        }
                        Start(state, fresh, "you raised your banner");
                    }
                    catch (Exception ex)
                    {
                        Log.Error("InternalWar", "Raising the player's banner failed.", ex);
                    }
                },
                () =>
                {
                    _askingPlayer = false;
                    PlayerDeclinedUntil[a.Kingdom] =
                        CampaignTime.DaysFromNow(IntrigueConstants.InternalWarPlayerAskAgainDays);
                }));
        }

        /// <summary>
        /// Begins an internal war. Public for the test command, which calls it on an assessment
        /// that has not met the thresholds; everything else goes through <see cref="DailyTick"/>.
        /// </summary>
        public static InternalWar Start(ModState state, Assessment a, string why)
        {
            if (state == null || a?.Claim?.Claimant?.Clan == null) return null;
            if (OngoingIn(state, a.Kingdom) != null) return null;

            if (AddClanInternal == null || RemoveClanInternal == null)
            {
                // The engine's own clan-list methods are not where v1.4.8 has them. Without them
                // the rising's lists stay empty and the AI would never besiege a rebel castle;
                // better no internal war than a hollow one.
                Log.Warn("InternalWar", "Kingdom.AddClanInternal/RemoveClanInternal not found - internal wars are disabled on this game version.");
                return null;
            }

            var kingdom = a.Kingdom;
            var claimant = a.Claim.Claimant;
            var banner = claimant.Clan;

            var rebels = new List<Clan>(a.Rebels);
            if (!rebels.Contains(banner)) rebels.Add(banner);

            var crownShare = CrownShare(kingdom, rebels);
            var faction = CreateFaction(kingdom, claimant, banner);
            var war = new InternalWar(kingdom, claimant, banner, faction, rebels, crownShare);
            state.InternalWars.Add(war);

            // The index and the rising's lists first, so that everything the war declaration
            // triggers - vanilla's listeners, party visuals, the at-war lists - already sees two
            // sides, and the rising already holds its castles.
            RebuildIndex(state);
            SyncFaction(war);
            SeparateArmies(war);

            DeclareWarAction.ApplyByDefault(faction, kingdom);

            if (!FactionManager.IsAtWarAgainstFaction(faction, kingdom))
            {
                // Something refused the declaration. A rising the stance table does not know
                // about would redirect the map with no hostility behind it, so undo it.
                Log.Warn("InternalWar", kingdom.Name + ": the declaration did not take - "
                                        + faction.Name + " is not at war with the crown. Abandoned.");
                End(state, war, InternalWarOutcome.Dissolved, "the declaration did not take");
                return null;
            }

            BlocModel.Invalidate();

            var names = new System.Text.StringBuilder();
            for (var i = 0; i < rebels.Count; i++)
            {
                if (names.Length > 0) names.Append(", ");
                names.Append(rebels[i].Name);
            }

            Log.Info("InternalWar", kingdom.Name + ": " + claimant.Name + " takes up arms against "
                                    + kingdom.Leader?.Name + " (" + why + "). Legitimacy "
                                    + a.Legitimacy.ToString("0.0") + ", bloc " + (a.BlocShare * 100f).ToString("0")
                                    + "%, " + a.DisloyalClans + " clans below 25. Rebels (" + rebels.Count
                                    + "): " + names + ". Crown side held " + (crownShare * 100f).ToString("0")
                                    + "% of the court. The rising (" + faction.StringId + ") holds "
                                    + faction.Clans.Count + " clans, " + faction.Fiefs.Count + " fiefs, "
                                    + faction.WarPartyComponents.Count + " war parties.");

            Log.Notify(claimant.Name + " has raised a banner against " + kingdom.Leader?.Name
                       + " - civil war in " + kingdom.Name + ".", Colors.Red);

            if (a.PlayerWouldRebel) AskPlayerToChooseSide(state, war);
            return war;
        }

        /// <summary>
        /// The player's clan meets the rule that would put an AI clan on the rebel side. An AI
        /// clan's side is its relation roll; the player is asked instead (design 07 §3a Q2).
        /// Until they answer they are on the crown's side, which is where any sworn clan starts.
        /// </summary>
        private static void AskPlayerToChooseSide(ModState state, InternalWar war)
        {
            if (_askingPlayer) return;
            _askingPlayer = true;

            InformationManager.ShowInquiry(new InquiryData(
                "Civil war in " + war.Kingdom.Name,
                war.Claimant.Name + " has taken up arms against " + war.Kingdom.Leader?.Name
                + ". You have little love for the crown, and the rebels would welcome you.\n\n"
                + "Join them, and your clan fights the crown until the war is decided. Stay, and"
                + " you fight for it.",
                true, true, "Join the rebellion", "Stay loyal",
                () =>
                {
                    try
                    {
                        _askingPlayer = false;
                        if (!war.IsOngoing || Clan.PlayerClan?.Kingdom != war.Kingdom) return;
                        JoinRising(state, war, Clan.PlayerClan);
                        Log.Info("InternalWar", war.Kingdom.Name + ": the player's clan joined the rebellion.");
                    }
                    catch (Exception ex)
                    {
                        Log.Error("InternalWar", "Joining the rebellion failed.", ex);
                    }
                },
                () => { _askingPlayer = false; }));
        }

        /// <summary>
        /// Splits the standing armies along the line between the two sides. An army belongs to
        /// a kingdom (`Army.Kingdom`), so one led from the other side is disbanded rather than
        /// handed across, and a party of the other side in an army is sent out of it. From then
        /// on each side raises its own - the AI raises an army under its party's map faction,
        /// which for a rebel is the rising - and `ModArmyManagementModel` keeps either side's
        /// call from reaching the other.
        ///
        /// Both kingdoms are walked. When the war starts the rising has no armies and only the
        /// realm's matter; once a house can change sides mid-war (2.6c), a house bought back by
        /// the crown can be standing in one of the rising's.
        /// </summary>
        private static void SeparateArmies(InternalWar war)
        {
            SplitArmies(war, war.Kingdom, rebelSide: false);
            SplitArmies(war, war.Faction, rebelSide: true);
        }

        private static void SplitArmies(InternalWar war, Kingdom owner, bool rebelSide)
        {
            if (owner?.Armies == null) return;

            var armies = new List<Army>(owner.Armies);
            for (var i = 0; i < armies.Count; i++)
            {
                var army = armies[i];
                try
                {
                    var leader = army?.LeaderParty?.ActualClan;
                    if (leader == null) continue;

                    if (war.IsRebel(leader) != rebelSide)
                    {
                        DisbandArmyAction.ApplyByUnknownReason(army);
                        continue;
                    }

                    var parties = new List<MobileParty>(army.Parties);
                    for (var p = 0; p < parties.Count; p++)
                    {
                        var party = parties[p];
                        if (party?.ActualClan == null || party == army.LeaderParty) continue;
                        if (war.IsRebel(party.ActualClan) != rebelSide) party.Army = null;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("InternalWar", "Separating an army of " + owner.Name + " failed.", ex);
                }
            }
        }

        // ----- The two acts of 2.6c (design 07 §6) ------------------------------

        /// <summary>
        /// Who speaks for a side: the claimant for the rising, the ruler for the crown. The only
        /// two heroes who can concede, and the two who pay for a house that comes over.
        /// </summary>
        public static Hero LeaderOf(InternalWar war, bool risingSide)
            => war == null ? null : risingSide ? war.Claimant : war.Kingdom?.Leader;

        /// <summary>
        /// A house takes the rising's side as the war begins - the side-choice prompt's answer.
        /// Not a change of sides: nothing is paid and nothing is recorded in
        /// <see cref="InternalWar.SideChanges"/>, so the house can still be bought later.
        /// </summary>
        internal static void JoinRising(ModState state, InternalWar war, Clan clan)
        {
            if (!war.IsRebel(clan)) war.Rebels.Add(new InternalWarMember(clan));
            RebuildIndex(state);
            SyncFaction(war);
            SeparateArmies(war);
        }

        /// <summary>
        /// A house goes over to the other side, mid-war. The record, then the same three steps
        /// the side-choice prompt takes when a house joins at the start: the map-faction index,
        /// the rising's lists, the armies. The price and who may change are
        /// <see cref="SideChange"/>'s; this only moves the house.
        /// </summary>
        internal static void ChangeSide(ModState state, InternalWar war, Clan clan, bool toRising)
        {
            war.MoveSide(clan, toRising);
            RebuildIndex(state);
            SyncFaction(war);
            SeparateArmies(war);
            BlocModel.Invalidate();
        }

        /// <summary>
        /// The side's leader gives up. Ends the war exactly as that side reaching exhaustion 100
        /// would: the crown conceding is a rebel win, the rising conceding a crown win - no new
        /// outcome (design 07 §6).
        /// </summary>
        public static bool Concede(ModState state, InternalWar war, bool risingSide, out string failed)
        {
            failed = null;
            if (war == null || !war.IsOngoing) { failed = "The war is already over."; return false; }

            var leader = LeaderOf(war, risingSide);
            if (leader == null || !leader.IsAlive) { failed = "That side has nobody to concede for it."; return false; }

            End(state, war, risingSide ? InternalWarOutcome.CrownWon : InternalWarOutcome.RebelsWon,
                leader.Name + " conceded");
            return true;
        }

        /// <summary>
        /// The AI's concession rule: its own side at <see cref="IntrigueConstants.InternalWarConcedeExhaustion"/>
        /// or more while the other side is still under <see cref="IntrigueConstants.InternalWarConcedeOtherBelow"/>.
        /// A player leader is never conceded for: they are shown the same numbers and decide,
        /// as a player claimant decides whether to rise (design 02 §9.2).
        /// </summary>
        private static bool AiWouldConcede(InternalWar war, bool risingSide)
        {
            var leader = LeaderOf(war, risingSide);
            if (leader == null || leader == Hero.MainHero) return false;

            var own = risingSide ? war.RebelExhaustion : war.CrownExhaustion;
            var other = risingSide ? war.CrownExhaustion : war.RebelExhaustion;
            return own >= IntrigueConstants.InternalWarConcedeExhaustion
                   && other < IntrigueConstants.InternalWarConcedeOtherBelow;
        }

        private static float CrownShare(Kingdom kingdom, List<Clan> rebels)
        {
            var total = 0f;
            var crown = 0f;
            foreach (var clan in Court.MembersOf(kingdom))
            {
                if (clan.Influence <= 0f) continue;
                total += clan.Influence;
                if (!rebels.Contains(clan)) crown += clan.Influence;
            }
            return total <= 0f ? 0f : crown / total;
        }

        /// <summary>
        /// One day of an internal war: membership re-checked, the rising's lists re-synced,
        /// time wears both sides down, captivity counted, and the end conditions in design 07
        /// §3a Q1 applied in a fixed order - a decisive result before a stalemate.
        /// </summary>
        private static void Advance(ModState state, InternalWar war)
        {
            var kingdom = war.Kingdom;
            var banner = war.Banner;
            var faction = war.Faction;

            if (kingdom == null || !kingdom.IsRealm())
            {
                End(state, war, InternalWarOutcome.Dissolved, "the kingdom fell");
                return;
            }
            if (faction == null || faction.IsEliminated)
            {
                End(state, war, InternalWarOutcome.Dissolved, "the rising's faction is gone");
                return;
            }
            if (banner == null || banner.IsEliminated || banner.Kingdom != kingdom)
            {
                End(state, war, InternalWarOutcome.Dissolved, "the rebel banner left the realm");
                return;
            }

            // A rebel clan that left the realm is simply gone from the war.
            var left = war.Rebels.RemoveAll(r => r.Clan == null || r.Clan.IsEliminated || r.Clan.Kingdom != kingdom);
            if (left > 0)
            {
                RebuildIndex(state);
                Log.Info("InternalWar", kingdom.Name + ": " + left + " rebel clan(s) left the realm mid-war.");
            }
            SyncFaction(war);

            if (!FactionManager.IsAtWarAgainstFaction(faction, kingdom))
            {
                End(state, war, InternalWarOutcome.Dissolved, "a peace was made outside the internal war");
                return;
            }

            // The throne already passed to the rebels - a ruler died and vanilla's election
            // chose one of them. That is their victory, reached by another road.
            if (kingdom.RulingClan != null && war.IsRebel(kingdom.RulingClan))
            {
                End(state, war, InternalWarOutcome.RebelsWon, kingdom.RulingClan.Name + " came to the throne");
                return;
            }

            if (war.Claimant == null || !war.Claimant.IsAlive)
            {
                End(state, war, InternalWarOutcome.CrownWon, "the claimant is dead");
                return;
            }

            var rate = Settings.Current.WarExhaustionRate;
            war.AddExhaustion(true, DiplomacyConstants.ExhaustionPerDayAtWar * rate);
            war.AddExhaustion(false, DiplomacyConstants.ExhaustionPerDayAtWar * rate);

            var claimantHeld = HeldBy(war.Claimant, kingdom);
            var rulerHeld = HeldBy(kingdom.Leader, faction);
            war.SetCaptiveDays(claimantHeld ? war.ClaimantCaptiveDays + 1 : 0,
                rulerHeld ? war.RulerCaptiveDays + 1 : 0);

            if (war.ClaimantCaptiveDays >= IntrigueConstants.InternalWarCaptiveDays)
                End(state, war, InternalWarOutcome.CrownWon,
                    war.Claimant.Name + " held by the crown for " + war.ClaimantCaptiveDays + " days");
            else if (war.RulerCaptiveDays >= IntrigueConstants.InternalWarCaptiveDays)
                End(state, war, InternalWarOutcome.RebelsWon,
                    kingdom.Leader?.Name + " held by the rebels for " + war.RulerCaptiveDays + " days");
            else if (war.RebelExhaustion >= IntrigueConstants.InternalWarCollapseExhaustion)
                End(state, war, InternalWarOutcome.CrownWon, "the rebellion is exhausted");
            else if (war.CrownExhaustion >= IntrigueConstants.InternalWarCollapseExhaustion)
                End(state, war, InternalWarOutcome.RebelsWon, "the crown's side is exhausted");
            // A concession cannot overlap the stalemate below: it needs the other side under
            // the line the stalemate needs both sides past.
            else if (AiWouldConcede(war, risingSide: true))
                End(state, war, InternalWarOutcome.CrownWon, war.Claimant.Name + " conceded");
            else if (AiWouldConcede(war, risingSide: false))
                End(state, war, InternalWarOutcome.RebelsWon, kingdom.Leader?.Name + " conceded");
            else if (war.RebelExhaustion >= IntrigueConstants.InternalWarStalemateExhaustion
                     && war.CrownExhaustion >= IntrigueConstants.InternalWarStalemateExhaustion)
                End(state, war, InternalWarOutcome.Stalemate, "both sides are worn out");
        }

        /// <summary>A hero in the hands of the given faction - a party of it, or one of its prisons.</summary>
        private static bool HeldBy(Hero hero, IFaction captor)
        {
            if (hero == null || !hero.IsPrisoner || captor == null) return false;
            var holder = hero.PartyBelongedToAsPrisoner?.MapFaction;
            return holder == captor;
        }

        /// <summary>
        /// A battle between the two sides. Casualties wear each side down by the same measure
        /// Phase 1 uses between kingdoms - losses against the side's own strength - so a civil
        /// war and a foreign war tire a realm at comparable rates.
        /// </summary>
        public static void OnMapEventEnded(ModState state, MapEvent mapEvent)
        {
            if (!Any || state == null || mapEvent == null) return;

            var attacker = mapEvent.AttackerSide?.MapFaction;
            var defender = mapEvent.DefenderSide?.MapFaction;
            if (attacker == null || defender == null || attacker == defender) return;

            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                if (!war.IsOngoing) continue;

                bool rebelsAttacked;
                if (attacker == war.Faction && defender == war.Kingdom) rebelsAttacked = true;
                else if (attacker == war.Kingdom && defender == war.Faction) rebelsAttacked = false;
                else continue;

                var rebelLosses = rebelsAttacked ? mapEvent.AttackerSide.TroopCasualties : mapEvent.DefenderSide.TroopCasualties;
                var crownLosses = rebelsAttacked ? mapEvent.DefenderSide.TroopCasualties : mapEvent.AttackerSide.TroopCasualties;

                SideStrength(war, out var rebelStrength, out var crownStrength);
                war.AddExhaustion(true, CasualtyExhaustion(rebelLosses, rebelStrength));
                war.AddExhaustion(false, CasualtyExhaustion(crownLosses, crownStrength));

                Log.Info("InternalWar", war.Kingdom.Name + ": " + mapEvent.EventType + ", rebels lost " + rebelLosses
                                        + ", crown lost " + crownLosses + " -> exhaustion rebels "
                                        + war.RebelExhaustion.ToString("0.0") + " / crown "
                                        + war.CrownExhaustion.ToString("0.0"));
                return;
            }
        }

        private static float CasualtyExhaustion(int losses, float strength)
        {
            if (losses <= 0) return 0f;
            var divisor = Math.Max(DiplomacyConstants.ExhaustionCasualtyMinDivisor,
                strength / DiplomacyConstants.ExhaustionCasualtyStrengthDivisor);
            return losses / divisor * Settings.Current.WarExhaustionRate;
        }

        private static void SideStrength(InternalWar war, out float rebels, out float crown)
        {
            rebels = 0f;
            crown = 0f;
            var clans = war.Kingdom.Clans;
            for (var i = 0; i < clans.Count; i++)
            {
                var clan = clans[i];
                if (clan == null || clan.IsEliminated) continue;
                if (war.IsRebel(clan)) rebels += clan.CurrentTotalStrength;
                else crown += clan.CurrentTotalStrength;
            }
        }

        /// <summary>
        /// A fief changed hands. If a rebel clan gained or lost one, the rising's fief list must
        /// follow at once: the AI picks siege targets from it, and a castle the rebels just took
        /// that is missing from it is a castle nobody will try to retake.
        /// </summary>
        public static void OnSettlementOwnerChanged(ModState state, Settlement settlement, Hero newOwner, Hero oldOwner)
        {
            if (!Any || state == null || settlement?.Town == null) return;

            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                if (!war.IsOngoing) continue;
                if (!war.IsRebel(newOwner?.Clan) && !war.IsRebel(oldOwner?.Clan)) continue;

                SyncFaction(war);
                Log.Info("InternalWar", war.Kingdom.Name + ": " + settlement.Name + " passed from "
                                        + oldOwner?.Clan?.Name + " to " + newOwner?.Clan?.Name
                                        + " - the rising now holds " + war.Faction.Fiefs.Count + " fiefs.");
            }
        }

        /// <summary>
        /// A war party was raised or destroyed, or a hero died. Vanilla tells only the clan's
        /// own kingdom (`Clan.Kingdom`), so the rising's lists would keep a destroyed party, or
        /// a dead lord among the living, until the next daily sync - and AI that walks those
        /// lists would be handed an object the engine has already finalised. Re-synced at once.
        ///
        /// A dead hero is removed first and re-added by the sync, which files them by their
        /// new state: vanilla moves a lord between its living and dead lists through
        /// `OnHeroChangedState`, which it never calls on the rising.
        /// </summary>
        public static void OnRosterChanged(ModState state, Clan clan, Hero diedHero = null)
        {
            if (!Any || state == null) return;

            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                if (!war.IsOngoing || war.Faction == null) continue;
                if (clan != null && !war.IsRebel(clan)) continue;

                if (diedHero != null && war.Faction.Heroes.Contains(diedHero))
                    war.Faction.OnHeroRemoved(diedHero);
                SyncFaction(war);
            }
        }

        /// <summary>
        /// A clan changed kingdom. If it was a rebel, its map faction must stop being redirected
        /// now - the patch already re-checks the kingdom on every read, so this only has to keep
        /// the saved record, the index and the rising's lists honest.
        /// </summary>
        public static void OnClanChangedKingdom(ModState state, Clan clan, Kingdom oldKingdom)
        {
            if (!Any || state == null || clan == null) return;

            var war = OngoingIn(state, oldKingdom);
            if (war == null || !war.IsRebel(clan)) return;

            if (clan == war.Banner)
            {
                End(state, war, InternalWarOutcome.Dissolved, clan.Name + " left the realm");
                return;
            }

            war.Rebels.RemoveAll(r => r.Clan == clan);
            RebuildIndex(state);
            SyncFaction(war);
            Log.Info("InternalWar", oldKingdom.Name + ": " + clan.Name + " left the realm and the rebellion.");
        }

        // ----- The end --------------------------------------------------------

        /// <summary>
        /// Ends an internal war and applies design 07 §3a Q1's outcome. Public for the test
        /// command; everything else reaches it through <see cref="Advance"/>.
        ///
        /// Order matters and is fixed:
        ///   1. the peace, while the two sides still exist on the map;
        ///   2. the rising's lists emptied - `DestroyKingdomAction` runs `DestroyClanAction` on
        ///      every clan still listed in a kingdom, so a rising destroyed with its rebels
        ///      listed would wipe them out of the game;
        ///   3. the index, so every rebel answers to the kingdom again;
        ///   4. the rising destroyed;
        ///   5. the outcome, so a change of ruling clan happens in a realm at peace with itself.
        /// </summary>
        public static void End(ModState state, InternalWar war, InternalWarOutcome outcome, string reason)
        {
            if (state == null || war == null || !war.IsOngoing) return;

            var kingdom = war.Kingdom;
            var faction = war.Faction;
            var oldRuler = kingdom?.Leader;
            var oldRulingClan = kingdom?.RulingClan;

            war.End(outcome);

            try
            {
                if (faction != null && kingdom != null && !faction.IsEliminated && !kingdom.IsEliminated
                    && FactionManager.IsAtWarAgainstFaction(faction, kingdom))
                    MakePeaceAction.Apply(faction, kingdom);
            }
            catch (Exception ex)
            {
                Log.Error("InternalWar", "Making peace between " + faction?.Name + " and " + kingdom?.Name + " failed.", ex);
            }

            try
            {
                // The rising's armies go first. An army is the kingdom's (`Army.Kingdom`), and one
                // left standing under a destroyed kingdom is a state vanilla never produces.
                if (faction != null)
                    foreach (var army in new List<Army>(faction.Armies))
                        DisbandArmyAction.ApplyByUnknownReason(army);

                SyncFaction(war, empty: true);
            }
            catch (Exception ex)
            {
                Log.Error("InternalWar", "Emptying the lists of " + faction?.Name + " failed.", ex);
            }

            RebuildIndex(state);
            BlocModel.Invalidate();

            try
            {
                if (faction != null && !faction.IsEliminated)
                {
                    // Belt and braces for step 2: never destroy a kingdom that still lists a clan.
                    if (faction.Clans.Count == 0) DestroyKingdomAction.Apply(faction);
                    else Log.Warn("InternalWar", faction.Name + " still lists " + faction.Clans.Count
                                                 + " clan(s) - left standing rather than destroying them with it.");
                }
            }
            catch (Exception ex)
            {
                Log.Error("InternalWar", "Destroying " + faction?.Name + " failed.", ex);
            }

            Log.Info("InternalWar", (kingdom?.Name?.ToString() ?? "?") + ": the internal war ends - " + outcome
                                    + " (" + reason + "). " + war);

            if (kingdom == null || !kingdom.IsRealm()) return;

            switch (outcome)
            {
                case InternalWarOutcome.RebelsWon:
                    RebelsWin(state, war, oldRuler, oldRulingClan);
                    break;

                case InternalWarOutcome.CrownWon:
                    SuccessionModel.RetireClaim(state, kingdom, war.Claimant, "the rising was put down");
                    LegitimacyRegistry.Adjust(state, kingdom, IntrigueConstants.LegitimacyWonJustWar,
                        "put down " + war.Claimant?.Name + "'s rising");
                    Log.Notify(kingdom.Leader?.Name + " has put down the rising in " + kingdom.Name + ".", Colors.Cyan);
                    break;

                default:
                    // A stalemate settles nothing: the claim stands and the crown keeps its
                    // standing, as a white peace between kingdoms moves neither pool (2.4).
                    Log.Notify("The civil war in " + kingdom.Name + " has ended with nothing settled.", Colors.Yellow);
                    break;
            }
        }

        private static void RebelsWin(ModState state, InternalWar war, Hero oldRuler, Clan oldRulingClan)
        {
            var kingdom = war.Kingdom;
            var banner = war.Banner;

            // The throne may already be theirs (a ruler died and the election chose a rebel);
            // otherwise the claimant's clan takes it now.
            if (kingdom.RulingClan != banner && !war.IsRebel(kingdom.RulingClan)
                && banner != null && !banner.IsEliminated && banner.Kingdom == kingdom)
                SuccessionModel.InstallByArms(state, kingdom, banner);

            LegitimacyRegistry.Adjust(state, kingdom, -IntrigueConstants.SuccessionContestedLegitimacy,
                "took the throne by force of arms");

            // The deposed house stays at court, and stays a claimant if it was a real party.
            if (oldRuler != null && oldRuler.IsAlive && oldRulingClan != null
                && oldRulingClan != kingdom.RulingClan && oldRulingClan.Kingdom == kingdom
                && war.CrownShareAtStart >= IntrigueConstants.SuccessionPretenderShare)
            {
                state.Pretenders.Add(new Pretender(kingdom, oldRuler, war.CrownShareAtStart));
                BlocModel.Invalidate();
                Log.Info("InternalWar", kingdom.Name + ": " + oldRuler.Name + " was deposed and remains a pretender ("
                                        + (war.CrownShareAtStart * 100f).ToString("0") + "% of the court stood on the crown's side).");
            }

            Log.Notify(kingdom.Leader?.Name + " has taken the throne of " + kingdom.Name + " by force of arms.", Colors.Red);
        }
    }
}
