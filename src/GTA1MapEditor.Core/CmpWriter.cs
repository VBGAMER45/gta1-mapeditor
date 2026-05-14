using System.Buffers.Binary;
using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.Core;

/// <summary>
/// Serializes a <see cref="CmpMap"/> back to GTA1 .CMP bytes.
///
/// Strategy: emit the map's existing base/columns/blocks arrays unchanged.
/// Edits performed via clone-on-write append to those arrays without
/// recomputing the dedupe, so files can grow modestly across edits. A
/// future "Compact" pass can dedupe at save time.
/// </summary>
public static class CmpWriter
{
    public static void WriteFile(CmpMap map, string path) => File.WriteAllBytes(path, Write(map));

    public static byte[] Write(CmpMap map)
    {
        // Pre-compute section sizes from the in-memory state so the header
        // matches what we emit byte-for-byte.
        int objectPosBytes = (map.Objects.Count + map.CarPositions.Count) * 14;
        int routeBytes = 0;
        foreach (var r in map.Routes) routeBytes += 2 + r.Vertices.Count * 3;
        int navBytes = map.NavSectors.Count * 35;
        int columnBytes = map.Columns.Length * 2;
        int blockBytes = map.Blocks.Count * 8;
        int baseBytes = map.Base.Length * 4;

        int total =
            28 +                  // header
            baseBytes +
            columnBytes +
            blockBytes +
            objectPosBytes +
            routeBytes +
            108 +                 // spawn locations are fixed-size
            navBytes;

        var buf = new byte[total];
        var span = buf.AsSpan();
        int offset = 0;

        // 1. Header --------------------------------------------------------
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..], map.Header.Version);
        span[4] = map.Header.StyleNumber;
        span[5] = map.Header.SampleNumber;
        // bytes 6-7 reserved (0)
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)routeBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)objectPosBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], (uint)columnBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)blockBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], (uint)navBytes);
        offset = 28;

        // 2. Base ----------------------------------------------------------
        for (int i = 0; i < map.Base.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], map.Base[i]);
            offset += 4;
        }

        // 3. Columns -------------------------------------------------------
        for (int i = 0; i < map.Columns.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], map.Columns[i]);
            offset += 2;
        }

        // 4. Blocks --------------------------------------------------------
        foreach (var b in map.Blocks)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], b.TypeMap);
            span[offset + 2] = b.TypeMapExt;
            span[offset + 3] = b.Left;
            span[offset + 4] = b.Right;
            span[offset + 5] = b.Top;
            span[offset + 6] = b.Bottom;
            span[offset + 7] = b.Lid;
            offset += 8;
        }

        // 5. Objects + car positions ---------------------------------------
        foreach (var o in map.Objects)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], o.X);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 2)..], o.Y);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 4)..], o.Z);
            span[offset + 6] = o.Type;
            span[offset + 7] = o.Remap;
            BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 8)..], o.Rotation);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 10)..], o.Pitch);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 12)..], o.Roll);
            offset += 14;
        }
        foreach (var c in map.CarPositions)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], c.X);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 2)..], c.Y);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 4)..], c.Z);
            span[offset + 6] = c.Type;
            // Cars are stored with Remap + 128 to signal "car not object".
            span[offset + 7] = (byte)(c.Remap + 128);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 8)..], c.Rotation);
            // pitch/roll unused for cars
            offset += 14;
        }

        // 6. Routes --------------------------------------------------------
        foreach (var r in map.Routes)
        {
            span[offset] = (byte)r.Vertices.Count;
            span[offset + 1] = r.Type;
            offset += 2;
            foreach (var v in r.Vertices)
            {
                span[offset] = v.X;
                span[offset + 1] = v.Y;
                span[offset + 2] = v.Z;
                offset += 3;
            }
        }

        // 7. Spawn locations (always 108 bytes; 6 groups × 6 slots × 3) ----
        var slots = new (byte x, byte y, byte z)[6, 6];
        int[] groupCounts = new int[6];
        var groupForType = new Dictionary<SpawnLocationType, int>
        {
            [SpawnLocationType.Police] = 0,
            [SpawnLocationType.Hospital] = 1,
            // groups 2 and 3 are unused
            [SpawnLocationType.Fire] = 4,
            // group 5 is unused
        };
        foreach (var sp in map.SpawnLocations)
        {
            int g = groupForType[sp.Type];
            int slot = groupCounts[g];
            if (slot >= 6) continue; // engine only honours 6 of each
            slots[g, slot] = (sp.X, sp.Y, sp.Z);
            groupCounts[g] = slot + 1;
        }
        for (int g = 0; g < 6; g++)
        for (int s = 0; s < 6; s++)
        {
            span[offset] = slots[g, s].x;
            span[offset + 1] = slots[g, s].y;
            span[offset + 2] = slots[g, s].z;
            offset += 3;
        }

        // 8. Nav data ------------------------------------------------------
        foreach (var n in map.NavSectors)
        {
            span[offset] = n.X;
            span[offset + 1] = n.Y;
            span[offset + 2] = n.W;
            span[offset + 3] = n.H;
            span[offset + 4] = n.Sam;
            var nameBytes = System.Text.Encoding.ASCII.GetBytes(n.Name);
            int copy = Math.Min(nameBytes.Length, 30);
            nameBytes.AsSpan(0, copy).CopyTo(span.Slice(offset + 5, copy));
            for (int i = copy; i < 30; i++) span[offset + 5 + i] = 0;
            offset += 35;
        }

        if (offset != total)
            throw new InvalidOperationException($"CMP writer offset mismatch: wrote {offset}, expected {total}");

        return buf;
    }
}
