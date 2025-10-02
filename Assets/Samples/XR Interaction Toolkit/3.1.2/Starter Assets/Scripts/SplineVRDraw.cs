using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[System.Serializable]
public class VRDrawSettings
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.white;
}

public class SplineVRDraw : MonoBehaviour
{
    [Header("References")]
    public Transform whiteboardPlane;
    public Transform drawingTip;
    public XRController rightController;
    public RedoUndoManager redoUndoManager;

    [Header("Drawing Settings")]
    public VRDrawSettings settings;
    public float drawDistanceThreshold = 0.01f;

    [Header("Drawing Bounds")]
    public float maxWidth = 1.0f;
    public float maxHeight = 1.0f;

    [Header("Debug")]
    public bool alwaysDraw = false;

    private LineRenderer currentLine;
    private bool isDrawing = false;
    private bool lastTriggerState = false;

    void Update()
    {
        if (rightController != null && rightController.inputDevice.isValid)
        {
            rightController.inputDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);

            if (triggerPressed && !lastTriggerState && !isDrawing)
                StartDrawing();
            else if (!triggerPressed && lastTriggerState && isDrawing)
                StopDrawing();

            lastTriggerState = triggerPressed;
        }

        if (isDrawing || alwaysDraw)
            UpdateDrawing();
    }

    void UpdateDrawing()
    {
        if (currentLine == null || whiteboardPlane == null || drawingTip == null) return;

        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(drawingTip.position - drawingTip.forward * 0.05f, drawingTip.forward);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 localPoint = whiteboardPlane.InverseTransformPoint(hitPoint);

            float halfWidth = maxWidth * 0.5f;
            float halfHeight = maxHeight * 0.5f;
            localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);
            localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight, halfHeight);
            localPoint.z = 0f;

            Vector3 clampedWorldPoint = whiteboardPlane.TransformPoint(localPoint);

            if (Mathf.Abs(localPoint.x) <= halfWidth && Mathf.Abs(localPoint.y) <= halfHeight)
            {
                currentLine.positionCount++;
                currentLine.SetPosition(currentLine.positionCount - 1, clampedWorldPoint);
            }
        }
    }

    public void StartDrawing()
    {
        if (settings == null)
        {
            Debug.LogWarning("VRDrawSettings not assigned!");
            return;
        }

        GameObject lineObj = new GameObject("VR_Drawing");
        currentLine = lineObj.AddComponent<LineRenderer>();

        currentLine.material = settings.lineMaterial;
        currentLine.startColor = settings.lineColor;
        currentLine.endColor = settings.lineColor;
        currentLine.startWidth = settings.lineWidth;
        currentLine.endWidth = settings.lineWidth;
        currentLine.positionCount = 0;

        isDrawing = true;
        Debug.Log("Started Drawing");
    }

    public void StopDrawing()
    {
        Debug.Log("StopDrawing() called!"); // DEBUG

        if (!isDrawing)
        {
            Debug.Log("Not drawing, returning early"); // DEBUG
            return;
        }

        isDrawing = false;

        if (currentLine != null)
        {
            if (currentLine.positionCount > 1)
            {
                currentLine.loop = false;

                if (redoUndoManager != null)
                {
                    // Store reference to GameObject before setting currentLine to null
                    GameObject lineObj = currentLine.gameObject;
                    redoUndoManager.RegisterLine(lineObj);
                    Debug.Log($"Line registered: {lineObj.name}. Undo stack count: {redoUndoManager.UndoCount}");
                }
                else
                {
                    Debug.LogWarning("RedoUndoManager not assigned!");
                }
            }
            else
            {
                Destroy(currentLine.gameObject);
                Debug.Log("Line discarded (too few points).");
            }
        }

        currentLine = null;
        Debug.Log("Stopped Drawing");
    }

    public void ToggleDrawing()
    {
        if (isDrawing)
            StopDrawing();
        else
            StartDrawing();
    }
}