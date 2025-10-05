using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[System.Serializable]
public class PolygonVRDrawSettings
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class PolygonVRDraw : MonoBehaviour
{
    [Header("References")]
    public Transform whiteboardPlane;
    public Transform leftControllerTip;   // Left controller for resizing
    public Transform rightControllerTip;  // Right controller for dragging
    public RedoUndoManager redoUndoManager;

    [Header("Drawing Settings")]
    public PolygonVRDrawSettings settings;
    public int polygonSides = 6;  // Number of sides for the polygon
    public float radiusChangeSpeed = 0.5f;
    public bool autoIncreaseRadius = true;

    [Header("Drawing Bounds")]
    public float maxRadius = 0.5f;
    public float minRadius = 0.05f;
    public float maxWidth = 1.0f;
    public float maxHeight = 1.0f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private LineRenderer currentPolygon;
    private bool isDrawing = false;
    private bool isDragging = false;
    private bool isResizing = false;
    private Vector3 polygonCenter;
    private float currentRadius;
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

        // Update drawing radius
        if (isDrawing)
        {
            UpdateRadiusWithLeftController();
        }
    }

    // ============================================
    // RESIZE MODE FUNCTIONS (Left Controller)
    // ============================================

    /// <summary>
    /// Call this from ONE UI button to toggle resize mode
    /// Once active, moving the left controller left/right will shrink/grow the polygon
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

        // Stop other modes (but DON'T destroy the polygon being drawn)
        if (isDrawing)
        {
            // Finish the drawing first, save the polygon
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

        // Raycast from left controller to find a polygon
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, leftControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closestPolygon = FindClosestPolygon(hitPoint);

            if (closestPolygon != null)
            {
                currentPolygon = closestPolygon;
                polygonCenter = GetPolygonCenter(closestPolygon);
                currentRadius = GetPolygonRadius(closestPolygon);

                // Store initial X position for horizontal tracking
                lastLeftControllerX = leftControllerTip.position.x;

                isResizing = true;
                if (showDebugLogs) Debug.Log($"Started resizing polygon: {closestPolygon.name}. Move left controller left/right to resize.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("No polygon found to resize");
            }
        }
    }

    /// <summary>
    /// Updates the polygon radius based on left controller horizontal movement (left-right)
    /// Called automatically in Update() while isResizing is true
    /// </summary>
    void UpdateResizeWithLeftController()
    {
        if (leftControllerTip == null || currentPolygon == null) return;

        // Calculate horizontal movement (left = shrink, right = grow)
        float currentX = leftControllerTip.position.x;
        float deltaX = currentX - lastLeftControllerX;

        // Change radius based on horizontal movement
        currentRadius += deltaX * radiusChangeSpeed * 10f; // Multiply for more sensitivity
        currentRadius = Mathf.Clamp(currentRadius, minRadius, maxRadius);

        UpdatePolygonPositions();

        lastLeftControllerX = currentX;
    }

    void StopResizeMode()
    {
        if (!isResizing) return;

        isResizing = false;
        currentPolygon = null;

        if (showDebugLogs) Debug.Log("Stopped resizing");
    }

    // ============================================
    // DRAG MODE FUNCTIONS (Right Controller)
    // ============================================

    /// <summary>
    /// Call this from ONE UI button to toggle drag mode
    /// Once active, moving the right controller will drag the polygon around
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

        // Stop other modes (but DON'T destroy the polygon being drawn)
        if (isDrawing)
        {
            // Finish the drawing first, save the polygon
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

        // Raycast from right controller to find a polygon
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(rightControllerTip.position, rightControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closestPolygon = FindClosestPolygon(hitPoint);

            if (closestPolygon != null)
            {
                currentPolygon = closestPolygon;
                polygonCenter = GetPolygonCenter(closestPolygon);
                currentRadius = GetPolygonRadius(closestPolygon);

                // Calculate drag offset so polygon doesn't jump
                dragOffset = polygonCenter - hitPoint;

                isDragging = true;
                if (showDebugLogs) Debug.Log($"Started dragging polygon: {closestPolygon.name}. Move right controller to drag.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("No polygon found near controller position");
            }
        }
    }

    /// <summary>
    /// Updates the polygon position to follow the right controller
    /// Called automatically in Update() while isDragging is true
    /// </summary>
    void UpdateDragging()
    {
        if (rightControllerTip == null || currentPolygon == null) return;

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
            polygonCenter = whiteboardPlane.TransformPoint(localPoint);
            UpdatePolygonPositions();
        }
    }

    void StopDragging()
    {
        if (!isDragging) return;

        isDragging = false;
        currentPolygon = null;

        if (showDebugLogs) Debug.Log("Stopped dragging");
    }

    // ============================================
    // DRAWING MODE FUNCTIONS (Original)
    // ============================================

    void UpdateRadiusWithLeftController()
    {
        if (leftControllerTip == null || currentPolygon == null) return;

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, -whiteboardPlane.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            float newRadius = Vector3.Distance(hitPoint, polygonCenter);
            currentRadius = Mathf.Clamp(newRadius, minRadius, maxRadius);
            UpdatePolygonPositions();
        }
    }

    /// <summary>
    /// Call this from a UI button to start drawing a new polygon
    /// </summary>
    public void StartDrawing()
    {
        if (isDrawing)
        {
            if (showDebugLogs) Debug.Log("Already drawing!");
            return;
        }

        // Stop other modes (but DON'T destroy the polygon being drawn)
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

            polygonCenter = whiteboardPlane.TransformPoint(localPoint);
        }
        else
        {
            Debug.LogWarning("No raycast hit for polygon center!");
            return;
        }

        GameObject polygonObj = new GameObject("VR_Polygon");
        currentPolygon = polygonObj.AddComponent<LineRenderer>();

        currentPolygon.material = settings.lineMaterial;
        currentPolygon.startColor = settings.lineColor;
        currentPolygon.endColor = settings.lineColor;
        currentPolygon.startWidth = settings.lineWidth;
        currentPolygon.endWidth = settings.lineWidth;
        currentPolygon.useWorldSpace = true;

        currentRadius = minRadius;
        UpdatePolygonPositions();

        isDrawing = true;
        if (showDebugLogs) Debug.Log("Started Polygon Drawing");
    }

    /// <summary>
    /// Call this from a UI button to stop drawing
    /// </summary>
    public void StopDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;

        if (currentPolygon != null && currentRadius > minRadius)
        {
            currentPolygon.loop = true;

            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(currentPolygon.gameObject);
                if (showDebugLogs) Debug.Log("Polygon registered in Undo stack");
            }
        }
        else
        {
            if (currentPolygon != null)
                Destroy(currentPolygon.gameObject);

            if (showDebugLogs) Debug.Log("Polygon destroyed: Too small radius");
        }

        currentPolygon = null;
        if (showDebugLogs) Debug.Log("Stopped Polygon Drawing");
    }

    /// <summary>
    /// Finishes drawing and keeps the polygon (called internally when switching modes)
    /// </summary>
    void FinishDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;

        if (currentPolygon != null)
        {
            currentPolygon.loop = true;

            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(currentPolygon.gameObject);
                if (showDebugLogs) Debug.Log("Polygon finished and registered in Undo stack");
            }
        }

        // DON'T set currentPolygon to null - we might want to use it immediately
        if (showDebugLogs) Debug.Log("Finished Polygon Drawing (polygon kept for further editing)");
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

    void UpdatePolygonPositions()
    {
        if (currentPolygon == null) return;

        currentPolygon.positionCount = polygonSides + 1;
        float angleStep = 360f / polygonSides;

        for (int i = 0; i <= polygonSides; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * currentRadius;
            Vector3 localPos = whiteboardPlane.InverseTransformPoint(polygonCenter);
            localPos += offset;
            Vector3 worldPos = whiteboardPlane.TransformPoint(localPos);
            currentPolygon.SetPosition(i, worldPos);
        }
    }

    LineRenderer FindClosestPolygon(Vector3 point)
    {
        LineRenderer[] allLines = FindObjectsOfType<LineRenderer>();
        LineRenderer closest = null;
        float closestDist = float.MaxValue;
        float searchRadius = 0.2f;

        foreach (LineRenderer line in allLines)
        {
            if (line.name.Contains("VR_Polygon"))
            {
                Vector3 center = GetPolygonCenter(line);
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

    Vector3 GetPolygonCenter(LineRenderer line)
    {
        if (line.positionCount < 2) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < line.positionCount; i++)
        {
            sum += line.GetPosition(i);
        }
        return sum / line.positionCount;
    }

    float GetPolygonRadius(LineRenderer line)
    {
        if (line.positionCount < 2) return 0f;

        Vector3 center = GetPolygonCenter(line);
        return Vector3.Distance(center, line.GetPosition(0));
    }
}