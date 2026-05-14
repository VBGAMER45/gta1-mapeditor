using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.Core.Commands;

public sealed class AddObjectCommand : IEditCommand
{
    private readonly MapObject _obj;
    public string Description => "Place object";
    public AddObjectCommand(MapObject obj) { _obj = obj; }
    public void Do(MapEditor e)   { e.Map.Objects.Add(_obj); e.MarkDirty(); }
    public void Undo(MapEditor e) { e.Map.Objects.Remove(_obj); e.MarkDirty(); }
}

public sealed class RemoveObjectCommand : IEditCommand
{
    private readonly MapObject _obj;
    private int _restoreIndex = -1;
    public string Description => "Delete object";
    public RemoveObjectCommand(MapObject obj) { _obj = obj; }
    public void Do(MapEditor e)
    {
        _restoreIndex = e.Map.Objects.IndexOf(_obj);
        if (_restoreIndex >= 0) e.Map.Objects.RemoveAt(_restoreIndex);
        e.MarkDirty();
    }
    public void Undo(MapEditor e)
    {
        if (_restoreIndex >= 0) e.Map.Objects.Insert(_restoreIndex, _obj);
        else e.Map.Objects.Add(_obj);
        e.MarkDirty();
    }
}

public sealed class MoveObjectCommand : IEditCommand
{
    private readonly MapObject _obj;
    private readonly ushort _oldX, _oldY, _newX, _newY;
    public string Description => "Move object";
    public MoveObjectCommand(MapObject obj, ushort oldX, ushort oldY, ushort newX, ushort newY)
    { _obj = obj; _oldX = oldX; _oldY = oldY; _newX = newX; _newY = newY; }
    public void Do(MapEditor e)   { _obj.X = _newX; _obj.Y = _newY; e.MarkDirty(); }
    public void Undo(MapEditor e) { _obj.X = _oldX; _obj.Y = _oldY; e.MarkDirty(); }
}

public sealed class AddCarCommand : IEditCommand
{
    private readonly CarPosition _car;
    public string Description => "Place car";
    public AddCarCommand(CarPosition car) { _car = car; }
    public void Do(MapEditor e)   { e.Map.CarPositions.Add(_car); e.MarkDirty(); }
    public void Undo(MapEditor e) { e.Map.CarPositions.Remove(_car); e.MarkDirty(); }
}

public sealed class RemoveCarCommand : IEditCommand
{
    private readonly CarPosition _car;
    private int _restoreIndex = -1;
    public string Description => "Delete car";
    public RemoveCarCommand(CarPosition car) { _car = car; }
    public void Do(MapEditor e)
    {
        _restoreIndex = e.Map.CarPositions.IndexOf(_car);
        if (_restoreIndex >= 0) e.Map.CarPositions.RemoveAt(_restoreIndex);
        e.MarkDirty();
    }
    public void Undo(MapEditor e)
    {
        if (_restoreIndex >= 0) e.Map.CarPositions.Insert(_restoreIndex, _car);
        else e.Map.CarPositions.Add(_car);
        e.MarkDirty();
    }
}

public sealed class MoveCarCommand : IEditCommand
{
    private readonly CarPosition _car;
    private readonly ushort _oldX, _oldY, _newX, _newY;
    public string Description => "Move car";
    public MoveCarCommand(CarPosition car, ushort oldX, ushort oldY, ushort newX, ushort newY)
    { _car = car; _oldX = oldX; _oldY = oldY; _newX = newX; _newY = newY; }
    public void Do(MapEditor e)   { _car.X = _newX; _car.Y = _newY; e.MarkDirty(); }
    public void Undo(MapEditor e) { _car.X = _oldX; _car.Y = _oldY; e.MarkDirty(); }
}

public readonly record struct ObjectSnapshot(byte Type, byte Remap, ushort Rotation, ushort Pitch, ushort Roll);
public readonly record struct CarSnapshot(byte Type, byte Remap, ushort Rotation);

public sealed class EditObjectCommand : IEditCommand
{
    private readonly MapObject _obj;
    private readonly ObjectSnapshot _before, _after;
    public string Description => "Edit object";
    public EditObjectCommand(MapObject obj, ObjectSnapshot before, ObjectSnapshot after)
    { _obj = obj; _before = before; _after = after; }
    public void Do(MapEditor e)   { Apply(_obj, _after); e.MarkDirty(); }
    public void Undo(MapEditor e) { Apply(_obj, _before); e.MarkDirty(); }
    private static void Apply(MapObject o, ObjectSnapshot s)
    { o.Type = s.Type; o.Remap = s.Remap; o.Rotation = s.Rotation; o.Pitch = s.Pitch; o.Roll = s.Roll; }
    public static ObjectSnapshot Capture(MapObject o) => new(o.Type, o.Remap, o.Rotation, o.Pitch, o.Roll);
}

public sealed class EditCarCommand : IEditCommand
{
    private readonly CarPosition _car;
    private readonly CarSnapshot _before, _after;
    public string Description => "Edit car";
    public EditCarCommand(CarPosition car, CarSnapshot before, CarSnapshot after)
    { _car = car; _before = before; _after = after; }
    public void Do(MapEditor e)   { Apply(_car, _after); e.MarkDirty(); }
    public void Undo(MapEditor e) { Apply(_car, _before); e.MarkDirty(); }
    private static void Apply(CarPosition c, CarSnapshot s)
    { c.Type = s.Type; c.Remap = s.Remap; c.Rotation = s.Rotation; }
    public static CarSnapshot Capture(CarPosition c) => new(c.Type, c.Remap, c.Rotation);
}

public sealed class AddSpawnCommand : IEditCommand
{
    private readonly SpawnLocation _spawn;
    public string Description => $"Place {_spawn.Type} spawn";
    public AddSpawnCommand(SpawnLocation spawn) { _spawn = spawn; }
    public void Do(MapEditor e)   { e.Map.SpawnLocations.Add(_spawn); e.MarkDirty(); }
    public void Undo(MapEditor e) { e.Map.SpawnLocations.Remove(_spawn); e.MarkDirty(); }
}

public sealed class RemoveSpawnCommand : IEditCommand
{
    private readonly SpawnLocation _spawn;
    private int _restoreIndex = -1;
    public string Description => $"Delete {_spawn.Type} spawn";
    public RemoveSpawnCommand(SpawnLocation spawn) { _spawn = spawn; }
    public void Do(MapEditor e)
    {
        _restoreIndex = e.Map.SpawnLocations.IndexOf(_spawn);
        if (_restoreIndex >= 0) e.Map.SpawnLocations.RemoveAt(_restoreIndex);
        e.MarkDirty();
    }
    public void Undo(MapEditor e)
    {
        if (_restoreIndex >= 0) e.Map.SpawnLocations.Insert(_restoreIndex, _spawn);
        else e.Map.SpawnLocations.Add(_spawn);
        e.MarkDirty();
    }
}
