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

        /// <summary>Range -100..100. Positive means the aggressor is winning. Sets peace terms.</summary>
        [SaveableProperty(7)] public float WarScore { get; private set; }

        [SaveableProperty(8)] public int AggressorCasualties { get; private set; }
        [SaveableProperty(9)] public int DefenderCasualties { get; private set; }
        [SaveableProperty(10)] public int FiefsTakenByAggressor { get; private set; }
        [SaveableProperty(11)] public int FiefsTakenByDefender { get; private set; }

        /// <summary>Set when peace is signed. CampaignTime.Never while the war runs.</summary>
        [SaveableProperty(12)] public CampaignTime EndedOn { get; private set; }

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

        /// <summary>War score from the point of view of the given kingdom.</summary>
        public float ScoreFor(Kingdom kingdom)
        {
            if (kingdom == Aggressor) return WarScore;
            if (kingdom == Defender) return -WarScore;
            return 0f;
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

        internal void AddWarScore(float delta) => WarScore = Clamp(WarScore + delta, -100f, 100f);

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

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        public override string ToString()
            => NameOf(Aggressor) + " vs " + NameOf(Defender)
               + " [" + Justification + "] score=" + WarScore.ToString("0.0")
               + " exhaustion=" + AggressorExhaustion.ToString("0") + "/" + DefenderExhaustion.ToString("0");

        private static string NameOf(Kingdom k) => k == null ? "?" : k.Name.ToString();
    }
}
