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
    
    public enum DrawMode { None, Freehand, StraightLine, Rectangle, Circle, Polygon }

    [Header("Modes")]
    [Tooltip("When enabled, draws a 2‑point straight line instead of a freehand spline")]
    public bool useStraightLineMode = false; // Kept for backward-compatibility
    [Tooltip("Current drawing mode")] public DrawMode drawMode = DrawMode.Freehand;
    [Tooltip("Minimum size threshold (world units)")] public float minLineLength = 0.02f;
    [Tooltip("Sides for polygon mode")] [Min(3)] public int polygonSides = 5;
    private const int circleSegmentsDefault = 64;

    // Straight line state
    private bool hasStartPoint = false;
    private Vector3 startWorldPoint;

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
                switch (drawMode)
                {
                    case DrawMode.StraightLine:
                        if (!hasStartPoint)
                        {
                            hasStartPoint = true;
                            startWorldPoint = clampedWorldPoint;
                            currentLine.positionCount = 2;
                            currentLine.loop = false;
                            currentLine.SetPosition(0, startWorldPoint);
                            currentLine.SetPosition(1, startWorldPoint);
                        }
                        else
                        {
                            currentLine.SetPosition(1, clampedWorldPoint);
                        }
                        break;
                    case DrawMode.Rectangle:
                        if (!hasStartPoint)
                        {
                            hasStartPoint = true;
                            startWorldPoint = clampedWorldPoint;
                        }
                        UpdateRectanglePreview(clampedWorldPoint);
                        break;
                    case DrawMode.Circle:
                        if (!hasStartPoint)
                        {
                            hasStartPoint = true;
                            startWorldPoint = clampedWorldPoint;
                        }
                        UpdateCirclePreview(clampedWorldPoint, circleSegmentsDefault);
                        break;
                    case DrawMode.Polygon:
                        if (!hasStartPoint)
                        {
                            hasStartPoint = true;
                            startWorldPoint = clampedWorldPoint;
                        }
                        UpdateCirclePreview(clampedWorldPoint, Mathf.Max(3, polygonSides));
                        break;
                    case DrawMode.Freehand:
                    default:
                        // Freehand mode: append points continuously
                        currentLine.positionCount++;
                        currentLine.SetPosition(currentLine.positionCount - 1, clampedWorldPoint);
                        break;
                }
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

        // Keep legacy bool in sync
        if (useStraightLineMode) drawMode = DrawMode.StraightLine;
        if (drawMode == DrawMode.None) drawMode = DrawMode.Freehand;

        string objName = drawMode switch
        {
            DrawMode.StraightLine => "VR_StraightLine",
            DrawMode.Rectangle   => "VR_Rectangle",
            DrawMode.Circle      => "VR_Circle",
            DrawMode.Polygon     => "VR_Polygon",
            _                    => "VR_Drawing"
        };
        GameObject lineObj = new GameObject(objName);
        currentLine = lineObj.AddComponent<LineRenderer>();

        currentLine.material = settings.lineMaterial;
        currentLine.startColor = settings.lineColor;
        currentLine.endColor = settings.lineColor;
        currentLine.startWidth = settings.lineWidth;
        currentLine.endWidth = settings.lineWidth;
        currentLine.positionCount = 0;

        isDrawing = true;
        hasStartPoint = false;
        Debug.Log($"Started {drawMode} Drawing");
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
            bool keep = false;

            switch (drawMode)
            {
                case DrawMode.StraightLine:
                    if (currentLine.positionCount >= 2)
                    {
                        float length = Vector3.Distance(currentLine.GetPosition(0), currentLine.GetPosition(1));
                        keep = length >= minLineLength;
                    }
                    break;
                case DrawMode.Rectangle:
                    if (currentLine.positionCount >= 4)
                    {
                        Vector3 a = currentLine.GetPosition(0);
                        Vector3 c = currentLine.GetPosition(2);
                        Vector3 la = whiteboardPlane.InverseTransformPoint(a);
                        Vector3 lc = whiteboardPlane.InverseTransformPoint(c);
                        float w = Mathf.Abs(lc.x - la.x);
                        float h = Mathf.Abs(lc.y - la.y);
                        keep = (w >= minLineLength && h >= minLineLength);
                    }
                    break;
                case DrawMode.Circle:
                case DrawMode.Polygon:
                    if (currentLine.positionCount >= 3)
                    {
                        float r = Vector3.Distance(startWorldPoint, currentLine.GetPosition(0));
                        keep = r >= minLineLength;
                    }
                    break;
                case DrawMode.Freehand:
                default:
                    keep = currentLine.positionCount > 1;
                    break;
            }

            if (keep)
            {
                // Close shapes if appropriate
                if (drawMode == DrawMode.Rectangle || drawMode == DrawMode.Circle || drawMode == DrawMode.Polygon)
                    currentLine.loop = true;
                else
                    currentLine.loop = false;

                if (redoUndoManager != null)
                {
                    GameObject lineObj = currentLine.gameObject;
                    redoUndoManager.RegisterLine(lineObj);
                    Debug.Log($"{drawMode} registered: {lineObj.name}. Undo stack count: {redoUndoManager.UndoCount}");
                }
                else
                {
                    Debug.LogWarning("RedoUndoManager not assigned!");
                }
            }
            else
            {
                Destroy(currentLine.gameObject);
                Debug.Log($"{drawMode} discarded (too short or too few points).");
            }
        }

        currentLine = null;
        hasStartPoint = false;
        Debug.Log($"Stopped {drawMode} Drawing");
        // Keep drawMode as-is for next start unless changed by toggles
    }

    public void ToggleDrawing()
    {
        if (isDrawing)
            StopDrawing();
        else
        {
            useStraightLineMode = false;
            drawMode = DrawMode.Freehand;
            StartDrawing();
        }
    }

    // Toggle a 2‑point straight line drawing (same principle as StraightLineVRDraw)
    public void ToggleStraightLineDrawing()
    {
        if (isDrawing && drawMode == DrawMode.StraightLine)
        {
            StopDrawing();
        }
        else
        {
            if (isDrawing) StopDrawing();
            useStraightLineMode = true;
            drawMode = DrawMode.StraightLine;
            StartDrawing();
        }
    }

    // Optional: just flip mode; next trigger press will draw in that mode
    public void ToggleStraightLineMode()
    {
        useStraightLineMode = !useStraightLineMode;
        drawMode = useStraightLineMode ? DrawMode.StraightLine : DrawMode.Freehand;
        Debug.Log($"Straight line mode: {useStraightLineMode}");
    }

    // Rectangle/Circle/Polygon toggles (start/stop immediately like straight line toggle)
    public void ToggleRectangleDrawing()
    {
        if (isDrawing && drawMode == DrawMode.Rectangle) StopDrawing();
        else { if (isDrawing) StopDrawing(); useStraightLineMode = false; drawMode = DrawMode.Rectangle; StartDrawing(); }
    }

    public void ToggleCircleDrawing()
    {
        if (isDrawing && drawMode == DrawMode.Circle) StopDrawing();
        else { if (isDrawing) StopDrawing(); useStraightLineMode = false; drawMode = DrawMode.Circle; StartDrawing(); }
    }

    public void TogglePolygonDrawing()
    {
        if (isDrawing && drawMode == DrawMode.Polygon) StopDrawing();
        else { if (isDrawing) StopDrawing(); useStraightLineMode = false; drawMode = DrawMode.Polygon; StartDrawing(); }
    }

    // ===== Helpers to build previews for Rectangle/Circle/Polygon =====
    void UpdateRectanglePreview(Vector3 currentWorld)
    {
        if (currentLine == null) return;
        // Build axis-aligned rectangle between start and current in plane local space
        Vector3 ls = whiteboardPlane.InverseTransformPoint(startWorldPoint);
        Vector3 lc = whiteboardPlane.InverseTransformPoint(currentWorld);
        Vector3 p0 = new Vector3(ls.x, ls.y, 0f);
        Vector3 p2 = new Vector3(lc.x, lc.y, 0f);
        Vector3 p1 = new Vector3(p2.x, p0.y, 0f);
        Vector3 p3 = new Vector3(p0.x, p2.y, 0f);

        currentLine.loop = true;
        currentLine.positionCount = 4;
        currentLine.SetPosition(0, whiteboardPlane.TransformPoint(p0));
        currentLine.SetPosition(1, whiteboardPlane.TransformPoint(p1));
        currentLine.SetPosition(2, whiteboardPlane.TransformPoint(p2));
        currentLine.SetPosition(3, whiteboardPlane.TransformPoint(p3));
    }

    void UpdateCirclePreview(Vector3 currentWorld, int segments)
    {
        if (currentLine == null) return;
        // Work entirely in the whiteboard's local XY to respect scaling
        Vector3 lCenter = whiteboardPlane.InverseTransformPoint(startWorldPoint);
        Vector3 lCurrent = whiteboardPlane.InverseTransformPoint(currentWorld);
        Vector2 delta = new Vector2(lCurrent.x - lCenter.x, lCurrent.y - lCenter.y);
        float radius = delta.magnitude;
        if (radius < Mathf.Epsilon) radius = 0f;

        currentLine.loop = true;
        if (currentLine.positionCount != segments) currentLine.positionCount = segments;

        float twoPi = Mathf.PI * 2f;
        float halfW = maxWidth * 0.5f;
        float halfH = maxHeight * 0.5f;
        for (int i = 0; i < segments; i++)
        {
            float t = (i / (float)segments) * twoPi;
            Vector3 localPoint = new Vector3(
                lCenter.x + Mathf.Cos(t) * radius,
                lCenter.y + Mathf.Sin(t) * radius,
                0f
            );

            // Clamp within local bounds then convert to world
            localPoint.x = Mathf.Clamp(localPoint.x, -halfW, halfW);
            localPoint.y = Mathf.Clamp(localPoint.y, -halfH, halfH);

            Vector3 worldPoint = whiteboardPlane.TransformPoint(localPoint);
            currentLine.SetPosition(i, worldPoint);
        }
    }
}
