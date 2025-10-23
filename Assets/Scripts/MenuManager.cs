// 7/28/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.EventSystems;

public class MenuManager : MonoBehaviour
{
    [Header("Submenu GameObjects")]
    public GameObject openImportSubmenu;
    public GameObject exportSubmenu;

    [Header("XR Canvas Setup")]
    public Canvas mainCanvas;

    private void Awake()
    {
        // Optional: Ensure canvas is enabled at start
        if (mainCanvas != null)
        {
            mainCanvas.worldCamera = Camera.main;
        }

        HideAllSubmenus();
    }

    public void ShowSubmenu(GameObject submenu)
    {
        HideAllSubmenus();

        if (submenu != null)
            submenu.SetActive(true);
    }

    private void HideAllSubmenus()
    {
        if (openImportSubmenu != null) openImportSubmenu.SetActive(false);
        if (exportSubmenu != null) exportSubmenu.SetActive(false);
    }

    public void OnImportButtonClick()
    {
        ShowSubmenu(openImportSubmenu);
    }

    public void OnExportButtonClick()
    {
        ShowSubmenu(exportSubmenu);
    }
}
