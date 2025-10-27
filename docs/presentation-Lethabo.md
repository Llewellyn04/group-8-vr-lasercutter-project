# Presentation Script — Lethabo

Role and topics
- Code discussion: Rectangle via SplineVRDraw (Rectangle mode)
- Conclusion

Objectives (2–3 minutes + 45 seconds)
- Explain rectangle creation/manipulation using SplineVRDraw’s rectangle mode and inspector setup.
- Close with impact, takeaways, and next steps.

Rectangle mode (in SplineVRDraw)
- File: `Assets/Scripts/SplineVRDraw.cs`
- Responsibilities
  - Build an axis‑aligned rectangle between `startWorldPoint` and current controller position in board local space.
  - Keep only if width/height meet thresholds; otherwise discard.
  - Drag/resize support via the shared manipulation paths (dragAction/resizeAction) operating in local space.
  - Register kept rectangles with `RedoUndoManager`.
- Inspector configuration
  - Assign `whiteboardPlane`, `drawingTip`, `rightController`, `redoUndoManager`.
  - `settings` (material/width/color).
  - Bounds: `maxWidth`, `maxHeight`; Thresholds: `minLineLength` (used as minimum dimension baseline).
  - Input actions: `startDrawingAction`, `stopDrawingAction`, `dragAction`, `resizeAction`, `undoAction`, `redoAction`.
- Demo cue
  - Use `ToggleRectangleDrawing()`; move the controller to preview; release to keep; drag across the board; resize; undo/redo.

Conclusion (45–60 seconds)
- We delivered a modular VR whiteboard for laser‑cutter workflows: robust drawing tools (including rectangle mode), manipulation, undo/redo, text annotations, and DXF/SVG interop.
- Built on Unity 6, OpenXR, and XR Interaction Toolkit, it’s portable across devices and easy to extend (new shapes or formats).
- Next steps: snapping/grid, bezier export fidelity, layer management, and collaborative multi‑user mode.

Code snippets (Rectangle mode)
- Preview generation (from `SplineVRDraw`)
```csharp
void UpdateRectanglePreview(Vector3 currentWorld)
{
    Vector3 ls = whiteboardPlane.InverseTransformPoint(startWorldPoint);
    Vector3 lc = whiteboardPlane.InverseTransformPoint(currentWorld);
    Vector3 p0 = new Vector3(ls.x, ls.y, 0f);
    Vector3 p2 = new Vector3(lc.x, lc.y, 0f);
    Vector3 p1 = new Vector3(p2.x, p0.y, 0f);
    Vector3 p3 = new Vector3(p0.x, p2.y, 0f);

    currentLine.loop = true;
    currentLine.positionCount = 4;
    currentLine.SetPosition(0, whiteboardPlane.TransformPoint(p0));
    currentLine.SetPosition(1, whiteboardPlane.TransformPoint(p1));
    currentLine.SetPosition(2, whiteboardPlane.TransformPoint(p2));
    currentLine.SetPosition(3, whiteboardPlane.TransformPoint(p3));
}
```
Explanation
- The rectangle is constructed axis‑aligned in board local space using the start and current pointer positions, then transformed back to world.
