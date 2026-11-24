using Photon.Deterministic;

namespace Quantum {
  /// <summary>Phase-one command path. Formation, attack, construction and queue commands are not active yet.</summary>
  public unsafe class AaiCommandSystem : SystemMainThread {
    public override void Update(Frame frame) {
      if (!frame.TryGet<AaiMatch>(frame.Global->AaiMatchEntity, out var match) || match.Result != AaiResult.Playing) return;
      var map = frame.FindAsset(match.Map);
      if (map == null) return;
      for (int slot = 0; slot < frame.MaxPlayerCount; slot++) {
        for (int index = 0; index < frame.GetPlayerCommandCount(slot); index++) {
          if (frame.GetPlayerCommand(slot, index) is AaiMoveUnitCommand move) Apply(frame, map, (PlayerRef)slot, move);
        }
      }
    }

    public static bool Apply(Frame frame, AaiMapAsset map, PlayerRef issuer, AaiMoveUnitCommand command) {
      if (!frame.TryGet<AaiIdentity>(command.Unit, out var identity) ||
          !frame.TryGet<AaiPlayer>(identity.Owner, out var owner) || owner.IsComputer || owner.Player != issuer ||
          !frame.TryGet<AaiMovement>(command.Unit, out var movement) ||
          !frame.TryGet<AaiUnit>(command.Unit, out var unit) || frame.TryGet<AaiEmbarked>(command.Unit, out _)) return false;

      if (command.Stop) {
        movement.HasDestination = false;
        unit.State = AaiUnitState.Idle;
        unit.Order = new AaiOrderData { Kind = AaiOrderKind.Stop };
      } else {
        long tileRaw = (long)map.TileSize * 65536;
        if (command.XRaw < 0 || command.YRaw < 0 || command.XRaw >= map.Width * tileRaw || command.YRaw >= map.Height * tileRaw ||
            !CanEnter(map.Tile((int)(command.XRaw / tileRaw), (int)(command.YRaw / tileRaw)), movement.Domain)) return false;
        var destination = new FPVector2(FP.FromRaw(command.XRaw), FP.FromRaw(command.YRaw));
        movement.HasDestination = true;
        movement.Destination = destination;
        unit.State = AaiUnitState.Moving;
        unit.Order = new AaiOrderData { Kind = AaiOrderKind.Move, Destination = destination, HasDestination = true };
      }
      frame.Set(command.Unit, movement);
      frame.Set(command.Unit, unit);
      return true;
    }

    public static bool CanEnter(AaiTerrain tile, AaiNavDomain domain) => domain == AaiNavDomain.Water ? tile == AaiTerrain.Water :
      tile == AaiTerrain.Grass || tile == AaiTerrain.Road || tile == AaiTerrain.Bridge;
  }
}
