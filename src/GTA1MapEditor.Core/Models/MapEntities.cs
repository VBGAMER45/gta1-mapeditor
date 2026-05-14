namespace GTA1MapEditor.Core.Models;

/// <summary>Static prop placed on the map (powerup, decoration, mission object).</summary>
public sealed class MapObject
{
    public ushort X { get; set; }
    public ushort Y { get; set; }
    public ushort Z { get; set; }
    public byte Type { get; set; }
    public byte Remap { get; set; }
    public ushort Rotation { get; set; }
    public ushort Pitch { get; set; }
    public ushort Roll { get; set; }
}

/// <summary>
/// Pre-placed vehicle. In the CMP these share the object_pos section with
/// MapObject; entries with Remap >= 128 are cars (Remap stored here is already
/// the raw byte minus 128).
/// </summary>
public sealed class CarPosition
{
    public ushort X { get; set; }
    public ushort Y { get; set; }
    public ushort Z { get; set; }
    public byte Type { get; set; }     // index into G24 car_info
    public byte Remap { get; set; }    // 0..MAX_CAR_REMAPS-1
    public ushort Rotation { get; set; }
}

public sealed class MapRoute
{
    /// <summary>255 = police patrol; 0..253 = roadblock junction ID.</summary>
    public byte Type { get; set; }
    public List<RouteVertex> Vertices { get; } = new();
}

public readonly record struct RouteVertex(byte X, byte Y, byte Z);

public sealed class NavSector
{
    public byte X { get; set; }
    public byte Y { get; set; }
    public byte W { get; set; }
    public byte H { get; set; }
    public byte Sam { get; set; }
    public string Name { get; set; } = string.Empty;
}

public enum SpawnLocationType
{
    Police,
    Hospital,
    Fire,
}

public sealed class SpawnLocation
{
    public byte X { get; set; }
    public byte Y { get; set; }
    public byte Z { get; set; }
    public SpawnLocationType Type { get; set; }
}
