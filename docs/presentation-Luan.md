# Presentation Script — Luan

Role and topic
- Code discussion: Straight line via SplineVRDraw (StraightLine mode)

Objectives (2–3 minutes)
- Explain how the straight line mode in SplineVRDraw works, how to configure it, and how to demo it.

Slide outline
- SplineVRDraw overview (shape modes)
- Unity configuration
- Short demo sequence

Straight line mode (in SplineVRDraw)
- File: `Assets/Scripts/SplineVRDraw.cs`
- Responsibilities
  - Draw a two‑point straight line, clamped to the whiteboard plane, using SplineVRDraw’s `DrawMode.StraightLine`.
  - Start/stop from XR trigger or Input Actions; keep/discard based on `minLineLength`.
  - Drag and resize using the component’s manipulation flows (dragAction/resizeAction).
  - Register completed lines with `RedoUndoManager`.
- Important fields to configure (Inspector)
  - `whiteboardPlane` (Transform), `drawingTip` (Transform), `rightController` (XRController)
  - `settings` (VRDrawSettings: material, width, color)
  - Bounds: `maxWidth`, `maxHeight`; Threshold: `minLineLength`
  - Input actions: `startDrawingAction`, `stopDrawingAction`, `dragAction`, `resizeAction`, `undoAction`, `redoAction`
  - History: `redoUndoManager`
- Core flows to mention
  - Toggle to StraightLine: `ToggleStraightLineDrawing()` (or set `drawMode = DrawMode.StraightLine` then `StartDrawing()`)
  - Update loop: raycast from `drawingTip` to plane, clamp to bounds, update endpoint; release to `StopDrawing()`
  - Manipulation: `UpdateDragging()` translates endpoints; `UpdateResizing()` adjusts length; both clamp to bounds
  - Undo/Redo: call into `RedoUndoManager` via bound actions

Unity configuration checklist
- Add `SplineVRDraw` to a board‑adjacent GameObject; assign `whiteboardPlane`, `drawingTip`, `rightController`, `redoUndoManager`.
- Create a material for `settings.lineMaterial`; set `settings.lineWidth` and `settings.lineColor`.
- Bind Input Actions for start/stop, drag, resize, undo, redo.
- Tune `maxWidth`, `maxHeight`, and `minLineLength`.

Demo cue (60 seconds)
- Toggle StraightLine → draw with trigger press, release to keep.
- Drag the line by grabbing near it; resize; then undo/redo.

Q&A prompts
- How do we pick a line? — Distance‑to‑polyline/segment in local space with a tolerance.
- Why local‑space clamping? — Ensures stability on transformed boards and predictable bounds.

Code snippets (StraightLine mode)
- Toggle into straight line and start (from `SplineVRDraw`)
```csharp
public void ToggleStraightLineDrawing()
{
    if (isDrawing && drawMode == DrawMode.StraightLine)
    {
        StopDrawing();
    }
    else
    {
        if (isDrawing) StopDrawing();
        useStraightLineMode = true;
        drawMode = DrawMode.StraightLine;
        StartDrawing();
    }
}
```
Explanation
- One toggle switches the tool into straight line mode and starts a new line when appropriate.

- Keep/discard logic for straight lines (from `SplineVRDraw`)
```csharp
case DrawMode.StraightLine:
    if (currentLine.positionCount >= 2)
    {
        float length = Vector3.Distance(currentLine.GetPosition(0), currentLine.GetPosition(1));
        keep = length >= minLineLength;
    }
    break;
```
Explanation
- We avoid spurious tiny strokes by enforcing a minimum line length threshold before registering to history.
