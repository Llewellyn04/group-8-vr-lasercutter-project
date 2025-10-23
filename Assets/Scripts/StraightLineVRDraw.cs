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

    void Update()
    {
        // Handle trigger input
        if (rightController != null && rightController.inputDevice.isValid)
        {
            rightController.inputDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);

            if (triggerPressed && !lastTriggerState && !isDrawing)
                StartDrawing();
            else if (!triggerPressed && lastTriggerState && isDrawing)
                StopDrawing();

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

        if (hasStartPoint)
        {
            currentLine.positionCount = 2;
            currentLine.SetPosition(0, startWorldPoint);
            currentLine.SetPosition(1, clampedWorldPoint);
        }
    }

    public void StartDrawing()
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

        // Stop conflicting modes first
        if (isResizing)
            StopResizeMode();
        if (isDragging)
            StopDragging();

        // Determine start point from current tip raycast
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(drawingTip.position - drawingTip.forward * 0.05f, drawingTip.forward);

        if (!plane.Raycast(ray, out float enter))
        {
            Debug.LogWarning("No plane hit for straight line start!");
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

        // Create the line object
        GameObject lineObj = new GameObject("VR_StraightLine");
        currentLine = lineObj.AddComponent<LineRenderer>();
        currentLine.material = settings.lineMaterial;
        currentLine.startColor = settings.lineColor;
        currentLine.endColor = settings.lineColor;
        currentLine.startWidth = settings.lineWidth;
        currentLine.endWidth = settings.lineWidth;
        currentLine.useWorldSpace = true;
        currentLine.positionCount = 2;
        currentLine.SetPosition(0, startWorldPoint);
        currentLine.SetPosition(1, startWorldPoint);

        isDrawing = true;
        Debug.Log("Started Straight Line Drawing");
    }

    public void StopDrawing()
    {
        if (!isDrawing)
            return;

        isDrawing = false;

        if (currentLine != null)
        {
            // Evaluate length
            if (currentLine.positionCount >= 2)
            {
                Vector3 p0 = currentLine.GetPosition(0);
                Vector3 p1 = currentLine.GetPosition(1);
                float length = Vector3.Distance(p0, p1);

                if (length >= minLineLength)
                {
                    currentLine.loop = false;

                    if (redoUndoManager != null)
                    {
                        GameObject lineObj = currentLine.gameObject;
                        redoUndoManager.RegisterLine(lineObj);
                        Debug.Log($"Straight line registered: {lineObj.name}. Undo stack count: {redoUndoManager.UndoCount}");
                    }
                    else
                    {
                        Debug.LogWarning("RedoUndoManager not assigned!");
                    }
                }
                else
                {
                    Destroy(currentLine.gameObject);
                    Debug.Log("Straight line discarded (too short).");
                }
            }
            else
            {
                Destroy(currentLine.gameObject);
                Debug.Log("Straight line discarded (invalid positions).");
            }
        }

        currentLine = null;
        hasStartPoint = false;
        Debug.Log("Stopped Straight Line Drawing");
    }

    public void ToggleDrawing()
    {
        if (isDrawing)
            StopDrawing();
        else
            StartDrawing();
    }

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
            Vector3 p0 = currentLine.GetPosition(0);
            Vector3 p1 = currentLine.GetPosition(1);
            float length = Vector3.Distance(p0, p1);

            if (length >= minLineLength)
            {
                currentLine.loop = false;
                if (redoUndoManager != null)
                {
                    redoUndoManager.RegisterLine(currentLine.gameObject);
                    Debug.Log("Straight line finished and registered in Undo stack");
                }
            }
            else
            {
                // Too short; destroy and clear
                Destroy(currentLine.gameObject);
                currentLine = null;
                hasStartPoint = false;
                Debug.Log("Straight line discarded on finish (too short)");
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
}
