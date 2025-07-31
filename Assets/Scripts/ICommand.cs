using UnityEngine;

public interface ICommand//Use for undo/redo
{
    void Execute();
    void Undo();
}
