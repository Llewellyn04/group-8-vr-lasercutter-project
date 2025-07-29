using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DXFPathRenderer : MonoBehaviour
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;

    // Clears previous paths and renders new ones with interaction enabled
    public void RenderPaths(List<Vector3[]> paths)
    {
        // Clear existing path GameObjects
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        foreach (var pathPoints in paths)
        {
            // Create a new GameObject for each path
            GameObject pathGO = new GameObject("ImportedPath");
            pathGO.transform.parent = this.transform;

            // Add LineRenderer to draw the path
            LineRenderer lr = pathGO.AddComponent<LineRenderer>();
            lr.positionCount = pathPoints.Length;
            lr.SetPositions(pathPoints);
            lr.material = lineMaterial;
            lr.widthMultiplier = lineWidth;
            lr.useWorldSpace = false;
            lr.loop = false;

            // Add Rigidbody for physics interaction (required by XRGrabInteractable)
            Rigidbody rb = pathGO.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;

            // Calculate bounds of the path to size the collider
            Bounds bounds = new Bounds(pathPoints[0], Vector3.zero);
            foreach (var pt in pathPoints)
            {
                bounds.Encapsulate(pt);
            }

            // Add BoxCollider sized to cover the path geometry
            BoxCollider boxCollider = pathGO.AddComponent<BoxCollider>();
            boxCollider.center = bounds.center;
            // Slightly expand collider size to ensure easy grabbing
            boxCollider.size = bounds.size + new Vector3(lineWidth * 2, lineWidth * 2, lineWidth * 2);

            // Add XR Grab Interactable for VR interaction
            XRGrabInteractable grabInteractable = pathGO.AddComponent<XRGrabInteractable>();

            // Optional: Configure grab interactable here (e.g., attach points, movement types)

            // Position pathGO properly (optional, depends on your coordinate space)
            pathGO.transform.localPosition = Vector3.zero;
            pathGO.transform.localRotation = Quaternion.identity;
        }
    }
}

