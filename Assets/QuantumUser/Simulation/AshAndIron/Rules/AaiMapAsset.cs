using Photon.Deterministic;

namespace Quantum {
  // Static baked terrain. Match-specific changes must live in the frame.
  public partial class AaiMapAsset : AssetObject {
    public string MapId;
    public string DisplayName;
    public bool HasSeed;
    public uint Seed;
    public AaiMode Mode;
    public int Width, Height, TileSize;
    public AaiTerrain[] Tiles;
    public long SeatOneXRaw, SeatOneYRaw, SeatTwoXRaw, SeatTwoYRaw;
    public FPVector2 SeatOneBase => new FPVector2(FP.FromRaw(SeatOneXRaw), FP.FromRaw(SeatOneYRaw));
    public FPVector2 SeatTwoBase => new FPVector2(FP.FromRaw(SeatTwoXRaw), FP.FromRaw(SeatTwoYRaw));
    public AaiMapResourceSpec[] Sources;

    public AaiTerrain Tile(int x, int y) {
      return x < 0 || y < 0 || x >= Width || y >= Height ? AaiTerrain.Rock : Tiles[y * Width + x];
    }
  }

  [System.Serializable]
  public struct AaiMapResourceSpec {
    public int CountPerSide;
    public bool UnlimitedStock;
    public int Stock;
    public bool UnlimitedWorkers;
    public int MaxWorkers;
  }
}
