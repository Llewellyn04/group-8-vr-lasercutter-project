using UnityEngine;
using UnityEngine.UI;
#if USING_WINFORMS
using System.Windows.Forms;
using System.Diagnostics;
#endif
using System.IO;
using System.Text;
using System.Collections.Generic;

public class ExportSubmenuController : MonoBehaviour
{
    public MenuNavigation menuNavigation; // Reference to MenuNavigation script to call BackToMainMenu
    public List<Vector2> sketchPoints = new List<Vector2>(); // To be populated by the drawing system

    void Start()
    {
        SetupButtons();
    }

    void SetupButtons()
    {
        Button exportSVGButton = transform.Find("ExportSVGbtn").GetComponent<Button>();
        if (exportSVGButton != null) exportSVGButton.onClick.AddListener(ExportSVG);
        else UnityEngine.Debug.LogError("ExportSVGbtn not found under ExportSubmenu!");

        Button exportDXFButton = transform.Find("ExportDXFbtn").GetComponent<Button>();
        if (exportDXFButton != null) exportDXFButton.onClick.AddListener(ExportDXF);
        else UnityEngine.Debug.LogError("ExportDXFbtn not found under ExportSubmenu!");

        Button backButton = transform.Find("BackButton").GetComponent<Button>();
        if (backButton != null && menuNavigation != null) backButton.onClick.AddListener(BackToMainMenu);
        else
        {
            if (backButton == null) UnityEngine.Debug.LogError("BackButton not found under ExportSubmenu!");
            if (menuNavigation == null) UnityEngine.Debug.LogError("MenuNavigation reference not assigned!");
        }
    }

    void ExportSVG()
    {
#if USING_WINFORMS
        SaveFileDialog saveFileDialog = new SaveFileDialog
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
            Title = "Export as SVG",
            FileName = "laserCutDesign.svg"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            string filePath = saveFileDialog.FileName;
            UnityEngine.Debug.Log("Saving as SVG to: " + filePath);
            SaveAsSVG(filePath);
        }
#else
        UnityEngine.Debug.LogWarning("File dialog functionality is only available on Windows with USING_WINFORMS defined.");
#endif
    }

    void ExportDXF()
    {
#if USING_WINFORMS
        SaveFileDialog saveFileDialog = new SaveFileDialog
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*",
            Title = "Export as DXF",
            FileName = "laserCutDesign.dxf"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            string filePath = saveFileDialog.FileName;
            UnityEngine.Debug.Log("Saving as DXF to: " + filePath);
            SaveAsDXF(filePath);
        }
#else
        UnityEngine.Debug.LogWarning("File dialog functionality is only available on Windows with USING_WINFORMS defined.");
#endif
    }

    private void SaveAsSVG(string path)
    {
        try
        {
            StringBuilder svgContent = new StringBuilder();
            svgContent.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
            svgContent.AppendLine("<svg width=\"1000\" height=\"1000\" viewBox=\"0 0 1000 1000\" xmlns=\"http://www.w3.org/2000/svg\">");

            // Add a polyline for the laser cutter path
            svgContent.Append("<polyline fill=\"none\" stroke=\"black\" stroke-width=\"1\" points=\"");
            for (int i = 0; i < sketchPoints.Count; i++)
            {
                Vector2 point = sketchPoints[i] * 500; // Scale to fit 1000x1000 viewBox (adjust as needed)
                svgContent.Append($"{point.x},{(1000 - point.y)}"); // Invert Y for SVG coordinate system
                if (i < sketchPoints.Count - 1) svgContent.Append(" ");
            }
            svgContent.AppendLine("\"/>");

            // Close the path if it’s a loop (optional for laser cutting)
            if (sketchPoints.Count > 2 && Vector2.Distance(sketchPoints[0], sketchPoints[sketchPoints.Count - 1]) < 0.1f)
            {
                svgContent.AppendLine("<path d=\"M0,0 Z\" fill=\"none\" stroke=\"black\"/>");
            }

            svgContent.AppendLine("</svg>");
            File.WriteAllText(path, svgContent.ToString());
            UnityEngine.Debug.Log("SVG file saved successfully for laser cutting.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to save SVG file: " + e.Message);
        }
    }

    private void SaveAsDXF(string path)
    {
        try
        {
            StringBuilder dxfContent = new StringBuilder();
            dxfContent.AppendLine("0");
            dxfContent.AppendLine("SECTION");
            dxfContent.AppendLine("2");
            dxfContent.AppendLine("ENTITIES");

            // Add lines for the laser cutter path
            for (int i = 0; i < sketchPoints.Count - 1; i++)
            {
                Vector2 startPoint = sketchPoints[i];
                Vector2 endPoint = sketchPoints[i + 1];
                dxfContent.AppendLine("0");
                dxfContent.AppendLine("LINE");
                dxfContent.AppendLine("8"); // Layer name
                dxfContent.AppendLine("LaserCut");
                dxfContent.AppendLine("10"); // Start X
                dxfContent.AppendLine(startPoint.x.ToString("F3"));
                dxfContent.AppendLine("20"); // Start Y
                dxfContent.AppendLine(startPoint.y.ToString("F3"));
                dxfContent.AppendLine("30"); // Start Z (0 for 2D)
                dxfContent.AppendLine("0.0");
                dxfContent.AppendLine("11"); // End X
                dxfContent.AppendLine(endPoint.x.ToString("F3"));
                dxfContent.AppendLine("21"); // End Y
                dxfContent.AppendLine(endPoint.y.ToString("F3"));
                dxfContent.AppendLine("31"); // End Z (0 for 2D)
                dxfContent.AppendLine("0.0");
            }

            // Optional: Add a closed polyline if the path loops
            if (sketchPoints.Count > 2 && Vector2.Distance(sketchPoints[0], sketchPoints[sketchPoints.Count - 1]) < 0.1f)
            {
                dxfContent.AppendLine("0");
                dxfContent.AppendLine("LWPOLYLINE");
                dxfContent.AppendLine("8"); // Layer name
                dxfContent.AppendLine("LaserCut");
                dxfContent.AppendLine("90"); // Number of vertices
                dxfContent.AppendLine(sketchPoints.Count.ToString());
                dxfContent.AppendLine("70"); // Closed flag
                dxfContent.AppendLine("1");
                for (int i = 0; i < sketchPoints.Count; i++)
                {
                    Vector2 point = sketchPoints[i];
                    dxfContent.AppendLine("10"); // X coordinate
                    dxfContent.AppendLine(point.x.ToString("F3"));
                    dxfContent.AppendLine("20"); // Y coordinate
                    dxfContent.AppendLine(point.y.ToString("F3"));
                }
            }

            dxfContent.AppendLine("0");
            dxfContent.AppendLine("ENDSEC");
            dxfContent.AppendLine("0");
            dxfContent.AppendLine("EOF");
            File.WriteAllText(path, dxfContent.ToString());
            UnityEngine.Debug.Log("DXF file saved successfully for laser cutting.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to save DXF file: " + e.Message);
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