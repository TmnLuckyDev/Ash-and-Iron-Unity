// Reference implementation for phase 2. Copy into the simulation assembly only
// when the command system has been added and registered. SDK 3.1 API.
using Photon.Deterministic;

namespace Quantum {
  public sealed class AaiMoveGroupCommand : DeterministicCommand {
    public int GroupId;
    public long XRaw;
    public long YRaw;
    public int Formation;

    public override void Serialize(BitStream stream) {
      stream.Serialize(ref GroupId);
      stream.Serialize(ref XRaw);
      stream.Serialize(ref YRaw);
      stream.Serialize(ref Formation);
    }

    public FPVector2 Destination => new FPVector2(FP.FromRaw(XRaw), FP.FromRaw(YRaw));
  }
}
