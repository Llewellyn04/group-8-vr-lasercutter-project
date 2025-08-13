using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem;


public class DrawingSystem : MonoBehaviour
{
    public Camera drawingCamera;
    public GameObject drawingPlane;
    public LineRenderer lineRendererPrefab;
    public int circleSegments = 100; // Smoothness of circle

    private LineRenderer currentLineRenderer;
    private Vector2 startLocalPoint;
    private bool isDrawing = false;
    private bool isCircleMode = false;

    void Update()
    {
        if (isDrawing)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                StartNewLine();
                Vector2 mousePosition = Mouse.current.position.ReadValue();
                startLocalPoint = GetLocalPointOnPlane(mousePosition);
                isCircleMode = true; // We're in circle drawing mode
            }

            if (Mouse.current.leftButton.isPressed && isCircleMode)
            {
                Vector2 mousePosition = Mouse.current.position.ReadValue();
                Vector2 currentLocalPoint = GetLocalPointOnPlane(mousePosition);
                float radius = Vector2.Distance(startLocalPoint, currentLocalPoint);
                DrawCircle(startLocalPoint, radius);
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isCircleMode = false;
            }
        }
    }

    public void EnableDrawingMode()
    {
        isDrawing = true;
    }

    public void DisableDrawingMode()
    {
        isDrawing = false;
    }

    private void StartNewLine()
    {
        currentLineRenderer = Instantiate(lineRendererPrefab, drawingPlane.transform);
        currentLineRenderer.positionCount = 0;
    }

    private Vector2 GetLocalPointOnPlane(Vector2 screenPosition)
    {
        Ray ray = drawingCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject == drawingPlane)
            {
                Vector3 worldPoint = hit.point;
                return drawingPlane.transform.InverseTransformPoint(worldPoint);
            }
        }
        return Vector2.zero;
    }

    private void DrawCircle(Vector2 centerLocal, float radius)
    {
        currentLineRenderer.positionCount = circleSegments + 1;
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / circleSegments;
            float x = centerLocal.x + Mathf.Cos(angle) * radius;
            float y = centerLocal.y + Mathf.Sin(angle) * radius;
            Vector3 worldPoint = drawingPlane.transform.TransformPoint(new Vector3(x, y, 0));
            currentLineRenderer.SetPosition(i, worldPoint);
        }
    }
}
