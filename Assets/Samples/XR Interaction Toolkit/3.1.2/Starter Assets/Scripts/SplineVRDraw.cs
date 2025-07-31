using UnityEngine;
using UnityEngine.XR;

[System.Serializable] // Makes it show in Inspector
public class VRDrawSettings
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class SplineVRDraw : MonoBehaviour
{
    public Transform whiteboardPlane; // Assign this in Inspector (e.g., a flat GameObject like a quad)
    public float drawDistanceThreshold = 0.01f; // Max distance allowed to the board to draw

    [Header("Drag References")]
    public Transform drawingTip; // Drag your DrawingTip here
    public VRDrawSettings settings;

    [Header("Debug")]
    public bool alwaysDraw = false; // For testing without buttons

    private LineRenderer currentLine;
    private bool isDrawing = false;

    void Update()
    {
        if (alwaysDraw || isDrawing)
        {
            UpdateDrawing();
        }
    }

   void UpdateDrawing()
{
    if (currentLine == null || whiteboardPlane == null) return;

    // Define the whiteboard plane
    Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);

    // Get tip position and direction
    Ray ray = new Ray(drawingTip.position - drawingTip.forward * 0.05f, drawingTip.forward);

    if (plane.Raycast(ray, out float enter))
    {
        Vector3 hitPoint = ray.GetPoint(enter);
        float distanceToTip = Vector3.Distance(drawingTip.position, hitPoint);

        // Only draw if tip is close enough to the surface
        if (distanceToTip <= drawDistanceThreshold)
        {
            currentLine.positionCount++;
            currentLine.SetPosition(currentLine.positionCount - 1, hitPoint);
        }
    }
}
    public void StartDrawing()
    {
        GameObject lineObj = new GameObject("VR_Drawing");
        currentLine = lineObj.AddComponent<LineRenderer>();

        currentLine.material = settings.lineMaterial;
        currentLine.startColor = settings.lineColor;
        currentLine.endColor = settings.lineColor;
        currentLine.startWidth = settings.lineWidth;
        currentLine.endWidth = settings.lineWidth;
        currentLine.positionCount = 0;

        isDrawing = true;
    }

    public void StopDrawing()
    {
        isDrawing = false;
        if (currentLine != null && currentLine.positionCount > 2)
        {
            currentLine.loop = true; // Close the shape
        }
    }
}