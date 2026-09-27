using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// Bookkeeping for one war between two kingdoms. The base game tracks only the
    /// fact of being at war; Diplomacy & Intrigue needs the history - who started it, why,
    /// and how worn down each side is.
    /// </summary>
    public sealed class WarRecord
    {
        [SaveableProperty(1)] public Kingdom Aggressor { get; private set; }
        [SaveableProperty(2)] public Kingdom Defender { get; private set; }
        [SaveableProperty(3)] public CampaignTime StartedOn { get; private set; }
        [SaveableProperty(4)] public CasusBelliType Justification { get; private set; }

        /// <summary>0-100. At high values the court sues for peace and unrest climbs.</summary>
        [SaveableProperty(5)] public float AggressorExhaustion { get; private set; }
        [SaveableProperty(6)] public float DefenderExhaustion { get; private set; }

        /// <summary>
        /// The battle part of the war score (design 10), aggressor-positive and decaying. Not
        /// the war score itself: that adds the prisoners each side holds, which are read live
        /// from the world, so the whole score is resolved in <c>Diplomacy.WarScore</c> and
        /// nowhere else.
        ///
        /// Save id 7 is the old <c>WarScore</c>, renamed. The save system keys members by id,
        /// not name (<c>MemberTypeId</c> is type level and local id only), so old saves load
        /// into it; their value still holds the old formula's fief and raid points, which the
        /// decay clears.
        /// </summary>
        [SaveableProperty(7)] public float BattleScore { get; private set; }

        [SaveableProperty(8)] public int AggressorCasualties { get; private set; }
        [SaveableProperty(9)] public int DefenderCasualties { get; private set; }
        [SaveableProperty(10)] public int FiefsTakenByAggressor { get; private set; }
        [SaveableProperty(11)] public int FiefsTakenByDefender { get; private set; }

        /// <summary>Set when peace is signed. CampaignTime.Never while the war runs.</summary>
        [SaveableProperty(12)] public CampaignTime EndedOn { get; private set; }

        /// <summary>
        /// Whether the trust reward for keeping this peace has already been paid. Kept on
        /// the war record so the dividend is paid exactly once, without a separate ledger.
        /// </summary>
        [SaveableProperty(13)] public bool PeaceDividendPaid { get; private set; }

        /// <summary>
        /// The kingdom whose call to arms brought the aggressor into this war, or null if
        /// it chose the war itself.
        ///
        /// This is what makes the obligation symmetric: a kingdom that can be dragged into
        /// someone else's war is released from it when that someone makes peace. Without
        /// this the ally is left fighting alone for a cause it never chose and cannot end.
        /// </summary>
        [SaveableProperty(14)] public Kingdom CalledBy { get; private set; }

        /// <summary>
        /// Men in each side's lord parties and garrisons when the war began: what a battle's
        /// losses are measured against (design 10 §3). Fixed rather than live, so the same
        /// defeat is worth the same on day 1 and day 200. Zero on a war from a save that
        /// predates the fields, until <c>Diplomacy.WarScore.EnsureManpower</c> fills it.
        /// </summary>
        [SaveableProperty(15)] public int AggressorManpowerAtStart { get; private set; }
        [SaveableProperty(16)] public int DefenderManpowerAtStart { get; private set; }

        /// <summary>True when this participant joined only because an ally called.</summary>
        public bool IsObligationWar => CalledBy != null;

        /// <summary>Total dead on both sides. Whether a war was ever really fought.</summary>
        public int TotalCasualties => AggressorCasualties + DefenderCasualties;

        internal WarRecord() { }

        internal WarRecord(Kingdom aggressor, Kingdom defender, CasusBelliType justification)
        {
            Aggressor = aggressor;
            Defender = defender;
            Justification = justification;
            StartedOn = CampaignTime.Now;
            EndedOn = CampaignTime.Never;
        }

        public bool IsOngoing => EndedOn == CampaignTime.Never;

        public bool Involves(Kingdom kingdom) => Aggressor == kingdom || Defender == kingdom;

        public bool IsBetween(Kingdom x, Kingdom y)
            => (Aggressor == x && Defender == y) || (Aggressor == y && Defender == x);

        public Kingdom Other(Kingdom kingdom)
        {
            if (Aggressor == kingdom) return Defender;
            if (Defender == kingdom) return Aggressor;
            return null;
        }

        public float ExhaustionOf(Kingdom kingdom)
        {
            if (kingdom == Aggressor) return AggressorExhaustion;
            if (kingdom == Defender) return DefenderExhaustion;
            return 0f;
        }

        /// <summary>
        /// The battle part only, from the given kingdom's side. Anything that wants the war
        /// score reads <c>Diplomacy.WarScore.For</c>, which adds the prisoners.
        /// </summary>
        public float BattleScoreFor(Kingdom kingdom)
        {
            if (kingdom == Aggressor) return BattleScore;
            if (kingdom == Defender) return -BattleScore;
            return 0f;
        }

        public int ManpowerAtStartOf(Kingdom kingdom)
        {
            if (kingdom == Aggressor) return AggressorManpowerAtStart;
            if (kingdom == Defender) return DefenderManpowerAtStart;
            return 0;
        }

        internal void SetManpowerAtStart(Kingdom kingdom, int men)
        {
            if (kingdom == Aggressor) AggressorManpowerAtStart = men;
            else if (kingdom == Defender) DefenderManpowerAtStart = men;
        }

        public float DaysElapsed
            => (float)(IsOngoing ? CampaignTime.Now - StartedOn : EndedOn - StartedOn).ToDays;

        internal void AddExhaustion(Kingdom kingdom, float amount)
        {
            if (kingdom == Aggressor)
                AggressorExhaustion = Clamp(AggressorExhaustion + amount, 0f, 100f);
            else if (kingdom == Defender)
                DefenderExhaustion = Clamp(DefenderExhaustion + amount, 0f, 100f);
        }

        // Unbounded, by the lead's call (design/04 §12.4.1): the effort a kingdom can pour into
        // a war has no ceiling, so the number measuring it has none either. The bound lives on
        // the demand side instead - PeaceTable.MinimumAcceptable caps what a victory can be
        // spent on, because what a kingdom can give away is finite even when what it earned is
        // not. Clamped to +/-100 until 2026-09-20, which made peace-table vassalage arithmetically
        // unreachable: a winner wants half the score, so it could never want more than 50, and
        // tribute at 65 always settled first.
        internal void AddBattleScore(float delta) => BattleScore += delta;

        internal void AddCasualties(Kingdom sufferer, int count)
        {
            if (sufferer == Aggressor) AggressorCasualties += count;
            else if (sufferer == Defender) DefenderCasualties += count;
        }

        internal void AddFiefCapture(Kingdom captor)
        {
            if (captor == Aggressor) FiefsTakenByAggressor++;
            else if (captor == Defender) FiefsTakenByDefender++;
        }

        internal void Close() => EndedOn = CampaignTime.Now;

        internal void MarkPeaceDividendPaid() => PeaceDividendPaid = true;

        internal void MarkCalledBy(Kingdom caller) => CalledBy = caller;

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        // One decimal on exhaustion is not cosmetic: daily accrual is 0.08, so integer
        // rounding makes a working system look like a dead one for the first fortnight.
        public override string ToString()
            => NameOf(Aggressor) + " vs " + NameOf(Defender)
               + " [" + Justification + "] battles=" + BattleScore.ToString("0.00")
               + " exhaustion=" + AggressorExhaustion.ToString("0.00") + "/" + DefenderExhaustion.ToString("0.00");

        private static string NameOf(Kingdom k) => k == null ? "?" : k.Name.ToString();
    }
}
