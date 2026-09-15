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

    public enum MissionOutcome
    {
        Pending = 0,
        Success = 1,
        Failure = 2,
        /// <summary>Failed, and the agent was traced back to us - a diplomatic incident.</summary>
        Exposed = 3,
    }
}
