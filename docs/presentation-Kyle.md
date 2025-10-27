# Presentation Script — Kyle

Role and topics
- Activity diagram (with Liam)
- Code discussions: Circle via SplineVRDraw (Circle mode) and Export

Objectives (3–4 minutes)
- Show how circle drawing is implemented in SplineVRDraw and configured.
- Explain SVG/DXF export paths and what gets written.

Slide outline
- Activity continuation (manipulation → export)
- SplineVRDraw circle mode
- ExportSVG and ExportDXF overview

Circle mode (in SplineVRDraw)
- File: `Assets/Scripts/SplineVRDraw.cs`
- Responsibilities
  - Create a looped `LineRenderer` approximating a circle (fixed segment count) around a clamped center.
  - Start with a center on the board; adjust radius by controller motion; discard if below minimum.
  - Support drag/resize via shared manipulation; register kept circles with `RedoUndoManager`.
- Inspector configuration
  - `whiteboardPlane`, `drawingTip`, `rightController` (Transforms/XR)
  - `settings` (material/width/color)
  - Bounds: `maxWidth`, `maxHeight`; threshold baseline: `minLineLength`
  - `redoUndoManager`
- Demo cue
  - Use `ToggleCircleDrawing()`; move to change radius; release to keep; drag; resize; undo/redo.

Export
- Files: `Assets/Scripts/ExportSVG.cs`, `Assets/Scripts/ExportDXF.cs`
- SVG path
  - Finds `LineRenderer` strokes (preferring names like VR_/Drawing) and converts board‑local points into SVG canvas coordinates.
  - Auto bounds option computes min/max from current drawings; otherwise uses manual bounds.
  - Also exports both UI text entries (UGUI under `whiteboardTextArea` or `textManager.textHistory`) and world‑space TMP labels under the whiteboard plane.
  - Save path: Editor uses `SaveFilePanel`; Windows standalone uses a native SaveFileDialog; other platforms fall back to `persistentDataPath`.
- DXF path
  - Similar bounds logic; writes `LWPOLYLINE` entities with color indices approximating Unity colors.
  - Scales to `dxfScale` units (e.g., millimeters if `1000`).
  - Optional text export: when `includeText` is true, writes TEXT entities from both UGUI and world‑space TMP; size scaled by `dxfTextHeightFactor`.
- Inspector configuration
  - SVG: assign `whiteboardPlane`, optional `whiteboardTextArea`, `textManager`; set `canvasWidth/Height`, `fileName`.
  - DXF: assign `whiteboardPlane`, optional `whiteboardTextArea`, `textManager`; choose `fileName`, `dxfScale`, `dxfTextHeightFactor`, bounds option, and `includeText`.
- Demo cue
  - Press `E` (SVG) or `D` (DXF) per scripts’ Update handlers to save; Editor reveals in Finder.

Q&A prompts
- How are circles exported? — SVG as polyline/path; DXF as `LWPOLYLINE` points.
- Coordinate accuracy? — Everything derives from board local space to maintain scale.

Code snippets (Circle + Export)
- Circle preview generation (from `SplineVRDraw`)
```csharp
void UpdateCirclePreview(Vector3 currentWorld, int segments)
{
    if (currentLine == null) return;
    Vector3 lCenter = whiteboardPlane.InverseTransformPoint(startWorldPoint);
    Vector3 lCurrent = whiteboardPlane.InverseTransformPoint(currentWorld);
    Vector2 delta = new Vector2(lCurrent.x - lCenter.x, lCurrent.y - lCenter.y);
    float radius = delta.magnitude;
    if (radius < Mathf.Epsilon) radius = 0f;

    currentLine.loop = true;
    if (currentLine.positionCount != segments) currentLine.positionCount = segments;

    float twoPi = Mathf.PI * 2f;
    float halfW = maxWidth * 0.5f;
    float halfH = maxHeight * 0.5f;
    for (int i = 0; i < segments; i++)
    {
        float t = (i / (float)segments) * twoPi;
        Vector3 localPoint = new Vector3(
            lCenter.x + Mathf.Cos(t) * radius,
            lCenter.y + Mathf.Sin(t) * radius,
            0f
        );
        localPoint.x = Mathf.Clamp(localPoint.x, -halfW, halfW);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfH, halfH);
        Vector3 worldPoint = whiteboardPlane.TransformPoint(localPoint);
        currentLine.SetPosition(i, worldPoint);
    }
}
```
Explanation
- Circle vertices are created in whiteboard local space and clamped to board bounds, then transformed to world for rendering.

- SVG export mapping (from `ExportSVG`)
```csharp
Vector3 localPos = whiteboardPlane.InverseTransformPoint(worldPos);
float normalizedX = (localPos.x - actualMin.x) / boundsWidth;
float normalizedY = (localPos.y - actualMin.y) / boundsHeight;
float svgX = normalizedX * canvasWidth;
float svgY = (1f - normalizedY) * canvasHeight; // Flip Y
```
Explanation
- We convert world → board‑local → normalized → SVG canvas coordinates; Y is flipped for SVG’s top‑left origin.

- DXF vertex writing (from `ExportDXF`)
```csharp
dxf.AppendLine("0");
dxf.AppendLine("LWPOLYLINE");
dxf.AppendLine("90");
dxf.AppendLine(line.positionCount.ToString());
for (int i = 0; i < line.positionCount; i++)
{
    Vector3 worldPos = line.GetPosition(i);
    Vector3 localPos = whiteboardPlane.InverseTransformPoint(worldPos);
    float normalizedX = (localPos.x - actualMin.x) / boundsWidth;
    float normalizedY = (localPos.y - actualMin.y) / boundsHeight;
    float dxfX = normalizedX * dxfScale;
    float dxfY = normalizedY * dxfScale;
    dxf.AppendLine("10"); dxf.AppendLine(dxfX.ToString("F6", culture));
    dxf.AppendLine("20"); dxf.AppendLine(dxfY.ToString("F6", culture));
}
```
Explanation
- DXF uses unit coordinates; we scale normalized board‑local values by `dxfScale` and emit LWPOLYLINE vertices.

- DXF TEXT entity writing (from `ExportDXF`)
```csharp
foreach (var t in texts)
{
    float nx = (t.localPos.x - actualMin.x) / boundsWidth;
    float ny = (t.localPos.y - actualMin.y) / boundsHeight;
    float dx = nx * dxfScale;
    float dy = ny * dxfScale;
    float height = Mathf.Max(1f, t.fontSize) * Mathf.Max(0.0001f, t.scale) * Mathf.Max(0.0001f, dxfTextHeightFactor);
    dxf.AppendLine("0"); dxf.AppendLine("TEXT");
    dxf.AppendLine("10"); dxf.AppendLine(dx.ToString("F6", culture));
    dxf.AppendLine("20"); dxf.AppendLine(dy.ToString("F6", culture));
    dxf.AppendLine("40"); dxf.AppendLine(height.ToString("F6", culture));
    dxf.AppendLine("1");  dxf.AppendLine(t.text ?? string.Empty);
}
```
Explanation
- When enabled, export maps text to DXF TEXT entities using normalized plane coordinates and a configurable text height factor.
