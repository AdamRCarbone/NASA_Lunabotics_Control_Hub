namespace NASA_Lunabotics_Control_Hub.Components;

public sealed class TerrainData
{
    public int Width      { get; init; }
    public int Height     { get; init; }
    public float CellMeters { get; init; }
    public float[] HeightMap { get; init; } = [];
    public byte[]  Rocks     { get; init; } = [];
    public byte[]  Craters   { get; init; } = [];
    public byte[]  Walls     { get; init; } = [];
}
