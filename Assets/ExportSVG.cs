using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class ExportSVG : MonoBehaviour
{
    [Header("Drawing Input")]
    public List<Vector2> drawingPoints = new List<Vector2>();

    [Header("SVG Settings")]
    public int canvasWidth = 1000;
    public int canvasHeight = 1000;
    public float strokeWidth = 2f;
    public string strokeColor = "black";
    public string fileName = "drawing.svg";

    private void Start()
    {
        // 🔧 Dummy test points for now
        drawingPoints = new List<Vector2>
        {
            new Vector2(100, 100),
            new Vector2(300, 400),
            new Vector2(500, 200),
            new Vector2(700, 450)
        };

        // Export immediately for testing (optional)
        ExportToSVG();
    }

    public void ExportToSVG()
    {
        if (drawingPoints == null || drawingPoints.Count < 2)
        {
            Debug.LogWarning("Not enough points to export.");
            return;
        }

        string svgContent = GenerateSVGContent(drawingPoints);
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            File.WriteAllText(filePath, svgContent, Encoding.UTF8);
            Debug.Log($"✅ SVG file saved to:\n{filePath}");
        }
        catch (IOException e)
        {
            Debug.LogError($"❌ Failed to save SVG: {e.Message}");
        }
    }

    private string GenerateSVGContent(List<Vector2> points)
    {
        StringBuilder svg = new StringBuilder();

        // XML and SVG headers
        svg.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>");
        svg.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" version=""1.1"" width=""{canvasWidth}"" height=""{canvasHeight}"">");

        // Start path
        svg.Append(@"  <path d=""");

        for (int i = 0; i < points.Count; i++)
        {
            float flippedY = canvasHeight - points[i].y; // Flip Y to match SVG coordinate system
            string command = i == 0 ? "M" : "L";
            svg.Append($"{command} {points[i].x:F2} {flippedY:F2} ");
        }

        // Close path element
        svg.AppendLine($@""" fill=""none"" stroke=""{strokeColor}"" stroke-width=""{strokeWidth}"" />");

        // Close SVG
        svg.AppendLine("</svg>");
        return svg.ToString();
    }
}
