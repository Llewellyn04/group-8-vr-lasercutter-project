using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class ExportSVG : MonoBehaviour
{
    [Header("References")]
    public Transform whiteboardPlane;

    [Header("SVG Settings")]
    public int canvasWidth = 1000;
    public int canvasHeight = 1000;
    public string fileName = "whiteboard_drawing.svg";

    [Header("Auto-Calculate Bounds")]
    public bool autoCalculateBounds = true;

    [Header("Manual Bounds (if autoCalculateBounds = false)")]
    public Vector2 boundsMin = new Vector2(-0.5f, -0.5f);
    public Vector2 boundsMax = new Vector2(0.5f, 0.5f);

    [Header("Debug")]
    public bool showDebugLogs = true;

    public void ExportToSVG()
    {
        if (whiteboardPlane == null)
        {
            Debug.LogError("Whiteboard plane not assigned!");
            return;
        }

        LineRenderer[] allLines = FindObjectsOfType<LineRenderer>();
        Debug.Log($"Total LineRenderers found: {allLines.Length}");

        if (allLines.Length == 0)
        {
            Debug.LogWarning("No drawings found to export!");
            return;
        }

        List<LineRenderer> drawings = new List<LineRenderer>();
        foreach (LineRenderer line in allLines)
        {
            if (line.name.Contains("VR_") || line.name.Contains("Drawing"))
            {
                drawings.Add(line);
                Debug.Log($"Added: {line.name} ({line.positionCount} points)");
            }
        }

        if (drawings.Count == 0)
        {
            Debug.LogWarning("No VR drawings found!");
            return;
        }

        Debug.Log($"Exporting {drawings.Count} drawings");

        string svgContent = GenerateSVGContent(drawings);
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            File.WriteAllText(filePath, svgContent, Encoding.UTF8);
            Debug.Log($"SVG saved to: {filePath}");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(filePath);
#endif
        }
        catch (IOException e)
        {
            Debug.LogError($"Failed to save: {e.Message}");
        }
    }

    private string GenerateSVGContent(List<LineRenderer> drawings)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        StringBuilder svg = new StringBuilder();

        svg.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>");
        svg.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" version=""1.1"" width=""{canvasWidth}"" height=""{canvasHeight}"">");
        svg.AppendLine($@"  <rect width=""{canvasWidth}"" height=""{canvasHeight}"" fill=""#f0f0f0""/>");

        // Calculate bounds from actual drawing positions
        Vector2 actualMin = boundsMin;
        Vector2 actualMax = boundsMax;

        if (autoCalculateBounds)
        {
            CalculateDrawingBounds(drawings, out actualMin, out actualMax);
            Debug.Log($"Auto-calculated bounds: Min({actualMin.x:F3}, {actualMin.y:F3}), Max({actualMax.x:F3}, {actualMax.y:F3})");
        }

        float boundsWidth = actualMax.x - actualMin.x;
        float boundsHeight = actualMax.y - actualMin.y;

        foreach (LineRenderer line in drawings)
        {
            if (line.positionCount < 2) continue;

            Color lineColor = line.startColor;
            // Convert white lines to black for visibility
            if (lineColor.r > 0.9f && lineColor.g > 0.9f && lineColor.b > 0.9f)
            {
                lineColor = Color.black;
            }
            string colorHex = ColorToHex(lineColor);
            float lineWidth = line.startWidth * 1000f;

            StringBuilder pathData = new StringBuilder();

            for (int i = 0; i < line.positionCount; i++)
            {
                Vector3 worldPos = line.GetPosition(i);
                Vector3 localPos = whiteboardPlane.InverseTransformPoint(worldPos);

                // Normalize to 0-1 based on actual bounds
                float normalizedX = (localPos.x - actualMin.x) / boundsWidth;
                float normalizedY = (localPos.y - actualMin.y) / boundsHeight;

                // Convert to SVG coordinates
                float svgX = normalizedX * canvasWidth;
                float svgY = (1f - normalizedY) * canvasHeight; // Flip Y

                if (showDebugLogs && i == 0)
                {
                    Debug.Log($"  {line.name} Point 0: World{worldPos} -> Local{localPos} -> SVG({svgX:F2}, {svgY:F2})");
                }

                string command = i == 0 ? "M" : "L";
                pathData.Append($"{command} {svgX.ToString("F2", culture)} {svgY.ToString("F2", culture)} ");
            }

            svg.AppendLine($@"  <path d=""{pathData}""");
            svg.AppendLine($@"        fill=""none""");
            svg.AppendLine($@"        stroke=""{colorHex}""");
            svg.AppendLine($@"        stroke-width=""{lineWidth.ToString("F2", culture)}""");
            svg.AppendLine($@"        stroke-linecap=""round""");
            svg.AppendLine($@"        stroke-linejoin=""round"" />");
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private void CalculateDrawingBounds(List<LineRenderer> drawings, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);

        foreach (LineRenderer line in drawings)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                Vector3 worldPos = line.GetPosition(i);
                Vector3 localPos = whiteboardPlane.InverseTransformPoint(worldPos);

                min.x = Mathf.Min(min.x, localPos.x);
                min.y = Mathf.Min(min.y, localPos.y);
                max.x = Mathf.Max(max.x, localPos.x);
                max.y = Mathf.Max(max.y, localPos.y);
            }
        }

        // Add 10% padding
        float paddingX = (max.x - min.x) * 0.1f;
        float paddingY = (max.y - min.y) * 0.1f;
        min.x -= paddingX;
        min.y -= paddingY;
        max.x += paddingX;
        max.y += paddingY;
    }

    private string ColorToHex(Color color)
    {
        int r = Mathf.RoundToInt(color.r * 255f);
        int g = Mathf.RoundToInt(color.g * 255f);
        int b = Mathf.RoundToInt(color.b * 255f);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            ExportToSVG();
        }
    }
}