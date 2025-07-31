using System.Collections;
using System.Collections.Generic;
using netDxf;
using netDxf.Entities;
using UnityEngine;
using static netDxf.Entities.HatchBoundaryPath;

public class DXFImporter : MonoBehaviour
{
    // List to store parsed geometry points
    public List<UnityEngine.Vector3[]> importedLines = new List<UnityEngine.Vector3[]>();

    // Method to load a DXF file by path
    public void LoadDXF(string filePath)
    {
        // Clear previous data
        importedLines.Clear();

        // Load the DXF file using netDxf
        try
        {
            DxfDocument dxf = DxfDocument.Load(filePath);
            if (dxf == null)
            {
                Debug.LogError("Failed to load DXF file: " + filePath);
                return;
            }

            // Get all entities
            var entities = dxf.Entities;

            // Count entities manually
            int lineCount = 0, circleCount = 0, polylineCount = 0;
            List<netDxf.Entities.Line> lines = new List<netDxf.Entities.Line>();
            List<Circle> circles = new List<Circle>();
            List<object> polylines = new List<object>(); // Use object to handle Polyline or LightWeightPolyline

            // Iterate through all entities and categorize them
            foreach (var entity in entities.All)
            {
                if (entity is netDxf.Entities.Line line)
                {
                    lines.Add(line);
                    lineCount++;
                }
                else if (entity is Circle circle)
                {
                    circles.Add(circle);
                    circleCount++;
                }
                else if (entity.GetType().Name == "LwPolyline" || entity.GetType().Name == "LightWeightPolyline" || entity is Polyline)
                {
                    polylines.Add(entity);
                    polylineCount++;
                }
            }

            Debug.Log($"DXF Loaded. Lines: {lineCount}, Circles: {circleCount}, Polylines: {polylineCount}");

            // Process Lines
            foreach (var line in lines)
            {
                var start = new UnityEngine.Vector3((float)line.StartPoint.X, (float)line.StartPoint.Y, (float)line.StartPoint.Z);
                var end = new UnityEngine.Vector3((float)line.EndPoint.X, (float)line.EndPoint.Y, (float)line.EndPoint.Z);
                importedLines.Add(new UnityEngine.Vector3[] { start, end });
                Debug.Log($"Line from {start} to {end}");
            }

            // Process Circles
            foreach (var circle in circles)
            {
                var center = new UnityEngine.Vector3((float)circle.Center.X, (float)circle.Center.Y, (float)circle.Center.Z);
                float radius = (float)circle.Radius;
                Debug.Log($"Circle at {center} with radius {radius}");
                // Optional: convert circle to points later
            }

            // Process Polylines
            foreach (var polylineObj in polylines)
            {
                UnityEngine.Vector3[] points = null;

                if (polylineObj.GetType().Name == "LwPolyline" || polylineObj.GetType().Name == "LightWeightPolyline")
                {
                    // Use reflection to access Vertexes
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
                    else if (vertexes is IList<object> vertexList)
                    {
                        points = new UnityEngine.Vector3[vertexList.Count];
                        for (int i = 0; i < vertexList.Count; i++)
                        {
                            var vertex = vertexList[i];
                            var position = vertex.GetType().GetProperty("Position")?.GetValue(vertex);
                            if (position is netDxf.Vector3 vector)
                            {
                                points[i] = new UnityEngine.Vector3(
                                    (float)vector.X,
                                    (float)vector.Y,
                                    0
                                );
                            }
                        }
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
                    else if (polyline.Vertexes is IList vertexList)
                    {
                        points = new UnityEngine.Vector3[vertexList.Count];
                        for (int i = 0; i < vertexList.Count; i++)
                        {
                            var vertex = vertexList[i];
                            var position = vertex.GetType().GetProperty("Position")?.GetValue(vertex);
                            if (position is netDxf.Vector3 vector)
                            {
                                points[i] = new UnityEngine.Vector3(
                                    (float)vector.X,
                                    (float)vector.Y,
                                    0
                                );
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Unsupported Polyline Vertexes type");
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
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error loading DXF file: {ex.Message}");

            DXFPathRenderer renderer = FindObjectOfType<DXFPathRenderer>();
            if (renderer != null)
            {
                renderer.RenderPaths(importedLines);
            }
        }
    }
}