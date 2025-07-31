using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR;


public class VRLineManager:MonoBehaviour
{
    public XRController vrController;
    public Material lineMaterial;//Material for line rendering
    private GameObject currentLine;//Line being drawn

    private void Update()
    {
        //Start new line when trigger is pressed
        if(vrController.inputDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed && currentLine == null)
        {
            StartNewLine();
        }

    }

    private void StartNewLine()
    {
        currentLine=new GameObject("Line");
        LineRenderer lineRenderer = currentLine.AddComponent<LineRenderer>();
        VRLineDrawer lineDrawer = currentLine.AddComponent<VRLineDrawer>();

        //configure LineRenderer
        lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.positionCount = 0; // Start with no points

        //configure VRLineDrawer
        lineDrawer.vrController = vrController;
        lineDrawer.gridSize = 0.1f; // Set grid size for snapping

        lineDrawer.OnDrawingStopped += () => currentLine = null; // Subscribe to drawing stopped event
    }

    public delegate void DrawingStoppedEvent();
    public event DrawingStoppedEvent OnDrawingStopped; // Event to notify when drawing stops

}

