# Presentation Script — Undo / Redo / Clear

Role and topic
- Code discussion: Undo, Redo and Clear

Objectives (2 minutes)
- Explain history management and how strokes are hidden/shown.

Core components
- File: `Assets/Scripts/RedoUndoManager.cs`
- Responsibilities
  - Track created line GameObjects in two stacks (undo/redo).
  - Undo: `Pop` from undo, deactivate GameObject, `Push` to redo.
  - Redo: `Pop` from redo, activate GameObject, `Push` to undo.
  - ClearAll: deactivate everything and clear stacks.
- Integration points
  - Drawing tools call `RegisterLine(GameObject)` when finishing a keepable stroke.
  - Spline tool binds `undoAction`/`redoAction` and invokes `.Undo()`/`.Redo()` safely after ending manipulations.

Unity configuration
- Create an empty GameObject `RedoUndoManager` and add the component.
- Assign this reference in drawing tools (`SplineVRDraw`, `StraightLineVRDraw`, shape tools).
- Map controller buttons to undo/redo Input Actions and wire to Spline tool.

Demo cue (45 seconds)
- Draw 3 shapes; press Undo twice and Redo once; call out console logs for stack sizes.

Code snippets (history stack operations)
- Register/Undo/Redo (from `Assets/Scripts/RedoUndoManager.cs`)
```csharp
public void RegisterLine(GameObject line)
{
    if (line == null) { Debug.LogError("Attempted to register a null line!"); return; }
    undoStack.Push(line);
    redoStack.Clear();
}

public void Undo()
{
    if (undoStack.Count == 0) return;
    GameObject line = undoStack.Pop();
    if (line != null) { line.SetActive(false); redoStack.Push(line); }
}

public void Redo()
{
    if (redoStack.Count == 0) return;
    GameObject line = redoStack.Pop();
    if (line != null) { line.SetActive(true); undoStack.Push(line); }
}
```
Explanation
- We move GameObjects between stacks and toggle active state for a fast visual undo/redo without destroying objects.
