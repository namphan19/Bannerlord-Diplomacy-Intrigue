namespace DiplomacyIntrigue.Models
{
    /// <summary>The kinds of binding agreement two kingdoms can hold.</summary>
    public enum TreatyType
    {
        /// <summary>Cannot declare war for the duration. Cheapest, most common.</summary>
        NonAggressionPact = 0,
        /// <summary>Forced cease-fire after a war, shorter and with a hard re-entry cost.</summary>
        Truce = 1,
        /// <summary>Obliges the signatory to join only when the partner is attacked.</summary>
        DefensivePact = 2,
        /// <summary>Full alliance: call to arms in offensive wars too, shared war goals.</summary>
        Alliance = 3,
        /// <summary>One side pays the other periodically in exchange for peace.</summary>
        TributaryPact = 4,
        /// <summary>Client state: tribute plus foreign-policy subordination.</summary>
        Vassalage = 5,
    }

    public enum TreatyStatus
    {
        Active = 0,
        Expired = 1,
        /// <summary>Terminated by one party in violation of its terms - carries a trust penalty.</summary>
        Broken = 2,
        /// <summary>Ended by mutual consent, no penalty.</summary>
        Dissolved = 3,
    }

    /// <summary>
    /// Why a war was started. Drives influence cost, internal legitimacy, ally
    /// obligations, and what the winner may demand at the peace table.
    /// </summary>
    public enum CasusBelliType
    {
        /// <summary>No justification - naked aggression. Maximum legitimacy damage.</summary>
        None = 0,
        Conquest = 1,
        /// <summary>A fief the claiming kingdom held within living memory.</summary>
        ReclaimAncestralLand = 2,
        AvengeRaid = 3,
        BrokenTreaty = 4,
        /// <summary>An enemy spy network was exposed on our soil.</summary>
        EspionageExposed = 5,
        /// <summary>Honouring a defensive pact or alliance.</summary>
        DefendAlly = 6,
        /// <summary>Backing a pretender who claims the enemy throne.</summary>
        SupportClaimant = 7,
        /// <summary>Retaliation for an embargo or for seized caravans.</summary>
        TradeDispute = 8,
    }

    /// <summary>How a court bloc inside a kingdom wants foreign policy run.</summary>
    public enum CourtAgenda
    {
        None = 0,
        /// <summary>Push for peace, oppose new wars.</summary>
        Doves = 1,
        /// <summary>Push for expansion, oppose truces.</summary>
        Hawks = 2,
        /// <summary>Wants power devolved from the crown to the lords.</summary>
        Autonomists = 3,
        /// <summary>Wants a stronger crown.</summary>
        Centralists = 4,
        /// <summary>Backs a rival claimant to the throne.</summary>
        Pretenders = 5,
    }

    public enum SpyMissionType
    {
        None = 0,
        /// <summary>Reveal enemy army positions and strengths on the map.</summary>
        ScoutArmies = 1,
        /// <summary>Reveal the diplomatic intentions and pending decisions of a court.</summary>
        ReadCourt = 2,
        SabotageGarrison = 3,
        /// <summary>Raise unrest in a target settlement.</summary>
        SpreadDissent = 4,
        /// <summary>Buy the loyalty of a lord; may flip them later.</summary>
        BribeLord = 5,
        /// <summary>Plant evidence implicating a lord in treason.</summary>
        ForgeLetters = 6,
        StealTreasury = 7,
        Assassinate = 8,
    }

    /// <summary>
    /// Why a clan holds something against another clan. Weights live in
    /// <see cref="Intrigue.IntrigueConstants"/> so a balance pass edits one file.
    /// </summary>
    public enum GrievanceType
    {
        None = 0,
        /// <summary>A fief they bid for went to a rival clan.</summary>
        FiefToRival = 1,
        /// <summary>A war their bloc opposed. Weight scales with how illegitimate it was.</summary>
        UnjustWar = 2,
        /// <summary>The realm bought peace by paying tribute. Only the paying side feels it.</summary>
        HumiliatingTribute = 3,
        /// <summary>A relative left in enemy captivity for more than a year.</summary>
        RelativeInCaptivity = 4,
        /// <summary>A fief of theirs was lost to the enemy; they blame the crown for not defending it.</summary>
        FiefLostToEnemy = 5,
        /// <summary>A policy passed against their agenda.</summary>
        PolicyAgainstAgenda = 6,
        /// <summary>Peace signed while they were winning. Hawks specifically.</summary>
        PeaceWhileWinning = 7,
        /// <summary>The ruler turned down something they asked for.</summary>
        RequestRefused = 8,
        /// <summary>
        /// They backed a losing claimant at a contested succession (design 02 §5).
        ///
        /// Value 9 because 1-8 are already in save files. A new *value* on an enum the definer
        /// already registers is safe - existing values keep their numbers - but reusing or
        /// renumbering one would silently change the meaning of grievances already stored.
        /// </summary>
        SuccessionPassedOver = 9,
        /// <summary>
        /// Letters in the ruler's hand that the ruler never wrote, shown to the house by a
        /// foreign network (design 03 §2 ForgeLetters, §6). The house believes them, so it weighs
        /// on loyalty exactly as a real slight would. Value 10, added the way 9 was.
        /// </summary>
        ForgedLetters = 10,
        /// <summary>
        /// The crown took a court seat back from one of the house's own (design 09 C2). Value 11,
        /// added the way 9 and 10 were.
        /// </summary>
        DismissedFromOffice = 11,
    }

    /// <summary>
    /// How an internal war ended (design 07 §3a). Registered at definer enum id 27.
    ///
    /// <see cref="Ongoing"/> is the zero value on purpose: a record loaded from a save made
    /// before the outcome was written reads as a war still running, which is the only safe
    /// reading of a record that has no end date either.
    /// </summary>
    public enum InternalWarOutcome
    {
        Ongoing = 0,
        /// <summary>The claimant took the throne.</summary>
        RebelsWon = 1,
        /// <summary>The claim was broken and retired.</summary>
        CrownWon = 2,
        /// <summary>Both sides stopped with nothing settled; the claim stands.</summary>
        Stalemate = 3,
        /// <summary>The war stopped existing under it - the rebel banner left the realm, the
        /// kingdom fell, or a peace was made outside this system.</summary>
        Dissolved = 4,
    }

    /// <summary>
    /// What a clan's loyalty means for its behaviour (design 02 §2).
    ///
    /// **Not registered in ModSaveDefiner, on purpose.** Loyalty is derived and never stored,
    /// so this enum never reaches a save file. Adding it to the definer would freeze an id
    /// for something no save contains.
    /// </summary>
    public enum LoyaltyBand
    {
        /// <summary>Below 25 - will leave, given a reason and somewhere to go.</summary>
        DefectionRisk = 0,
        /// <summary>25-39 - votes against the ruler, volunteers nothing.</summary>
        Disaffected = 1,
        /// <summary>40-69 - votes its own interest.</summary>
        Transactional = 2,
        /// <summary>70+ - votes with the ruler and answers the call regardless of agenda.</summary>
        Reliable = 3,
    }

    public enum MissionOutcome
    {
        Pending = 0,
        Success = 1,
        Failure = 2,
        /// <summary>Failed, and the agent was traced back to us - a diplomatic incident.</summary>
        Exposed = 3,
    }

    /// <summary>
    /// The six political jobs of a realm (design 08 §4.1). Leadership belongs to the ruler; the
    /// other five are filled by an appointed holder (design 09 C2, <c>CourtOffice</c>) or, while a
    /// seat is empty, by the ruling house's best hero in that skill.
    ///
    /// Saved since design 09 C2 (a court seat is one of these), registered at definer enum id 28.
    /// The values are frozen from then on: never renumber them. Moved here from Statecraft then,
    /// since a saved type has to live in this layer.
    /// </summary>
    public enum Portfolio
    {
        Ruler = 0,
        Envoy = 1,
        Steward = 2,
        Treasurer = 3,
        Spymaster = 4,
        Watch = 5
    }
}
