using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class ExportDXF : MonoBehaviour
{
    [Header("References")]
    public Transform whiteboardPlane;

    [Header("DXF Settings")]
    public string fileName = "whiteboard_drawing.dxf";

    [Header("Auto-Calculate Bounds")]
    public bool autoCalculateBounds = true;

    [Header("Manual Bounds (if autoCalculateBounds = false)")]
    public Vector2 boundsMin = new Vector2(-0.5f, -0.5f);
    public Vector2 boundsMax = new Vector2(0.5f, 0.5f);

    [Header("DXF Scale")]
    public float dxfScale = 1000f; // Scale factor for DXF units (1000 = millimeters)

    [Header("Debug")]
    public bool showDebugLogs = true;

    public void ExportToDXF()
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

        Debug.Log($"Exporting {drawings.Count} drawings to DXF");

        string dxfContent = GenerateDXFContent(drawings);
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            File.WriteAllText(filePath, dxfContent, Encoding.UTF8);
            Debug.Log($"DXF saved to: {filePath}");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(filePath);
#endif
        }
        catch (IOException e)
        {
            Debug.LogError($"Failed to save DXF: {e.Message}");
        }
    }

    private string GenerateDXFContent(List<LineRenderer> drawings)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        StringBuilder dxf = new StringBuilder();

        // Calculate bounds
        Vector2 actualMin = boundsMin;
        Vector2 actualMax = boundsMax;

        if (autoCalculateBounds)
        {
            CalculateDrawingBounds(drawings, out actualMin, out actualMax);
            Debug.Log($"Auto-calculated bounds: Min({actualMin.x:F3}, {actualMin.y:F3}), Max({actualMax.x:F3}, {actualMax.y:F3})");
        }

        float boundsWidth = actualMax.x - actualMin.x;
        float boundsHeight = actualMax.y - actualMin.y;

        // DXF Header Section
        dxf.AppendLine("0");
        dxf.AppendLine("SECTION");
        dxf.AppendLine("2");
        dxf.AppendLine("HEADER");
        dxf.AppendLine("9");
        dxf.AppendLine("$ACADVER");
        dxf.AppendLine("1");
        dxf.AppendLine("AC1015");
        dxf.AppendLine("0");
        dxf.AppendLine("ENDSEC");

        // Tables Section
        dxf.AppendLine("0");
        dxf.AppendLine("SECTION");
        dxf.AppendLine("2");
        dxf.AppendLine("TABLES");
        dxf.AppendLine("0");
        dxf.AppendLine("TABLE");
        dxf.AppendLine("2");
        dxf.AppendLine("LTYPE");
        dxf.AppendLine("70");
        dxf.AppendLine("1");
        dxf.AppendLine("0");
        dxf.AppendLine("LTYPE");
        dxf.AppendLine("2");
        dxf.AppendLine("CONTINUOUS");
        dxf.AppendLine("70");
        dxf.AppendLine("0");
        dxf.AppendLine("3");
        dxf.AppendLine("Solid line");
        dxf.AppendLine("72");
        dxf.AppendLine("65");
        dxf.AppendLine("73");
        dxf.AppendLine("0");
        dxf.AppendLine("40");
        dxf.AppendLine("0.0");
        dxf.AppendLine("0");
        dxf.AppendLine("ENDTAB");
        dxf.AppendLine("0");
        dxf.AppendLine("TABLE");
        dxf.AppendLine("2");
        dxf.AppendLine("LAYER");
        dxf.AppendLine("70");
        dxf.AppendLine("1");
        dxf.AppendLine("0");
        dxf.AppendLine("LAYER");
        dxf.AppendLine("2");
        dxf.AppendLine("0");
        dxf.AppendLine("70");
        dxf.AppendLine("0");
        dxf.AppendLine("62");
        dxf.AppendLine("7");
        dxf.AppendLine("6");
        dxf.AppendLine("CONTINUOUS");
        dxf.AppendLine("0");
        dxf.AppendLine("ENDTAB");
        dxf.AppendLine("0");
        dxf.AppendLine("ENDTAB");
        dxf.AppendLine("0");
        dxf.AppendLine("ENDSEC");

        // Entities Section
        dxf.AppendLine("0");
        dxf.AppendLine("SECTION");
        dxf.AppendLine("2");
        dxf.AppendLine("ENTITIES");

        // Process each drawing
        foreach (LineRenderer line in drawings)
        {
            if (line.positionCount < 2) continue;

            // Create POLYLINE entity
            dxf.AppendLine("0");
            dxf.AppendLine("LWPOLYLINE");
            dxf.AppendLine("8");
            dxf.AppendLine("0"); // Layer name
            dxf.AppendLine("62"); // Color code
            dxf.AppendLine(GetDXFColorCode(line.startColor).ToString());
            dxf.AppendLine("90"); // Number of vertices
            dxf.AppendLine(line.positionCount.ToString());
            dxf.AppendLine("70"); // Polyline flag (1 = closed, 0 = open)
            dxf.AppendLine(line.loop ? "1" : "0");

            // Add vertices
            for (int i = 0; i < line.positionCount; i++)
            {
                Vector3 worldPos = line.GetPosition(i);
                Vector3 localPos = whiteboardPlane.InverseTransformPoint(worldPos);

                // Normalize to 0-1 based on actual bounds
                float normalizedX = (localPos.x - actualMin.x) / boundsWidth;
                float normalizedY = (localPos.y - actualMin.y) / boundsHeight;

                // Scale to DXF units
                float dxfX = normalizedX * dxfScale;
                float dxfY = normalizedY * dxfScale;

                if (showDebugLogs && i == 0)
                {
                    Debug.Log($"  {line.name} Point 0: World{worldPos} -> Local{localPos} -> DXF({dxfX:F2}, {dxfY:F2})");
                }

                dxf.AppendLine("10");
                dxf.AppendLine(dxfX.ToString("F6", culture));
                dxf.AppendLine("20");
                dxf.AppendLine(dxfY.ToString("F6", culture));
            }
        }

        // End Entities Section
        dxf.AppendLine("0");
        dxf.AppendLine("ENDSEC");

        // EOF
        dxf.AppendLine("0");
        dxf.AppendLine("EOF");

        return dxf.ToString();
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

    private int GetDXFColorCode(Color color)
    {
        // Convert Unity color to closest AutoCAD color index
        // 7 = white/black (default)
        // 1 = red, 2 = yellow, 3 = green, 4 = cyan, 5 = blue, 6 = magenta

        if (color.r > 0.9f && color.g > 0.9f && color.b > 0.9f)
            return 7; // White

        if (color.r > 0.7f && color.g < 0.3f && color.b < 0.3f)
            return 1; // Red

        if (color.r > 0.7f && color.g > 0.7f && color.b < 0.3f)
            return 2; // Yellow

        if (color.r < 0.3f && color.g > 0.7f && color.b < 0.3f)
            return 3; // Green

        if (color.r < 0.3f && color.g > 0.7f && color.b > 0.7f)
            return 4; // Cyan

        if (color.r < 0.3f && color.g < 0.3f && color.b > 0.7f)
            return 5; // Blue

        if (color.r > 0.7f && color.g < 0.3f && color.b > 0.7f)
            return 6; // Magenta

        return 7; // Default
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            ExportToDXF();
        }
    }
}