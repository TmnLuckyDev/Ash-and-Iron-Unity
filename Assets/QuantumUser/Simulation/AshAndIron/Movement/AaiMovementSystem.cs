using Photon.Deterministic;

namespace Quantum {
  /// <summary>Fixed-point straight-line prototype; terrain stops movement. Grid pathfinding and collision are the next core milestone.</summary>
  public unsafe class AaiMovementSystem : SystemMainThread {
    public override void Update(Frame frame) {
      if (!frame.TryGet<AaiMatch>(frame.Global->AaiMatchEntity, out var match) || match.Result != AaiResult.Playing) return;
      var rules = frame.FindAsset(match.Rules);
      var map = frame.FindAsset(match.Map);
      if (rules == null || map == null) return;
      foreach (var (entity, movement) in frame.GetComponentIterator<AaiMovement>()) {
        if (!movement.HasDestination || !frame.TryGet<AaiIdentity>(entity, out var identity) ||
            !frame.TryGet<Transform2D>(entity, out var transform) || !frame.TryGet<AaiUnit>(entity, out var unit)) continue;
        var next = movement;
        var delta = movement.Destination - transform.Position;
        var stride = rules.Entity(identity.Race, identity.Kind).Speed * movement.SpeedModifier * frame.DeltaTime;
        if (stride <= FP._0) continue;
        bool reached = delta.SqrMagnitude <= stride * stride;
        var candidate = reached ? movement.Destination : transform.Position + delta.Normalized * stride;
        long tileRaw = (long)map.TileSize * 65536;
        bool allowed = candidate.X.RawValue >= 0 && candidate.Y.RawValue >= 0 &&
          candidate.X.RawValue < map.Width * tileRaw && candidate.Y.RawValue < map.Height * tileRaw &&
          AaiCommandSystem.CanEnter(map.Tile((int)(candidate.X.RawValue / tileRaw), (int)(candidate.Y.RawValue / tileRaw)), movement.Domain);
        if (allowed) {
          transform.Position = candidate;
          frame.Set(entity, transform);
        }
        if (!allowed || reached) {
          next.HasDestination = false;
          unit.State = AaiUnitState.Idle;
          unit.Order = default;
          frame.Set(entity, next);
          frame.Set(entity, unit);
        }
      }
    }
  }
}
