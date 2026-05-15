namespace GTA1MapEditor.Rendering;

/// <summary>
/// The three logical tile sections inside a G24 file. CMP block bytes are
/// 1-based indices into one of these sections (lid byte → Lid, four wall
/// bytes → Side; Aux is used for animation frames, not block faces).
/// </summary>
public enum TileSection
{
    Side,
    Lid,
    Aux,
}
