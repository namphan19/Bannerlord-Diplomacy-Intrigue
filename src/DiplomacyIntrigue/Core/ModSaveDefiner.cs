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
            AddClassDefinition(typeof(KingdomWeariness), 4);
            AddClassDefinition(typeof(FiefOwnershipRecord), 5);
            AddClassDefinition(typeof(Claim), 6);
            AddClassDefinition(typeof(FabricationAttempt), 7);
            AddClassDefinition(typeof(TrustRecord), 8);
            AddClassDefinition(typeof(KingdomPower), 9);
            AddClassDefinition(typeof(Grievance), 10);
            AddClassDefinition(typeof(KingdomLegitimacy), 11);
            AddClassDefinition(typeof(Pretender), 12);
            AddClassDefinition(typeof(InternalWar), 13);
            AddClassDefinition(typeof(InternalWarMember), 14);
            AddClassDefinition(typeof(SpyNetwork), 15);
            AddClassDefinition(typeof(SpyMission), 16);
        }

        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(TreatyType), 20);
            AddEnumDefinition(typeof(TreatyStatus), 21);
            AddEnumDefinition(typeof(CasusBelliType), 22);
            AddEnumDefinition(typeof(CourtAgenda), 23);
            AddEnumDefinition(typeof(SpyMissionType), 24);
            AddEnumDefinition(typeof(MissionOutcome), 25);
            AddEnumDefinition(typeof(GrievanceType), 26);
            AddEnumDefinition(typeof(InternalWarOutcome), 27);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<Treaty>));
            ConstructContainerDefinition(typeof(List<WarRecord>));
            ConstructContainerDefinition(typeof(List<KingdomWeariness>));
            ConstructContainerDefinition(typeof(List<FiefOwnershipRecord>));
            ConstructContainerDefinition(typeof(List<Claim>));
            ConstructContainerDefinition(typeof(List<FabricationAttempt>));
            ConstructContainerDefinition(typeof(List<TrustRecord>));
            ConstructContainerDefinition(typeof(List<KingdomPower>));
            ConstructContainerDefinition(typeof(List<Grievance>));
            ConstructContainerDefinition(typeof(List<KingdomLegitimacy>));
            ConstructContainerDefinition(typeof(List<Pretender>));
            ConstructContainerDefinition(typeof(List<InternalWar>));
            ConstructContainerDefinition(typeof(List<InternalWarMember>));
            ConstructContainerDefinition(typeof(List<SpyNetwork>));
            ConstructContainerDefinition(typeof(List<SpyMission>));
        }
    }
}
