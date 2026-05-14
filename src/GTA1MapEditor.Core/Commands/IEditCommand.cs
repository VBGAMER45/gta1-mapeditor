namespace GTA1MapEditor.Core.Commands;

/// <summary>One reversible map edit. Pushed onto the editor's command stack on Do().</summary>
public interface IEditCommand
{
    string Description { get; }
    void Do(MapEditor editor);
    void Undo(MapEditor editor);
}
