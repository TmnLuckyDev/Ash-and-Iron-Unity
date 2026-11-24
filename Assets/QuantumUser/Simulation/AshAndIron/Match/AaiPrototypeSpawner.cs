using Photon.Deterministic;

namespace Quantum {
  /// <summary>Spawn the exact legacy opening army on each independently chosen race.</summary>
  public static class AaiPrototypeSpawner {
    private static EntityRef CreateSeat(Frame frame, AaiRulesAsset rules, AaiSeat seat, AaiRace race,
      PlayerRef playerRef, bool isComputer, AaiResources starting, int populationCap) {
      var owner = frame.Create();
      var player = new AaiPlayer {
        Player = playerRef, IsComputer = isComputer, Seat = seat, Team = (byte)seat, Race = race,
        Bank = starting, TownHallLevel = 1, PopulationCap = populationCap,
        PopulationCapacity = rules.BasePopulation, NextGroupId = 1
      };
      frame.Add(owner, player);
      return owner;
    }

    public static unsafe EntityRef CreateOpening(Frame frame, AaiRulesAsset rules, AaiMapAsset map,
      AaiRace raceOne, AaiRace raceTwo, PlayerRef playerOne, PlayerRef playerTwo, bool computerTwo) {
      var bank = new AaiResources { Gold = rules.DefaultStarting.Gold, Wood = rules.DefaultStarting.Wood, Stone = rules.DefaultStarting.Stone };
      var one = CreateSeat(frame, rules, AaiSeat.One, raceOne, playerOne, false, bank, rules.DefaultPopulationCap);
      var two = CreateSeat(frame, rules, AaiSeat.Two, raceTwo, playerTwo, computerTwo, bank, rules.DefaultPopulationCap);
      // Keep relative spawn order of the TypeScript constructor: both ranks,
      // then both worker groups. IDs are versioned Quantum refs, not old IDs.
      SpawnRanks(frame, rules, one, map.SeatOneBase);
      SpawnRanks(frame, rules, two, map.SeatTwoBase);
      SpawnWorkers(frame, rules, one, map.SeatOneBase);
      SpawnWorkers(frame, rules, two, map.SeatTwoBase);
      var matchEntity = frame.Create();
      var match = new AaiMatch {
        Rules = rules, Map = map, MapSeed = map.Seed,
        Settings = new AaiMatchSettings {
          Mode = map.Mode, Progression = true, RandomStarts = map.HasSeed,
          PopulationCap = rules.DefaultPopulationCap, Starting = bank
        }
      };
      match.Players[0] = one;
      match.Players[1] = two;
      var settings = match.Settings;
      for (int index = 0; index < 3; index++) {
        var source = map.Sources[index];
        settings.Sources[index] = new AaiResourceSettings {
          CountPerSide = source.CountPerSide, UnlimitedStock = source.UnlimitedStock, Stock = source.Stock,
          UnlimitedWorkers = source.UnlimitedWorkers, MaxWorkers = source.MaxWorkers
        };
      }
      match.Settings = settings;
      frame.Add(matchEntity, match);
      frame.Global->AaiMatchEntity = matchEntity;
      return matchEntity;
    }

    private static void SpawnRanks(Frame frame, AaiRulesAsset rules, EntityRef owner, FPVector2 start) {
      var player = frame.Get<AaiPlayer>(owner);
      player.Headquarters = Spawn(frame, rules, owner, player.Race, AaiKind.Headquarters, start);
      int direction = player.Seat == AaiSeat.One ? 1 : -1;
      AddUnit(AaiKind.Blade, 74, -31);
      AddUnit(AaiKind.Blade, 74, 1);
      AddUnit(AaiKind.Blade, 74, 33);
      AddUnit(AaiKind.Ranged, 40, -23);
      AddUnit(AaiKind.Ranged, 40, 24);
      AddUnit(AaiKind.Polearm, 109, 1);
      frame.Set(owner, player);

      void AddUnit(AaiKind kind, int x, int y) {
        Spawn(frame, rules, owner, player.Race, kind, start + new FPVector2(x * direction, y * direction));
        player.PopulationUsed += rules.TrainingRule(player.Race, kind).Population;
      }
    }

    private static void SpawnWorkers(Frame frame, AaiRulesAsset rules, EntityRef owner, FPVector2 start) {
      var player = frame.Get<AaiPlayer>(owner);
      int direction = player.Seat == AaiSeat.One ? 1 : -1;
      Spawn(frame, rules, owner, player.Race, AaiKind.Worker, start + new FPVector2(-46 * direction, 38 * direction));
      Spawn(frame, rules, owner, player.Race, AaiKind.Worker, start + new FPVector2(-12 * direction, 53 * direction));
      Spawn(frame, rules, owner, player.Race, AaiKind.Worker, start + new FPVector2(22 * direction, 50 * direction));
      player.PopulationUsed += 3 * rules.TrainingRule(player.Race, AaiKind.Worker).Population;
      frame.Set(owner, player);
    }

    public static EntityRef Spawn(Frame frame, AaiRulesAsset rules, EntityRef owner, AaiRace race, AaiKind kind, FPVector2 position) {
      var entity = frame.Create();
      var spec = rules.Entity(race, kind);
      frame.Add(entity, Transform2D.Create(position));
      frame.Add(entity, new AaiIdentity { Owner = owner, Race = race, Kind = kind });
      frame.Add(entity, new AaiHealth { Current = spec.Health, Maximum = spec.Health });
      frame.Add(entity, new AaiVisibility { SightTenths = spec.SightTenths });
      if ((int)kind < 13) {
        frame.Add(entity, new AaiUnit { State = AaiUnitState.Idle });
        frame.Add(entity, new AaiMovement {
          SpeedModifier = FP._1,
          Domain = (kind == AaiKind.Transport || kind == AaiKind.Warship) ? AaiNavDomain.Water : AaiNavDomain.Land
        });
        if (kind == AaiKind.Worker) frame.Add(entity, new AaiWorker());
        if (kind == AaiKind.Vanguard || kind == AaiKind.Pathfinder) frame.Add(entity, new AaiHero { Level = 1 });
        if (kind == AaiKind.Transport) frame.Add(entity, new AaiTransport { Capacity = rules.TransportCapacity });
      } else {
        frame.Add(entity, new AaiBuilding { Construction = AaiConstructionState.Complete, Level = 1,
          PopulationContribution = kind == AaiKind.Headquarters ? rules.BasePopulation : kind == AaiKind.Farm ? rules.FarmPopulation : 0,
          IsDropOff = kind == AaiKind.Headquarters || kind == AaiKind.Settlement || kind == AaiKind.Farm && race == AaiRace.Dwarf });
        if (spec.Trainable.Length > 0) frame.Add(entity, new AaiProduction());
      }
      if (spec.Damage > 0) frame.Add(entity, new AaiCombat());
      foreach (var ability in rules.Abilities) {
        if (ability.Kind == kind && ability.Race == race) { frame.Add(entity, new AaiAbility()); break; }
      }
      return entity;
    }
  }
}
