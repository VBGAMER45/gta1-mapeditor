namespace GTA1MapEditor.Core;

public static class GameConfig
{
    public const int TileSize = 64;
    public const int MapWidth = 256;
    public const int MapHeight = 256;
    public const int MaxBlockHeight = 6;
}

/// <summary>Bit layout of the 16-bit BlockInfo.TypeMap field.</summary>
public static class BlockFlagsMasks
{
    public const ushort UpOk           = 0x0001;
    public const ushort DownOk         = 0x0002;
    public const ushort LeftOk         = 0x0004;
    public const ushort RightOk        = 0x0008;
    public const ushort BlockTypeMask  = 0x0070;
    public const int    BlockTypeShift = 4;
    public const ushort IsFlat         = 0x0080;
    public const ushort SlopeTypeMask  = 0x3F00;
    public const int    SlopeTypeShift = 8;
    public const ushort RotationMask   = 0xC000;
    public const int    RotationShift  = 14;
}

/// <summary>Bit layout of the 8-bit BlockInfo.TypeMapExt field.</summary>
public static class BlockExtFlagsMasks
{
    public const byte TrafficLights = 0x01;
    public const byte RailEndTurn   = 0x02;
    public const byte RailStartTurn = 0x04;
    public const byte RemapMask     = 0x30;
    public const int  RemapShift    = 4;
    public const byte FlipLR        = 0x40;
    public const byte Railway       = 0x80;
}

/// <summary>BLOCK_TYPE values stored in bits 4-6 of TypeMap.</summary>
public enum BlockType : byte
{
    Air      = 0,
    Water    = 1,
    Road     = 2,
    Pavement = 3,
    Field    = 4,
    Building = 5,
}

/// <summary>Rotation in 90° steps stored in top 2 bits of TypeMap.</summary>
public enum BlockRotation : byte
{
    None   = 0,
    Cw90   = 1,
    Cw180  = 2,
    Cw270  = 3,
}

/// <summary>Sprite categories — order matches the 21 spriteNumbers entries in a G24.</summary>
public enum SpriteCategory
{
    Arrow = 0, Digit = 1, Boat = 2, Box = 3, Bus = 4, Car = 5, Object = 6,
    Ped = 7, Speedo = 8, Tank = 9, TrafficLight = 10, Train = 11, TrDoor = 12,
    Bike = 13, Tram = 14, WBus = 15, WCar = 16, Ex = 17, TumCar = 18, TumTruck = 19, Ferry = 20,
}

/// <summary>Vehicle class IDs stored in G24 car_info records.</summary>
public enum VehicleClass : byte
{
    Bus = 0,
    FrontJuggernaut = 1,
    BackJuggernaut = 2,
    Motorcycle = 3,
    StandardCar = 4,
    Train = 8,
    Tram = 9,
    Boat = 13,
    Tank = 14,
}
