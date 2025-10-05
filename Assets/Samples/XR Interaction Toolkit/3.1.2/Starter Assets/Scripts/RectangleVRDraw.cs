using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[System.Serializable]
public class RectangleVRDrawSettings
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class RectangleVRDraw : MonoBehaviour
{
    [Header("References")]
    public Transform whiteboardPlane;
    public Transform leftControllerTip;   // Left controller for resizing
    public Transform rightControllerTip;  // Right controller for dragging
    public RedoUndoManager redoUndoManager;

    [Header("Drawing Settings")]
    public RectangleVRDrawSettings settings;
    public float sizeChangeSpeed = 0.5f;
    public bool autoIncreaseSize = true;

    [Header("Drawing Bounds")]
    public float maxRectWidth = 0.8f;
    public float minRectWidth = 0.05f;
    public float maxRectHeight = 0.8f;
    public float minRectHeight = 0.05f;
    public float maxWidth = 1.0f;
    public float maxHeight = 1.0f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private LineRenderer currentRectangle;
    private bool isDrawing = false;
    private bool isDragging = false;
    private bool isResizing = false;
    private Vector3 rectangleCenter;
    private float currentRectWidth;
    private float currentRectHeight;
    private Vector3 dragOffset;
    private float lastLeftControllerX;
    private float lastLeftControllerY;

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

        // Update drawing size
        if (isDrawing)
        {
            UpdateSizeWithLeftController();
        }
    }

    // ============================================
    // RESIZE MODE FUNCTIONS (Left Controller)
    // ============================================

    /// <summary>
    /// Call this from ONE UI button to toggle resize mode
    /// Once active, moving the left controller left/right and up/down will resize the rectangle
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

        // Stop other modes (but DON'T destroy the rectangle being drawn)
        if (isDrawing)
        {
            // Finish the drawing first, save the rectangle
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

        // Raycast from left controller to find a rectangle
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, leftControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closestRectangle = FindClosestRectangle(hitPoint);

            if (closestRectangle != null)
            {
                currentRectangle = closestRectangle;
                rectangleCenter = GetRectangleCenter(closestRectangle);
                Vector2 size = GetRectangleSize(closestRectangle);
                currentRectWidth = size.x;
                currentRectHeight = size.y;

                // Store initial position for tracking
                lastLeftControllerX = leftControllerTip.position.x;
                lastLeftControllerY = leftControllerTip.position.y;

                isResizing = true;
                if (showDebugLogs) Debug.Log($"Started resizing rectangle: {closestRectangle.name}. Move left controller to resize.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("No rectangle found to resize");
            }
        }
    }

    /// <summary>
    /// Updates the rectangle size based on left controller movement
    /// Called automatically in Update() while isResizing is true
    /// </summary>
    void UpdateResizeWithLeftController()
    {
        if (leftControllerTip == null || currentRectangle == null) return;

        // Calculate horizontal and vertical movement
        float currentX = leftControllerTip.position.x;
        float currentY = leftControllerTip.position.y;
        float deltaX = currentX - lastLeftControllerX;
        float deltaY = currentY - lastLeftControllerY;

        // Change size based on movement
        currentRectWidth += deltaX * sizeChangeSpeed * 10f;
        currentRectHeight += deltaY * sizeChangeSpeed * 10f;

        // Clamp size
        currentRectWidth = Mathf.Clamp(currentRectWidth, minRectWidth, maxRectWidth);
        currentRectHeight = Mathf.Clamp(currentRectHeight, minRectHeight, maxRectHeight);

        UpdateRectanglePositions();

        lastLeftControllerX = currentX;
        lastLeftControllerY = currentY;
    }

    void StopResizeMode()
    {
        if (!isResizing) return;

        isResizing = false;
        currentRectangle = null;

        if (showDebugLogs) Debug.Log("Stopped resizing");
    }

    // ============================================
    // DRAG MODE FUNCTIONS (Right Controller)
    // ============================================

    /// <summary>
    /// Call this from ONE UI button to toggle drag mode
    /// Once active, moving the right controller will drag the rectangle around
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

        // Stop other modes (but DON'T destroy the rectangle being drawn)
        if (isDrawing)
        {
            // Finish the drawing first, save the rectangle
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

        // Raycast from right controller to find a rectangle
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(rightControllerTip.position, rightControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closestRectangle = FindClosestRectangle(hitPoint);

            if (closestRectangle != null)
            {
                currentRectangle = closestRectangle;
                rectangleCenter = GetRectangleCenter(closestRectangle);
                Vector2 size = GetRectangleSize(closestRectangle);
                currentRectWidth = size.x;
                currentRectHeight = size.y;

                // Calculate drag offset so rectangle doesn't jump
                dragOffset = rectangleCenter - hitPoint;

                isDragging = true;
                if (showDebugLogs) Debug.Log($"Started dragging rectangle: {closestRectangle.name}. Move right controller to drag.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("No rectangle found near controller position");
            }
        }
    }

    /// <summary>
    /// Updates the rectangle position to follow the right controller
    /// Called automatically in Update() while isDragging is true
    /// </summary>
    void UpdateDragging()
    {
        if (rightControllerTip == null || currentRectangle == null) return;

        // Project right controller onto whiteboard plane
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(rightControllerTip.position, rightControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 newCenter = hitPoint + dragOffset;

            // Convert to local space for bounds checking
            Vector3 localPoint = whiteboardPlane.InverseTransformPoint(newCenter);

            // Clamp to whiteboard bounds
            float halfWidth = maxWidth * 0.5f;
            float halfHeight = maxHeight * 0.5f;
            localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
            localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight, halfHeight);
            localPoint.z = 0f;

            // Convert back to world space
            rectangleCenter = whiteboardPlane.TransformPoint(localPoint);
            UpdateRectanglePositions();
        }
    }

    void StopDragging()
    {
        if (!isDragging) return;

        isDragging = false;
        currentRectangle = null;

        if (showDebugLogs) Debug.Log("Stopped dragging");
    }

    // ============================================
    // DRAWING MODE FUNCTIONS (Original)
    // ============================================

    void UpdateSizeWithLeftController()
    {
        if (leftControllerTip == null || currentRectangle == null) return;

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, -whiteboardPlane.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 localHit = whiteboardPlane.InverseTransformPoint(hitPoint);
            Vector3 localCenter = whiteboardPlane.InverseTransformPoint(rectangleCenter);

            // Calculate width and height from center to hit point
            float newWidth = Mathf.Abs(localHit.x - localCenter.x) * 2f;
            float newHeight = Mathf.Abs(localHit.y - localCenter.y) * 2f;

            currentRectWidth = Mathf.Clamp(newWidth, minRectWidth, maxRectWidth);
            currentRectHeight = Mathf.Clamp(newHeight, minRectHeight, maxRectHeight);
            UpdateRectanglePositions();
        }
    }

    /// <summary>
    /// Call this from a UI button to start drawing a new rectangle
    /// </summary>
    public void StartDrawing()
    {
        if (isDrawing)
        {
            if (showDebugLogs) Debug.Log("Already drawing!");
            return;
        }

        // Stop other modes (but DON'T destroy the rectangle being drawn)
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

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position - leftControllerTip.forward * 0.05f, leftControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 localPoint = whiteboardPlane.InverseTransformPoint(hitPoint);

            float halfWidth = maxWidth * 0.5f;
            float halfHeight = maxHeight * 0.5f;
            localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
            localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight, halfHeight);
            localPoint.z = 0f;

            rectangleCenter = whiteboardPlane.TransformPoint(localPoint);
        }
        else
        {
            Debug.LogWarning("No raycast hit for rectangle center!");
            return;
        }

        GameObject rectangleObj = new GameObject("VR_Rectangle");
        currentRectangle = rectangleObj.AddComponent<LineRenderer>();

        currentRectangle.material = settings.lineMaterial;
        currentRectangle.startColor = settings.lineColor;
        currentRectangle.endColor = settings.lineColor;
        currentRectangle.startWidth = settings.lineWidth;
        currentRectangle.endWidth = settings.lineWidth;
        currentRectangle.useWorldSpace = true;

        currentRectWidth = minRectWidth;
        currentRectHeight = minRectHeight;
        UpdateRectanglePositions();

        isDrawing = true;
        if (showDebugLogs) Debug.Log("Started Rectangle Drawing");
    }

    /// <summary>
    /// Call this from a UI button to stop drawing
    /// </summary>
    public void StopDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;

        if (currentRectangle != null && (currentRectWidth > minRectWidth || currentRectHeight > minRectHeight))
        {
            currentRectangle.loop = true;

            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(currentRectangle.gameObject);
                if (showDebugLogs) Debug.Log("Rectangle registered in Undo stack");
            }
        }
        else
        {
            if (currentRectangle != null)
                Destroy(currentRectangle.gameObject);

            if (showDebugLogs) Debug.Log("Rectangle destroyed: Too small size");
        }

        currentRectangle = null;
        if (showDebugLogs) Debug.Log("Stopped Rectangle Drawing");
    }

    /// <summary>
    /// Finishes drawing and keeps the rectangle (called internally when switching modes)
    /// </summary>
    void FinishDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;

        if (currentRectangle != null)
        {
            currentRectangle.loop = true;

            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(currentRectangle.gameObject);
                if (showDebugLogs) Debug.Log("Rectangle finished and registered in Undo stack");
            }
        }

        // DON'T set currentRectangle to null - we might want to use it immediately
        if (showDebugLogs) Debug.Log("Finished Rectangle Drawing (rectangle kept for further editing)");
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

    void UpdateRectanglePositions()
    {
        if (currentRectangle == null) return;

        // Rectangle has 5 points (4 corners + back to start)
        currentRectangle.positionCount = 5;

        // Get local center
        Vector3 localCenter = whiteboardPlane.InverseTransformPoint(rectangleCenter);

        // Calculate corners in local space
        float halfW = currentRectWidth * 0.5f;
        float halfH = currentRectHeight * 0.5f;

        Vector3[] localCorners = new Vector3[5]
        {
            new Vector3(localCenter.x - halfW, localCenter.y - halfH, 0), // Bottom-left
            new Vector3(localCenter.x + halfW, localCenter.y - halfH, 0), // Bottom-right
            new Vector3(localCenter.x + halfW, localCenter.y + halfH, 0), // Top-right
            new Vector3(localCenter.x - halfW, localCenter.y + halfH, 0), // Top-left
            new Vector3(localCenter.x - halfW, localCenter.y - halfH, 0)  // Back to bottom-left
        };

        // Convert to world space and set positions
        for (int i = 0; i < 5; i++)
        {
            Vector3 worldPos = whiteboardPlane.TransformPoint(localCorners[i]);
            currentRectangle.SetPosition(i, worldPos);
        }
    }

    LineRenderer FindClosestRectangle(Vector3 point)
    {
        LineRenderer[] allLines = FindObjectsOfType<LineRenderer>();
        LineRenderer closest = null;
        float closestDist = float.MaxValue;
        float searchRadius = 0.2f;

        foreach (LineRenderer line in allLines)
        {
            if (line.name.Contains("VR_Rectangle"))
            {
                Vector3 center = GetRectangleCenter(line);
                float dist = Vector3.Distance(point, center);

                if (dist < searchRadius && dist < closestDist)
                {
                    closest = line;
                    closestDist = dist;
                }
            }
        }

        return closest;
    }

    Vector3 GetRectangleCenter(LineRenderer line)
    {
        if (line.positionCount < 4) return Vector3.zero;

        // Average of first 4 corners (5th point is duplicate of first)
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            sum += line.GetPosition(i);
        }
        return sum / 4f;
    }

    Vector2 GetRectangleSize(LineRenderer line)
    {
        if (line.positionCount < 4) return Vector2.zero;

        // Get width from distance between corner 0 and 1
        float width = Vector3.Distance(line.GetPosition(0), line.GetPosition(1));

        // Get height from distance between corner 0 and 3
        float height = Vector3.Distance(line.GetPosition(0), line.GetPosition(3));

        return new Vector2(width, height);
    }
}