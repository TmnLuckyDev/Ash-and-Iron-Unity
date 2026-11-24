// Reference view code for phase 2. Input router calls these methods after it has
// classified tap/drag/pinch and checked the UI. Do not add as a per-unit Update.
using System;
using System.Collections.Generic;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

namespace AshAndIron.View {
  public sealed class ArmyInputBridge : MonoBehaviour {
    [SerializeField] private Camera battleCamera;
    [SerializeField] private LayerMask selectableLayers;
    private readonly HashSet<EntityRef> selection = new HashSet<EntityRef>();

    // Call only after local visibility/ownership screening against the verified
    // frame. The simulation must independently validate every command.
    public void SelectAt(Vector2 screenPosition, Func<EntityRef, bool> canSelect) {
      selection.Clear();
      if (!Physics.Raycast(battleCamera.ScreenPointToRay(screenPosition), out var hit, 10000, selectableLayers)) return;
      var view = hit.collider.GetComponentInParent<QuantumEntityView>();
      if (view != null && canSelect(view.EntityRef)) selection.Add(view.EntityRef);
    }

    // A chosen group is an existing simulation group, never an implicit list of
    // arbitrary selected enemies. For ad hoc selections, use a separate bounded
    // entity-list command with ownership checks.
    public void MoveSelectedGroup(Vector2 screenPosition, int groupId, AaiFormation formation) {
      var ray = battleCamera.ScreenPointToRay(screenPosition);
      if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance)) return;
      Vector3 point = ray.GetPoint(distance);
      // View-space float input is quantized once. Only the resulting raw values
      // travel in the deterministic command. No simulation-side float parsing.
      long x = checked((long)Math.Round(point.x * 65536.0, MidpointRounding.AwayFromZero));
      long y = checked((long)Math.Round(point.z * 65536.0, MidpointRounding.AwayFromZero));
      var runner = QuantumRunner.Default;
      if (runner == null || runner.Game == null) return;
      var result = runner.Game.AddCommand(new AaiMoveGroupCommand {
        GroupId = groupId, XRaw = x, YRaw = y, Formation = (int)formation
      });
      Debug.Log(result); // Replace with compact action feedback in production.
    }
  }
}
