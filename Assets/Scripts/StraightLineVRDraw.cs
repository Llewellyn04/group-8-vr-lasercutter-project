using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[System.Serializable]
public class StraightLineVRDrawSettings
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class StraightLineVRDraw : MonoBehaviour
{
    public enum DrawMode { None, Line, Rectangle, Circle, Polygon }

    [Header("References")]
    public Transform whiteboardPlane;
    public Transform drawingTip;               // Usually the right controller tip for drawing
    public Transform leftControllerTip;        // For resize mode
    public Transform rightControllerTip;       // For drag mode (can be same as drawingTip)
    public XRController rightController;       // Used to read trigger
    public RedoUndoManager redoUndoManager;

    [Header("Drawing Settings")]
    public StraightLineVRDrawSettings settings;
    public float minLineLength = 0.02f;        // Discard lines shorter than this
    [Tooltip("Use trigger on right controller to start/stop line drawing (Line mode only)")]
    public bool useTriggerForLine = true;
    [Tooltip("Number of sides for regular polygon drawing")]
    [Min(3)] public int polygonSides = 5;

    [Header("Drawing Bounds")]
    public float maxWidth = 1.0f;              // Local-space width from center
    public float maxHeight = 1.0f;             // Local-space height from center

    [Header("Debug")]
    public bool alwaysDraw = false;            // If true, preview end while not holding trigger

    private LineRenderer currentLine;
    private bool isDrawing = false;
    private bool isDragging = false;
    private bool isResizing = false;
    private bool lastTriggerState = false;
    private Vector3 startWorldPoint;
    private bool hasStartPoint = false;
    private int resizingEndIndex = -1;         // 0 or 1
    private Vector3 dragOffset;                // Offset from hitpoint to center during drag
    private Vector3 p0OffsetFromCenter;        // Initial relative offsets for drag
    private Vector3 p1OffsetFromCenter;
    private DrawMode drawMode = DrawMode.None;
    private const int circleSegmentsDefault = 64;

    void Update()
    {
        // Handle trigger input for Line mode only (optional)
        if (useTriggerForLine && drawMode == DrawMode.Line && rightController != null && rightController.inputDevice.isValid)
        {
            rightController.inputDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);

            if (triggerPressed && !lastTriggerState && !isDrawing)
                StartDrawingInternal(DrawMode.Line);
            else if (!triggerPressed && lastTriggerState && isDrawing)
                StopDrawingInternal();

            lastTriggerState = triggerPressed;
        }

        // While in modes, update continuously
        if (isResizing)
            UpdateResizeWithLeftController();

        if (isDragging)
            UpdateDragging();

        if (isDrawing || alwaysDraw)
            UpdateDrawing();
    }

    void UpdateDrawing()
    {
        if (whiteboardPlane == null || drawingTip == null)
            return;

        // Raycast from tip to the whiteboard plane
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(drawingTip.position - drawingTip.forward * 0.05f, drawingTip.forward);

        if (!plane.Raycast(ray, out float enter))
            return;

        Vector3 hitPoint = ray.GetPoint(enter);

        // Clamp to bounds in local space
        Vector3 localPoint = whiteboardPlane.InverseTransformPoint(hitPoint);
        float halfWidth = maxWidth * 0.5f;
        float halfHeight = maxHeight * 0.5f;
        localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight, halfHeight);
        localPoint.z = 0f;
        Vector3 clampedWorldPoint = whiteboardPlane.TransformPoint(localPoint);

        // Allow updating while drawing or when explicitly previewing
        if (!isDrawing && !(alwaysDraw && currentLine != null))
            return;

        // Ensure line exists and set positions
        if (currentLine == null)
            return;

        if (!hasStartPoint) return;

        switch (drawMode)
        {
            case DrawMode.Line:
                currentLine.loop = false;
                currentLine.positionCount = 2;
                currentLine.SetPosition(0, startWorldPoint);
                currentLine.SetPosition(1, clampedWorldPoint);
                break;
            case DrawMode.Rectangle:
                UpdateRectanglePreview(clampedWorldPoint);
                break;
            case DrawMode.Circle:
                UpdateCirclePreview(clampedWorldPoint, circleSegmentsDefault);
                break;
            case DrawMode.Polygon:
                int sides = Mathf.Max(3, polygonSides);
                UpdateCirclePreview(clampedWorldPoint, sides);
                break;
            default:
                break;
        }
    }

    // Public toggles callable from Unity Events / Input bindings
    public void ToggleDrawing()
    {
        if (isDrawing) StopDrawingInternal(); else StartDrawingInternal(DrawMode.Line);
    }

    public void ToggleRectangleDrawing()
    {
        if (isDrawing && drawMode == DrawMode.Rectangle) StopDrawingInternal(); else StartDrawingInternal(DrawMode.Rectangle);
    }

    public void ToggleCircleDrawing()
    {
        if (isDrawing && drawMode == DrawMode.Circle) StopDrawingInternal(); else StartDrawingInternal(DrawMode.Circle);
    }

    public void TogglePolygonDrawing()
    {
        if (isDrawing && drawMode == DrawMode.Polygon) StopDrawingInternal(); else StartDrawingInternal(DrawMode.Polygon);
    }

    // Backwards-compatible direct calls (if already wired in scene)
    public void StartDrawing() { StartDrawingInternal(DrawMode.Line); }
    public void StopDrawing() { StopDrawingInternal(); }

    // ============================================
    // RESIZE MODE (Left Controller)
    // ============================================

    public void ToggleResizeMode()
    {
        if (isResizing)
            StopResizeMode();
        else
            StartResizeMode();
    }

    void StartResizeMode()
    {
        if (isResizing) return;

        // Finish any active drawing to keep the line in scene
        if (isDrawing)
            FinishDrawing();

        // Stop dragging if active
        if (isDragging)
            StopDragging();

        if (leftControllerTip == null || whiteboardPlane == null)
        {
            Debug.LogWarning("Left controller tip or whiteboard not assigned!");
            return;
        }

        // Raycast to pick the closest straight line near the hit point
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, leftControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closest = FindClosestStraightLine(hitPoint);

            if (closest != null)
            {
                currentLine = closest;

                // Decide which end to resize based on proximity
                Vector3 p0 = currentLine.GetPosition(0);
                Vector3 p1 = currentLine.GetPosition(1);
                resizingEndIndex = (Vector3.Distance(hitPoint, p0) <= Vector3.Distance(hitPoint, p1)) ? 0 : 1;

                isResizing = true;
                Debug.Log($"Started resizing straight line: {currentLine.name}. Editing end {resizingEndIndex}");
            }
        }
    }

    void UpdateResizeWithLeftController()
    {
        if (!isResizing || currentLine == null || leftControllerTip == null) return;

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, -whiteboardPlane.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 clamped = ClampWorldToBounds(hitPoint);

            // Enforce minimum length by adjusting edited end if needed
            int otherIndex = resizingEndIndex == 0 ? 1 : 0;
            if (currentLine.positionCount < 2) return;
            Vector3 other = currentLine.GetPosition(otherIndex);
            Vector3 dir = (clamped - other);
            float d = dir.magnitude;
            if (d < Mathf.Epsilon)
            {
                // Degenerate direction; fallback to plane right axis for extension
                dir = whiteboardPlane.right * minLineLength;
                d = dir.magnitude;
            }
            if (d < minLineLength)
            {
                dir = dir.normalized * minLineLength;
                clamped = other + dir;
                clamped = ClampWorldToBounds(clamped);
            }

            // Update chosen end
            if (resizingEndIndex == 0)
                currentLine.SetPosition(0, clamped);
            else
                currentLine.SetPosition(1, clamped);
        }
    }

    void StopResizeMode()
    {
        if (!isResizing) return;
        isResizing = false;
        resizingEndIndex = -1;
        Debug.Log("Stopped resizing straight line");
    }

    // ============================================
    // DRAG MODE (Right Controller)
    // ============================================

    public void ToggleDragMode()
    {
        if (isDragging)
            StopDragging();
        else
            StartDragging();
    }

    void StartDragging()
    {
        if (isDragging) return;

        if (isDrawing)
            FinishDrawing();

        if (isResizing)
            StopResizeMode();

        Transform tip = rightControllerTip != null ? rightControllerTip : drawingTip;
        if (tip == null || whiteboardPlane == null)
        {
            Debug.LogWarning("Right controller tip or whiteboard not assigned!");
            return;
        }

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(tip.position, tip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closest = FindClosestStraightLine(hitPoint);

            if (closest != null)
            {
                currentLine = closest;

                Vector3 center = GetLineCenter(currentLine);
                dragOffset = center - hitPoint;

                // Cache endpoint offsets relative to center for rigid drag
                Vector3 p0 = currentLine.GetPosition(0);
                Vector3 p1 = currentLine.GetPosition(1);
                p0OffsetFromCenter = p0 - center;
                p1OffsetFromCenter = p1 - center;

                isDragging = true;
                Debug.Log($"Started dragging straight line: {currentLine.name}");
            }
        }
    }

    void UpdateDragging()
    {
        if (!isDragging || currentLine == null) return;

        Transform tip = rightControllerTip != null ? rightControllerTip : drawingTip;
        if (tip == null) return;

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(tip.position, -whiteboardPlane.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);

            // Compute new center, clamped to bounds
            Vector3 desiredCenter = hitPoint + dragOffset;
            Vector3 clampedCenter = ClampWorldToBounds(desiredCenter);

            // Apply relative offsets to keep shape
            Vector3 newP0 = clampedCenter + p0OffsetFromCenter;
            Vector3 newP1 = clampedCenter + p1OffsetFromCenter;

            // Clamp endpoints individually to bounds
            newP0 = ClampWorldToBounds(newP0);
            newP1 = ClampWorldToBounds(newP1);

            // If clamping collapses the line, enforce min length along current direction
            Vector3 dir = newP1 - newP0;
            float len = dir.magnitude;
            if (len < Mathf.Epsilon)
            {
                // Use plane right as fallback direction
                dir = whiteboardPlane.right * minLineLength;
                len = dir.magnitude;
            }
            if (len < minLineLength)
            {
                Vector3 mid = (newP0 + newP1) * 0.5f;
                Vector3 half = dir.normalized * (minLineLength * 0.5f);
                newP0 = ClampWorldToBounds(mid - half);
                newP1 = ClampWorldToBounds(mid + half);
            }

            currentLine.SetPosition(0, newP0);
            currentLine.SetPosition(1, newP1);
        }
    }

    void StopDragging()
    {
        if (!isDragging) return;
        isDragging = false;
        Debug.Log("Stopped dragging straight line");
    }

    // ============================================
    // FINISH (internal) & HELPERS
    // ============================================

    void FinishDrawing()
    {
        if (!isDrawing) return;
        isDrawing = false;

        if (currentLine != null)
        {
            bool keep = ShouldKeepShapeOnFinish();
            if (keep)
            {
                if (drawMode == DrawMode.Line) currentLine.loop = false; else currentLine.loop = true;
                if (redoUndoManager != null)
                {
                    redoUndoManager.RegisterLine(currentLine.gameObject);
                    Debug.Log($"{drawMode} finished and registered in Undo stack");
                }
            }
            else
            {
                Destroy(currentLine.gameObject);
                currentLine = null;
                hasStartPoint = false;
                Debug.Log($"{drawMode} discarded on finish (too small)");
            }
        }
    }

    Vector3 GetLineCenter(LineRenderer line)
    {
        if (line == null || line.positionCount < 2) return Vector3.zero;
        Vector3 p0 = line.GetPosition(0);
        Vector3 p1 = line.GetPosition(1);
        return (p0 + p1) * 0.5f;
    }

    LineRenderer FindClosestStraightLine(Vector3 point)
    {
        LineRenderer[] all = FindObjectsOfType<LineRenderer>();
        LineRenderer closest = null;
        float closestDist = float.MaxValue;
        float searchRadius = 0.2f; // Tunable pick radius

        foreach (LineRenderer lr in all)
        {
            if (lr == null) continue;
            // Consider any 2-point line a candidate, prefer our named ones
            if (lr.positionCount == 2 || lr.name.Contains("VR_StraightLine"))
            {
                // Distance to segment for robust picking on long lines
                Vector3 a = lr.GetPosition(0);
                Vector3 b = lr.GetPosition(1);
                float d = DistancePointToSegment(point, a, b);
                if (d < searchRadius && d < closestDist)
                {
                    closest = lr;
                    closestDist = d;
                }
            }
        }

        return closest;
    }

    Vector3 ClampWorldToBounds(Vector3 worldPoint)
    {
        if (whiteboardPlane == null) return worldPoint;
        Vector3 local = whiteboardPlane.InverseTransformPoint(worldPoint);
        float halfWidth = maxWidth * 0.5f;
        float halfHeight = maxHeight * 0.5f;
        local.x = Mathf.Clamp(local.x, -halfWidth, halfWidth);
        local.y = Mathf.Clamp(local.y, -halfHeight, halfHeight);
        local.z = 0f;
        return whiteboardPlane.TransformPoint(local);
    }

    float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ap = p - a;
        Vector3 ab = b - a;
        float ab2 = Vector3.Dot(ab, ab);
        if (ab2 < Mathf.Epsilon) return ap.magnitude;
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / ab2);
        Vector3 closest = a + ab * t;
        return Vector3.Distance(p, closest);
    }

    // ================================
    // New: Multi-shape drawing support
    // ================================

    void StartDrawingInternal(DrawMode mode)
    {
        if (settings == null)
        {
            Debug.LogWarning("StraightLineVRDrawSettings not assigned!");
            return;
        }

        if (whiteboardPlane == null || drawingTip == null)
        {
            Debug.LogWarning("Whiteboard plane or drawing tip not assigned!");
            return;
        }

        // Stop conflicting modes
        if (isResizing) StopResizeMode();
        if (isDragging) StopDragging();

        // Determine start point from current tip raycast
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(drawingTip.position - drawingTip.forward * 0.05f, drawingTip.forward);
        if (!plane.Raycast(ray, out float enter))
        {
            Debug.LogWarning($"No plane hit for {mode} start!");
            return;
        }

        Vector3 hitPoint = ray.GetPoint(enter);
        Vector3 localPoint = whiteboardPlane.InverseTransformPoint(hitPoint);
        float halfWidth = maxWidth * 0.5f;
        float halfHeight = maxHeight * 0.5f;
        localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight, halfHeight);
        localPoint.z = 0f;
        startWorldPoint = whiteboardPlane.TransformPoint(localPoint);
        hasStartPoint = true;

        // Create line object
        string goName = mode switch
        {
            DrawMode.Line => "VR_StraightLine",
            DrawMode.Rectangle => "VR_Rectangle",
            DrawMode.Circle => "VR_Circle",
            DrawMode.Polygon => "VR_Polygon",
            _ => "VR_Shape"
        };

        GameObject lineObj = new GameObject(goName);
        currentLine = lineObj.AddComponent<LineRenderer>();
        currentLine.material = settings.lineMaterial;
        currentLine.startColor = settings.lineColor;
        currentLine.endColor = settings.lineColor;
        currentLine.startWidth = settings.lineWidth;
        currentLine.endWidth = settings.lineWidth;
        currentLine.useWorldSpace = true;

        drawMode = mode;

        // Initialize renderer counts / loop
        switch (mode)
        {
            case DrawMode.Line:
                currentLine.loop = false;
                currentLine.positionCount = 2;
                currentLine.SetPosition(0, startWorldPoint);
                currentLine.SetPosition(1, startWorldPoint);
                break;
            case DrawMode.Rectangle:
                currentLine.loop = true;
                currentLine.positionCount = 4;
                for (int i = 0; i < 4; i++) currentLine.SetPosition(i, startWorldPoint);
                break;
            case DrawMode.Circle:
                currentLine.loop = true;
                currentLine.positionCount = circleSegmentsDefault;
                for (int i = 0; i < circleSegmentsDefault; i++) currentLine.SetPosition(i, startWorldPoint);
                break;
            case DrawMode.Polygon:
                int sides = Mathf.Max(3, polygonSides);
                currentLine.loop = true;
                currentLine.positionCount = sides;
                for (int i = 0; i < sides; i++) currentLine.SetPosition(i, startWorldPoint);
                break;
        }

        isDrawing = true;
        Debug.Log($"Started {mode} drawing");
    }

    void StopDrawingInternal()
    {
        if (!isDrawing) return;
        isDrawing = false;

        if (currentLine != null)
        {
            bool keep = ShouldKeepShapeOnFinish();
            if (keep)
            {
                if (drawMode == DrawMode.Line) currentLine.loop = false; else currentLine.loop = true;
                if (redoUndoManager != null)
                {
                    GameObject lineObj = currentLine.gameObject;
                    redoUndoManager.RegisterLine(lineObj);
                    Debug.Log($"{drawMode} registered: {lineObj.name}. Undo stack count: {redoUndoManager.UndoCount}");

                    // Update current selection for attachment tool
                    var attachTool = FindObjectOfType<AttachDesignToController>();
                    if (attachTool != null)
                        attachTool.SetCurrentDesign(lineObj);
                }
                else
                {
                    Debug.LogWarning("RedoUndoManager not assigned!");

                    var attachTool = FindObjectOfType<AttachDesignToController>();
                    if (attachTool != null)
                        attachTool.SetCurrentDesign(currentLine.gameObject);
                }
            }
            else
            {
                Destroy(currentLine.gameObject);
                Debug.Log($"{drawMode} discarded (too small).");
            }
        }

        currentLine = null;
        hasStartPoint = false;
        Debug.Log($"Stopped {drawMode} drawing");
        drawMode = DrawMode.None;
    }

    bool ShouldKeepShapeOnFinish()
    {
        if (currentLine == null) return false;
        switch (drawMode)
        {
            case DrawMode.Line:
                if (currentLine.positionCount >= 2)
                {
                    float length = Vector3.Distance(currentLine.GetPosition(0), currentLine.GetPosition(1));
                    return length >= minLineLength;
                }
                return false;
            case DrawMode.Rectangle:
                if (currentLine.positionCount >= 4)
                {
                    // Use axis-aligned dimensions in plane local space
                    Vector3 a = currentLine.GetPosition(0);
                    Vector3 c = currentLine.GetPosition(2);
                    Vector3 la = whiteboardPlane.InverseTransformPoint(a);
                    Vector3 lc = whiteboardPlane.InverseTransformPoint(c);
                    float w = Mathf.Abs(lc.x - la.x);
                    float h = Mathf.Abs(lc.y - la.y);
                    return (w >= minLineLength && h >= minLineLength);
                }
                return false;
            case DrawMode.Circle:
            case DrawMode.Polygon:
                // Radius based on start as center and any current point
                if (currentLine.positionCount >= 3)
                {
                    float r = Vector3.Distance(startWorldPoint, currentLine.GetPosition(0));
                    return r >= minLineLength;
                }
                return false;
            default:
                return false;
        }
    }

    void UpdateRectanglePreview(Vector3 currentWorld)
    {
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
        // Work entirely in the whiteboard's local XY to respect scaling
        Vector3 lCenter = whiteboardPlane.InverseTransformPoint(startWorldPoint);
        Vector3 lCurrent = whiteboardPlane.InverseTransformPoint(currentWorld);
        Vector2 delta = new Vector2(lCurrent.x - lCenter.x, lCurrent.y - lCenter.y);
        float radius = delta.magnitude;
        if (radius < Mathf.Epsilon) radius = 0f;

        currentLine.loop = true;
        if (currentLine.positionCount != segments) currentLine.positionCount = segments;

        float twoPi = Mathf.PI * 2f;
        for (int i = 0; i < segments; i++)
        {
            float t = (i / (float)segments) * twoPi;
            Vector3 localPoint = new Vector3(
                lCenter.x + Mathf.Cos(t) * radius,
                lCenter.y + Mathf.Sin(t) * radius,
                0f
            );

            // Clamp within local bounds then convert to world
            float halfW = maxWidth * 0.5f;
            float halfH = maxHeight * 0.5f;
            localPoint.x = Mathf.Clamp(localPoint.x, -halfW, halfW);
            localPoint.y = Mathf.Clamp(localPoint.y, -halfH, halfH);

            Vector3 worldPoint = whiteboardPlane.TransformPoint(localPoint);
            currentLine.SetPosition(i, worldPoint);
        }
    }
}
