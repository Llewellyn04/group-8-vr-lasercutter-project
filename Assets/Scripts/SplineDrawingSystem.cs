using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DrawingSystem : MonoBehaviour
{
    [Header("Drawing Setup")]
    public Camera drawingCamera; // Camera used to view the drawing plane
    public GameObject drawingPlane; // The 2D plane where the user will draw
    public LineRenderer lineRendererPrefab; // Prefab for the LineRenderer

    [Header("Drawing Settings")]
    public float minPointDistance = 0.01f; // Minimum distance between points
    public float lineWidth = 0.02f; // Width of drawn lines
    public Color drawingColor = Color.blue;

    [Header("Drawing Bounds (Optional)")]
    public bool constrainDrawing = false;
    public float maxDrawingWidth = 10f;  // Remove the constraint of 1
    public float maxDrawingHeight = 10f; // Remove the constraint of 1
    public Vector2 drawingAreaCenter = Vector2.zero;

    private LineRenderer currentLineRenderer;
    private List<Vector2> drawingPoints = new List<Vector2>();
    private List<LineRenderer> allDrawnLines = new List<LineRenderer>(); // Track all lines
     private bool isDrawing = false;
    private bool isCurrentlyDrawingLine = false;

    void Start()
    {
        Debug.Log("=== DrawingSystem Start ===");

        // Validate components
        if (drawingCamera == null)
        {
            drawingCamera = Camera.main;
            Debug.LogWarning("Drawing camera not assigned, using main camera");
        }

        if (drawingPlane == null)
        {
            Debug.LogError("Drawing plane not assigned!");
        }

        if (lineRendererPrefab == null)
        {
            Debug.LogError("Line renderer prefab not assigned!");
        }

        // Ensure drawing plane has a collider
        if (drawingPlane != null && drawingPlane.GetComponent<Collider>() == null)
        {
            Debug.LogWarning("Drawing plane missing collider - adding BoxCollider");
            drawingPlane.AddComponent<BoxCollider>();
        }
    }

    void Update()
    {
        if (!isDrawing) return;

        // Handle mouse input for drawing
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                StartDrawingLine();
            }
            else if (Mouse.current.leftButton.isPressed && isCurrentlyDrawingLine)
            {
                ContinueDrawingLine();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame && isCurrentlyDrawingLine)
            {
                FinishDrawingLine();
            }
        }

        // Handle touch input for XR/mobile
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];

            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                StartDrawingLine();
            }
            else if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved && isCurrentlyDrawingLine)
            {
                ContinueDrawingLine();
            }
            else if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended && isCurrentlyDrawingLine)
            {
                FinishDrawingLine();
            }
        }
    }

    public void EnableDrawingMode()
    {
        Debug.Log("=== EnableDrawingMode Called ===");
        isDrawing = true;
        Debug.Log("Drawing mode enabled");
    }

    public void DisableDrawingMode()
    {
        Debug.Log("=== DisableDrawingMode Called ===");
        isDrawing = false;
        isCurrentlyDrawingLine = false;
        Debug.Log("Drawing mode disabled");
    }

    private void StartDrawingLine()
    {
        Debug.Log("=== StartDrawingLine ===");

        Vector2 inputPosition = GetInputPosition();
        Vector3 worldPoint = GetWorldPointFromInput(inputPosition);

        if (worldPoint != Vector3.zero)
        {
            isCurrentlyDrawingLine = true;
            currentLineRenderer = CreateNewLineRenderer();
            drawingPoints.Clear();

            Vector2 localPoint = drawingPlane.transform.InverseTransformPoint(worldPoint);

            // Check constraints if enabled
            if (constrainDrawing && !IsPointInBounds(localPoint))
            {
                Debug.LogWarning("Starting point outside drawing bounds");
                return;
            }

            drawingPoints.Add(localPoint);
            UpdateLineRenderer();
            Debug.Log($"Started new line at local point: {localPoint}");
        }
    }

    private void ContinueDrawingLine()
    {
        if (currentLineRenderer == null) return;

        Vector2 inputPosition = GetInputPosition();
        Vector3 worldPoint = GetWorldPointFromInput(inputPosition);

        if (worldPoint != Vector3.zero)
        {
            Vector2 localPoint = drawingPlane.transform.InverseTransformPoint(worldPoint);

            // Check constraints if enabled
            if (constrainDrawing && !IsPointInBounds(localPoint))
            {
                Debug.LogWarning($"Point outside bounds: {localPoint}");
                return;
            }

            // Only add point if it's far enough from the last point
            if (drawingPoints.Count == 0 ||
                Vector2.Distance(drawingPoints[drawingPoints.Count - 1], localPoint) > minPointDistance)
            {
                drawingPoints.Add(localPoint);
                UpdateLineRenderer();
            }
        }
    }

    private void FinishDrawingLine()
    {
        Debug.Log($"=== FinishDrawingLine with {drawingPoints.Count} points ===");
        isCurrentlyDrawingLine = false;

        if (currentLineRenderer != null && drawingPoints.Count > 1)
        {
            allDrawnLines.Add(currentLineRenderer);
            Debug.Log("Line completed and added to drawn lines list");
        }
        else if (currentLineRenderer != null)
        {
            // Remove single-point lines
            DestroyImmediate(currentLineRenderer.gameObject);
            Debug.Log("Removed single-point line");
        }

        currentLineRenderer = null;
    }

    private Vector2 GetInputPosition()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            return Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            return Touchscreen.current.touches[0].position.ReadValue();
        }
        return Vector2.zero;
    }

    private Vector3 GetWorldPointFromInput(Vector2 screenPosition)
    {
        if (drawingCamera == null || drawingPlane == null)
        {
            Debug.LogError("DrawingCamera or DrawingPlane is null!");
            return Vector3.zero;
        }

        // Cast ray from camera through screen position
        Ray ray = drawingCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject == drawingPlane)
            {
                return hit.point;
            }
        }

        // Alternative method: project onto plane
        Plane plane = new Plane(drawingPlane.transform.forward, drawingPlane.transform.position);
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return Vector3.zero;
    }

    private bool IsPointInBounds(Vector2 localPoint)
    {
        if (!constrainDrawing) return true;

        Vector2 relativePoint = localPoint - drawingAreaCenter;
        return Mathf.Abs(relativePoint.x) <= maxDrawingWidth / 2f &&
               Mathf.Abs(relativePoint.y) <= maxDrawingHeight / 2f;
    }

    private LineRenderer CreateNewLineRenderer()
    {
        Debug.Log("=== Creating New LineRenderer ===");

        GameObject lineObj = Instantiate(lineRendererPrefab.gameObject, drawingPlane.transform);
        LineRenderer lr = lineObj.GetComponent<LineRenderer>();

        // Set up line renderer properties
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = false; // Use local space relative to drawing plane
        lr.positionCount = 0;

        // Set material color
        if (lr.material != null)
        {
            lr.material.color = drawingColor;
        }

        Debug.Log("LineRenderer created successfully");
        return lr;
    }

    private void UpdateLineRenderer()
    {
        if (currentLineRenderer == null || drawingPoints.Count == 0) return;

        currentLineRenderer.positionCount = drawingPoints.Count;

        for (int i = 0; i < drawingPoints.Count; i++)
        {
            // Convert local 2D point to local 3D point (Z = 0)
            Vector3 localPoint3D = new Vector3(drawingPoints[i].x, drawingPoints[i].y, 0);
            currentLineRenderer.SetPosition(i, localPoint3D);
        }
    }

    // Public methods for external access
    public List<Vector2> GetCurrentDrawingPoints()
    {
        return new List<Vector2>(drawingPoints);
    }

    public List<Vector2> GetAllDrawingPoints()
    {
        List<Vector2> allPoints = new List<Vector2>();

        foreach (LineRenderer line in allDrawnLines)
        {
            if (line != null)
            {
                Vector3[] positions = new Vector3[line.positionCount];
                line.GetPositions(positions);

                foreach (Vector3 pos in positions)
                {
                    allPoints.Add(new Vector2(pos.x, pos.y));
                }
            }
        }

        return allPoints;
    }

    public void ClearAllDrawings()
    {
        Debug.Log("=== Clearing All Drawings ===");

        // Clear current line
        if (currentLineRenderer != null)
        {
            DestroyImmediate(currentLineRenderer.gameObject);
            currentLineRenderer = null;
        }

        // Clear all drawn lines
        foreach (LineRenderer line in allDrawnLines)
        {
            if (line != null)
            {
                DestroyImmediate(line.gameObject);
            }
        }

        allDrawnLines.Clear();
        drawingPoints.Clear();
        isCurrentlyDrawingLine = false;

        Debug.Log("All drawings cleared");
    }

    public int GetDrawnLinesCount()
    {
        return allDrawnLines.Count;
    }

    // Debug method
    public void LogDrawingInfo()
    {
        Debug.Log($"Drawing Mode: {isDrawing}");
        Debug.Log($"Currently Drawing Line: {isCurrentlyDrawingLine}");
        Debug.Log($"Current Points: {drawingPoints.Count}");
        Debug.Log($"Total Lines: {allDrawnLines.Count}");
        Debug.Log($"Drawing Plane: {(drawingPlane != null ? drawingPlane.name : "NULL")}");
        Debug.Log($"Drawing Camera: {(drawingCamera != null ? drawingCamera.name : "NULL")}");
    }
}