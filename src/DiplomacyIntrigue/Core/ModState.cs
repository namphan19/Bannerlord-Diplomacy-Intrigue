using System.Collections.Generic;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// Root savable container for everything Diplomacy & Intrigue adds to a campaign.
    /// One instance per campaign, owned by CoreBehavior.
    ///
    /// Rules for this type:
    ///  - Never remove or renumber a SaveableProperty id. Retire it and take the next free number.
    ///  - Bump CurrentSchemaVersion when the meaning of existing data changes, and migrate
    ///    in Migrate() so that saves from older mod versions keep loading.
    /// </summary>
    public sealed class ModState
    {
        public const int CurrentSchemaVersion = 4;

        [SaveableProperty(1)] public int SchemaVersion { get; private set; }
        [SaveableProperty(2)] public List<Treaty> Treaties { get; private set; }
        [SaveableProperty(3)] public List<WarRecord> Wars { get; private set; }
        [SaveableProperty(4)] public int NextTreatyId { get; private set; }
        [SaveableProperty(5)] public List<KingdomWeariness> Weariness { get; private set; }
        [SaveableProperty(6)] public List<FiefOwnershipRecord> FiefHistory { get; private set; }
        [SaveableProperty(7)] public List<Claim> Claims { get; private set; }
        [SaveableProperty(8)] public List<FabricationAttempt> Fabrications { get; private set; }
        [SaveableProperty(9)] public List<TrustRecord> Trust { get; private set; }

        public ModState()
        {
            SchemaVersion = CurrentSchemaVersion;
            Treaties = new List<Treaty>();
            Wars = new List<WarRecord>();
            Weariness = new List<KingdomWeariness>();
            FiefHistory = new List<FiefOwnershipRecord>();
            Claims = new List<Claim>();
            Fabrications = new List<FabricationAttempt>();
            Trust = new List<TrustRecord>();
            NextTreatyId = 1;
        }

        /// <summary>
        /// Called right after load. Older saves - and saves made before a given list
        /// existed - come back with nulls, so every collection is re-checked here.
        /// </summary>
        internal void AfterLoad()
        {
            if (Treaties == null) Treaties = new List<Treaty>();
            if (Wars == null) Wars = new List<WarRecord>();
            if (Weariness == null) Weariness = new List<KingdomWeariness>();
            if (FiefHistory == null) FiefHistory = new List<FiefOwnershipRecord>();
            if (Claims == null) Claims = new List<Claim>();
            if (Fabrications == null) Fabrications = new List<FabricationAttempt>();
            if (Trust == null) Trust = new List<TrustRecord>();
            if (NextTreatyId < 1) NextTreatyId = 1;

            Migrate();

            // Objects removed from the game (a kingdom destroyed and cleaned up) leave
            // dangling references. Drop those records rather than crash later.
            Treaties.RemoveAll(t => t == null || t.PartyA == null || t.PartyB == null);
            Wars.RemoveAll(w => w == null || w.Aggressor == null || w.Defender == null);
            Weariness.RemoveAll(w => w == null || w.Kingdom == null);
            FiefHistory.RemoveAll(f => f == null || f.Settlement == null || f.Kingdom == null);
            Claims.RemoveAll(c => c == null || c.Claimant == null || c.Target == null);
            Fabrications.RemoveAll(f => f == null || f.Claimant == null || f.Target == null);
            Trust.RemoveAll(t => t == null || t.From == null || t.To == null);

            Log.Info("State", "Loaded: " + Treaties.Count + " treaties, " + Wars.Count
                              + " war records, " + Weariness.Count + " weariness entries, "
                              + Claims.Count + " claims, " + FiefHistory.Count + " fief records, "
                              + Trust.Count + " trust records, schema v" + SchemaVersion + ".");
        }

        private void Migrate()
        {
            if (SchemaVersion == CurrentSchemaVersion) return;
            var from = SchemaVersion;

            // v1 -> v2 added the weariness pool; v2 -> v3 the fief ledger, claims and
            // fabrications; v3 -> v4 the trust ledger. AfterLoad() creates each list
            // when it comes back null, so there is nothing to convert - only to record
            // that we moved.

            SchemaVersion = CurrentSchemaVersion;
            Log.Info("State", "Migrated save data from schema v" + from + " to v" + CurrentSchemaVersion + ".");
        }

        internal int TakeNextTreatyId() => NextTreatyId++;

        // ----- Treaty queries -------------------------------------------------

        public IEnumerable<Treaty> ActiveTreatiesOf(Kingdom kingdom)
        {
            for (var i = 0; i < Treaties.Count; i++)
            {
                var t = Treaties[i];
                if (t.IsActive && t.Involves(kingdom)) yield return t;
            }
        }

        public Treaty ActiveTreatyBetween(Kingdom x, Kingdom y, TreatyType type)
        {
            for (var i = 0; i < Treaties.Count; i++)
            {
                var t = Treaties[i];
                if (t.IsActive && t.Type == type && t.IsBetween(x, y)) return t;
            }
            return null;
        }

        public bool HasTreatyForbiddingWar(Kingdom x, Kingdom y)
        {
            for (var i = 0; i < Treaties.Count; i++)
            {
                var t = Treaties[i];
                if (t.IsActive && t.ForbidsWar && t.IsBetween(x, y)) return true;
            }
            return false;
        }

        // ----- War queries ----------------------------------------------------

        public WarRecord OngoingWarBetween(Kingdom x, Kingdom y)
        {
            for (var i = 0; i < Wars.Count; i++)
            {
                var w = Wars[i];
                if (w.IsOngoing && w.IsBetween(x, y)) return w;
            }
            return null;
        }

        public IEnumerable<WarRecord> OngoingWarsOf(Kingdom kingdom)
        {
            for (var i = 0; i < Wars.Count; i++)
            {
                var w = Wars[i];
                if (w.IsOngoing && w.Involves(kingdom)) yield return w;
            }
        }

        // ----- Weariness ------------------------------------------------------

        /// <summary>Zero for a kingdom with no entry, which is the common case.</summary>
        public float WearinessOf(Kingdom kingdom)
        {
            for (var i = 0; i < Weariness.Count; i++)
                if (Weariness[i].Kingdom == kingdom) return Weariness[i].Value;
            return 0f;
        }

        internal void AddWeariness(Kingdom kingdom, float amount)
        {
            if (kingdom == null || amount <= 0f) return;

            for (var i = 0; i < Weariness.Count; i++)
            {
                if (Weariness[i].Kingdom != kingdom) continue;
                Weariness[i].Add(amount);
                return;
            }
            Weariness.Add(new KingdomWeariness(kingdom, amount));
        }
    }
}
