# Presentation Script — Liam

Role and topics
- Activity diagram (with Kyle)

Objectives (2–3 minutes)
- Visualize the user activity flow from tool selection through drawing, manipulation, and export.

Slide 1 — Activity Diagram (Mermaid)
```mermaid
flowchart TD
  A[Start] --> B[Choose Mode]
  B -->|Freehand| C[StartDrawing]
  B -->|Straight/Rect/Circle/Poly| C
  C --> D{Drawing Active?}
  D -->|Yes| E[Update Preview / Points]
  E --> D
  D -->|No| F{Keep? Thresholds}
  F -->|Yes| G[Register with RedoUndo]
  F -->|No| H[Discard]
  G --> I{Manipulate?}
  I -->|Drag| J[Translate Points]
  I -->|Resize| K[Scale Around Pivot]
  J --> L[Finish Manipulation]
  K --> L
  L --> M{Undo/Redo?}
  M -->|Undo| N[Hide Last]
  M -->|Redo| O[Show Last]
  M -->|No| P{Import/Export?}
  P -->|Import DXF/SVG| Q[Parse + Draw]
  P -->|Export DXF/SVG| R[Write Files]
  Q --> B
  R --> B
```

Narration notes
- Emphasize thresholds: shapes are kept only if size/radius/length exceeds `minLineLength` or shape‑specific minimums.
- All manipulations operate in whiteboard local space to avoid drift.
- Undo/Redo are visual (activate/deactivate) via `RedoUndoManager`.

Hand‑off to Kyle
- Kyle will extend the activity perspective with Circle drawing and Export details, plus a demo snippet.

Code snippet (activity loop hook)
- Trigger‑driven drawing and Update path (from `Assets/Scripts/SplineVRDraw.cs`)
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
- The activity loop transitions based on trigger edge detection. While active, `UpdateDrawing()` executes the selected mode’s preview/update.
