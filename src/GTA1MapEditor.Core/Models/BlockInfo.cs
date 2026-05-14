namespace GTA1MapEditor.Core.Models;

/// <summary>
/// One CMP block record (8 bytes). Multiple tiles in a column index into a shared
/// pool of blocks, so the same instance can be referenced from many positions —
/// the editor must clone-on-write before mutating a single tile in isolation.
/// </summary>
public sealed class BlockInfo
{
    /// <summary>Navigation flags, block type, slope, rotation.</summary>
    public ushort TypeMap { get; set; }

    /// <summary>Traffic lights, railway, remap, flip.</summary>
    public byte TypeMapExt { get; set; }

    public byte Left { get; set; }   // west wall texture
    public byte Right { get; set; }  // east wall texture
    public byte Top { get; set; }    // north wall texture
    public byte Bottom { get; set; } // south wall texture
    public byte Lid { get; set; }    // roof texture

    // Decoded TypeMap fields -------------------------------------------------

    public BlockType BlockType
    {
        get => (BlockType)((TypeMap & BlockFlagsMasks.BlockTypeMask) >> BlockFlagsMasks.BlockTypeShift);
        set => TypeMap = (ushort)((TypeMap & ~BlockFlagsMasks.BlockTypeMask)
            | ((((int)value) << BlockFlagsMasks.BlockTypeShift) & BlockFlagsMasks.BlockTypeMask));
    }

    public byte SlopeType
    {
        get => (byte)((TypeMap & BlockFlagsMasks.SlopeTypeMask) >> BlockFlagsMasks.SlopeTypeShift);
        set => TypeMap = (ushort)((TypeMap & ~BlockFlagsMasks.SlopeTypeMask)
            | ((value << BlockFlagsMasks.SlopeTypeShift) & BlockFlagsMasks.SlopeTypeMask));
    }

    public BlockRotation Rotation
    {
        get => (BlockRotation)((TypeMap & BlockFlagsMasks.RotationMask) >> BlockFlagsMasks.RotationShift);
        set => TypeMap = (ushort)((TypeMap & ~BlockFlagsMasks.RotationMask)
            | ((((int)value) << BlockFlagsMasks.RotationShift) & BlockFlagsMasks.RotationMask));
    }

    public bool IsFlat
    {
        get => (TypeMap & BlockFlagsMasks.IsFlat) != 0;
        set => TypeMap = (ushort)(value ? TypeMap | BlockFlagsMasks.IsFlat : TypeMap & ~BlockFlagsMasks.IsFlat);
    }

    public bool UpOk    { get => (TypeMap & BlockFlagsMasks.UpOk)    != 0; set => SetTm(BlockFlagsMasks.UpOk, value); }
    public bool DownOk  { get => (TypeMap & BlockFlagsMasks.DownOk)  != 0; set => SetTm(BlockFlagsMasks.DownOk, value); }
    public bool LeftOk  { get => (TypeMap & BlockFlagsMasks.LeftOk)  != 0; set => SetTm(BlockFlagsMasks.LeftOk, value); }
    public bool RightOk { get => (TypeMap & BlockFlagsMasks.RightOk) != 0; set => SetTm(BlockFlagsMasks.RightOk, value); }

    // Decoded TypeMapExt fields ---------------------------------------------

    public bool TrafficLights { get => (TypeMapExt & BlockExtFlagsMasks.TrafficLights) != 0; set => SetExt(BlockExtFlagsMasks.TrafficLights, value); }
    public bool FlipLeftRight { get => (TypeMapExt & BlockExtFlagsMasks.FlipLR)        != 0; set => SetExt(BlockExtFlagsMasks.FlipLR, value); }
    public bool Railway       { get => (TypeMapExt & BlockExtFlagsMasks.Railway)       != 0; set => SetExt(BlockExtFlagsMasks.Railway, value); }
    public bool RailStartTurn { get => (TypeMapExt & BlockExtFlagsMasks.RailStartTurn) != 0; set => SetExt(BlockExtFlagsMasks.RailStartTurn, value); }
    public bool RailEndTurn   { get => (TypeMapExt & BlockExtFlagsMasks.RailEndTurn)   != 0; set => SetExt(BlockExtFlagsMasks.RailEndTurn, value); }

    public byte RemapIndex
    {
        get => (byte)((TypeMapExt & BlockExtFlagsMasks.RemapMask) >> BlockExtFlagsMasks.RemapShift);
        set => TypeMapExt = (byte)((TypeMapExt & ~BlockExtFlagsMasks.RemapMask)
            | ((value << BlockExtFlagsMasks.RemapShift) & BlockExtFlagsMasks.RemapMask));
    }

    public BlockInfo Clone() => new()
    {
        TypeMap = TypeMap, TypeMapExt = TypeMapExt,
        Left = Left, Right = Right, Top = Top, Bottom = Bottom, Lid = Lid,
    };

    private void SetTm(ushort mask, bool on) => TypeMap = (ushort)(on ? TypeMap | mask : TypeMap & ~mask);
    private void SetExt(byte mask, bool on) => TypeMapExt = (byte)(on ? TypeMapExt | mask : TypeMapExt & ~mask);
}
