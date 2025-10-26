using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[System.Serializable]
public class VRDrawSettings
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class SplineVRDraw : MonoBehaviour
{
    [Header("References")]
    public Transform whiteboardPlane;
    public Transform drawingTip;
    public XRController rightController;
    public RedoUndoManager redoUndoManager;

    [Header("Drawing Settings")]
    public VRDrawSettings settings;
    public float drawDistanceThreshold = 0.01f;

    [Header("Drawing Bounds")]
    public float maxWidth = 1.0f;
    public float maxHeight = 1.0f;

    [Header("Debug")]
    public bool alwaysDraw = false;

    private LineRenderer currentLine;
    private bool isDrawing = false;
    private bool lastTriggerState = false;
    
    [Header("Stop Input")]
    [Tooltip("Input Action to stop the current drawing (bind to a controller button, e.g. A/B)")]
    public UnityEngine.InputSystem.InputActionProperty stopDrawingAction;
    private bool lastStopPressed = false;

    [Header("Start Input")]
    [Tooltip("Single Input Action to start/resume drawing in the current mode (Freehand, StraightLine, Rectangle, Circle, Polygon)")]
    public UnityEngine.InputSystem.InputActionProperty startDrawingAction;
    private bool lastStartPressed = false;
    
    [Header("Drag")]
    [Tooltip("Input Action used to grab/drag a closed shape (e.g. A button)")]
    public UnityEngine.InputSystem.InputActionProperty dragAction;
    [Tooltip("Only allow dragging for closed shapes (looped LineRenderers)")]
    public bool dragClosedShapesOnly = true;
    [Tooltip("If false, also allow picking open lines by distance to segments (meters)")]
    public bool allowOpenLineDrag = false;
    [Min(0f)] public float openLinePickDistance = 0.01f;

    private bool isDragging = false;
    private LineRenderer draggedLine;
    private Vector3 dragStartLocalPoint;
    private Vector3[] draggedOriginalLocalPoints;

    [Header("Resize")]
    [Tooltip("Input Action used to resize a shape (e.g. B button)")]
    public UnityEngine.InputSystem.InputActionProperty resizeAction;
    [Tooltip("Only allow resizing for closed shapes (looped LineRenderers)")]
    public bool resizeClosedShapesOnly = true;
    [Tooltip("If false, also allow picking open lines by distance to segments (meters)")]
    public bool allowOpenLineResize = false;
    [Min(0.001f)] public float minResizeScale = 0.2f;
    public float maxResizeScale = 5f;

    private bool isResizing = false;
    private LineRenderer resizingLine;
    private Vector3 resizePivotLocal;
    private float resizeStartRadius = 0f;
    private Vector3[] resizingOriginalLocalPoints;

    public enum DrawMode { None, Freehand, StraightLine, Rectangle, Circle, Polygon }

    [Header("Modes")]
    [Tooltip("When enabled, draws a 2‑point straight line instead of a freehand spline")]
    public bool useStraightLineMode = false; // Kept for backward-compatibility
    [Tooltip("Current drawing mode")] public DrawMode drawMode = DrawMode.Freehand;
    [Tooltip("Minimum size threshold (world units)")] public float minLineLength = 0.02f;
    [Tooltip("Sides for polygon mode")] [Min(3)] public int polygonSides = 5;
    private const int circleSegmentsDefault = 64;

    // Straight line state
    private bool hasStartPoint = false;
    private Vector3 startWorldPoint;

    void OnEnable()
    {
        if (dragAction.action != null) dragAction.action.Enable();
        if (resizeAction.action != null) resizeAction.action.Enable();
        if (stopDrawingAction.action != null) stopDrawingAction.action.Enable();
        if (startDrawingAction.action != null) startDrawingAction.action.Enable();
        if (undoAction.action != null) undoAction.action.Enable();
        if (redoAction.action != null) redoAction.action.Enable();
    }

    void OnDisable()
    {
        if (dragAction.action != null) dragAction.action.Disable();
        if (resizeAction.action != null) resizeAction.action.Disable();
        if (stopDrawingAction.action != null) stopDrawingAction.action.Disable();
        if (startDrawingAction.action != null) startDrawingAction.action.Disable();
        if (undoAction.action != null) undoAction.action.Disable();
        if (redoAction.action != null) redoAction.action.Disable();
    }

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

        // Manipulations are available only when not drawing
        if (!isDrawing)
        {
            UpdateResizing();
            if (!isResizing)
                UpdateDragging();
        }

        // Undo/Redo bindings (trigger on press edges)
        UpdateUndoRedo();

        // Optional: controller button bound Stop (edge-triggered)
        bool stopPressed = false;
        if (stopDrawingAction.action != null)
        {
            try { stopPressed = stopDrawingAction.action.ReadValue<float>() > 0.5f; }
            catch { stopPressed = false; }
        }
        if (stopPressed && !lastStopPressed && isDrawing)
        {
            StopDrawing();
        }
        lastStopPressed = stopPressed;

        // Start/Resume current mode (single button) on press edge
        bool startPressed = false;
        if (startDrawingAction.action != null)
        {
            try { startPressed = startDrawingAction.action.ReadValue<float>() > 0.5f; }
            catch { startPressed = false; }
        }
        if (startPressed && !lastStartPressed && !isDrawing)
        {
            StartDrawing(); // uses current drawMode / useStraightLineMode
        }
        lastStartPressed = startPressed;
    }

    void UpdateDrawing()
    {
        if (currentLine == null || whiteboardPlane == null || drawingTip == null) return;

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(drawingTip.position - drawingTip.forward * 0.05f, drawingTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 localPoint = whiteboardPlane.InverseTransformPoint(hitPoint);

            float halfWidth = maxWidth * 0.5f;
            float halfHeight = maxHeight * 0.5f;
            localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
            localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight, halfHeight);
            localPoint.z = 0f;

            Vector3 clampedWorldPoint = whiteboardPlane.TransformPoint(localPoint);

            if (Mathf.Abs(localPoint.x) <= halfWidth && Mathf.Abs(localPoint.y) <= halfHeight)
            {
                switch (drawMode)
                {
                    case DrawMode.StraightLine:
                        if (!hasStartPoint)
                        {
                            hasStartPoint = true;
                            startWorldPoint = clampedWorldPoint;
                            currentLine.positionCount = 2;
                            currentLine.loop = false;
                            currentLine.SetPosition(0, startWorldPoint);
                            currentLine.SetPosition(1, startWorldPoint);
                        }
                        else
                        {
                            currentLine.SetPosition(1, clampedWorldPoint);
                        }
                        break;
                    case DrawMode.Rectangle:
                        if (!hasStartPoint)
                        {
                            hasStartPoint = true;
                            startWorldPoint = clampedWorldPoint;
                        }
                        UpdateRectanglePreview(clampedWorldPoint);
                        break;
                    case DrawMode.Circle:
                        if (!hasStartPoint)
                        {
                            hasStartPoint = true;
                            startWorldPoint = clampedWorldPoint;
                        }
                        UpdateCirclePreview(clampedWorldPoint, circleSegmentsDefault);
                        break;
                    case DrawMode.Polygon:
                        if (!hasStartPoint)
                        {
                            hasStartPoint = true;
                            startWorldPoint = clampedWorldPoint;
                        }
                        UpdateCirclePreview(clampedWorldPoint, Mathf.Max(3, polygonSides));
                        break;
                    case DrawMode.Freehand:
                    default:
                        // Freehand mode: append points continuously
                        currentLine.positionCount++;
                        currentLine.SetPosition(currentLine.positionCount - 1, clampedWorldPoint);
                        break;
                }
            }
        }
    }

    public void StartDrawing()
    {
        if (settings == null)
        {
            Debug.LogWarning("VRDrawSettings not assigned!");
            return;
        }

        // Keep legacy bool in sync
        if (useStraightLineMode) drawMode = DrawMode.StraightLine;
        if (drawMode == DrawMode.None) drawMode = DrawMode.Freehand;

        string objName = drawMode switch
        {
            DrawMode.StraightLine => "VR_StraightLine",
            DrawMode.Rectangle   => "VR_Rectangle",
            DrawMode.Circle      => "VR_Circle",
            DrawMode.Polygon     => "VR_Polygon",
            _                    => "VR_Drawing"
        };
        GameObject lineObj = new GameObject(objName);
        currentLine = lineObj.AddComponent<LineRenderer>();

        currentLine.material = settings.lineMaterial;
        currentLine.startColor = settings.lineColor;
        currentLine.endColor = settings.lineColor;
        currentLine.startWidth = settings.lineWidth;
        currentLine.endWidth = settings.lineWidth;
        currentLine.positionCount = 0;

        isDrawing = true;
        hasStartPoint = false;
        Debug.Log($"Started {drawMode} Drawing");
    }

    public void StopDrawing()
    {
        Debug.Log("StopDrawing() called!"); // DEBUG

        if (!isDrawing)
        {
            Debug.Log("Not drawing, returning early"); // DEBUG
            return;
        }

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
                // Close shapes if appropriate
                if (drawMode == DrawMode.Rectangle || drawMode == DrawMode.Circle || drawMode == DrawMode.Polygon)
                    currentLine.loop = true;
                else
                    currentLine.loop = false;

                if (redoUndoManager != null)
                {
                    GameObject lineObj = currentLine.gameObject;
                    redoUndoManager.RegisterLine(lineObj);
                    Debug.Log($"{drawMode} registered: {lineObj.name}. Undo stack count: {redoUndoManager.UndoCount}");
                }
                else
                {
                    Debug.LogWarning("RedoUndoManager not assigned!");
                }
            }
            else
            {
                Destroy(currentLine.gameObject);
                Debug.Log($"{drawMode} discarded (too short or too few points).");
            }
        }

        currentLine = null;
        hasStartPoint = false;
        Debug.Log($"Stopped {drawMode} Drawing");
        // Keep drawMode as-is for next start unless changed by toggles
    }

    public void ToggleDrawing()
    {
        if (isDrawing)
            StopDrawing();
        else
        {
            useStraightLineMode = false;
            drawMode = DrawMode.Freehand;
            StartDrawing();
        }
    }

    // Toggle a 2‑point straight line drawing (same principle as StraightLineVRDraw)
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

    // Optional: just flip mode; next trigger press will draw in that mode
    public void ToggleStraightLineMode()
    {
        useStraightLineMode = !useStraightLineMode;
        drawMode = useStraightLineMode ? DrawMode.StraightLine : DrawMode.Freehand;
        Debug.Log($"Straight line mode: {useStraightLineMode}");
    }

    // Rectangle/Circle/Polygon toggles (start/stop immediately like straight line toggle)
    public void ToggleRectangleDrawing()
    {
        if (isDrawing && drawMode == DrawMode.Rectangle) StopDrawing();
        else { if (isDrawing) StopDrawing(); useStraightLineMode = false; drawMode = DrawMode.Rectangle; StartDrawing(); }
    }

    public void ToggleCircleDrawing()
    {
        if (isDrawing && drawMode == DrawMode.Circle) StopDrawing();
        else { if (isDrawing) StopDrawing(); useStraightLineMode = false; drawMode = DrawMode.Circle; StartDrawing(); }
    }

    public void TogglePolygonDrawing()
    {
        if (isDrawing && drawMode == DrawMode.Polygon) StopDrawing();
        else { if (isDrawing) StopDrawing(); useStraightLineMode = false; drawMode = DrawMode.Polygon; StartDrawing(); }
    }

    // ===== Helpers to build previews for Rectangle/Circle/Polygon =====
    void UpdateRectanglePreview(Vector3 currentWorld)
    {
        if (currentLine == null) return;
        // Build axis-aligned rectangle between start and current in plane local space
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

    void UpdateCirclePreview(Vector3 currentWorld, int segments)
    {
        if (currentLine == null) return;
        // Work entirely in the whiteboard's local XY to respect scaling
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

            // Clamp within local bounds then convert to world
            localPoint.x = Mathf.Clamp(localPoint.x, -halfW, halfW);
            localPoint.y = Mathf.Clamp(localPoint.y, -halfH, halfH);

            Vector3 worldPoint = whiteboardPlane.TransformPoint(localPoint);
            currentLine.SetPosition(i, worldPoint);
        }
    }

    // ===== Dragging logic =====
    void UpdateDragging()
    {
        if (whiteboardPlane == null || drawingTip == null) return;

        bool dragPressed = false;
        if (dragAction.action != null)
        {
            try { dragPressed = dragAction.action.ReadValue<float>() > 0.5f; }
            catch { dragPressed = false; }
        }

        if (!TryGetPointerOnBoard(out Vector3 hitWorld, out Vector3 hitLocal))
        {
            if (!dragPressed && isDragging)
                EndDrag();
            return;
        }

        if (dragPressed)
        {
            if (!isDragging)
            {
                if (TryPickShape(hitLocal, out LineRenderer picked, out Vector3[] originalLocal))
                {
                    isDragging = true;
                    draggedLine = picked;
                    draggedOriginalLocalPoints = originalLocal;
                    dragStartLocalPoint = hitLocal;
                }
            }
            else if (draggedLine != null && draggedOriginalLocalPoints != null)
            {
                Vector3 delta = hitLocal - dragStartLocalPoint;
                int count = draggedOriginalLocalPoints.Length;
                Vector3[] newWorld = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    Vector3 newLocal = draggedOriginalLocalPoints[i] + delta;
                    newWorld[i] = whiteboardPlane.TransformPoint(newLocal);
                }
                if (draggedLine.positionCount != count) draggedLine.positionCount = count;
                draggedLine.SetPositions(newWorld);
            }
        }
        else
        {
            if (isDragging)
                EndDrag();
        }
    }

    void EndDrag()
    {
        isDragging = false;
        draggedLine = null;
        draggedOriginalLocalPoints = null;
    }

    bool TryGetPointerOnBoard(out Vector3 worldPoint, out Vector3 localPoint)
    {
        worldPoint = Vector3.zero;
        localPoint = Vector3.zero;
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(drawingTip.position - drawingTip.forward * 0.05f, drawingTip.forward);
        if (plane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            localPoint = whiteboardPlane.InverseTransformPoint(worldPoint);
            localPoint.z = 0f;
            return true;
        }
        return false;
    }

    bool TryPickShape(Vector3 pointerLocal, out LineRenderer picked, out Vector3[] originalLocal)
        => TryPickShapeWithOptions(pointerLocal, dragClosedShapesOnly, allowOpenLineDrag, openLinePickDistance, out picked, out originalLocal);

    bool TryPickShapeWithOptions(Vector3 pointerLocal, bool closedOnly, bool allowOpen, float openPickDistance,
        out LineRenderer picked, out Vector3[] originalLocal)
    {
        picked = null;
        originalLocal = null;

        LineRenderer[] candidates = FindObjectsOfType<LineRenderer>();
        foreach (var lr in candidates)
        {
            if (lr == null || !lr.gameObject.activeInHierarchy) continue;
            int count = lr.positionCount;
            if (count < 2) continue;

            Vector3[] localPts = new Vector3[count];
            for (int i = 0; i < count; i++)
                localPts[i] = whiteboardPlane.InverseTransformPoint(lr.GetPosition(i));

            if (lr.loop && count >= 3)
            {
                if (PointInPolygon(pointerLocal, localPts))
                {
                    picked = lr;
                    originalLocal = localPts;
                    return true;
                }
            }
            else if (!closedOnly && allowOpen)
            {
                if (DistanceToPolyline(pointerLocal, localPts) <= openPickDistance)
                {
                    picked = lr;
                    originalLocal = localPts;
                    return true;
                }
            }
        }
        return false;
    }

    // ===== Resizing logic =====
    void UpdateResizing()
    {
        if (whiteboardPlane == null || drawingTip == null) return;

        bool resizePressed = false;
        if (resizeAction.action != null)
        {
            try { resizePressed = resizeAction.action.ReadValue<float>() > 0.5f; }
            catch { resizePressed = false; }
        }

        if (!TryGetPointerOnBoard(out Vector3 hitWorld, out Vector3 hitLocal))
        {
            if (!resizePressed && isResizing) EndResize();
            return;
        }

        if (resizePressed)
        {
            if (!isResizing)
            {
                if (TryPickShapeWithOptions(hitLocal, resizeClosedShapesOnly, allowOpenLineResize, openLinePickDistance, out LineRenderer picked, out Vector3[] originalLocal))
                {
                    isResizing = true;
                    resizingLine = picked;
                    resizingOriginalLocalPoints = originalLocal;
                    resizePivotLocal = ComputeCentroid(originalLocal);
                    resizeStartRadius = Mathf.Max(Vector2.Distance(new Vector2(hitLocal.x, hitLocal.y), new Vector2(resizePivotLocal.x, resizePivotLocal.y)), 0.001f);
                }
            }
            else if (resizingLine != null && resizingOriginalLocalPoints != null)
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
        }
        else
        {
            if (isResizing) EndResize();
        }
    }

    void EndResize()
    {
        isResizing = false;
        resizingLine = null;
        resizingOriginalLocalPoints = null;
        resizeStartRadius = 0f;
    }

    static Vector3 ComputeCentroid(Vector3[] points)
    {
        if (points == null || points.Length == 0) return Vector3.zero;
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < points.Length; i++)
        {
            sum.x += points[i].x;
            sum.y += points[i].y;
        }
        Vector3 c = new Vector3(sum.x / points.Length, sum.y / points.Length, 0f);
        return c;
    }

    static bool PointInPolygon(Vector3 pLocal, Vector3[] polyLocal)
    {
        bool inside = false;
        int j = polyLocal.Length - 1;
        for (int i = 0; i < polyLocal.Length; i++)
        {
            Vector3 pi = polyLocal[i];
            Vector3 pj = polyLocal[j];
            bool intersect = ((pi.y > pLocal.y) != (pj.y > pLocal.y)) &&
                             (pLocal.x < (pj.x - pi.x) * (pLocal.y - pi.y) / (pj.y - pi.y + Mathf.Epsilon) + pi.x);
            if (intersect) inside = !inside;
            j = i;
        }
        return inside;
    }

    static float DistanceToPolyline(Vector3 pLocal, Vector3[] ptsLocal)
    {
        float minDist = float.MaxValue;
        for (int i = 0; i < ptsLocal.Length - 1; i++)
        {
            float d = DistancePointToSegment2D(pLocal, ptsLocal[i], ptsLocal[i + 1]);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    static float DistancePointToSegment2D(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 p2 = new Vector2(p.x, p.y);
        Vector2 a2 = new Vector2(a.x, a.y);
        Vector2 b2 = new Vector2(b.x, b.y);
        Vector2 ab = b2 - a2;
        float t = Vector2.Dot(p2 - a2, ab) / (ab.sqrMagnitude + Mathf.Epsilon);
        t = Mathf.Clamp01(t);
        Vector2 closest = a2 + t * ab;
        return Vector2.Distance(p2, closest);
    }

    // ===== Undo/Redo bindings =====
    [Header("Undo/Redo")]
    [Tooltip("Input Action to Undo the last stroke")]
    public UnityEngine.InputSystem.InputActionProperty undoAction;
    [Tooltip("Input Action to Redo the last undone stroke")]
    public UnityEngine.InputSystem.InputActionProperty redoAction;

    private bool lastUndoPressed = false;
    private bool lastRedoPressed = false;

    void UpdateUndoRedo()
    {
        if (redoUndoManager == null)
            return;

        bool undoPressed = false;
        bool redoPressed = false;

        if (undoAction.action != null)
        {
            try { undoPressed = undoAction.action.ReadValue<float>() > 0.5f; } catch { undoPressed = false; }
        }
        if (redoAction.action != null)
        {
            try { redoPressed = redoAction.action.ReadValue<float>() > 0.5f; } catch { redoPressed = false; }
        }

        if (undoPressed && !lastUndoPressed)
        {
            // End any active manipulation first
            if (isDragging) EndDrag();
            if (isResizing) EndResize();
            redoUndoManager.Undo();
        }
        if (redoPressed && !lastRedoPressed)
        {
            if (isDragging) EndDrag();
            if (isResizing) EndResize();
            redoUndoManager.Redo();
        }

        lastUndoPressed = undoPressed;
        lastRedoPressed = redoPressed;
    }
}
