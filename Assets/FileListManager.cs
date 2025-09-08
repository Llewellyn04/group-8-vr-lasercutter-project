using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FileListManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The panel that contains the scroll view")]
    public GameObject fileSelectionPanel;

    [Tooltip("The button that opens/closes the panel")]
    public Button togglePanelButton;

    [Tooltip("The Content object inside the ScrollView where buttons will be added")]
    public Transform scrollViewContent;

    [Tooltip("The prefab button that will be instantiated for each file")]
    public GameObject fileButtonPrefab;

    [Header("Settings")]
    [Tooltip("The folder path relative to Assets folder")]
    public string importFolderPath = "ImportsDxfSVG134567";

    // Private variables
    private List<GameObject> instantiatedButtons = new List<GameObject>();
    private bool isPanelOpen = false;

    void Start()
    {
        // Make sure panel starts closed
        fileSelectionPanel.SetActive(false);

        // Connect the toggle button to our method
        if (togglePanelButton != null)
        {
            togglePanelButton.onClick.AddListener(TogglePanel);
        }
        else
        {
            Debug.LogError("Toggle Panel Button is not assigned!");
        }
    }

    public void TogglePanel()
    {
        Debug.Log("=== TogglePanel() called ===");

        isPanelOpen = !isPanelOpen;
        Debug.Log($"Panel open state: {isPanelOpen}");

        if (fileSelectionPanel != null)
        {
            fileSelectionPanel.SetActive(isPanelOpen);
            Debug.Log("Panel visibility toggled");
        }
        else
        {
            Debug.LogError("Cannot toggle panel - File Selection Panel is null!");
        }

        if (isPanelOpen)
        {
            Debug.Log("Panel is now open, calling PopulateFileList...");
            PopulateFileList();
        }
    }

    private void PopulateFileList()
    {
        // Clear any existing buttons first
        ClearExistingButtons();

        // Get the full path to our import folder
        string fullPath = Path.Combine(Application.dataPath, importFolderPath);

        // Check if the folder exists
        if (!Directory.Exists(fullPath))
        {
            Debug.LogError($"Import folder not found at: {fullPath}");
            return;
        }

        // Get all DXF and SVG files
        List<string> allFiles = new List<string>();

        // Add DXF files
        string[] dxfFiles = Directory.GetFiles(fullPath, "*.dxf");
        allFiles.AddRange(dxfFiles);

        // Add SVG files
        string[] svgFiles = Directory.GetFiles(fullPath, "*.svg");
        allFiles.AddRange(svgFiles);

        Debug.Log($"Found {allFiles.Count} files in {fullPath}");

        // Create a button for each file
        foreach (string filePath in allFiles)
        {
            CreateFileButton(filePath);
        }
    }

    private void CreateFileButton(string filePath)
    {
        Debug.Log($"=== Creating button for file: {filePath} ===");

        // Get just the filename without the path
        string fileName = Path.GetFileName(filePath);
        Debug.Log($"File name: {fileName}");

        // Check if we have the required references
        if (fileButtonPrefab == null)
        {
            Debug.LogError("File Button Prefab is not assigned!");
            return;
        }

        if (scrollViewContent == null)
        {
            Debug.LogError("Scroll View Content is not assigned!");
            return;
        }

        // Instantiate the button prefab
        GameObject newButton = Instantiate(fileButtonPrefab, scrollViewContent);
        Debug.Log($"Button instantiated: {newButton.name}");

        // Get the TextMeshPro component (it's in the child object)
        TextMeshProUGUI buttonText = newButton.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonText != null)
        {
            buttonText.text = fileName;
            Debug.Log($"Button text set to: {fileName}");
        }
        else
        {
            // Try regular Text component as fallback
            Text regularText = newButton.GetComponentInChildren<Text>();
            if (regularText != null)
            {
                regularText.text = fileName;
                Debug.Log($"Button text (regular Text) set to: {fileName}");
            }
            else
            {
                Debug.LogError("Could not find TextMeshProUGUI or Text component in button prefab!");
            }
        }

        // Get the Button component and add a click listener
        Button buttonComponent = newButton.GetComponent<Button>();
        if (buttonComponent != null)
        {
            // This captures the fileName in a closure for the click event
            string capturedFileName = fileName;
            buttonComponent.onClick.AddListener(() => OnFileButtonClicked(capturedFileName, filePath));
            Debug.Log("Click listener added to button");
        }
        else
        {
            Debug.LogError("No Button component found on instantiated prefab!");
        }

        // Keep track of instantiated buttons so we can clean them up later
        instantiatedButtons.Add(newButton);

        Debug.Log($"Button creation complete. Total buttons: {instantiatedButtons.Count}");
    }

    private void OnFileButtonClicked(string fileName, string fullFilePath)
    {
        Debug.Log($"User selected file: {fileName}");
        Debug.Log($"Full path: {fullFilePath}");

        // TODO: Add your file processing logic here
        // For example, you might want to:
        // - Load the selected file
        // - Close the panel
        // - Display the file contents

        // Close the panel after selection (optional)
        TogglePanel();
    }

    private void ClearExistingButtons()
    {
        // Destroy all previously created buttons
        foreach (GameObject button in instantiatedButtons)
        {
            if (button != null)
            {
                DestroyImmediate(button);
            }
        }

        // Clear the list
        instantiatedButtons.Clear();
    }

    // Optional: Method to refresh the file list without closing/opening panel
    public void RefreshFileList()
    {
        if (isPanelOpen)
        {
            PopulateFileList();
        }
    }

    // Test method - add this temporarily to test if file reading works
    [ContextMenu("Test File Reading")]
    public void TestFileReading()
    {
        string fullPath = Path.Combine(Application.dataPath, importFolderPath);
        Debug.Log($"Testing path: {fullPath}");

        if (Directory.Exists(fullPath))
        {
            string[] allFiles = Directory.GetFiles(fullPath);
            Debug.Log($"All files in folder: {allFiles.Length}");
            foreach (string file in allFiles)
            {
                Debug.Log($"File: {Path.GetFileName(file)}");
            }
        }
        else
        {
            Debug.LogError("Directory doesn't exist!");
        }
    }
}