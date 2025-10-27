# Mermaid Class Diagram

The diagram below captures the project classes under `Assets/Scripts`, including their key properties and public methods. Unity lifecycle methods (e.g., `Start`, `Awake`, `Update`) are shown where present.

```mermaid
classDiagram

%% =========================
%% Drawing + Editing Systems
%% =========================

class DrawingSystem {
  +Camera drawingCamera
  +GameObject drawingPlane
  +LineRenderer lineRendererPrefab
  +float minPointDistance
  +float lineWidth
  +Color drawingColor
  +bool constrainDrawing
  +float maxDrawingWidth
  +float maxDrawingHeight
  +Vector2 drawingAreaCenter
  -void Start()
  -void Update()
  +void EnableDrawingMode()
  +void DisableDrawingMode()
  -void StartDrawingLine()
  -void ContinueDrawingLine()
  -void FinishDrawingLine()
  -Vector2 GetInputPosition()
  -Vector3 GetWorldPointFromInput(Vector2 screenPosition)
  -bool IsPointInBounds(Vector2 localPoint)
  -LineRenderer CreateNewLineRenderer()
  -void UpdateLineRenderer()
  +void ClearAllDrawings()
  +int GetDrawnLinesCount()
  +void LogDrawingInfo()
}

class SplineVRDraw {
  +Transform whiteboardPlane
  +Transform drawingTip
  +XRController rightController
  +RedoUndoManager redoUndoManager
  +VRDrawSettings settings
  +float drawDistanceThreshold
  +float maxWidth
  +float maxHeight
  +bool alwaysDraw
  +InputActionProperty stopDrawingAction
  +InputActionProperty startDrawingAction
  +InputActionProperty dragAction
  +bool dragClosedShapesOnly
  +bool allowOpenLineDrag
  +float openLinePickDistance
  +InputActionProperty resizeAction
  +bool resizeClosedShapesOnly
  +bool allowOpenLineResize
  +float minResizeScale
  +float maxResizeScale
  +InputActionProperty undoAction
  +InputActionProperty redoAction
  +DrawMode drawMode
  +bool useStraightLineMode
  +float minLineLength
  +int polygonSides
  -void OnEnable()
  -void OnDisable()
  -void Update()
  +void StartDrawing()
  +void StopDrawing()
  +void ToggleDrawing()
  +void ToggleStraightLineDrawing()
  +void ToggleStraightLineMode()
  +void ToggleRectangleDrawing()
  +void ToggleCircleDrawing()
  +void TogglePolygonDrawing()
}

class VRDrawSettings {
  +Material lineMaterial
  +float lineWidth
  +Color lineColor
}

class StraightLineVRDraw {
  +Transform whiteboardPlane
  +Transform drawingTip
  +Transform leftControllerTip
  +Transform rightControllerTip
  +XRController rightController
  +RedoUndoManager redoUndoManager
  +StraightLineVRDrawSettings settings
  +float minLineLength
  +bool useTriggerForLine
  +int polygonSides
  +float maxWidth
  +float maxHeight
  +bool alwaysDraw
  -void Update()
  -void UpdateDrawing()
  -void StartDragging()
  -void UpdateDragging()
  -void StopDragging()
  -void FinishDrawing()
  -Vector3 GetLineCenter(LineRenderer line)
  -LineRenderer FindClosestStraightLine(Vector3 point)
  -Vector3 ClampWorldToBounds(Vector3 worldPoint)
  -float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
  -void StartDrawingInternal(DrawMode mode)
  -void StopDrawingInternal()
  -bool ShouldKeepShapeOnFinish()
  -void UpdateRectanglePreview(Vector3 currentWorld)
  -void UpdateCirclePreview(Vector3 currentWorld, int segments)
}

class StraightLineVRDrawSettings {
  +Material lineMaterial
  +float lineWidth
  +Color lineColor
}

class RectangleVRDraw {
  +Transform whiteboardPlane
  +Transform leftControllerTip
  +Transform rightControllerTip
  +RedoUndoManager redoUndoManager
  +RectangleVRDrawSettings settings
  +float sizeChangeSpeed
  +bool autoIncreaseSize
  +float maxRectWidth
  +float minRectWidth
  +float maxRectHeight
  +float minRectHeight
  +float maxWidth
  +float maxHeight
  +bool showDebugLogs
  -void Update()
  +void ToggleResizeMode()
  +void StartDrawing()
  +void StopDrawing()
  +void ToggleDrawing()
}

class RectangleVRDrawSettings {
  +Material lineMaterial
  +float lineWidth
  +Color lineColor
}

class CircleVRDraw {
  +Transform whiteboardPlane
  +Transform leftControllerTip
  +Transform rightControllerTip
  +RedoUndoManager redoUndoManager
  +CircleVRDrawSettings settings
  +int circleSegments
  +float radiusChangeSpeed
  +bool autoIncreaseRadius
  +float maxRadius
  +float minRadius
  +float maxWidth
  +float maxHeight
  +bool showDebugLogs
  -void Update()
  +void ToggleResizeMode()
  +void StartDrawing()
  +void StopDrawing()
  +void ToggleDrawing()
}

class CircleVRDrawSettings {
  +Material lineMaterial
  +float lineWidth
  +Color lineColor
}

class PolygonVRDraw {
  +Transform whiteboardPlane
  +Transform leftControllerTip
  +Transform rightControllerTip
  +RedoUndoManager redoUndoManager
  +PolygonVRDrawSettings settings
  +int polygonSides
  +float radiusChangeSpeed
  +bool autoIncreaseRadius
  +float maxRadius
  +float minRadius
  +float maxWidth
  +float maxHeight
  +bool showDebugLogs
  -void Update()
  +void ToggleResizeMode()
  +void StartDrawing()
  +void StopDrawing()
  +void ToggleDrawing()
}

class PolygonVRDrawSettings {
  +Material lineMaterial
  +float lineWidth
  +Color lineColor
}

class RedoUndoManager {
  +int UndoCount
  +int RedoCount
  +void RegisterLine(GameObject line)
  +void Undo()
  +void Redo()
  +void ClearAll()
}

%% =========================
%% Import / Export
%% =========================

class ExportSVG {
  +Transform whiteboardPlane
  +RectTransform whiteboardTextArea
  +WhiteboardTextManager textManager
  +int canvasWidth
  +int canvasHeight
  +string fileName
  +bool autoCalculateBounds
  +Vector2 boundsMin
  +Vector2 boundsMax
  +bool showDebugLogs
  +void ExportToSVG()
}

class ExportDXF {
  +Transform whiteboardPlane
  +string fileName
  +bool autoCalculateBounds
  +Vector2 boundsMin
  +Vector2 boundsMax
  +float dxfScale
  +bool showDebugLogs
  +void ExportToDXF()
}

class DXFImporter {
  +List<Vector3[]> importedLines
  +void LoadDXF(string filePath)
}

class DXFPathRenderer {
  +Material lineMaterial
  +float lineWidth
  +void RenderPaths(List<Vector3[]> paths)
}

%% =========================
%% UI / Text / Utilities
%% =========================

class WhiteboardTextManager {
  +Button openInputButton
  +TMP_InputField textInputField
  +RectTransform whiteboardArea
  +TextMeshProUGUI textPrefab
  +List<VectorTextEntry> textHistory
  -void Start()
  +void VerifySetup()
  -void ShowInputField()
  -void OnTextEntered(string newText)
  +string ExportToSVG(int width, int height)
  -void Update()
}

class VectorTextEntry {
  +string content
  +Vector2 position
  +int fontSize
}

class DraggableText {
  -RectTransform rectTransform
  -Canvas canvas
  -RectTransform whiteboardRect
  -bool isDragging
  -Vector2 dragStartOffset
  -Camera eventCamera
  -void Awake()
  +void Initialize(RectTransform whiteboard)
  -void Update()
}

class MenuManager {
  +GameObject openImportSubmenu
  +GameObject exportSubmenu
  +Canvas mainCanvas
  -void Awake()
  +void ShowSubmenu(GameObject submenu)
  -void HideAllSubmenus()
  +void OnImportButtonClick()
  +void OnExportButtonClick()
}

class FocusModeToggle {
  +GameObject[] objectsToHide
  +Image whiteBoardImage
  +Color focusColor
  -bool focusMode
  -Color originalColor
  -void Start()
  +void ToggleFocusMode()
}

class ClickTest {
  +void OnPointerClick(PointerEventData eventData)
  -void OnEnable()
}

class StraightLineDots {
  +Camera drawingCamera
  +GameObject drawingPlane
  +GameObject dotPrefab
  +float dotSpacing
  +float dotSize
  +Color dotColor
  -void Start()
  -void Update()
  +void EnableDrawing()
  +void DisableDrawing()
  +void ClearAll()
  +List<List<Vector3>> GetAllLines()
}

class FileDisplayManager {
}

class UserPresenceFeature {
  +const string featureId
  +bool OnInstanceCreate(ulong instance)
}

%% =========================
%% File Import UI Manager
%% =========================

class FileListManager {
  +GameObject fileSelectionPanel
  +Button togglePanelButton
  +Transform scrollViewContent
  +GameObject fileButtonPrefab
  +string importFolderPath
  +GameObject drawingManagerObject
  +GameObject drawingPlane
  +LineRenderer lineRendererPrefab
  +float whiteboardWidth
  +float whiteboardHeight
  +Vector2 whiteboardCenter
  +Color importedDrawingColor
  +bool useSimplifiedParsing
  +bool showDXFAsPoints
  +GameObject pointPrefab
  +bool logDXFContent
  +bool enableDebugLogs
  +float importedDrawingZOffset
  -List<GameObject> instantiatedButtons
  -List<GameObject> currentDrawings
  -bool isPanelOpen
  -const double COORD_ABS_MAX
  -void Start()
  +void TogglePanel()
  +void DebugButtonBlocking()
  +void ClearWhiteboard()
}

class DrawingCommand {
  +enum CommandType { MoveTo, LineTo, Circle }
  +CommandType type
  +Vector2 position
  +float radius
}

%% =========================
%% Relationships
%% =========================

ExportSVG --> WhiteboardTextManager : reads textHistory
DXFImporter --> DXFPathRenderer : renders imported paths
SplineVRDraw --> RedoUndoManager : registers lines
StraightLineVRDraw --> RedoUndoManager : registers lines
RectangleVRDraw --> RedoUndoManager : registers lines
CircleVRDraw --> RedoUndoManager : registers lines
PolygonVRDraw --> RedoUndoManager : registers lines
WhiteboardTextManager --> DraggableText : adds component
SplineVRDraw --> VRDrawSettings
StraightLineVRDraw --> StraightLineVRDrawSettings
RectangleVRDraw --> RectangleVRDrawSettings
CircleVRDraw --> CircleVRDrawSettings
PolygonVRDraw --> PolygonVRDrawSettings
FileListManager ..> DrawingCommand : produces

```

Notes
- Classes from Unity packages and samples are intentionally omitted.
- Only project classes located under `Assets/Scripts` are diagrammed.

