// 7/22/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;

public class DrawingBoard : MonoBehaviour
{
    private Vector3 startPoint;
    private bool isDrawing = false;

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left mouse button pressed
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject)
            {
                startPoint = hit.point;
                isDrawing = true;
            }
        }

        if (Input.GetMouseButtonUp(0) && isDrawing) // Left mouse button released
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject)
            {
                Vector3 endPoint = hit.point;
                DrawShape(startPoint, endPoint);
                isDrawing = false;
            }
        }
    }

    void DrawShape(Vector3 start, Vector3 end)
    {
        switch (SketchToolManager.ActiveTool)
        {
            case SketchToolManager.ToolType.Line:
                DrawLine(start, end);
                break;
            case SketchToolManager.ToolType.Rectangle:
                DrawRectangle(start, end);
                break;
            case SketchToolManager.ToolType.Ellipse:
                DrawEllipse(start, end);
                break;
        }
    }

    void DrawLine(Vector3 start, Vector3 end)
    {
        GameObject line = new GameObject("Line");
        LineRenderer lineRenderer = line.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.black;
        lineRenderer.endColor = Color.black;
    }

    void DrawRectangle(Vector3 start, Vector3 end)
    {
        Vector3 topLeft = new Vector3(start.x, start.y, end.z);
        Vector3 topRight = end;
        Vector3 bottomLeft = start;
        Vector3 bottomRight = new Vector3(end.x, start.y, start.z);

        DrawLine(bottomLeft, topLeft);
        DrawLine(topLeft, topRight);
        DrawLine(topRight, bottomRight);
        DrawLine(bottomRight, bottomLeft);
    }

    void DrawEllipse(Vector3 start, Vector3 end)
    {
        GameObject ellipse = new GameObject("Ellipse");
        LineRenderer lineRenderer = ellipse.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 100;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.black;
        lineRenderer.endColor = Color.black;

        float a = Mathf.Abs(end.x - start.x) / 2;
        float b = Mathf.Abs(end.z - start.z) / 2;
        Vector3 center = (start + end) / 2;

        for (int i = 0; i < 100; i++)
        {
            float angle = i * Mathf.PI * 2 / 100;
            float x = center.x + a * Mathf.Cos(angle);
            float z = center.z + b * Mathf.Sin(angle);
            lineRenderer.SetPosition(i, new Vector3(x, center.y, z));
        }
    }
}