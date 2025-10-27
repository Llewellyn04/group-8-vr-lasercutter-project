# Presentation Script — Magane

Role and topic
- Tech stack

Objectives (2–3 minutes)
- Summarize platform, packages, XR stack, rendering, input, and third‑party libraries used.
- Call out versions and why they matter.

Slide outline
- Unity + Rendering
- XR + Input
- UI + Text
- Interop libraries

Unity and rendering
- Unity Editor: 6000.2.2f1 (Unity 6) — `ProjectSettings/ProjectVersion.txt`
- Render pipeline: Universal Render Pipeline 17.2.0
  - Package: `com.unity.render-pipelines.universal@17.2.0` (`Packages/manifest.json`)

XR and input
- XR Interaction Toolkit 3.2.1 — `com.unity.xr.interaction.toolkit@3.2.1`
- OpenXR Plugin 1.15.1 — `com.unity.xr.openxr@1.15.1`
- Meta OpenXR 2.3.0 — `com.unity.xr.meta-openxr@2.3.0` (enables Meta runtimes)
- XR Management 4.5.3 — `com.unity.xr.management@4.5.3`
- Input System 1.14.2 — `com.unity.inputsystem@1.14.2`

UI and text
- UGUI (Unity UI) modules
- TextMeshPro (TMPro) integrated — code: `using TMPro;`
- Example scripts: `Assets/Scripts/TextManager.cs`, `Assets/Scripts/DraggableText.cs`

Import/Export and utilities
- DXF import: `netDxf` (C# library), used in `Assets/Scripts/ModelingTools/Import/DXFImporter.cs`
- DXF path rendering: `Assets/Scripts/ModelingTools/Import/DXFPathRenderer.cs` (LineRenderer + XRGrabInteractable)
- SVG/DXF export: `Assets/Scripts/ExportSVG.cs`, `Assets/Scripts/ExportDXF.cs`

Build targets and devices
- PCVR (Standalone) and Android XR build target groups supported via OpenXR.
- Device support includes Meta headsets (Meta OpenXR package) and PC OpenXR runtimes.

Configuration pointers
- OpenXR: enable required features and interaction profiles in Project Settings → XR Plug‑in Management → OpenXR.
- XR Interaction Toolkit: Action‑based controllers; input bindings assigned through `InputActionProperty` fields in scripts.
- URP: ensure a URP asset is assigned in Graphics settings if you add new materials/effects.

Q&A prompts
- Why OpenXR? — Single API surface for multiple headsets/controllers.
- Why URP? — Good VR performance/predictable lighting at scale.

Code snippets (versions and deps)
- Unity version (`ProjectSettings/ProjectVersion.txt`)
```text
m_EditorVersion: 6000.2.2f1
m_EditorVersionWithRevision: 6000.2.2f1 (ea398eefe1c2)
```

- Packages (excerpt from `Packages/manifest.json`)
```json
{
  "dependencies": {
    "com.unity.inputsystem": "1.14.2",
    "com.unity.xr.openxr": "1.15.1",
    "com.unity.xr.interaction.toolkit": "3.2.1",
    "com.unity.xr.management": "4.5.3",
    "com.unity.xr.meta-openxr": "2.3.0",
    "com.unity.render-pipelines.universal": "17.2.0",
    "com.unity.xr.hands": "1.5.1"
  }
}
```
Explanation
- We depend on OpenXR + XRI for cross‑device XR, Input System for actions, and URP for rendering performance in VR.
