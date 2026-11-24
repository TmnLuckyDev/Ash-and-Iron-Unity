using Photon.Deterministic;

namespace Quantum {
  public sealed class AaiMoveUnitCommand : DeterministicCommand {
    public EntityRef Unit;
    public long XRaw;
    public long YRaw;
    public bool Stop;

    public override void Serialize(BitStream stream) {
      stream.Serialize(ref Unit.Index);
      stream.Serialize(ref Unit.Version);
      stream.Serialize(ref XRaw);
      stream.Serialize(ref YRaw);
      stream.Serialize(ref Stop);
    }
  }
}
