using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[System.Serializable]
public class CircleVRDrawSettings   // ✅ renamed to avoid duplicate
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class CircleVRDraw : MonoBehaviour
{
    [Header("References")]
    public Transform whiteboardPlane;        // Assign in Inspector
    public Transform drawingTip;             // Assign in Inspector (right controller tip)
    public XRController rightController;     // Assign right-hand controller
    public XRController leftController;      // Assign left-hand controller
    public RedoUndoManager redoUndoManager;  // Assign RedoUndoManager component

    [Header("Drawing Settings")]
    public CircleVRDrawSettings settings;   // ✅ updated type name
    public int circleSegments = 32;
    public float radiusSensitivity = 0.2f;   // Speed of radius change

    [Header("Drawing Bounds")]
    public float maxRadius = 0.5f;
    public float minRadius = 0.01f;

    private LineRenderer currentCircle;
    private bool isDrawing = false;
    private Vector3 circleCenter;
    private float currentRadius;

    void Update()
    {
        if (rightController == null)
        {
            Debug.LogError("RightController is not assigned!");
            return;
        }

        // --- Right controller trigger ---
        bool rightTriggerPressed = false;
        if (rightController.inputDevice.isValid)
            rightController.inputDevice.TryGetFeatureValue(CommonUsages.triggerButton, out rightTriggerPressed);

        // --- Left controller input ---
        Vector2 leftJoystick = Vector2.zero;
        if (leftController != null && leftController.inputDevice.isValid)
            leftController.inputDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftJoystick);

        // --- Start / Stop Drawing ---
        if (rightTriggerPressed && !isDrawing)
        {
            StartDrawing();
        }
        else if (!rightTriggerPressed && isDrawing)
        {
            StopDrawing();
        }

        // --- While Drawing ---
        if (isDrawing)
        {
            // Adjust radius with left joystick (x-axis)
            currentRadius += leftJoystick.x * radiusSensitivity * Time.deltaTime;
            currentRadius = Mathf.Clamp(currentRadius, minRadius, maxRadius);

            UpdateCirclePositions();
        }
    }

    void UpdateCirclePositions()
    {
        if (currentCircle == null) return;

        Vector3[] points = new Vector3[circleSegments + 1];
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = i * 2 * Mathf.PI / circleSegments;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, 0f);
            points[i] = circleCenter + offset;
        }
        currentCircle.positionCount = points.Length;
        currentCircle.SetPositions(points);
    }

    public void StartDrawing()
    {
        if (settings == null)
        {
            Debug.LogWarning("VRDrawSettings not assigned!");
            return;
        }
        if (drawingTip == null || whiteboardPlane == null)
        {
            Debug.LogWarning("DrawingTip or WhiteboardPlane not assigned!");
            return;
        }

        // Fix circle center at the tip position (on whiteboard plane)
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(drawingTip.position - drawingTip.forward * 0.05f, drawingTip.forward);
        if (plane.Raycast(ray, out float enter))
        {
            circleCenter = ray.GetPoint(enter);
        }
        else
        {
            Debug.LogWarning("No raycast hit for circle center!");
            return;
        }

        GameObject circleObj = new GameObject("VR_Circle");
        circleObj.transform.SetParent(whiteboardPlane);
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
        Debug.Log("Started Circle Drawing");
    }

    public void StopDrawing()
    {
        isDrawing = false;

        if (currentCircle != null && currentRadius > minRadius)
        {
            currentCircle.loop = true;

            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(currentCircle.gameObject);
                Debug.Log("Circle registered in Undo stack");
            }
        }
        else
        {
            if (currentCircle != null)
                Destroy(currentCircle.gameObject);

            Debug.Log("Circle destroyed: Too small radius");
        }

        currentCircle = null;
        Debug.Log("Stopped Circle Drawing");
    }

    public void ToggleDrawing()
    {
        if (isDrawing)
            StopDrawing();
        else
            StartDrawing();
    }
}
