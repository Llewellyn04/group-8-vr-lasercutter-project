using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VRRectangleDrawer : MonoBehaviour
{
    public XRController Controller;
    public InputHelpers.Button drawButton;
    public float triggerThreshold = 0.1f;
    public Material rectangleMaterial;
    public float rectangleThickness = 0.01f;

    private bool isDrawing = false;
    private Vector3 startPoint;
    private GameObject currentRectangle;
    private LineRenderer lineRenderer;

    void Update()
    {
        if (Controller.inputDevice.IsPressed(drawButton, out bool isPressed, triggerThreshold))
        {
            if (isPressed && !isDrawing)
                StartDrawing();
            else if (isPressed && isDrawing)
                ContinueDrawing();
            else if (!isPressed && isDrawing)
                StopDrawing();
        }
    }

    void StartDrawing()
    {
        isDrawing = true;
        startPoint = Controller.transform.position;

        // Create a new rectangle object
        currentRectangle = new GameObject("Rectangle");
        lineRenderer = currentRectangle.AddComponent<LineRenderer>();

        // Configure the line renderer
        lineRenderer.material = rectangleMaterial;
        lineRenderer.startWidth = rectangleThickness;
        lineRenderer.endWidth = rectangleThickness;
        lineRenderer.loop = true; // Makes it a closed shape
        lineRenderer.positionCount = 5; // 4 corners + back to start
    }

    void ContinueDrawing()
    {
        Vector3 endPoint = Controller.transform.position;

        // Calculate the rectangle corners
        Vector3[] corners = new Vector3[5];
        corners[0] = startPoint;
        corners[1] = new Vector3(endPoint.x, startPoint.y, startPoint.z);
        corners[2] = endPoint;
        corners[3] = new Vector3(startPoint.x, endPoint.y, startPoint.z);
        corners[4] = startPoint; // Close the loop

        lineRenderer.SetPositions(corners);
    }

    void StopDrawing()
    {
        isDrawing = false;
       
    }
}