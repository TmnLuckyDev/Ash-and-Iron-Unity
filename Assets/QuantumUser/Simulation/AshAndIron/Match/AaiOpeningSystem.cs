namespace Quantum {
  /// <summary>Prototype bootstrap for two configured seats. Lobby/AI registration follows in the full match port.</summary>
  public unsafe class AaiOpeningSystem : SystemMainThread {
    public override void OnInit(Frame frame) {
      var rules = frame.FindAsset(frame.RuntimeConfig.AshRules);
      var map = frame.FindAsset(frame.RuntimeConfig.AshMap);
      if (rules == null || map == null) throw new System.InvalidOperationException("Set AshRules and AshMap in RuntimeConfig.");
      AaiPrototypeSpawner.CreateOpening(frame, rules, map, frame.RuntimeConfig.AshSeatOneRace,
        frame.RuntimeConfig.AshSeatTwoRace, (PlayerRef)0, (PlayerRef)1, frame.RuntimeConfig.AshSeatTwoComputer);
    }

    public override void Update(Frame frame) {
      var entity = frame.Global->AaiMatchEntity;
      var match = frame.Get<AaiMatch>(entity);
      if (match.Result != AaiResult.Playing) return;
      match.ElapsedTicks++;
      frame.Set(entity, match);
    }
  }
}
