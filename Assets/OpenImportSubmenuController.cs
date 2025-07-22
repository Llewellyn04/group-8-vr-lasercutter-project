using UnityEngine;
using UnityEngine.UI;
#if USING_WINFORMS
using System.Windows.Forms;
using System.Diagnostics;
#endif
using System.IO;
using System;

public class OpenImportSubmenuController : MonoBehaviour
{
    public MenuNavigation menuNavigation; // Reference to MenuNavigation script to call BackToMainMenu

    void Start()
    {
        SetupButtons();
    }

    void SetupButtons()
    {
        Button newFileButton = transform.Find("NewFilebtn").GetComponent<Button>();
        if (newFileButton != null) newFileButton.onClick.AddListener(OpenNewFileExplorer);
        else UnityEngine.Debug.LogError("NewFilebtn not found under OpenImportSubmenu!");

        Button openFileButton = transform.Find("OpenFilebtn").GetComponent<Button>();
        if (openFileButton != null) openFileButton.onClick.AddListener(OpenFileExplorer);
        else UnityEngine.Debug.LogError("OpenFilebtn not found under OpenImportSubmenu!");

        Button importDXFButton = transform.Find("ImportDXFbtn").GetComponent<Button>();
        if (importDXFButton != null) importDXFButton.onClick.AddListener(ImportDXFExplorer);
        else UnityEngine.Debug.LogError("ImportDXFbtn not found under OpenImportSubmenu!");

        Button importSVGButton = transform.Find("ImportSVGbtn").GetComponent<Button>();
        if (importSVGButton != null) importSVGButton.onClick.AddListener(ImportSVGExplorer);
        else UnityEngine.Debug.LogError("ImportSVGbtn not found under OpenImportSubmenu!");

        Button backButton = transform.Find("BackButton").GetComponent<Button>();
        if (backButton != null && menuNavigation != null) backButton.onClick.AddListener(BackToMainMenu);
        else
        {
            if (backButton == null) UnityEngine.Debug.LogError("BackButton not found under OpenImportSubmenu!");
            if (menuNavigation == null) UnityEngine.Debug.LogError("MenuNavigation reference not assigned!");
        }
    }

    void OpenNewFileExplorer()
    {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#if USING_WINFORMS
        try
        {
            Process.Start("explorer.exe", documentsPath);
            UnityEngine.Debug.Log("Opened file explorer at: " + documentsPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to open file explorer: " + e.Message);
        }
#else
        UnityEngine.Debug.LogWarning("File explorer functionality is only available on Windows with USING_WINFORMS defined.");
#endif
    }

    void OpenFileExplorer()
    {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#if USING_WINFORMS
        try
        {
            Process.Start("explorer.exe", documentsPath);
            UnityEngine.Debug.Log("Opened file explorer at: " + documentsPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to open file explorer: " + e.Message);
        }
#else
        UnityEngine.Debug.LogWarning("File explorer functionality is only available on Windows with USING_WINFORMS defined.");
#endif
    }

    void ImportDXFExplorer()
    {
#if USING_WINFORMS
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*",
            Title = "Select a DXF File",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            string selectedFile = openFileDialog.FileName;
            UnityEngine.Debug.Log("Selected DXF file: " + selectedFile);
            LoadFile(selectedFile); // Placeholder for DXF import logic
        }
#else
        UnityEngine.Debug.LogWarning("File dialog functionality is only available on Windows with USING_WINFORMS defined.");
#endif
    }

    void ImportSVGExplorer()
    {
#if USING_WINFORMS
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
            Title = "Select an SVG File",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            string selectedFile = openFileDialog.FileName;
            UnityEngine.Debug.Log("Selected SVG file: " + selectedFile);
            LoadFile(selectedFile); // Placeholder for SVG import logic
        }
#else
        UnityEngine.Debug.LogWarning("File dialog functionality is only available on Windows with USING_WINFORMS defined.");
#endif
    }

    void LoadFile(string filePath)
    {
        try
        {
            string fileContent = File.ReadAllText(filePath);
            UnityEngine.Debug.Log("File content loaded: " + fileContent.Substring(0, Mathf.Min(100, fileContent.Length)) + "...");
            // Add your specific DXF/SVG parsing logic here
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to load file: " + e.Message);
        }
    }

    void BackToMainMenu()
    {
        if (menuNavigation != null)
        {
            menuNavigation.BackToMainMenu();
        }
        else
        {
            UnityEngine.Debug.LogError("MenuNavigation reference not assigned, cannot return to main menu!");
        }
    }
}