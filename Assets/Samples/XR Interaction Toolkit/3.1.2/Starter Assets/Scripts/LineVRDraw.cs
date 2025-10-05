using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[System.Serializable]
public class LineVRDrawSettings
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class LineVRDraw : MonoBehaviour
{
    [Header("References")]
    public Transform whiteboardPlane;
    public Transform leftControllerTip;   // Left controller for resizing
    public Transform rightControllerTip;  // Right controller for dragging
    public RedoUndoManager redoUndoManager;

    [Header("Drawing Settings")]
    public LineVRDrawSettings settings;
    public float lengthChangeSpeed = 0.5f;
    public bool autoIncreaseLength = true;

    [Header("Drawing Bounds")]
    public float maxLength = 1.0f;
    public float minLength = 0.05f;
    public float maxWidth = 1.0f;
    public float maxHeight = 1.0f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private LineRenderer currentLine;
    private bool isDrawing = false;
    private bool isDragging = false;
    private bool isResizing = false;
    private Vector3 lineStart;
    private Vector3 lineEnd;
    private Vector3 dragOffset;
    private float lastLeftControllerX;

    void Update()
    {
        // Continuously update resize while in resize mode
        if (isResizing)
        {
            UpdateResizeWithLeftController();
        }

        // Continuously update drag while in drag mode
        if (isDragging)
        {
            UpdateDragging();
        }

        // Update drawing length
        if (isDrawing)
        {
            UpdateLengthWithLeftController();
        }
    }

    // ============================================
    // RESIZE MODE FUNCTIONS (Left Controller)
    // ============================================

    /// <summary>
    /// Call this from ONE UI button to toggle resize mode
    /// Once active, moving the left controller left/right will shrink/grow the line
    /// </summary>
    public void ToggleResizeMode()
    {
        if (isResizing)
            StopResizeMode();
        else
            StartResizeMode();
    }

    void StartResizeMode()
    {
        if (isResizing)
        {
            if (showDebugLogs) Debug.Log("Already resizing!");
            return;
        }

        // Stop other modes (but DON'T destroy the line being drawn)
        if (isDrawing)
        {
            // Finish the drawing first, save the line
            FinishDrawing();
        }

        if (isDragging)
        {
            StopDragging();
        }

        if (leftControllerTip == null || whiteboardPlane == null)
        {
            Debug.LogWarning("Left controller or whiteboard not assigned!");
            return;
        }

        // Raycast from left controller to find a line
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, leftControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closestLine = FindClosestLine(hitPoint);

            if (closestLine != null)
            {
                currentLine = closestLine;
                lineStart = currentLine.GetPosition(0);
                lineEnd = currentLine.GetPosition(1);

                // Store initial X position for horizontal tracking
                lastLeftControllerX = leftControllerTip.position.x;

                isResizing = true;
                if (showDebugLogs) Debug.Log($"Started resizing line: {closestLine.name}. Move left controller left/right to resize.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("No line found to resize");
            }
        }
    }

    /// <summary>
    /// Updates the line length based on left controller horizontal movement (left-right)
    /// Called automatically in Update() while isResizing is true
    /// </summary>
    void UpdateResizeWithLeftController()
    {
        if (leftControllerTip == null || currentLine == null) return;

        // Calculate horizontal movement (left = shrink, right = grow)
        float currentX = leftControllerTip.position.x;
        float deltaX = currentX - lastLeftControllerX;

        // Calculate current line properties
        Vector3 lineCenter = (lineStart + lineEnd) / 2f;
        Vector3 lineDirection = (lineEnd - lineStart).normalized;
        float currentLength = Vector3.Distance(lineStart, lineEnd);

        // Change length based on horizontal movement
        currentLength += deltaX * lengthChangeSpeed * 10f;
        currentLength = Mathf.Clamp(currentLength, minLength, maxLength);

        // Update line endpoints from center
        float halfLength = currentLength / 2f;
        lineStart = lineCenter - lineDirection * halfLength;
        lineEnd = lineCenter + lineDirection * halfLength;

        UpdateLinePositions();

        lastLeftControllerX = currentX;
    }

    void StopResizeMode()
    {
        if (!isResizing) return;

        isResizing = false;
        currentLine = null;

        if (showDebugLogs) Debug.Log("Stopped resizing");
    }

    // ============================================
    // DRAG MODE FUNCTIONS (Right Controller)
    // ============================================

    /// <summary>
    /// Call this from ONE UI button to toggle drag mode
    /// Once active, moving the right controller will drag the line around
    /// </summary>
    public void ToggleDragMode()
    {
        if (isDragging)
            StopDragging();
        else
            StartDraggingMode();
    }

    void StartDraggingMode()
    {
        if (isDragging)
        {
            if (showDebugLogs) Debug.Log("Already dragging!");
            return;
        }

        // Stop other modes (but DON'T destroy the line being drawn)
        if (isDrawing)
        {
            // Finish the drawing first, save the line
            FinishDrawing();
        }

        if (isResizing)
        {
            StopResizeMode();
        }

        if (rightControllerTip == null)
        {
            Debug.LogWarning("Right controller tip not assigned!");
            return;
        }

        // Raycast from right controller to find a line
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(rightControllerTip.position, rightControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closestLine = FindClosestLine(hitPoint);

            if (closestLine != null)
            {
                currentLine = closestLine;
                lineStart = currentLine.GetPosition(0);
                lineEnd = currentLine.GetPosition(1);

                Vector3 lineCenter = (lineStart + lineEnd) / 2f;

                // Calculate drag offset so line doesn't jump
                dragOffset = lineCenter - hitPoint;

                isDragging = true;
                if (showDebugLogs) Debug.Log($"Started dragging line: {closestLine.name}. Move right controller to drag.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("No line found near controller position");
            }
        }
    }

    /// <summary>
    /// Updates the line position to follow the right controller
    /// Called automatically in Update() while isDragging is true
    /// </summary>
    void UpdateDragging()
    {
        if (rightControllerTip == null || currentLine == null) return;

        // Project right controller onto whiteboard plane
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(rightControllerTip.position, rightControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 newCenter = hitPoint + dragOffset;

            // Calculate line offset from center
            Vector3 oldCenter = (lineStart + lineEnd) / 2f;
            Vector3 offset = newCenter - oldCenter;

            // Move both endpoints
            lineStart += offset;
            lineEnd += offset;

            // Clamp to whiteboard bounds
            lineStart = ClampToWhiteboard(lineStart);
            lineEnd = ClampToWhiteboard(lineEnd);

            UpdateLinePositions();
        }
    }

    void StopDragging()
    {
        if (!isDragging) return;

        isDragging = false;
        currentLine = null;

        if (showDebugLogs) Debug.Log("Stopped dragging");
    }

    // ============================================
    // DRAWING MODE FUNCTIONS (Original)
    // ============================================

    void UpdateLengthWithLeftController()
    {
        if (leftControllerTip == null || currentLine == null) return;

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, -whiteboardPlane.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);

            // Calculate new length from start to hit point
            float newLength = Vector3.Distance(hitPoint, lineStart);
            newLength = Mathf.Clamp(newLength, minLength, maxLength);

            // Update line end based on direction from start to controller
            Vector3 direction = (hitPoint - lineStart).normalized;
            lineEnd = lineStart + direction * newLength;

            UpdateLinePositions();
        }
    }

    /// <summary>
    /// Call this from a UI button to start drawing a new line
    /// </summary>
    public void StartDrawing()
    {
        if (isDrawing)
        {
            if (showDebugLogs) Debug.Log("Already drawing!");
            return;
        }

        // Stop other modes (but DON'T destroy the line being drawn)
        if (isDragging)
        {
            StopDragging();
        }

        if (isResizing)
        {
            StopResizeMode();
        }

        if (settings == null || leftControllerTip == null || whiteboardPlane == null)
        {
            Debug.LogWarning("Settings or references not assigned!");
            return;
        }

        // Raycast from controller to whiteboard (same as SplineVRDraw)
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position - leftControllerTip.forward * 0.05f, leftControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 localPoint = whiteboardPlane.InverseTransformPoint(hitPoint);

            // Clamp to whiteboard bounds
            float halfWidth = maxWidth * 0.5f;
            float halfHeight = maxHeight * 0.5f;
            localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
            localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight, halfHeight);
            localPoint.z = 0f;

            lineStart = whiteboardPlane.TransformPoint(localPoint);
            lineEnd = lineStart; // Start with zero length
        }
        else
        {
            Debug.LogWarning("No raycast hit for line start - make sure controller is pointing at whiteboard!");
            return;
        }

        GameObject lineObj = new GameObject("VR_Line");
        currentLine = lineObj.AddComponent<LineRenderer>();

        currentLine.material = settings.lineMaterial;
        currentLine.startColor = settings.lineColor;
        currentLine.endColor = settings.lineColor;
        currentLine.startWidth = settings.lineWidth;
        currentLine.endWidth = settings.lineWidth;
        currentLine.useWorldSpace = true;
        currentLine.positionCount = 2;

        UpdateLinePositions();

        isDrawing = true;
        if (showDebugLogs) Debug.Log("Started Line Drawing - point controller at whiteboard and move to resize");
    }

    /// <summary>
    /// Call this from a UI button to stop drawing
    /// </summary>
    public void StopDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;

        if (currentLine != null && Vector3.Distance(lineStart, lineEnd) > minLength)
        {
            currentLine.loop = false;

            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(currentLine.gameObject);
                if (showDebugLogs) Debug.Log("Line registered in Undo stack");
            }
        }
        else
        {
            if (currentLine != null)
                Destroy(currentLine.gameObject);

            if (showDebugLogs) Debug.Log("Line destroyed: Too small length");
        }

        currentLine = null;
        if (showDebugLogs) Debug.Log("Stopped Line Drawing");
    }

    /// <summary>
    /// Finishes drawing and keeps the line (called internally when switching modes)
    /// </summary>
    void FinishDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;

        if (currentLine != null)
        {
            currentLine.loop = false;

            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(currentLine.gameObject);
                if (showDebugLogs) Debug.Log("Line finished and registered in Undo stack");
            }
        }

        // DON'T set currentLine to null - we might want to use it immediately
        if (showDebugLogs) Debug.Log("Finished Line Drawing (line kept for further editing)");
    }

    /// <summary>
    /// Call this from a UI button to toggle drawing on/off
    /// </summary>
    public void ToggleDrawing()
    {
        if (isDrawing)
            StopDrawing();
        else
            StartDrawing();
    }

    // ============================================
    // HELPER FUNCTIONS
    // ============================================

    void UpdateLinePositions()
    {
        if (currentLine == null) return;

        currentLine.SetPosition(0, lineStart);
        currentLine.SetPosition(1, lineEnd);
    }

    Vector3 ClampToWhiteboard(Vector3 worldPos)
    {
        Vector3 localPoint = whiteboardPlane.InverseTransformPoint(worldPos);

        float halfWidth = maxWidth * 0.5f;
        float halfHeight = maxHeight * 0.5f;
        localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight, halfHeight);
        localPoint.z = 0f;

        return whiteboardPlane.TransformPoint(localPoint);
    }

    LineRenderer FindClosestLine(Vector3 point)
    {
        LineRenderer[] allLines = FindObjectsOfType<LineRenderer>();
        LineRenderer closest = null;
        float closestDist = float.MaxValue;
        float searchRadius = 0.2f;

        foreach (LineRenderer line in allLines)
        {
            if (line.name.Contains("VR_Line"))
            {
                Vector3 lineCenter = GetLineCenter(line);
                float dist = Vector3.Distance(point, lineCenter);

                if (dist < searchRadius && dist < closestDist)
                {
                    closest = line;
                    closestDist = dist;
                }
            }
        }

        return closest;
    }

    Vector3 GetLineCenter(LineRenderer line)
    {
        if (line.positionCount < 2) return Vector3.zero;

        Vector3 start = line.GetPosition(0);
        Vector3 end = line.GetPosition(1);
        return (start + end) / 2f;
    }
}