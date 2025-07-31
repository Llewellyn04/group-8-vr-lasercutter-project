using System.Collections.Generic;
using System.IO;
using UnityEngine;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

public class ExportDXF : MonoBehaviour
{
    [Header("Drawing Input")]
    public List<UnityEngine.Vector2> drawingPoints = new List<UnityEngine.Vector2>();

    [Header("DXF Settings")]
    public string fileName = "drawing.dxf";

    private void Start()
    {
        // 🔧 Dummy test square (clockwise)
        drawingPoints = new List<UnityEngine.Vector2>
        {
            new UnityEngine.Vector2(100, 100),
            new UnityEngine.Vector2(300, 100),
            new UnityEngine.Vector2(300, 300),
            new UnityEngine.Vector2(100, 300),
            new UnityEngine.Vector2(100, 100) // Close the shape
        };

        // Automatically export for testing
        ExportToDXF();
    }

    public void ExportToDXF()
    {
        if (drawingPoints == null || drawingPoints.Count < 2)
        {
            Debug.LogWarning("Not enough points to export.");
            return;
        }

        DxfDocument dxf = new DxfDocument();

        // Export each line segment
        for (int i = 0; i < drawingPoints.Count - 1; i++)
        {
            UnityEngine.Vector2 p1 = drawingPoints[i];
            UnityEngine.Vector2 p2 = drawingPoints[i + 1];
            Line line = new Line(p1.ToDxfVector(), p2.ToDxfVector());
            line.Layer = new Layer("Drawing") { Color = AciColor.Blue };
            dxf.Entities.Add(line); // <-- fixed here
        }

        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            dxf.Save(filePath);
            Debug.Log($"DXF exported to:\n{filePath}");
        }
        catch (IOException e)
        {
            Debug.LogError($"Failed to save DXF: {e.Message}");
        }
    }
}

// Helper extension for Vector2 → netDxf.Vector3
public static class VectorExtensions
{
    public static netDxf.Vector3 ToDxfVector(this UnityEngine.Vector2 v)
    {
        return new netDxf.Vector3(v.x, v.y, 0); // Z = 0 for 2D
    }
}
