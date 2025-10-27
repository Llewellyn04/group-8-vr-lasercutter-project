# Presentation Script — Kevin

Role and topics
- System development overview
- Code discussion: Spline drawing (SplineVRDraw)

Objectives (3–4 minutes)
- Explain the system architecture: input → drawing tools → manipulation → undo/redo → import/export.
- Walk through `SplineVRDraw` capabilities and configuration.

Slide outline
- High‑level architecture map
- Class diagram focus: Drawing + Editing clusters (`docs/class-diagram.md`)
- SplineVRDraw flows (start, update, stop; modes; drag/resize; undo/redo)

System development overview
- Architecture blocks
  - Input and XR: OpenXR with XR Interaction Toolkit; Input System actions for start/stop/drag/resize/undo/redo.
  - Drawing tools: freehand spline and assisted shapes create/update `LineRenderer` geometry.
  - Manipulation: drag/resize operate on line points in whiteboard local space.
  - State: `RedoUndoManager` tracks created line GameObjects for undo/redo.
  - Interop: DXF/SVG import/output modules for pipeline compatibility.
- Key scripts to name-drop
  - Spline: `Assets/Scripts/SplineVRDraw.cs`
  - Straight line: `Assets/Scripts/StraightLineVRDraw.cs`
  - Shapes: `Assets/Scripts/RectangleVRDraw.cs`, `Assets/Scripts/CircleVRDraw.cs`, `Assets/Scripts/PolygonVRDraw.cs`
  - State: `Assets/Scripts/RedoUndoManager.cs`
  - Text: `Assets/Scripts/TextManager.cs` (WhiteboardTextManager), `Assets/Scripts/DraggableText.cs`
  - Import/Export: `Assets/Scripts/FileListManager.cs`, `Assets/Scripts/ExportSVG.cs`, `Assets/Scripts/ExportDXF.cs`

Code discussion — SplineVRDraw
- File: `Assets/Scripts/SplineVRDraw.cs`
- Responsibilities
  - Draw freehand polylines and assisted shapes (StraightLine, Rectangle, Circle, Polygon) using a single component.
  - Bind to XR `rightController` trigger and InputSystem actions for start/stop, drag, resize, undo/redo.
  - Register finished lines with `RedoUndoManager`.
- Important fields to configure (Inspector)
  - `whiteboardPlane` (Transform): the drawing board reference plane.
  - `drawingTip` (Transform): controller tip used for raycasting.
  - `rightController` (XRController): provides trigger input.
  - `settings` (VRDrawSettings): material, width, color.
  - `redoUndoManager` (RedoUndoManager): for history.
  - Bounds: `maxWidth`, `maxHeight` to clamp drawing in board space.
  - Actions: `startDrawingAction`, `stopDrawingAction`, `dragAction`, `resizeAction`, `undoAction`, `redoAction`.
  - Mode: `drawMode` (Freehand, StraightLine, Rectangle, Circle, Polygon), `polygonSides`, `minLineLength`.
- Core flows to mention
  - StartDrawing() → spawns a `LineRenderer` and seeds initial points based on mode; for shapes, caches `startWorldPoint`.
  - Update() → when drawing, adds/clamps points in whiteboard local space; for shapes, calls `UpdateRectanglePreview`/`UpdateCirclePreview`.
  - StopDrawing() → decides keep/discard by thresholds; registers with `RedoUndoManager` on keep.
  - Drag/Resize → `UpdateDragging()` and `UpdateResizing()` transform stored local points relative to a pivot/centroid.
  - Undo/Redo → calls into `RedoUndoManager.Undo()` / `.Redo()` after ending manipulations.

Unity configuration checklist (demo‑safe)
- Add `SplineVRDraw` to a GameObject near the whiteboard.
- Assign references: `whiteboardPlane`, `drawingTip`, `rightController`, `redoUndoManager`.
- Create and assign a material in `settings.lineMaterial`; set `settings.lineWidth` and `settings.lineColor`.
- Wire Input Actions (simple press type) for start/stop, drag, resize, undo, redo.
- Tune bounds (`maxWidth`, `maxHeight`) and thresholds (`minLineLength`).

Live demo cues (60–90 seconds)
- Start in Freehand mode → draw a quick curve; undo/redo.
- Toggle Rectangle → draw and show preview snapping; drag shape across the board; resize around centroid.
- Toggle Polygon (e.g., 5 sides) → show parameterized sides.
- End with a quick undo/redo stack count callout in Console.

Q&A prompts
- Performance on large drawings? — LineRenderer cost scales with vertex count; we clamp density and reuse arrays where possible.
- Precision and scaling? — All ops occur in board local space, clamped by `maxWidth`/`maxHeight`.

Import/Export updates (what’s new)
- SVG export (ExportSVG)
  - Auto-calculates bounds from current board strokes or uses manual bounds; maps world → plane local → normalized → canvas coordinates.
  - Includes text from two sources: UGUI labels (via `whiteboardTextArea`/`textManager.textHistory`) and world‑space TMP under the whiteboard plane.
  - Save path uses Editor `SaveFilePanel`; Windows standalone shows a native SaveFileDialog; other platforms fall back to `persistentDataPath`.
- DXF export (ExportDXF)
  - Emits `LWPOLYLINE` vertices in DXF units scaled by `dxfScale`.
  - Optional text export via `includeText`, writing TEXT entities; height scaled by `dxfTextHeightFactor`.
- Import pipeline (FileListManager)
  - Robust DXF/SVG import with complete and simplified parsers, plus path separation to avoid unintended connections.
  - Supports LINE, LWPOLYLINE, ARC (segmented), and CIRCLE; maps to board space and renders with a safe Z‑offset and renderQueue for visibility.
  - Debug logging and UI plumbing improved; file list built from `Assets/<importFolderPath>`.

Text updates (integration)
- WhiteboardTextManager now shares controller bindings with Spline tools: drag/resize/undo/redo actions and plane projection for pointer.
- DraggableText supports controller‑based drag and resize on the plane, with min/max scale clamps; labels register with `RedoUndoManager` like strokes.

Code snippets (architecture and SplineVRDraw)
- Update loop and trigger control (from `SplineVRDraw`)
```csharp
void Update()
{
    if (rightController != null && rightController.inputDevice.isValid)
    {
        rightController.inputDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);

        if (triggerPressed && !lastTriggerState && !isDrawing)
            StartDrawing();
        else if (!triggerPressed && lastTriggerState && isDrawing)
            StopDrawing();

        lastTriggerState = triggerPressed;
    }

    if (isDrawing || alwaysDraw)
        UpdateDrawing();
}
```
Explanation
- The XR trigger starts/stops drawing. We then delegate to `UpdateDrawing()` for the active mode.

- Mode toggles and registration (from `SplineVRDraw`)
```csharp
public void ToggleRectangleDrawing()
{
    if (isDrawing && drawMode == DrawMode.Rectangle) StopDrawing();
    else { if (isDrawing) StopDrawing(); useStraightLineMode = false; drawMode = DrawMode.Rectangle; StartDrawing(); }
}

public void StopDrawing()
{
    if (!isDrawing) return;
    isDrawing = false;
    if (currentLine != null)
    {
        bool keep = false;
        switch (drawMode)
        {
            case DrawMode.StraightLine:
                if (currentLine.positionCount >= 2)
                {
                    float length = Vector3.Distance(currentLine.GetPosition(0), currentLine.GetPosition(1));
                    keep = length >= minLineLength;
                }
                break;
            case DrawMode.Rectangle:
                if (currentLine.positionCount >= 4)
                {
                    Vector3 a = currentLine.GetPosition(0);
                    Vector3 c = currentLine.GetPosition(2);
                    Vector3 la = whiteboardPlane.InverseTransformPoint(a);
                    Vector3 lc = whiteboardPlane.InverseTransformPoint(c);
                    float w = Mathf.Abs(lc.x - la.x);
                    float h = Mathf.Abs(lc.y - la.y);
                    keep = (w >= minLineLength && h >= minLineLength);
                }
                break;
            case DrawMode.Circle:
            case DrawMode.Polygon:
                if (currentLine.positionCount >= 3)
                {
                    float r = Vector3.Distance(startWorldPoint, currentLine.GetPosition(0));
                    keep = r >= minLineLength;
                }
                break;
            case DrawMode.Freehand:
            default:
                keep = currentLine.positionCount > 1;
                break;
        }

        if (keep)
        {
            currentLine.loop = (drawMode == DrawMode.Rectangle || drawMode == DrawMode.Circle || drawMode == DrawMode.Polygon);
            if (redoUndoManager != null)
                redoUndoManager.RegisterLine(currentLine.gameObject);
        }
        else
        {
            Destroy(currentLine.gameObject);
        }
    }
    currentLine = null;
    hasStartPoint = false;
}
```
Explanation
- A single stop path evaluates “keep or discard” per mode, then registers with `RedoUndoManager` for history.
