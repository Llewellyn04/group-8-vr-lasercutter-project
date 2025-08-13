using UnityEngine;


[RequireComponent(typeof(LineRenderer))]
public class VRCircleDrawer : MonoBehaviour
{
    public Transform controller; // VR controller transform
    public UnityEngine.XR.InputDeviceRole controllerRole = UnityEngine.XR.InputDeviceRole.RightHanded;// Change for your SDK
    public int segments = 100;

    private LineRenderer line;
    private Vector3 startPoint;
    private bool isDrawing = false;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 0;
        line.useWorldSpace = true;
        line.widthMultiplier = 0.01f; // Thickness
    }

    void Update()
    {
        if (OVRInput.GetDown(drawButton))
        {
            startPoint = controller.position;
            isDrawing = true;
        }

        if (isDrawing && OVRInput.Get(drawButton))
        {
            float radius = Vector3.Distance(startPoint, controller.position);
            DrawCircle(startPoint, radius);
        }

        if (OVRInput.GetUp(drawButton))
        {
            isDrawing = false;
        }
    }

    void DrawCircle(Vector3 center, float radius)
    {
        line.positionCount = segments + 1;
        float angle = 0f;

        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            float z = Mathf.Sin(Mathf.Deg2Rad * angle) * radius; // Z axis for ground plane
            line.SetPosition(i, new Vector3(center.x + x, center.y, center.z + z));
            angle += 360f / segments;
        }
    }
}
