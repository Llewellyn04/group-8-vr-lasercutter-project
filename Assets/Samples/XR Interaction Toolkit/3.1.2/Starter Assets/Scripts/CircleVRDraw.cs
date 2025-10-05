using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[System.Serializable]
public class CircleVRDrawSettings
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class CircleVRDraw : MonoBehaviour
{
    [Header("References")]
    public Transform whiteboardPlane;
    public Transform leftControllerTip;   // Left controller for resizing
    public Transform rightControllerTip;  // Right controller for dragging
    public RedoUndoManager redoUndoManager;

    [Header("Drawing Settings")]
    public CircleVRDrawSettings settings;
    public int circleSegments = 32;
    public float radiusChangeSpeed = 0.5f;
    public bool autoIncreaseRadius = true;

    [Header("Drawing Bounds")]
    public float maxRadius = 0.5f;
    public float minRadius = 0.05f;
    public float maxWidth = 1.0f;
    public float maxHeight = 1.0f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private LineRenderer currentCircle;
    private bool isDrawing = false;
    private bool isDragging = false;
    private bool isResizing = false;
    private Vector3 circleCenter;
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
    /// Once active, moving the left controller left/right will shrink/grow the circle
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

        // Stop other modes (but DON'T destroy the circle being drawn)
        if (isDrawing)
        {
            // Finish the drawing first, save the circle
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

        // Raycast from left controller to find a circle
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, leftControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closestCircle = FindClosestCircle(hitPoint);

            if (closestCircle != null)
            {
                currentCircle = closestCircle;
                circleCenter = GetCircleCenter(closestCircle);
                currentRadius = GetCircleRadius(closestCircle);

                // Store initial X position for horizontal tracking
                lastLeftControllerX = leftControllerTip.position.x;

                isResizing = true;
                if (showDebugLogs) Debug.Log($"Started resizing circle: {closestCircle.name}. Move left controller left/right to resize.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("No circle found to resize");
            }
        }
    }

    /// <summary>
    /// Updates the circle radius based on left controller horizontal movement (left-right)
    /// Called automatically in Update() while isResizing is true
    /// </summary>
    void UpdateResizeWithLeftController()
    {
        if (leftControllerTip == null || currentCircle == null) return;

        // Calculate horizontal movement (left = shrink, right = grow)
        float currentX = leftControllerTip.position.x;
        float deltaX = currentX - lastLeftControllerX;

        // Change radius based on horizontal movement
        currentRadius += deltaX * radiusChangeSpeed * 10f; // Multiply for more sensitivity
        currentRadius = Mathf.Clamp(currentRadius, minRadius, maxRadius);

        UpdateCirclePositions();

        lastLeftControllerX = currentX;
    }

    void StopResizeMode()
    {
        if (!isResizing) return;

        isResizing = false;
        currentCircle = null;

        if (showDebugLogs) Debug.Log("Stopped resizing");
    }

    // ============================================
    // DRAG MODE FUNCTIONS (Right Controller)
    // ============================================

    /// <summary>
    /// Call this from ONE UI button to toggle drag mode
    /// Once active, moving the right controller will drag the circle around
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

        // Stop other modes (but DON'T destroy the circle being drawn)
        if (isDrawing)
        {
            // Finish the drawing first, save the circle
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

        // Raycast from right controller to find a circle
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(rightControllerTip.position, rightControllerTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            LineRenderer closestCircle = FindClosestCircle(hitPoint);

            if (closestCircle != null)
            {
                currentCircle = closestCircle;
                circleCenter = GetCircleCenter(closestCircle);
                currentRadius = GetCircleRadius(closestCircle);

                // Calculate drag offset so circle doesn't jump
                dragOffset = circleCenter - hitPoint;

                isDragging = true;
                if (showDebugLogs) Debug.Log($"Started dragging circle: {closestCircle.name}. Move right controller to drag.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("No circle found near controller position");
            }
        }
    }

    /// <summary>
    /// Updates the circle position to follow the right controller
    /// Called automatically in Update() while isDragging is true
    /// </summary>
    void UpdateDragging()
    {
        if (rightControllerTip == null || currentCircle == null) return;

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
            circleCenter = whiteboardPlane.TransformPoint(localPoint);
            UpdateCirclePositions();
        }
    }

    void StopDragging()
    {
        if (!isDragging) return;

        isDragging = false;
        currentCircle = null;

        if (showDebugLogs) Debug.Log("Stopped dragging");
    }

    // ============================================
    // DRAWING MODE FUNCTIONS (Original)
    // ============================================

    void UpdateRadiusWithLeftController()
    {
        if (leftControllerTip == null || currentCircle == null) return;

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(leftControllerTip.position, -whiteboardPlane.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            float newRadius = Vector3.Distance(hitPoint, circleCenter);
            currentRadius = Mathf.Clamp(newRadius, minRadius, maxRadius);
            UpdateCirclePositions();
        }
    }

    /// <summary>
    /// Call this from a UI button to start drawing a new circle
    /// </summary>
    public void StartDrawing()
    {
        if (isDrawing)
        {
            if (showDebugLogs) Debug.Log("Already drawing!");
            return;
        }

        // Stop other modes
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

            circleCenter = whiteboardPlane.TransformPoint(localPoint);
        }
        else
        {
            Debug.LogWarning("No raycast hit for circle center!");
            return;
        }

        GameObject circleObj = new GameObject("VR_Circle");
        currentCircle = circleObj.AddComponent<LineRenderer>();

        currentCircle.material = settings.lineMaterial;
        currentCircle.startColor = settings.lineColor;
        currentCircle.endColor = settings.lineColor;
        currentCircle.startWidth = settings.lineWidth;
        currentCircle.endWidth = settings.lineWidth;
        currentCircle.useWorldSpace = true;

        currentRadius = minRadius;
        UpdateCirclePositions();

        isDrawing = true;
        if (showDebugLogs) Debug.Log("Started Circle Drawing");
    }

    /// <summary>
    /// Call this from a UI button to stop drawing
    /// </summary>
    public void StopDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;

        if (currentCircle != null && currentRadius > minRadius)
        {
            currentCircle.loop = true;

            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(currentCircle.gameObject);
                if (showDebugLogs) Debug.Log("Circle registered in Undo stack");
            }
        }
        else
        {
            if (currentCircle != null)
                Destroy(currentCircle.gameObject);

            if (showDebugLogs) Debug.Log("Circle destroyed: Too small radius");
        }

        currentCircle = null;
        if (showDebugLogs) Debug.Log("Stopped Circle Drawing");
    }

    /// <summary>
    /// Finishes drawing and keeps the circle (called internally when switching modes)
    /// </summary>
    void FinishDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;

        if (currentCircle != null)
        {
            currentCircle.loop = true;

            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(currentCircle.gameObject);
                if (showDebugLogs) Debug.Log("Circle finished and registered in Undo stack");
            }
        }

        // DON'T set currentCircle to null - we might want to use it immediately
        if (showDebugLogs) Debug.Log("Finished Circle Drawing (circle kept for further editing)");
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

    void UpdateCirclePositions()
    {
        if (currentCircle == null) return;

        currentCircle.positionCount = circleSegments + 1;
        float angleStep = 360f / circleSegments;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * currentRadius;
            Vector3 localPos = whiteboardPlane.InverseTransformPoint(circleCenter);
            localPos += offset;
            Vector3 worldPos = whiteboardPlane.TransformPoint(localPos);
            currentCircle.SetPosition(i, worldPos);
        }
    }

    LineRenderer FindClosestCircle(Vector3 point)
    {
        LineRenderer[] allLines = FindObjectsOfType<LineRenderer>();
        LineRenderer closest = null;
        float closestDist = float.MaxValue;
        float searchRadius = 0.2f;

        foreach (LineRenderer line in allLines)
        {
            if (line.name.Contains("VR_Circle"))
            {
                Vector3 center = GetCircleCenter(line);
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

    Vector3 GetCircleCenter(LineRenderer line)
    {
        if (line.positionCount < 2) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < line.positionCount; i++)
        {
            sum += line.GetPosition(i);
        }
        return sum / line.positionCount;
    }

    float GetCircleRadius(LineRenderer line)
    {
        if (line.positionCount < 2) return 0f;

        Vector3 center = GetCircleCenter(line);
        return Vector3.Distance(center, line.GetPosition(0));
    }
}