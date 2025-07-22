using UnityEngine;
using UnityEngine.UI;

public class MenuNavigation : MonoBehaviour
{
    public GameObject panelMain;        // Reference to Panel_Main
    public GameObject openImportPanel;  // Reference to OpenImportSubmenu
    public GameObject exportPanel;      // Reference to ExportSubmenu
    public GameObject confirmModal;     // Reference to ConfirmModal

    void Start()
    {
        // Ensure all submenus and modal are hidden initially
        openImportPanel.SetActive(false);
        exportPanel.SetActive(false);
        confirmModal.SetActive(false);

        // Add click listener to the Open/Import button
        Button openImportButton = panelMain.transform.Find("OpenImportbtn").GetComponent<Button>();
        if (openImportButton != null)
        {
            openImportButton.onClick.AddListener(ToggleOpenImportPanel);
        }

        // Add click listener to the Export button
        Button exportButton = panelMain.transform.Find("Exportbtn").GetComponent<Button>();
        if (exportButton != null)
        {
            exportButton.onClick.AddListener(ToggleExportPanel);
        }
    }

    void ToggleOpenImportPanel()
    {
        // Hide main panel and show Open/Import submenu
        panelMain.SetActive(false);
        openImportPanel.SetActive(true);
        exportPanel.SetActive(false);
        confirmModal.SetActive(false);

        // Position submenu in the middle
        RectTransform submenuRect = openImportPanel.GetComponent<RectTransform>();
        submenuRect.anchoredPosition = Vector2.zero;
    }

    void ToggleExportPanel()
    {
        // Hide main panel and show Export submenu
        panelMain.SetActive(false);
        exportPanel.SetActive(true);
        openImportPanel.SetActive(false);
        confirmModal.SetActive(false);

        // Position submenu in the middle
        RectTransform submenuRect = exportPanel.GetComponent<RectTransform>();
        submenuRect.anchoredPosition = Vector2.zero;
    }

    // Show Confirm modal (e.g., triggered by a button in Open/Import or Export submenu)
    public void ShowConfirmModal()
    {
        panelMain.SetActive(false);
        openImportPanel.SetActive(false);
        exportPanel.SetActive(false);
        confirmModal.SetActive(true);

        // Position modal in the middle
        RectTransform modalRect = confirmModal.GetComponent<RectTransform>();
        modalRect.anchoredPosition = Vector2.zero;
    }

    // Return to main menu from any submenu or modal
    public void BackToMainMenu()
    {
        openImportPanel.SetActive(false);
        exportPanel.SetActive(false);
        confirmModal.SetActive(false);
        panelMain.SetActive(true);
    }
}