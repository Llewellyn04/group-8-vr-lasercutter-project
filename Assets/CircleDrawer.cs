using UnityEngine;

public class PerfectCircleDrawer : MonoBehaviour
{
    public Camera drawingCamera;               // Camera that looks at the drawing plane
    public GameObject drawingPlane;            // Plane (must have a Collider)
    public LineRenderer lineRendererPrefab;    // Prefab with a LineRenderer + material
    public int circleSegments = 100;           // Smoothness of the circle

    private LineRenderer currentLineRenderer;
    private Vector2 startLocalPoint;           // Circle center in plane local space
    private bool isDrawing = false;
    private bool isCircleMode = false;

    void Update()
    {
        if (!isDrawing) return;

        // Start circle on mouse down
        if (Input.GetMouseButtonDown(0))
        {
            StartNewLine();
            Vector2 mousePosition = Input.mousePosition;
            startLocalPoint = GetLocalPointOnPlane(mousePosition);
            isCircleMode = true;
        }

        // Update circle while holding mouse
        if (Input.GetMouseButton(0) && isCircleMode)
        {
            Vector2 mousePosition = Input.mousePosition;
            Vector2 currentLocalPoint = GetLocalPointOnPlane(mousePosition);
            float radius = Vector2.Distance(startLocalPoint, currentLocalPoint);
            DrawCircle(startLocalPoint, radius);
        }

        // Stop drawing on release
        if (Input.GetMouseButtonUp(0))
        {
            isCircleMode = false;
        }
    }

    public void EnableDrawingMode() { isDrawing = true; }
    public void DisableDrawingMode() { isDrawing = false; isCircleMode = false; }

    private void StartNewLine()
    {
        currentLineRenderer = Instantiate(lineRendererPrefab, drawingPlane.transform);
        currentLineRenderer.positionCount = 0;
        currentLineRenderer.loop = true;        // close the circle
        currentLineRenderer.useWorldSpace = true;
    }

    private Vector2 GetLocalPointOnPlane(Vector2 screenPosition)
    {
        Ray ray = drawingCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == drawingPlane)
        {
            Vector3 worldPoint = hit.point;
            return drawingPlane.transform.InverseTransformPoint(worldPoint);
        }
        return Vector2.zero;
    }

    private void DrawCircle(Vector2 centerLocal, float radius)
    {
        if (currentLineRenderer == null) return;

        currentLineRenderer.positionCount = circleSegments + 1;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / circleSegments;
            float x = centerLocal.x + Mathf.Cos(angle) * radius;
            float y = centerLocal.y + Mathf.Sin(angle) * radius;

            Vector3 worldPoint = drawingPlane.transform.TransformPoint(new Vector3(x, y, 0f));
            currentLineRenderer.SetPosition(i, worldPoint);
        }
    }
}
