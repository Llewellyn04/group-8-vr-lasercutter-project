using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DrawingSystem : MonoBehaviour
{
    public Camera drawingCamera; // Camera used to view the drawing plane
    public GameObject drawingPlane; // The 2D plane where the user will draw
    public LineRenderer lineRendererPrefab; // Prefab for the LineRenderer
    private LineRenderer currentLineRenderer;
    private List<Vector2> drawingPoints = new List<Vector2>();
    private bool isDrawing = false;

    void Update()
    {
        if (isDrawing)
        {
            // Capture input and draw
            if (Mouse.current.leftButton.isPressed)
            {
                Vector2 mousePosition = Mouse.current.position.ReadValue();
                DrawOnPlane(mousePosition);
            }
        }
    }

    public void EnableDrawingMode()
    {
        // Called when the user clicks the "Spline" button
        isDrawing = true;
        StartNewLine();
    }

    public void DisableDrawingMode()
    {
        // Disable drawing mode
        isDrawing = false;
    }

    private void StartNewLine()
    {
        // Create a new LineRenderer for the current drawing
        currentLineRenderer = Instantiate(lineRendererPrefab, drawingPlane.transform);
        currentLineRenderer.positionCount = 0;
        drawingPoints.Clear();
    }

    private void DrawOnPlane(Vector2 screenPosition)
    {
        // Convert screen position to a point on the drawing plane
        Ray ray = drawingCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject == drawingPlane)
            {
                Vector3 worldPoint = hit.point;
                Vector2 localPoint = drawingPlane.transform.InverseTransformPoint(worldPoint);

                // Add the point to the drawing
                if (drawingPoints.Count == 0 || Vector2.Distance(drawingPoints[drawingPoints.Count - 1], localPoint) > 0.01f)
                {
                    drawingPoints.Add(localPoint);
                    UpdateLineRenderer();
                }
            }
        }
    }

    private void UpdateLineRenderer()
    {
        // Update the LineRenderer with the current points
        currentLineRenderer.positionCount = drawingPoints.Count;
        for (int i = 0; i < drawingPoints.Count; i++)
        {
            Vector3 worldPoint = drawingPlane.transform.TransformPoint(new Vector3(drawingPoints[i].x, drawingPoints[i].y, 0));
            currentLineRenderer.SetPosition(i, worldPoint);
        }
    }

    public List<Vector2> GetDrawingPoints()
    {
        // Return the list of drawing points for export
        return new List<Vector2>(drawingPoints);
    }
}