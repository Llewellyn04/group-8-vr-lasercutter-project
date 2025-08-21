using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class DrawStraightLine : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public Camera drawingCamera;
    public GameObject drawingPlane;
    
    [Header("Button Events")]
    public UnityEvent onStartDrawing;
    public UnityEvent onFinishDrawing;
    
    private bool isDrawing = false;
    private bool lineStarted = false;
    private Vector3 startPoint;

    void Update()
    {
        if (isDrawing && lineStarted)
        {
            // Update the end point based on mouse position
            Vector3 endPoint = GetMouseWorldPosition();
            lineRenderer.SetPosition(1, endPoint);
            
            // Check for mouse click to finish the line
            if (Input.GetMouseButtonDown(0))
            {
                FinishLine();
            }
        }
    }
    
    public void StartDrawingLine()
    {
        if (!isDrawing)
        {
            isDrawing = true;
            lineStarted = false;
            
            lineRenderer.positionCount = 2;
            
            StartCoroutine(WaitForStartPoint());
        }
    }

    private IEnumerator WaitForStartPoint()
    {
        while (!Input.GetMouseButtonDown(0))
        {
            yield return null;
        }
        
        startPoint = GetMouseWorldPosition();
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, startPoint);
        lineStarted = true;
        
        onStartDrawing?.Invoke();
    }

    private void FinishLine()
    {
        isDrawing = false;
        lineStarted = false;
        onFinishDrawing?.Invoke();
    }

    Vector3 GetMouseWorldPosition()
    {
        Ray ray = drawingCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject == drawingPlane)
            {
                return hit.point;
            }
        }
        
        // Fallback to screen-to-world conversion
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 10f;
        return drawingCamera.ScreenToWorldPoint(mousePos);
    }

    // Alternative method for VR controller input
    public void StartLineWithController(Vector3 position)
    {
        if (!isDrawing)
        {
            isDrawing = true;
            startPoint = position;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startPoint);
            lineRenderer.SetPosition(1, startPoint);
            lineStarted = true;
        }
    }

    public void EndLineWithController(Vector3 position)
    {
        if (isDrawing && lineStarted)
        {
            lineRenderer.SetPosition(1, position);
            FinishLine();
        }
    }
}
