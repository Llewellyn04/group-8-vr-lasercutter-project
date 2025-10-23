using UnityEngine;
using UnityEngine.UI; // For Image

public class FocusModeToggle : MonoBehaviour
{
    [Header("Objects to Hide in Focus Mode")]
    public GameObject[] objectsToHide;  // Add Menu_Left, Menu_Right, BoxPanel, etc.

    [Header("WhiteBoard Settings")]
    public Image whiteBoardImage;       // Assign WhiteBoard's Image component
    public Color focusColor = new Color(1f, 1f, 1f, 0f); // Transparent by default
    private Color originalColor;

    private bool focusMode = false;

    void Start()
    {
        if (whiteBoardImage != null)
            originalColor = whiteBoardImage.color;
    }

    public void ToggleFocusMode()
    {
        focusMode = !focusMode;

        if (focusMode)
        {
            // Hide all assigned objects
            foreach (GameObject obj in objectsToHide)
            {
                if (obj) obj.SetActive(false);
            }

            // Change WhiteBoard color
            if (whiteBoardImage != null)
                whiteBoardImage.color = focusColor;
        }
        else
        {
            // Show all assigned objects again
            foreach (GameObject obj in objectsToHide)
            {
                if (obj) obj.SetActive(true);
            }

            // Restore WhiteBoard color
            if (whiteBoardImage != null)
                whiteBoardImage.color = originalColor;
        }
    }
}
