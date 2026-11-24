using System;
using Photon.Deterministic;

namespace Quantum {
  // Immutable, populated by an Editor importer in phase 1. No runtime mutation.
  public partial class AaiRulesAsset : AssetObject {
    public int SchemaVersion;
    public string SourceRevision;
    public int SourceTicksPerSecond;
    public int TileSize;
    public AaiCostSpec DefaultStarting;
    public int DefaultPopulationCap;
    public int MaxPopulationCap;
    public AaiEntitySpec[] Entities;
    public AaiTrainingSpec[] Training;
    public AaiBuildingSpec[] Buildings;
    public AaiResearchSpec[] Research;
    public AaiHallSpec[] Hall;
    public AaiStructureSpec[] Structures;
    public AaiGatherSpec[] Gathering;
    public AaiRaceSpec[] Races;
    public AaiAbilitySpec[] Abilities;
    public int[] HeroThresholds;
    public int HeroHealthPerLevel;
    public int HeroDamagePerLevel;
    public int BasePopulation;
    public int FarmPopulation;
    public int TransportCapacity;
    public int RepairHealth;
    public int RepairTicks;
    public long SiegeStructureRangeRaw;
    public FP SiegeStructureRange => FP.FromRaw(SiegeStructureRangeRaw);
    public int SiegeStructureBonus;
    public int SiegeUnitPenalty;
    public int MaxArmyGroups;
    public int MaxArmyGroupSize;
    public int MaxSelection;
    public long ControlRadiusRaw;
    public FP ControlRadius => FP.FromRaw(ControlRadiusRaw);
    public int ControlVictoryScore;

    public AaiEntitySpec Entity(AaiRace race, AaiKind kind) {
      return Entities[(int)race * 23 + (int)kind];
    }

    public AaiTrainingSpec TrainingRule(AaiRace race, AaiKind kind) {
      return Training[(int)race * 13 + (int)kind];
    }
  }

  [Serializable] public struct AaiCostSpec { public int Gold, Wood, Stone; }
  [Serializable] public struct AaiEntitySpec {
    public AaiRace Race;
    public AaiKind Kind;
    public int Health, Damage, Armor, CooldownTicks, SightTenths;
    public long RangeRaw, SpeedRaw, RadiusRaw;
    public FP Range => FP.FromRaw(RangeRaw);
    public FP Speed => FP.FromRaw(SpeedRaw);
    public FP Radius => FP.FromRaw(RadiusRaw);
    public AaiKind[] Trainable;
  }
  [Serializable] public struct AaiTrainingSpec {
    public AaiRace Race;
    public AaiKind Kind;
    public int Population, Ticks, RequiredHall;
    public AaiCostSpec Cost;
  }
  [Serializable] public struct AaiBuildingSpec {
    public AaiKind Kind;
    public int Ticks, RequiredHall;
    public long FootprintRaw;
    public FP Footprint => FP.FromRaw(FootprintRaw);
    public AaiCostSpec Cost, RepairCost;
  }
  [Serializable] public struct AaiResearchSpec {
    public AaiResearchKind Kind;
    public int Level, Ticks, LegacyTicks, RequiredHall, Bonus;
    public AaiKind Producer;
    public AaiCostSpec Cost;
  }
  [Serializable] public struct AaiHallSpec {
    public int Level, Ticks;
    public AaiCostSpec Cost;
    public AaiKind[] Requirements;
  }
  [Serializable] public struct AaiStructureSpec {
    public AaiKind Kind;
    public int Level, Ticks;
    public AaiCostSpec Cost, BulkCost;
  }
  [Serializable] public struct AaiGatherSpec {
    public AaiResource Resource;
    public int Amount, Ticks;
  }
  [Serializable] public struct AaiRaceSpec {
    public AaiRace Race;
    public long GatheringMultiplierRaw, StoneMultiplierRaw, ScoutSightMultiplierRaw, TowerMultiplierRaw, WallMultiplierRaw;
    public FP GatheringMultiplier => FP.FromRaw(GatheringMultiplierRaw);
    public FP StoneMultiplier => FP.FromRaw(StoneMultiplierRaw);
    public FP ScoutSightMultiplier => FP.FromRaw(ScoutSightMultiplierRaw);
    public FP TowerMultiplier => FP.FromRaw(TowerMultiplierRaw);
    public FP WallMultiplier => FP.FromRaw(WallMultiplierRaw);
    public AaiKind[] Ambush;
  }
  [Serializable] public struct AaiAbilitySpec {
    public AaiRace Race;
    public AaiKind Kind;
    public int CooldownTicks, DurationTicks, MaxTargets;
    public long RadiusRaw, BonusRaw;
    public FP Radius => FP.FromRaw(RadiusRaw);
    public FP Bonus => FP.FromRaw(BonusRaw);
  }
}
