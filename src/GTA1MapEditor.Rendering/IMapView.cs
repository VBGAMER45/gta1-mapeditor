using GTA1MapEditor.Core.Models;
using OpenTK.Mathematics;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// Renderer contract shared by the three view modes. The host control swaps
/// the active implementation when the user changes view; input handling
/// stays in the host but routes through the methods below.
/// </summary>
public interface IMapView : IDisposable
{
    void SetMap(CmpMap map, G24StyleData style);
    void RebuildMesh();
    void Render();
    void Resize(int width, int height);

    (int x, int y, int z)? Selection { get; set; }

    /// <summary>
    /// Camera focus point in world (tile) coordinates. For top-down this is
    /// the orthographic center; for iso it's the look-at target; for 3D it's
    /// projected to the ground plane and may behave loosely (fly-cam doesn't
    /// pan in tile-space anyway).
    /// </summary>
    Vector2 CameraWorld { get; set; }

    /// <summary>Pan by a screen-space delta (in pixels). 3D views interpret as strafe.</summary>
    void Pan(float screenDx, float screenDy);

    /// <summary>Zoom by a multiplier. Top-down/iso scale the orthographic size; 3D dolly the camera.</summary>
    void Zoom(float factor, int anchorX, int anchorY);

    void ResetView();
    void FitMap();

    /// <summary>Set zoom so one tile renders at its native G24 pixel size (64x64). 3D resets the camera.</summary>
    void NativeZoom();

    /// <summary>Hit-test a screen point to a tile (x,y); null when not over the map.</summary>
    (int x, int y)? PickTile(int screenX, int screenY);

    /// <summary>True if this view supports mouse-look style camera control.</summary>
    bool UsesFlyCamera { get; }

    /// <summary>Apply a relative pointer delta as look input (only honoured if UsesFlyCamera).</summary>
    void ApplyLookDelta(float dx, float dy);

    /// <summary>Per-frame camera tick for held WASD movement (3D only).</summary>
    void Tick(float deltaSeconds, bool fwd, bool back, bool left, bool right, bool up, bool down);
}
