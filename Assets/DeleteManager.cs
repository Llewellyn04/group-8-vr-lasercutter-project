using UnityEngine;

public class DeleteManager : MonoBehaviour
{
    public GameObject framePrefab;   // Assign a prefab in Inspector (highlight/frame)

    private GameObject selectedObject;
    private GameObject highlightFrame;
    private Camera cam;

    void Start()
    {
        cam = Camera.main; // get main camera for raycasting
    }

    void Update()
    {
        // Left mouse click to select object
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            if (hit.collider != null)
            {
                SelectObject(hit.collider.gameObject);
            }
        }
    }

    // Select an object
    private void SelectObject(GameObject obj)
    {
        // Remove old frame if exists
        if (highlightFrame != null)
        {
            Destroy(highlightFrame);
        }

        selectedObject = obj;
        Debug.Log("Selected: " + obj.name);

        // Add highlight frame as a child
        if (framePrefab != null)
        {
            highlightFrame = Instantiate(framePrefab, selectedObject.transform);
            highlightFrame.transform.localPosition = Vector3.zero;
            highlightFrame.transform.localRotation = Quaternion.identity;
            highlightFrame.transform.localScale = Vector3.one * 1.1f;
        }
    }

    // Called by Delete Button
    public void DeleteSelected()
    {
        if (selectedObject != null)
        {
            Destroy(selectedObject);
            selectedObject = null;

            if (highlightFrame != null)
            {
                Destroy(highlightFrame);
                highlightFrame = null;
            }

            Debug.Log("Deleted selected object");
        }
    }
}
