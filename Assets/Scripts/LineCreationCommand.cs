using UnityEngine;

public class LineCreationCommand : ICommand
{
    private GameObject lineObject;
    private Vector3[] linePoints;//store line points
    private bool isActive;//track if the line is active


    public LineCreationCommand(GameObject lineObject, Vector3[] linePoints)
    {
        this.lineObject = lineObject;
        this.linePoints = linePoints;
        this.isActive = lineObject.activeSelf; //store the initial active state of the line object
    }

    public void Execute()
    {
        if(lineObject != null)
        {
            lineObject.SetActive(true); //show the line
            LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = linePoints.Length; //set the number of points in the line renderer
                lineRenderer.SetPositions(linePoints); //restore line points
            }
        }
    }

    public void Undo()
    {
        if (lineObject != null)
        {
            lineObject.SetActive(false);//Line gets hidden
        }
    }
}
