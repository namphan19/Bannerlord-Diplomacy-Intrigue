using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// A war fought inside one kingdom: a claimant's party against the crown (design 07).
    ///
    /// **Its own record, not a <see cref="WarRecord"/>.** An internal war ends by its own rules -
    /// the throne changes hands or the claim is broken (design 07 §3a Q1) - not at the peace
    /// table. Recorded as a `WarRecord`, every Phase 1 system would take it for a foreign war:
    /// the peace table would price it in tribute and fiefs, call to arms would summon the
    /// crown's allies, trust and claims would be written against a faction that exists for a
    /// season. The rebels' map faction (<see cref="Faction"/>) is kept out of all of them on
    /// purpose, and this record is where the war lives instead. Design 07 §3a Q4.
    ///
    /// Kept after it ends, with its outcome, because "this kingdom fought a civil war two years
    /// ago" is what stops the next one being declared the day after a stalemate.
    ///
    /// Save ids are frozen. This type is definer class id 13 and uses properties 1-14;
    /// <see cref="InternalWarMember"/> is class id 14; next free class id is 15.
    /// </summary>
    public sealed class InternalWar
    {
        [SaveableProperty(1)] public Kingdom Kingdom { get; private set; }

        /// <summary>Who the rebels would crown.</summary>
        [SaveableProperty(2)] public Hero Claimant { get; private set; }

        /// <summary>
        /// The claimant's clan at the start: the house the rising is fought for, and the
        /// ruling clan of <see cref="Faction"/>. Stored rather than read off
        /// <see cref="Claimant"/>, because it must not move if the claimant dies mid-war.
        /// </summary>
        [SaveableProperty(3)] public Clan Banner { get; private set; }

        /// <summary>
        /// Every clan on the rebel side, the banner included. Set at the start (design 07
        /// §3a Q2), and changed since 2.6c only by a house bought over to the other side
        /// (design 07 §6), which <see cref="SideChanges"/> records.
        ///
        /// A list of a wrapper type rather than a `List&lt;Clan&gt;`: that container belongs to
        /// the base game's definer, and whether registering it a second time from a mod is
        /// harmless could not be established from metadata. A container of our own type is
        /// certain to be ours to define.
        /// </summary>
        [SaveableProperty(4)] public List<InternalWarMember> Rebels { get; private set; }

        [SaveableProperty(5)] public CampaignTime StartedOn { get; private set; }

        [SaveableProperty(6)] public float RebelExhaustion { get; private set; }

        [SaveableProperty(7)] public float CrownExhaustion { get; private set; }

        /// <summary>Consecutive days the claimant has been held by the crown's side.</summary>
        [SaveableProperty(8)] public int ClaimantCaptiveDays { get; private set; }

        /// <summary>Consecutive days the ruler has been held by the rebels.</summary>
        [SaveableProperty(9)] public int RulerCaptiveDays { get; private set; }

        [SaveableProperty(10)] public InternalWarOutcome Outcome { get; private set; }

        [SaveableProperty(11)] public CampaignTime EndedOn { get; private set; }

        /// <summary>
        /// Share of the court's influence on the crown's side when the war began, 0-1. Kept for
        /// the one outcome that needs it: a deposed ruling clan remains a pretender only if its
        /// side was a real party (design 07 §3a Q1), and by the end of a lost war its members'
        /// influence says more about the war than about the party.
        /// </summary>
        [SaveableProperty(12)] public float CrownShareAtStart { get; private set; }

        /// <summary>
        /// The kingdom the rebels fight under **on the map only**: every rebel clan's and rebel
        /// hero's `MapFaction` answers with it for the duration, while their `Clan.Kingdom`
        /// stays <see cref="Kingdom"/> - they keep their seats and votes.
        ///
        /// A real `Kingdom`, not the banner clan, because the first build used the clan and
        /// crashed: vanilla casts a map faction to `Kingdom` without checking at ~25 places
        /// (design 07 §3c). Its clan and fief lists are kept in step by
        /// `InternalWars.SyncFaction`; the engine never saves those lists, so they are rebuilt
        /// after every load. Destroyed when the war ends - after its lists are emptied, since
        /// `DestroyKingdomAction` destroys every clan still listed in a kingdom.
        /// </summary>
        [SaveableProperty(13)] public Kingdom Faction { get; private set; }

        /// <summary>
        /// Every house that has changed sides in this war, in either direction (design 07 §6).
        /// A house changes sides once per war; this is how that survives a reload.
        ///
        /// The same wrapper as <see cref="Rebels"/>, so no new class or container definition:
        /// `List&lt;InternalWarMember&gt;` is already registered. Null on a save written before
        /// 2.6c, and created in <see cref="AfterLoad"/>.
        /// </summary>
        [SaveableProperty(14)] public List<InternalWarMember> SideChanges { get; private set; }

        internal InternalWar() { }

        internal InternalWar(Kingdom kingdom, Hero claimant, Clan banner, Kingdom faction,
            IEnumerable<Clan> rebels, float crownShareAtStart)
        {
            Kingdom = kingdom;
            Claimant = claimant;
            Banner = banner;
            Faction = faction;
            Rebels = new List<InternalWarMember>();
            foreach (var clan in rebels) Rebels.Add(new InternalWarMember(clan));
            StartedOn = CampaignTime.Now;
            EndedOn = CampaignTime.Never;
            CrownShareAtStart = crownShareAtStart;
            SideChanges = new List<InternalWarMember>();
        }

        public bool IsOngoing => Outcome == InternalWarOutcome.Ongoing;

        public bool IsRebel(Clan clan)
        {
            if (clan == null || Rebels == null) return false;
            for (var i = 0; i < Rebels.Count; i++)
                if (Rebels[i].Clan == clan) return true;
            return false;
        }

        /// <summary>True once this house has changed sides in this war. It cannot do so again.</summary>
        public bool HasChangedSides(Clan clan)
        {
            if (clan == null || SideChanges == null) return false;
            for (var i = 0; i < SideChanges.Count; i++)
                if (SideChanges[i].Clan == clan) return true;
            return false;
        }

        internal void AfterLoad()
        {
            if (Rebels == null) Rebels = new List<InternalWarMember>();
            Rebels.RemoveAll(r => r == null || r.Clan == null);
            if (SideChanges == null) SideChanges = new List<InternalWarMember>();
            SideChanges.RemoveAll(r => r == null || r.Clan == null);
        }

        /// <summary>
        /// Moves a house to the other side and records that it did. The record only: the
        /// map-faction index, the rising's lists and the armies are <c>InternalWars.ChangeSide</c>'s.
        /// </summary>
        internal void MoveSide(Clan clan, bool toRising)
        {
            if (clan == null) return;
            if (toRising)
            {
                if (!IsRebel(clan)) Rebels.Add(new InternalWarMember(clan));
            }
            else
            {
                Rebels.RemoveAll(r => r.Clan == clan);
            }
            if (!HasChangedSides(clan)) SideChanges.Add(new InternalWarMember(clan));
        }

        internal void AddExhaustion(bool rebelSide, float amount)
        {
            if (amount <= 0f) return;
            if (rebelSide) RebelExhaustion = Clamp(RebelExhaustion + amount);
            else CrownExhaustion = Clamp(CrownExhaustion + amount);
        }

        internal void SetCaptiveDays(int claimantDays, int rulerDays)
        {
            ClaimantCaptiveDays = claimantDays;
            RulerCaptiveDays = rulerDays;
        }

        internal void End(InternalWarOutcome outcome)
        {
            Outcome = outcome;
            EndedOn = CampaignTime.Now;
        }

        private static float Clamp(float v) => v < 0f ? 0f : (v > 100f ? 100f : v);

        public override string ToString()
            => (Kingdom == null ? "?" : Kingdom.Name.ToString()) + ": "
               + (Claimant == null ? "?" : Claimant.Name.ToString()) + " (" + (Rebels?.Count ?? 0)
               + " clans) against the crown, exhaustion rebels " + RebelExhaustion.ToString("0.0")
               + " / crown " + CrownExhaustion.ToString("0.0")
               + (IsOngoing ? "" : ", ended " + Outcome);
    }

    /// <summary>
    /// One clan on the rebel side of an <see cref="InternalWar"/>. A wrapper rather than a bare
    /// `Clan` in the list - see <see cref="InternalWar.Rebels"/>.
    ///
    /// Save ids are frozen. This type is definer class id 14.
    /// </summary>
    public sealed class InternalWarMember
    {
        [SaveableProperty(1)] public Clan Clan { get; private set; }

        internal InternalWarMember() { }

        internal InternalWarMember(Clan clan)
        {
            Clan = clan;
        }
    }
}
