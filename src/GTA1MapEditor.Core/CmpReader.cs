using System.Buffers.Binary;
using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.Core;

/// <summary>
/// Parses a GTA1 .CMP file into a <see cref="CmpMap"/>. Ported from
/// OpenGTA's read_cmp.cpp via the project's TS reference reader.
/// </summary>
public static class CmpReader
{
    private const int HeaderSize = 28;
    private const int BaseSize = GameConfig.MapWidth * GameConfig.MapHeight * sizeof(uint);
    private const int BlockRecordSize = 8;
    private const int ObjectRecordSize = 14;
    private const int NavSectorSize = 35;

    public static CmpMap ReadFile(string path) => Read(File.ReadAllBytes(path));

    public static CmpMap Read(byte[] data)
    {
        var span = new ReadOnlySpan<byte>(data);
        int offset = 0;

        // 1. Header --------------------------------------------------------
        var header = new CmpHeader
        {
            Version       = BinaryPrimitives.ReadUInt32LittleEndian(span[0..]),
            StyleNumber   = span[4],
            SampleNumber  = span[5],
            // bytes 6-7 reserved
            RouteSize     = BinaryPrimitives.ReadUInt32LittleEndian(span[8..]),
            ObjectPosSize = BinaryPrimitives.ReadUInt32LittleEndian(span[12..]),
            ColumnSize    = BinaryPrimitives.ReadUInt32LittleEndian(span[16..]),
            BlockSize     = BinaryPrimitives.ReadUInt32LittleEndian(span[20..]),
            NavDataSize   = BinaryPrimitives.ReadUInt32LittleEndian(span[24..]),
        };
        offset = HeaderSize;

        // 2. Base (256x256 uint32) -----------------------------------------
        var baseArr = new uint[GameConfig.MapWidth * GameConfig.MapHeight];
        for (int i = 0; i < baseArr.Length; i++)
        {
            baseArr[i] = BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);
            offset += 4;
        }

        // 3. Columns -------------------------------------------------------
        int columnCount = (int)(header.ColumnSize / 2);
        var columns = new ushort[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            columns[i] = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]);
            offset += 2;
        }

        // 4. Blocks --------------------------------------------------------
        int blockCount = (int)(header.BlockSize / BlockRecordSize);
        var blocks = new List<BlockInfo>(blockCount);
        for (int i = 0; i < blockCount; i++)
        {
            blocks.Add(new BlockInfo
            {
                TypeMap    = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]),
                TypeMapExt = span[offset + 2],
                Left       = span[offset + 3],
                Right      = span[offset + 4],
                Top        = span[offset + 5],
                Bottom     = span[offset + 6],
                Lid        = span[offset + 7],
            });
            offset += BlockRecordSize;
        }

        var map = new CmpMap
        {
            Header  = header,
            Base    = baseArr,
            Columns = columns,
            Blocks  = blocks,
        };

        // 5. Objects + car positions (Remap >= 128 → car) -------------------
        int objectCount = (int)(header.ObjectPosSize / ObjectRecordSize);
        for (int i = 0; i < objectCount; i++)
        {
            ushort x = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]);
            ushort y = BinaryPrimitives.ReadUInt16LittleEndian(span[(offset + 2)..]);
            ushort z = BinaryPrimitives.ReadUInt16LittleEndian(span[(offset + 4)..]);
            byte type = span[offset + 6];
            byte remap = span[offset + 7];
            ushort rotation = BinaryPrimitives.ReadUInt16LittleEndian(span[(offset + 8)..]);
            ushort pitch = BinaryPrimitives.ReadUInt16LittleEndian(span[(offset + 10)..]);
            ushort roll = BinaryPrimitives.ReadUInt16LittleEndian(span[(offset + 12)..]);
            offset += ObjectRecordSize;

            if (remap >= 128)
            {
                map.CarPositions.Add(new CarPosition
                {
                    X = x, Y = y, Z = z, Type = type,
                    Remap = (byte)(remap - 128),
                    Rotation = rotation,
                });
            }
            else
            {
                map.Objects.Add(new MapObject
                {
                    X = x, Y = y, Z = z, Type = type, Remap = remap,
                    Rotation = rotation, Pitch = pitch, Roll = roll,
                });
            }
        }

        // 6. Routes --------------------------------------------------------
        int routeEnd = offset + (int)header.RouteSize;
        while (offset < routeEnd)
        {
            int numVertices = span[offset]; offset += 1;
            byte routeType = span[offset]; offset += 1;
            if (numVertices == 0 || numVertices > 50 || offset + numVertices * 3 > routeEnd)
            {
                offset = routeEnd;
                break;
            }
            var route = new MapRoute { Type = routeType };
            for (int v = 0; v < numVertices; v++)
            {
                route.Vertices.Add(new RouteVertex(span[offset], span[offset + 1], span[offset + 2]));
                offset += 3;
            }
            map.Routes.Add(route);
        }

        // 7. Spawn locations (36 entries × 3 bytes = 108 bytes) -------------
        // Group order per CDS: police, hospital, unused, unused, fire, unused.
        var groupTypes = new SpawnLocationType?[]
        {
            SpawnLocationType.Police,
            SpawnLocationType.Hospital,
            null, null,
            SpawnLocationType.Fire,
            null,
        };
        for (int group = 0; group < 6; group++)
        {
            var gtype = groupTypes[group];
            for (int i = 0; i < 6; i++)
            {
                byte lx = span[offset];
                byte ly = span[offset + 1];
                byte lz = span[offset + 2];
                offset += 3;
                if (lx == 0 && ly == 0 && lz == 0) continue;
                if (gtype is null) continue;
                map.SpawnLocations.Add(new SpawnLocation { X = lx, Y = ly, Z = lz, Type = gtype.Value });
            }
        }

        // 8. Nav data (35 bytes per sector) --------------------------------
        int navCount = (int)(header.NavDataSize / NavSectorSize);
        for (int i = 0; i < navCount; i++)
        {
            var sector = new NavSector
            {
                X = span[offset],
                Y = span[offset + 1],
                W = span[offset + 2],
                H = span[offset + 3],
                Sam = span[offset + 4],
            };
            int nameEnd = 30;
            for (int j = 0; j < 30; j++)
            {
                if (span[offset + 5 + j] == 0) { nameEnd = j; break; }
            }
            sector.Name = System.Text.Encoding.ASCII.GetString(span.Slice(offset + 5, nameEnd));
            map.NavSectors.Add(sector);
            offset += NavSectorSize;
        }

        return map;
    }
}
