// 7/22/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;

public class SketchToolManager : MonoBehaviour
{
    public enum ToolType { None, Line, Rectangle, Ellipse }
    public static ToolType ActiveTool = ToolType.None;

    public void SetActiveTool(string toolName)
    {
        ActiveTool = (ToolType)System.Enum.Parse(typeof(ToolType), toolName);
        Debug.Log("Active Tool: " + ActiveTool);
    }
}