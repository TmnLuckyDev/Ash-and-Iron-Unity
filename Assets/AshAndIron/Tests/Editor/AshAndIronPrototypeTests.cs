using System.IO;
using System.Linq;
using NUnit.Framework;
using Photon.Deterministic;
using Quantum;
using Quantum.Testing;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace AshAndIron.Tests {
  public sealed class AshAndIronPrototypeTests {
    private static AaiRulesAsset Rules() {
      var rules = AssetObject.Create<AaiRulesAsset>();
      JsonUtility.FromJsonOverwrite(File.ReadAllText("Assets/AshAndIron/Reference/Generated/rules.editor.json"), rules);
      rules.Guid = AssetGuid.NewGuid();
      return rules;
    }

    private static AaiMapAsset TestMap() => AssetObject.Create<AaiMapAsset>(map => {
      map.Guid = AssetGuid.NewGuid();
      map.Width = 32;
      map.Height = 32;
      map.TileSize = 32;
      map.Tiles = new AaiTerrain[map.Width * map.Height];
      map.Sources = new AaiMapResourceSpec[3];
      map.SeatOneXRaw = map.SeatOneYRaw = 256L * 65536;
      map.SeatTwoXRaw = map.SeatTwoYRaw = 768L * 65536;
    });

    [Test]
    public void ExportedCatalogUsesActualRaceStatsAndPrices() {
      var rules = Rules();
      try {
        Assert.AreEqual(115, rules.Entities.Length);
        Assert.AreEqual(65, rules.Training.Length);
        Assert.AreEqual(31, rules.Research.Length);
        Assert.AreEqual(14, rules.Abilities.Length);
        Assert.AreEqual(205, rules.Entity(AaiRace.Human, AaiKind.Tower).Range.AsInt);
        Assert.AreEqual(120, rules.Entity(AaiRace.Human, AaiKind.Warship).Range.AsInt);
        Assert.AreEqual(143, rules.Entity(AaiRace.Elf, AaiKind.Scout).SightTenths);
        Assert.AreEqual(60, rules.TrainingRule(AaiRace.Undead, AaiKind.Blade).Cost.Gold);
        Assert.AreEqual(6885, rules.Research.Single(r => r.Kind == AaiResearchKind.Weapons && r.Level == 5).Cost.Gold);
        Assert.AreEqual(1, rules.TrainingRule(AaiRace.Dwarf, AaiKind.Transport).Population);
        Assert.AreEqual(2, rules.TrainingRule(AaiRace.Dwarf, AaiKind.Warship).Population);
      } finally { Object.DestroyImmediate(rules); }
    }

    [Test]
    public void TrainingReservationIsAtomicAndUsesPopulationCost() {
      var rules = Rules();
      try {
        var owner = new AaiPlayer { Race = AaiRace.Human, TownHallLevel = 3,
          Bank = new AaiResources { Gold = 1000, Wood = 1000, Stone = 1000 },
          PopulationUsed = 9, PopulationCapacity = 10, PopulationCap = 500 };
        Assert.False(AaiEconomyRules.TryReserveTraining(ref owner, rules.TrainingRule(AaiRace.Human, AaiKind.Warship), 1));
        Assert.AreEqual(1000, owner.Bank.Gold);
        Assert.AreEqual(0, owner.PopulationReserved);
        Assert.True(AaiEconomyRules.TryReserveTraining(ref owner, rules.TrainingRule(AaiRace.Human, AaiKind.Transport), 1));
        Assert.AreEqual(840, owner.Bank.Gold);
        Assert.AreEqual(870, owner.Bank.Wood);
        Assert.AreEqual(1, owner.PopulationReserved);
        Assert.True(AaiEconomyRules.CompleteReservedUnit(ref owner, 1));
        Assert.AreEqual(10, owner.PopulationUsed);
        Assert.AreEqual(0, owner.PopulationReserved);
      } finally { Object.DestroyImmediate(rules); }
    }

    [TestCase(AaiRace.Human, AaiRace.Orc)]
    [TestCase(AaiRace.Elf, AaiRace.Elf)]
    [TestCase(AaiRace.Dwarf, AaiRace.Undead)]
    public unsafe void ExactOpeningAndOwnershipAndMovement(AaiRace one, AaiRace two) {
      var rules = Rules();
      var map = TestMap();
      using var emulator = new FrameEmulator(new FrameEmulator.Args {
        Systems = new SystemBase[] { new AaiOpeningSystem(), new AaiMovementSystem() },
        SimulationConfig = FrameEmulator.CreateSimulationConfig(),
        SessionConfig = new DeterministicSessionConfig { PlayerCount = 2, UpdateFPS = 20 },
        RuntimeConfig = new RuntimeConfig { AshRules = rules, AshMap = map, AshSeatOneRace = one, AshSeatTwoRace = two },
        Assets = new AssetObject[] { rules, map }
      });
      var frame = emulator.Frame;
      var match = frame.Get<AaiMatch>(frame.Global->AaiMatchEntity);
      var ownerOne = frame.Get<AaiPlayer>(match.Players[0]);
      var ownerTwo = frame.Get<AaiPlayer>(match.Players[1]);
      Assert.AreEqual(one, ownerOne.Race);
      Assert.AreEqual(two, ownerTwo.Race);
      Assert.AreEqual(9, ownerOne.PopulationUsed);
      Assert.AreEqual(9, ownerTwo.PopulationUsed);
      Assert.AreEqual(300, ownerOne.Bank.Gold);
      Assert.AreEqual(180, ownerTwo.Bank.Wood);
      Assert.AreEqual(20, frame.ComponentCount<AaiIdentity>());
      Assert.AreEqual(18, frame.ComponentCount<AaiUnit>());
      Assert.AreEqual(6, frame.ComponentCount<AaiWorker>());
      EntityRef moving = default;
      foreach (var (entity, identity) in frame.GetComponentIterator<AaiIdentity>()) {
        if (identity.Owner == match.Players[0] && identity.Kind == AaiKind.Blade) { moving = entity; break; }
      }
      var start = frame.Get<Transform2D>(moving).Position;
      var target = start + new FPVector2(32, 0);
      var order = new AaiMoveUnitCommand { Unit = moving, XRaw = target.X.RawValue, YRaw = target.Y.RawValue };
      Assert.False(AaiCommandSystem.Apply(frame, map, (PlayerRef)1, order));
      Assert.True(AaiCommandSystem.Apply(frame, map, (PlayerRef)0, order));
      emulator.Step(20);
      Assert.AreEqual(target, frame.Get<Transform2D>(moving).Position);
      Assert.False(frame.Get<AaiMovement>(moving).HasDestination);
      Assert.AreEqual(20, frame.Get<AaiMatch>(frame.Global->AaiMatchEntity).ElapsedTicks);
    }
  }
}
