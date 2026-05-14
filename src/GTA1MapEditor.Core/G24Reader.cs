using System.Buffers.Binary;
using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.Core;

/// <summary>
/// Parses a GTA1 .G24 (24-bit paged) style file. Ported from Carnage3D's
/// StyleData.cpp via the project's TS reference reader.
/// </summary>
public static class G24Reader
{
    private const int HeaderSize = 64;
    private const int TileBytes = 4096;     // 64×64 indexed
    private const int ClutSize = 1024;      // 256 entries × 4 bytes
    private const int MaxCarRemaps = 12;

    public static G24StyleData ReadFile(string path) => Read(File.ReadAllBytes(path));

    public static G24StyleData Read(byte[] data)
    {
        var span = new ReadOnlySpan<byte>(data);
        var header = ReadHeader(span);
        var offsets = ComputeOffsets(header);

        return new G24StyleData
        {
            Header = header,
            Offsets = offsets,
            TileData = ExtractRange(data, offsets.Tiles, (int)(header.SideSize + header.LidSize + header.AuxSize)),
            Anims = ReadAnims(span, header, offsets),
            PaletteData = ExtractPaged(data, offsets.Clut, header.ClutSize),
            PaletteIndices = ReadPaletteIndices(span, header, offsets),
            Objects = ReadObjectInfo(span, header, offsets),
            Cars = ReadCarInfo(span, header, offsets),
            Sprites = ReadSpriteInfo(span, header, offsets),
            SpriteGraphics = ExtractRange(data, offsets.SpriteGraphics, (int)header.SpriteGraphicsSize),
            SpriteNumbers = ReadSpriteNumbers(span, header, offsets),
        };
    }

    // ─── Header / offsets ────────────────────────────────────────────────

    public static G24Header ReadHeader(ReadOnlySpan<byte> span) => new()
    {
        VersionCode        = BinaryPrimitives.ReadUInt32LittleEndian(span[0x00..]),
        SideSize           = BinaryPrimitives.ReadUInt32LittleEndian(span[0x04..]),
        LidSize            = BinaryPrimitives.ReadUInt32LittleEndian(span[0x08..]),
        AuxSize            = BinaryPrimitives.ReadUInt32LittleEndian(span[0x0C..]),
        AnimSize           = BinaryPrimitives.ReadUInt32LittleEndian(span[0x10..]),
        ClutSize           = BinaryPrimitives.ReadUInt32LittleEndian(span[0x14..]),
        TileClutSize       = BinaryPrimitives.ReadUInt32LittleEndian(span[0x18..]),
        SpriteClutSize     = BinaryPrimitives.ReadUInt32LittleEndian(span[0x1C..]),
        NewCarClutSize     = BinaryPrimitives.ReadUInt32LittleEndian(span[0x20..]),
        FontClutSize       = BinaryPrimitives.ReadUInt32LittleEndian(span[0x24..]),
        PaletteIndexSize   = BinaryPrimitives.ReadUInt32LittleEndian(span[0x28..]),
        ObjectInfoSize     = BinaryPrimitives.ReadUInt32LittleEndian(span[0x2C..]),
        CarInfoSize        = BinaryPrimitives.ReadUInt32LittleEndian(span[0x30..]),
        SpriteInfoSize     = BinaryPrimitives.ReadUInt32LittleEndian(span[0x34..]),
        SpriteGraphicsSize = BinaryPrimitives.ReadUInt32LittleEndian(span[0x38..]),
        SpriteNumbersSize  = BinaryPrimitives.ReadUInt32LittleEndian(span[0x3C..]),
    };

    public static G24SectionOffsets ComputeOffsets(G24Header h)
    {
        // Tile data: side + lid + aux, padded to a 4-block boundary.
        int totalBlocks = (int)((h.SideSize + h.LidSize + h.AuxSize) / TileBytes);
        int extra = (4 - (totalBlocks % 4)) % 4;
        int tileDataSize = (totalBlocks + extra) * TileBytes;

        int tiles = HeaderSize;
        int anim = tiles + tileDataSize;
        int clut = anim + (int)h.AnimSize;

        int clutsRounded = RoundUpTo((int)h.ClutSize, Palette.ClutPageSize);
        int paletteIndex = clut + clutsRounded;
        int objectInfo = paletteIndex + (int)h.PaletteIndexSize;
        int carInfo = objectInfo + (int)h.ObjectInfoSize;
        int spriteInfo = carInfo + (int)h.CarInfoSize;
        int spriteGraphics = spriteInfo + (int)h.SpriteInfoSize;
        int spriteNumbers = spriteGraphics + (int)h.SpriteGraphicsSize;

        return new G24SectionOffsets(tiles, anim, clut, paletteIndex,
            objectInfo, carInfo, spriteInfo, spriteGraphics, spriteNumbers);
    }

    // ─── Section readers ─────────────────────────────────────────────────

    private static List<G24AnimInfo> ReadAnims(ReadOnlySpan<byte> span, G24Header h, G24SectionOffsets o)
    {
        var result = new List<G24AnimInfo>();
        if (h.AnimSize < 1) return result;

        int pos = o.Anim;
        int end = pos + (int)h.AnimSize;
        byte numAnims = span[pos]; pos += 1;

        for (int i = 0; i < numAnims && pos < end; i++)
        {
            var anim = new G24AnimInfo
            {
                Block = span[pos],
                Which = span[pos + 1],
                Speed = span[pos + 2],
                FrameCount = span[pos + 3],
            };
            pos += 4;
            for (int f = 0; f < anim.FrameCount && pos < end; f++)
                anim.Frames.Add(span[pos++]);
            result.Add(anim);
        }
        return result;
    }

    private static ushort[] ReadPaletteIndices(ReadOnlySpan<byte> span, G24Header h, G24SectionOffsets o)
    {
        int count = (int)(h.PaletteIndexSize / 2);
        var indices = new ushort[count];
        for (int i = 0; i < count; i++)
            indices[i] = BinaryPrimitives.ReadUInt16LittleEndian(span[(o.PaletteIndex + i * 2)..]);
        return indices;
    }

    private static List<G24ObjectInfo> ReadObjectInfo(ReadOnlySpan<byte> span, G24Header h, G24SectionOffsets o)
    {
        var result = new List<G24ObjectInfo>();
        int pos = o.ObjectInfo;
        int end = pos + (int)h.ObjectInfoSize;
        while (pos + 14 <= end)
        {
            var obj = new G24ObjectInfo
            {
                Width = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]),
                Height = BinaryPrimitives.ReadInt16LittleEndian(span[(pos + 2)..]),
                Depth = BinaryPrimitives.ReadInt16LittleEndian(span[(pos + 4)..]),
                BaseSprite = BinaryPrimitives.ReadUInt16LittleEndian(span[(pos + 6)..]),
                Weight = BinaryPrimitives.ReadUInt16LittleEndian(span[(pos + 8)..]),
                Aux = BinaryPrimitives.ReadUInt16LittleEndian(span[(pos + 10)..]),
                Status = (sbyte)span[pos + 12],
                NumInto = span[pos + 13],
            };
            pos += 14;
            for (int i = 0; i < obj.NumInto && pos + 2 <= end; i++)
            {
                obj.Into.Add(BinaryPrimitives.ReadUInt16LittleEndian(span[pos..]));
                pos += 2;
            }
            result.Add(obj);
        }
        return result;
    }

    private static List<G24CarInfo> ReadCarInfo(ReadOnlySpan<byte> span, G24Header h, G24SectionOffsets o)
    {
        var result = new List<G24CarInfo>();
        int pos = o.CarInfo;
        int end = pos + (int)h.CarInfoSize;

        while (pos < end)
        {
            int start = pos;
            var car = new G24CarInfo();

            car.Width = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.Height = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.Depth = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.SpriteNum = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.Weight = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.MaxSpeed = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.MinSpeed = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.Acceleration = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.Braking = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.Grip = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.Handling = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;

            for (int i = 0; i < MaxCarRemaps; i++)
            {
                ushort hh = BinaryPrimitives.ReadUInt16LittleEndian(span[pos..]); pos += 2;
                ushort ll = BinaryPrimitives.ReadUInt16LittleEndian(span[pos..]); pos += 2;
                ushort ss = BinaryPrimitives.ReadUInt16LittleEndian(span[pos..]); pos += 2;
                car.Remaps.Add(new G24HlsRemap(hh, ll, ss));
            }
            pos += MaxCarRemaps; // skip 8-bit remap indices

            car.VehicleType = span[pos++];
            car.ModelId = span[pos++];
            car.Turning = span[pos++];
            car.Damagable = span[pos++];

            for (int i = 0; i < 4; i++)
            {
                car.Value[i] = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]);
                pos += 2;
            }

            car.Cx = (sbyte)span[pos++];
            car.Cy = (sbyte)span[pos++];

            car.Moment = BinaryPrimitives.ReadInt32LittleEndian(span[pos..]); pos += 4;
            car.Mass             = ReadFixed32(span[pos..]); pos += 4;
            car.Thrust           = ReadFixed32(span[pos..]); pos += 4;
            car.TyreAdhesionX    = ReadFixed32(span[pos..]); pos += 4;
            car.TyreAdhesionY    = ReadFixed32(span[pos..]); pos += 4;
            car.HandbrakeFriction = ReadFixed32(span[pos..]); pos += 4;
            car.FootbrakeFriction = ReadFixed32(span[pos..]); pos += 4;
            car.FrontBrakeBias   = ReadFixed32(span[pos..]); pos += 4;

            car.TurnRatio           = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.DriveWheelOffset    = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            car.SteeringWheelOffset = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;

            car.BackEndSlideValue  = ReadFixed32(span[pos..]); pos += 4;
            car.HandbrakeSlideValue = ReadFixed32(span[pos..]); pos += 4;

            byte flags = span[pos++];
            car.Convertible = (flags & 1) != 0;
            car.ExtraDrivingAnim = (flags & 2) != 0;

            car.Engine        = span[pos++];
            car.Radio         = span[pos++];
            car.Horn          = span[pos++];
            car.SoundFunction = span[pos++];
            car.FastChangeFlag = span[pos++];

            short doorCount = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
            for (int i = 0; i < doorCount; i++)
            {
                short rpy = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
                short rpx = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
                short obj = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
                short del = BinaryPrimitives.ReadInt16LittleEndian(span[pos..]); pos += 2;
                car.Doors.Add(new G24DoorInfo(rpx, rpy, obj, del));
            }

            if (pos <= start) break; // safety
            result.Add(car);
        }
        return result;
    }

    private static List<G24SpriteInfo> ReadSpriteInfo(ReadOnlySpan<byte> span, G24Header h, G24SectionOffsets o)
    {
        var result = new List<G24SpriteInfo>();
        int pos = o.SpriteInfo;
        int end = pos + (int)h.SpriteInfoSize;

        while (pos < end)
        {
            var sprite = new G24SpriteInfo
            {
                Width = span[pos],
                Height = span[pos + 1],
                DeltaCount = span[pos + 2],
                // pos + 3 is a waste byte
                Size = BinaryPrimitives.ReadUInt16LittleEndian(span[(pos + 4)..]),
                Clut = BinaryPrimitives.ReadUInt16LittleEndian(span[(pos + 6)..]),
                PageOffsetX = span[pos + 8],
                PageOffsetY = span[pos + 9],
                PageNumber = BinaryPrimitives.ReadUInt16LittleEndian(span[(pos + 10)..]),
            };
            pos += 12;
            for (int d = 0; d < sprite.DeltaCount && pos + 6 <= end; d++)
            {
                ushort dSize = BinaryPrimitives.ReadUInt16LittleEndian(span[pos..]);
                uint dOffset = BinaryPrimitives.ReadUInt32LittleEndian(span[(pos + 2)..]);
                sprite.Deltas.Add(new G24SpriteDelta(dSize, dOffset));
                pos += 6;
            }
            result.Add(sprite);
        }
        return result;
    }

    private static ushort[] ReadSpriteNumbers(ReadOnlySpan<byte> span, G24Header h, G24SectionOffsets o)
    {
        // Carnage3D treats these as uint16, regardless of what the CDS guide says.
        int count = (int)(h.SpriteNumbersSize / 2);
        var arr = new ushort[count];
        for (int i = 0; i < count; i++)
            arr[i] = BinaryPrimitives.ReadUInt16LittleEndian(span[(o.SpriteNumbers + i * 2)..]);
        return arr;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static float ReadFixed32(ReadOnlySpan<byte> span) =>
        BinaryPrimitives.ReadInt32LittleEndian(span) / 65536f;

    private static int RoundUpTo(int value, int n) => ((value + n - 1) / n) * n;

    /// <summary>Slice <paramref name="data"/> [offset, offset+length) into a fresh array.</summary>
    private static byte[] ExtractRange(byte[] data, int offset, int length)
    {
        var arr = new byte[length];
        Buffer.BlockCopy(data, offset, arr, 0, length);
        return arr;
    }

    /// <summary>Extract a CLUT region padded to a multiple of 64 KiB.</summary>
    private static byte[] ExtractPaged(byte[] data, int offset, uint clutSize)
    {
        int rounded = RoundUpTo((int)clutSize, Palette.ClutPageSize);
        return ExtractRange(data, offset, rounded);
    }
}
