# Presentation Script — Drag and Resize

Role and topic
- Code discussion: Drag and Resize

Objectives (2 minutes)
- Explain how shapes are picked and manipulated in local space.

Where implemented
- Spline tool: `Assets/Scripts/SplineVRDraw.cs` (`UpdateDragging`, `TryPickShape`, `UpdateResizing`, centroid scaling)
- Straight line: `Assets/Scripts/StraightLineVRDraw.cs` (center‑offset drag, endpoint clamp)
- Circle/Rectangle/Polygon tools each provide drag/resize toggles and update logic.

Picking strategies
- Closed shapes: point‑in‑polygon test in whiteboard local space.
- Open lines: nearest distance to polyline within `openLinePickDistance` tolerance.

Transforms
- Drag: translate each stored local point by delta, then convert back to world with `TransformPoint`.
- Resize: compute pivot (centroid), scale offsets by a ratio derived from controller distance; clamp scale.

Unity configuration
- Ensure `dragAction` and `resizeAction` input bindings exist and are assigned.
- Verify `whiteboardPlane` is correct and stable; manipulation relies on its transform.
- Text labels use the same plane projection and InputAction bindings via `DraggableText` for consistent drag/resize behavior.

Demo cue (45 seconds)
- Drag a closed rectangle by grabbing inside; resize it around centroid; repeat on a polyline using open‑line tolerance.

Code snippets (picking and transforms)
- Pick closed or open shapes (from `SplineVRDraw`)
```csharp
bool TryPickShapeWithOptions(Vector3 pointerLocal, bool closedOnly, bool allowOpen, float openPickDistance,
    out LineRenderer picked, out Vector3[] originalLocal)
{
    picked = null; originalLocal = null;
    LineRenderer[] candidates = FindObjectsOfType<LineRenderer>();
    foreach (var lr in candidates)
    {
        int count = lr.positionCount; if (count < 2) continue;
        Vector3[] localPts = new Vector3[count];
        for (int i = 0; i < count; i++) localPts[i] = whiteboardPlane.InverseTransformPoint(lr.GetPosition(i));
        if (lr.loop && count >= 3)
        {
            if (PointInPolygon(pointerLocal, localPts)) { picked = lr; originalLocal = localPts; return true; }
        }
        else if (!closedOnly && allowOpen)
        {
            if (DistanceToPolyline(pointerLocal, localPts) <= openPickDistance) { picked = lr; originalLocal = localPts; return true; }
        }
    }
    return false;
}
```
Explanation
- Closed shapes use point‑in‑polygon; open lines use distance to the nearest segment within a tolerance.

- Resize around centroid (from `SplineVRDraw`)
```csharp
if (resizingLine != null && resizingOriginalLocalPoints != null)
{
    float currentRadius = Mathf.Max(Vector2.Distance(new Vector2(hitLocal.x, hitLocal.y), new Vector2(resizePivotLocal.x, resizePivotLocal.y)), 0.0001f);
    float scale = currentRadius / resizeStartRadius;
    scale = Mathf.Clamp(scale, minResizeScale, maxResizeScale);
    int count = resizingOriginalLocalPoints.Length;
    Vector3[] newWorld = new Vector3[count];
    for (int i = 0; i < count; i++)
    {
        Vector3 offset = resizingOriginalLocalPoints[i] - resizePivotLocal;
        Vector3 newLocal = resizePivotLocal + offset * scale;
        newWorld[i] = whiteboardPlane.TransformPoint(newLocal);
    }
    if (resizingLine.positionCount != count) resizingLine.positionCount = count;
    resizingLine.SetPositions(newWorld);
}
```
Explanation
- We scale all local points about the centroid pivot, then transform them back to world space and apply to the `LineRenderer`.

- Drag translation (from `SplineVRDraw`)
```csharp
Vector3 delta = hitLocal - dragStartLocalPoint;
int count = draggedOriginalLocalPoints.Length;
Vector3[] newWorld = new Vector3[count];
for (int i = 0; i < count; i++)
{
    Vector3 newLocal = draggedOriginalLocalPoints[i] + delta;
    newWorld[i] = whiteboardPlane.TransformPoint(newLocal);
}
draggedLine.SetPositions(newWorld);
```
Explanation
- Dragging applies a uniform local translation to all points, preserving the shape.
