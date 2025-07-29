using System.Collections;
using System.Collections.Generic;
using System.Collections; // Needed for IList
using netDxf;
using netDxf.Entities;
using UnityEngine;
using static netDxf.Entities.HatchBoundaryPath;
using SFB; // Standalone File Browser namespace

public class DXFImporter : MonoBehaviour
{
    // Add this line to fix the error
    public List<UnityEngine.Vector3[]> importedLines = new List<UnityEngine.Vector3[]>();

    // Step 1: File picker method
    // Step 1: File picker method
    public void OpenFilePicker()
    {
        var extensions = new[] {
        new ExtensionFilter("DXF Files", "dxf")
    };

        string[] paths = StandaloneFileBrowser.OpenFilePanel("Open DXF File", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            LoadDXF(paths[0]);
        }
    }

    // Step 2: Load DXF file and parse geometry
    public void LoadDXF(string filePath)
    {
        importedLines.Clear();
        Debug.Log("Cleared importedLines");

        try
        {
            DxfDocument dxf = DxfDocument.Load(filePath);
            if (dxf == null)
            {
                Debug.LogError("Failed to load DXF file: " + filePath);
                return;
            }

            var entities = dxf.Entities;

            int lineCount = 0, circleCount = 0, polylineCount = 0;
            List<netDxf.Entities.Line> lines = new List<netDxf.Entities.Line>();
            List<Circle> circles = new List<Circle>();
            List<object> polylines = new List<object>();

            foreach (var entity in entities.All)
            {
                switch (entity)
                {
                    case netDxf.Entities.Line line:
                        lines.Add(line);
                        lineCount++;
                        break;
                    case Circle circle:
                        circles.Add(circle);
                        circleCount++;
                        break;
                    default:
                        var typeName = entity.GetType().Name;
                        if (typeName == "LwPolyline" || typeName == "LightWeightPolyline" || entity is Polyline)
                        {
                            polylines.Add(entity);
                            polylineCount++;
                        }
                        break;
                }
            }

            Debug.Log($"DXF Loaded. Lines: {lineCount}, Circles: {circleCount}, Polylines: {polylineCount}");

            foreach (var line in lines)
            {
                var start = new UnityEngine.Vector3((float)line.StartPoint.X, (float)line.StartPoint.Y, (float)line.StartPoint.Z);
                var end = new UnityEngine.Vector3((float)line.EndPoint.X, (float)line.EndPoint.Y, (float)line.EndPoint.Z);
                importedLines.Add(new UnityEngine.Vector3[] { start, end });
                Debug.Log($"Line from {start} to {end}");
            }

            foreach (var circle in circles)
            {
                var center = new UnityEngine.Vector3((float)circle.Center.X, (float)circle.Center.Y, (float)circle.Center.Z);
                float radius = (float)circle.Radius;
                Debug.Log($"Circle at {center} with radius {radius}");
                // Optionally visualize as arc or mesh later
            }

            foreach (var polylineObj in polylines)
            {
                UnityEngine.Vector3[] points = null;

                if (polylineObj.GetType().Name == "LwPolyline" || polylineObj.GetType().Name == "LightWeightPolyline")
                {
                    var vertexes = polylineObj.GetType().GetProperty("Vertexes")?.GetValue(polylineObj);
                    if (vertexes is netDxf.Vector3[] vertexArray)
                    {
                        points = new UnityEngine.Vector3[vertexArray.Length];
                        for (int i = 0; i < vertexArray.Length; i++)
                        {
                            points[i] = new UnityEngine.Vector3(
                                (float)vertexArray[i].X,
                                (float)vertexArray[i].Y,
                                0
                            );
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Unsupported LwPolyline Vertexes type: {vertexes?.GetType().FullName}");
                    }
                }
                else if (polylineObj is Polyline polyline)
                {
                    if (polyline.Vertexes is netDxf.Vector3[] vertexArray)
                    {
                        points = new UnityEngine.Vector3[vertexArray.Length];
                        for (int i = 0; i < vertexArray.Length; i++)
                        {
                            points[i] = new UnityEngine.Vector3(
                                (float)vertexArray[i].X,
                                (float)vertexArray[i].Y,
                                0
                            );
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Unsupported Polyline Vertexes type: {polyline.Vertexes?.GetType().FullName}");
                    }
                }

                if (points != null && points.Length > 0)
                {
                    importedLines.Add(points);
                    Debug.Log($"Polyline with {points.Length} points");
                }
                else
                {
                    Debug.LogWarning("Failed to process polyline vertices");
                }
            }

            Debug.Log($"importedLines contains {importedLines.Count} paths");

            // Render using DXFPathRenderer
            DXFPathRenderer renderer = FindObjectOfType<DXFPathRenderer>();
            if (renderer != null)
            {
                renderer.RenderPaths(importedLines);
                Debug.Log("Called RenderPaths with importedLines");
            }
            else
            {
                Debug.LogError("DXFPathRenderer not found in the scene.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error loading DXF file: {ex.Message}");
        }
    }

    // Public method to access importedLines
    public List<UnityEngine.Vector3[]> GetImportedLines()
    {
        return importedLines;
    }
}