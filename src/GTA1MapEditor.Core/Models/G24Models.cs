namespace GTA1MapEditor.Core.Models;

public sealed class G24Header
{
    public uint VersionCode { get; set; }
    public uint SideSize { get; set; }
    public uint LidSize { get; set; }
    public uint AuxSize { get; set; }
    public uint AnimSize { get; set; }
    public uint ClutSize { get; set; }
    public uint TileClutSize { get; set; }
    public uint SpriteClutSize { get; set; }
    public uint NewCarClutSize { get; set; }
    public uint FontClutSize { get; set; }
    public uint PaletteIndexSize { get; set; }
    public uint ObjectInfoSize { get; set; }
    public uint CarInfoSize { get; set; }
    public uint SpriteInfoSize { get; set; }
    public uint SpriteGraphicsSize { get; set; }
    public uint SpriteNumbersSize { get; set; }
}

/// <summary>
/// Byte offsets to every variable-length section of the G24, computed from
/// the header. Useful both for parsing and for tools that want to dump
/// individual sections.
/// </summary>
public sealed record G24SectionOffsets(
    int Tiles,
    int Anim,
    int Clut,
    int PaletteIndex,
    int ObjectInfo,
    int CarInfo,
    int SpriteInfo,
    int SpriteGraphics,
    int SpriteNumbers
);

public sealed record G24DoorInfo(short Rpx, short Rpy, short Object, short Delta);

public sealed record G24HlsRemap(ushort H, ushort L, ushort S);

public sealed class G24SpriteInfo
{
    public byte Width { get; set; }
    public byte Height { get; set; }
    public byte DeltaCount { get; set; }
    public ushort Size { get; set; }
    public ushort Clut { get; set; }
    public byte PageOffsetX { get; set; }
    public byte PageOffsetY { get; set; }
    public ushort PageNumber { get; set; }
    public List<G24SpriteDelta> Deltas { get; } = new();
}

public sealed record G24SpriteDelta(ushort Size, uint Offset);

public sealed class G24ObjectInfo
{
    public short Width { get; set; }
    public short Height { get; set; }
    public short Depth { get; set; }
    public ushort BaseSprite { get; set; }
    public ushort Weight { get; set; }
    public ushort Aux { get; set; }
    public sbyte Status { get; set; }
    public byte NumInto { get; set; }
    public List<ushort> Into { get; } = new();
}

public sealed class G24AnimInfo
{
    public byte Block { get; set; }
    public byte Which { get; set; } // 0 = side tile, 1 = lid tile
    public byte Speed { get; set; }
    public byte FrameCount { get; set; }
    public List<byte> Frames { get; } = new();
}

public sealed class G24CarInfo
{
    public short Width { get; set; }
    public short Height { get; set; }
    public short Depth { get; set; }
    public short SpriteNum { get; set; }

    public short Weight { get; set; }
    public short MaxSpeed { get; set; }
    public short MinSpeed { get; set; }
    public short Acceleration { get; set; }
    public short Braking { get; set; }
    public short Grip { get; set; }
    public short Handling { get; set; }

    public List<G24HlsRemap> Remaps { get; } = new();

    public byte VehicleType { get; set; }
    public byte ModelId { get; set; }
    public byte Turning { get; set; }
    public byte Damagable { get; set; }
    public short[] Value { get; } = new short[4];

    public sbyte Cx { get; set; }
    public sbyte Cy { get; set; }
    public int Moment { get; set; }

    public float Mass { get; set; }
    public float Thrust { get; set; }
    public float TyreAdhesionX { get; set; }
    public float TyreAdhesionY { get; set; }
    public float HandbrakeFriction { get; set; }
    public float FootbrakeFriction { get; set; }
    public float FrontBrakeBias { get; set; }

    public short TurnRatio { get; set; }
    public short DriveWheelOffset { get; set; }
    public short SteeringWheelOffset { get; set; }

    public float BackEndSlideValue { get; set; }
    public float HandbrakeSlideValue { get; set; }

    public bool Convertible { get; set; }
    public bool ExtraDrivingAnim { get; set; }

    public byte Engine { get; set; }
    public byte Radio { get; set; }
    public byte Horn { get; set; }
    public byte SoundFunction { get; set; }
    public byte FastChangeFlag { get; set; }

    public List<G24DoorInfo> Doors { get; } = new();
}
