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
        public const int CurrentSchemaVersion = 1;

        [SaveableProperty(1)] public int SchemaVersion { get; private set; }
        [SaveableProperty(2)] public List<Treaty> Treaties { get; private set; }
        [SaveableProperty(3)] public List<WarRecord> Wars { get; private set; }
        [SaveableProperty(4)] public int NextTreatyId { get; private set; }

        public ModState()
        {
            SchemaVersion = CurrentSchemaVersion;
            Treaties = new List<Treaty>();
            Wars = new List<WarRecord>();
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
            if (NextTreatyId < 1) NextTreatyId = 1;

            Migrate();

            // Objects removed from the game (a kingdom destroyed and cleaned up) leave
            // dangling references. Drop those records rather than crash later.
            Treaties.RemoveAll(t => t == null || t.PartyA == null || t.PartyB == null);
            Wars.RemoveAll(w => w == null || w.Aggressor == null || w.Defender == null);

            Log.Info("State", "Loaded: " + Treaties.Count + " treaties, " + Wars.Count
                              + " war records, schema v" + SchemaVersion + ".");
        }

        private void Migrate()
        {
            if (SchemaVersion == CurrentSchemaVersion) return;
            var from = SchemaVersion;

            // Future migrations go here, one if-block per version step.

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
    }
}
