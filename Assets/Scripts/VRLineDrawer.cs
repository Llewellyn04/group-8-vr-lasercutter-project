using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR;
public class VRLineDrawer:MonoBehaviour
{

    public XRController vrController; // Reference to the VR controller
    private LineRenderer lineRenderer; // LineRenderer component to draw the line
    private Vector3[] linePoints; // Array to store points of the line
    private bool isDrawing; // Flag to check if currently drawing
    [SerializeField] public float gridSize = 0.1f; // Size of the grid for snapping (meters)
    private static bool isGridSnapping = false; // Static flag for grid snapping state
    public event System.Action OnDrawingStopped; // Event to notify when drawing stops
    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            Debug.LogError("VRLineDrawer requires a LineRenderer component!");
            enabled = false;
        }
    }

    private void Update()
    {
        // Toggle grid snapping with grip button
        if (vrController.inputDevice.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed) && gripPressed)
        {
            ToggleGridSnapping();
        }

        // Start drawing when trigger is pressed
        if (vrController.inputDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed && !isDrawing)
        {
            StartDrawing();
        }
        // Add points while drawing
        else if (isDrawing && triggerPressed)
        {
            AddPointToLine();
        }
        // Stop drawing when trigger is released
        else if (isDrawing && !triggerPressed)
        {
            StopDrawing();
        }
    }

    private void ToggleGridSnapping()
    {
        isGridSnapping = !isGridSnapping;
        Debug.Log($"Grid Snapping {(isGridSnapping ? "Enabled" : "Disabled")}");
        vrController.inputDevice.SendHapticImpulse(0, 0.5f, 0.1f);
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        if (!isGridSnapping) return position;
        float x = Mathf.Round(position.x / gridSize) * gridSize;
        float y = Mathf.Round(position.y / gridSize) * gridSize;
        float z = Mathf.Round(position.z / gridSize) * gridSize;
        return new Vector3(x, y, z);
    }

    private void StartDrawing()
    {
        isDrawing = true;
        linePoints = new Vector3[0];
        lineRenderer.positionCount = 0;
    }

    private void AddPointToLine()
    {
        vrController.inputDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 controllerPos);
        Vector3 snappedPos = SnapToGrid(controllerPos);
        System.Array.Resize(ref linePoints, linePoints.Length + 1);
        linePoints[linePoints.Length - 1] = snappedPos;
        lineRenderer.positionCount = linePoints.Length;
        lineRenderer.SetPositions(linePoints);
    }

    private void StopDrawing()
    {
        isDrawing = false;
        ICommand command = new LineCreationCommand(gameObject, linePoints);
        CommandManager.Instance.ExecuteCommand(command);
        OnDrawingStopped?.Invoke(); // Notify manager
    }

    public static void SetGridSnapping(bool state)
    {
        isGridSnapping = state;
        Debug.Log($"Grid Snapping {(isGridSnapping ? "Enabled" : "Disabled")}");
    }
}
