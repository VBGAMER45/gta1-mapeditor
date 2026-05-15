using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Commands;
using GTA1MapEditor.Core.Models;
using GTA1MapEditor.Rendering;

namespace GTA1MapEditor.App;

public enum ToolMode
{
    Select,
    PaintLid,
    PlaceObject,
    PlaceCar,
    PlaceSpawn,
    Erase,
}

public enum ViewMode
{
    TopDown,
    Isometric,
    Perspective3D,
}

public readonly record struct TileSelection(int X, int Y, int Z);

/// <summary>
/// Single source of truth for the open map, current tool, selection, and
/// edit history. Listeners get notified via events; the form rebuilds the
/// view in response.
/// </summary>
/// <summary>Default values stamped onto every freshly-placed MapObject.</summary>
public sealed class ObjectTemplate
{
    public byte Type { get; set; }
    public byte Remap { get; set; }
    public ushort Rotation { get; set; }
    public ushort Pitch { get; set; }
    public ushort Roll { get; set; }
}

/// <summary>Default values stamped onto every freshly-placed CarPosition.</summary>
public sealed class CarTemplate
{
    public byte Type { get; set; }
    public byte Remap { get; set; }
    public ushort Rotation { get; set; }
}

public sealed class EditorState
{
    public CmpMap? Map { get; private set; }
    public G24StyleData? Style { get; private set; }
    public MapEditor? Editor { get; private set; }
    public TileAtlas? Atlas { get; private set; }
    public string? FilePath { get; private set; }
    /// <summary>True when Atlas was built from pre-extracted PNGs instead of G24 CLUT decoding.</summary>
    public bool UsingPngTiles { get; private set; }

    public TileSelection? Selection { get; private set; }
    public ToolMode Tool { get; private set; } = ToolMode.Select;
    public ViewMode View { get; private set; } = ViewMode.TopDown;

    /// <summary>Tile index in use as the paint brush (lid only for now).</summary>
    public int PaintTile { get; set; } = 1;

    /// <summary>Template applied when dropping an object in PlaceObject mode.</summary>
    public ObjectTemplate ObjectTemplate { get; set; } = new();

    /// <summary>Template applied when dropping a car spawn in PlaceCar mode.</summary>
    public CarTemplate CarTemplate { get; set; } = new();

    /// <summary>Type stamped onto every spawn dropped in PlaceSpawn mode.</summary>
    public SpawnLocationType SpawnType { get; set; } = SpawnLocationType.Police;

    /// <summary>If true, MapView2D draws nav-flag arrows on every drivable tile.</summary>
    public bool ShowTrafficArrows { get; set; }

    /// <summary>If true, top-down view renders the player's ground level (skips decorative overlays only). False (default) renders the topmost lid in each column, like Junction25.</summary>
    public bool ShowGroundLevel { get; set; }

    public CommandStack Commands { get; } = new();

    public bool IsDirty => Editor?.IsDirty ?? false;

    // Events ----------------------------------------------------------------
    public event Action? MapLoaded;
    public event Action? MapEdited;        // map structure changed (rebuild renderer mesh)
    public event Action? SelectionChanged;
    public event Action? ToolChanged;
    public event Action? ViewModeChanged;
    public event Action? CommandsChanged;
    public event Action? OverlaysChanged;

    public EditorState()
    {
        Commands.Changed += () => CommandsChanged?.Invoke();
    }

    public void LoadMap(CmpMap map, G24StyleData style, string path)
    {
        Map = map;
        Style = style;

        // Prefer pre-extracted PNGs if found in the sibling styles folder —
        // they were authored offline with full palette knowledge so colours
        // always match the reference. Fall back to live G24 CLUT decoding
        // otherwise.
        var png = PngTileSource.TryLoad(path, map.Header.StyleNumber,
            style.SideTileCount, style.LidTileCount, style.AuxTileCount);
        if (png is not null)
        {
            Atlas = TileAtlas.BuildFromTiles(png.TileCount, png.GetTile);
            UsingPngTiles = true;
        }
        else
        {
            Atlas = TileAtlas.Build(style);
            UsingPngTiles = false;
        }

        Editor = new MapEditor(map);
        FilePath = path;
        Selection = null;
        Commands.Clear();
        MapLoaded?.Invoke();
    }

    public void SetSelection(TileSelection? sel)
    {
        Selection = sel;
        SelectionChanged?.Invoke();
    }

    public void SetTool(ToolMode tool)
    {
        if (Tool == tool) return;
        Tool = tool;
        ToolChanged?.Invoke();
    }

    public void SetViewMode(ViewMode mode)
    {
        if (View == mode) return;
        View = mode;
        ViewModeChanged?.Invoke();
    }

    public void ToggleTrafficArrows()
    {
        ShowTrafficArrows = !ShowTrafficArrows;
        OverlaysChanged?.Invoke();
    }

    public void ToggleGroundLevel()
    {
        ShowGroundLevel = !ShowGroundLevel;
        MapEdited?.Invoke(); // re-renderer + rebuild mesh
    }

    public void ExecuteCommand(IEditCommand cmd)
    {
        if (Editor is null) return;
        Commands.Execute(cmd, Editor);
        MapEdited?.Invoke();
    }

    public void Undo()
    {
        if (Editor is null) return;
        Commands.Undo(Editor);
        MapEdited?.Invoke();
    }

    public void Redo()
    {
        if (Editor is null) return;
        Commands.Redo(Editor);
        MapEdited?.Invoke();
    }

    public void Save(string? overridePath = null)
    {
        if (Map is null) return;
        string path = overridePath ?? FilePath
            ?? throw new InvalidOperationException("No save path.");
        CmpWriter.WriteFile(Map, path);
        FilePath = path;
        Editor?.ClearDirty();
        CommandsChanged?.Invoke();
    }
}
