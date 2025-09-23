using System.Collections.Generic;
using UnityEngine;

public class StraightLineDots : MonoBehaviour
{
    [Header("Drawing Setup")]
    public Camera drawingCamera;       // Camera looking at the whiteboard
    public GameObject drawingPlane;    // The whiteboard (must have a Collider)
    public GameObject dotPrefab;       // Small dot prefab (like your spline tip)

    [Header("Drawing Settings")]
    public float dotSpacing = 0.02f;   // Fixed spacing (2 cm)
    public float dotSize = 0.02f;      // Size of each dot
    public Color dotColor = Color.blue;

    private bool isDrawing = false;
    private Vector3 lastPoint;
    private bool hasLastPoint = false;

    private List<List<Vector3>> allLines = new List<List<Vector3>>();
    private List<Vector3> currentLinePoints;

    void Start()
    {
        if (drawingCamera == null)
            drawingCamera = Camera.main;

        if (drawingPlane != null && drawingPlane.GetComponent<Collider>() == null)
        {
            drawingPlane.AddComponent<BoxCollider>();
            Debug.LogWarning("Added BoxCollider to drawing plane automatically.");
        }
    }

    void Update()
    {
        if (!isDrawing) return;

        Vector3? hitPoint = GetPlaneHit();
        if (hitPoint.HasValue)
        {
            Vector3 worldPoint = hitPoint.Value;



            if (!hasLastPoint)
            {
                // Start new line
                currentLinePoints = new List<Vector3>();
                allLines.Add(currentLinePoints);

                PlaceDot(worldPoint);
                currentLinePoints.Add(worldPoint);

                lastPoint = worldPoint;
                hasLastPoint = true;
            }
            else
            {
                float dist = Vector3.Distance(lastPoint, worldPoint);
                if (dist >= dotSpacing)
                {
                    int steps = Mathf.FloorToInt(dist / dotSpacing);
                    Vector3 dir = (worldPoint - lastPoint).normalized;

                    for (int i = 1; i <= steps; i++)
                    {
                        Vector3 newPoint = lastPoint + dir * (i * dotSpacing);
                        PlaceDot(newPoint);
                        currentLinePoints.Add(newPoint);
                        lastPoint = newPoint;
                    }
                }
            }
        }
    }

    public void EnableDrawing() => isDrawing = true;

    public void DisableDrawing()
    {
        isDrawing = false;
        hasLastPoint = false;
        currentLinePoints = null;
    }

    public void ClearAll()
    {
        foreach (Transform child in drawingPlane.transform)
        {
            Destroy(child.gameObject);
        }

        allLines.Clear();
        currentLinePoints = null;
        hasLastPoint = false;

        Debug.Log("Cleared all drawings");
    }

    private void PlaceDot(Vector3 pos)
    {
        if (dotPrefab == null) return;

        GameObject dot = Instantiate(dotPrefab, pos, Quaternion.identity, drawingPlane.transform);
        dot.transform.localScale = Vector3.one * dotSize;

        Renderer r = dot.GetComponent<Renderer>();
        if (r != null) r.material.color = dotColor;
    }

    private Vector3? GetPlaneHit()
    {
        Ray ray = drawingCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject == drawingPlane)
                return hit.point;
        }
        return null;
    }

    public List<List<Vector3>> GetAllLines() => new List<List<Vector3>>(allLines);
}
