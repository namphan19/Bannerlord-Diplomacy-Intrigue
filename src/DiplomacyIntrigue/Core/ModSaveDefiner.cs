using System.Collections.Generic;
using DiplomacyIntrigue.Models;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// Registers every Diplomacy & Intrigue type with the save system.
    ///
    /// The base id below reserves the block [2749100, 2749199] for this mod. It must not
    /// collide with another loaded module, so do not change it once saves exist in the wild,
    /// and keep every local id inside the reserved span.
    /// </summary>
    public sealed class ModSaveDefiner : SaveableTypeDefiner
    {
        private const int BaseId = 2749100;

        public ModSaveDefiner() : base(BaseId) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(ModState), 1);
            AddClassDefinition(typeof(Treaty), 2);
            AddClassDefinition(typeof(WarRecord), 3);
        }

        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(TreatyType), 20);
            AddEnumDefinition(typeof(TreatyStatus), 21);
            AddEnumDefinition(typeof(CasusBelliType), 22);
            AddEnumDefinition(typeof(CourtAgenda), 23);
            AddEnumDefinition(typeof(SpyMissionType), 24);
            AddEnumDefinition(typeof(MissionOutcome), 25);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<Treaty>));
            ConstructContainerDefinition(typeof(List<WarRecord>));
        }
    }
}
