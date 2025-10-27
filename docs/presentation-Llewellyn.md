# Presentation Script — Llewellyn

Role and topics
- Introduction
- Solution overview

Objective (60–90 seconds)
- Set the context, the problem we address, and the value of our VR Laser Cutter Whiteboard solution.
- Prime the audience for the technical deep‑dive by the team.

What this project solves
- Rapid, precise sketching and layout in VR with shape tools (freehand spline, straight line, rectangle, circle, polygon).
- Non‑destructive editing via Undo/Redo and manipulation (drag/resize) for shapes.
- Import DXF/SVG artwork to use as guides; export your final work to DXF/SVG for fabrication.
- Text annotations on the whiteboard for labeling and instructions.

Key features at a glance
- Drawing: `Assets/Scripts/SplineVRDraw.cs`, `Assets/Scripts/StraightLineVRDraw.cs`, `Assets/Scripts/RectangleVRDraw.cs`, `Assets/Scripts/CircleVRDraw.cs`, `Assets/Scripts/PolygonVRDraw.cs`
- Editing: `Assets/Scripts/RedoUndoManager.cs`, drag/resize in drawing scripts
- Text: `Assets/Scripts/TextManager.cs`, `Assets/Scripts/DraggableText.cs`
- Import/Export: `Assets/Scripts/FileListManager.cs`, `Assets/Scripts/ModelingTools/Import/DXFImporter.cs`, `Assets/Scripts/ModelingTools/Import/DXFPathRenderer.cs`, `Assets/Scripts/ExportSVG.cs`, `Assets/Scripts/ExportDXF.cs`
- Class diagram: `docs/class-diagram.md`

Solution overview narrative (60–90 seconds)
- We built a VR whiteboard optimized for laser‑cutter workflows. Users draw freehand or with assisted shape tools, then adjust geometry directly on the board. The system tracks each stroke for Undo/Redo and supports manipulation.
- For interoperability, we import DXF/SVG guides and export finished sketches in both DXF and SVG formats. Text labels are supported and exported with positions and sizes.
- Technically, the app is built on Unity 6 with OpenXR and XR Interaction Toolkit. The codebase is modular: drawing tools, state/undo manager, text manager, and import/export modules. See the Mermaid class diagram in `docs/class-diagram.md` for structure.

Slide pointers
- Title: “VR Laser Cutter Whiteboard — Overview”
- Problem → Solution → Impact
- Show class diagram thumbnail and highlight high‑level areas (Drawing, Editing, Text, Import/Export).

Demo cue
- Tease that Kevin will start with an architecture/dev overview, then the team will deep‑dive each component and a live demo.

Q&A seeds
- Integration with new controllers? — Built on OpenXR; bindings configurable.
- File fidelity? — DXF/SVG export covers lines, polygons, circles; text included in both (SVG text elements, DXF TEXT entities when enabled).

Code snippets (for context)
- Drawing modes and settings (from `Assets/Scripts/SplineVRDraw.cs`)
```csharp
public class VRDrawSettings
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class SplineVRDraw : MonoBehaviour
{
    public enum DrawMode { None, Freehand, StraightLine, Rectangle, Circle, Polygon }
    public DrawMode drawMode = DrawMode.Freehand;
    public VRDrawSettings settings;
    public float maxWidth = 1.0f, maxHeight = 1.0f;
    public float minLineLength = 0.02f;
    public RedoUndoManager redoUndoManager;
}
```
Explanation
- We centralize all shape tools behind a single component with a `DrawMode` enum and shared settings, enabling consistent handling of bounds, materials, and history.
