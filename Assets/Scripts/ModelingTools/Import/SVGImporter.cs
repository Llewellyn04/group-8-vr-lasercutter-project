using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;

// Import SVG files from a file dialog, place as LineRenderers on the whiteboard,
// and make them compatible with existing drag/resize + export flows.
public class SVGImporter : MonoBehaviour
{
    [Header("Whiteboard References")]
    public Transform whiteboardPlane;
    [Tooltip("LineRenderer prefab used for imported paths")] public LineRenderer lineRendererPrefab;
    public RedoUndoManager redoUndoManager;

    [Header("Placement Settings")]
    [Tooltip("Whiteboard width (local units) used for scaling imports")] public float whiteboardWidth = 1.0f;
    [Tooltip("Whiteboard height (local units) used for scaling imports")] public float whiteboardHeight = 1.0f;
    [Tooltip("Center offset on the whiteboard plane")] public Vector2 whiteboardCenter = Vector2.zero;
    [Tooltip("Z offset on the whiteboard to draw slightly in front")] public float importedZOffset = 0.0f;

    [Header("Style Defaults")]
    public Color importedColor = Color.red;
    public float importedLineWidth = 0.02f;

    [Header("Debug")]
    public bool enableLogs = true;

    // Entry point for UI Button
    public void ImportSVGFromDialog()
    {
        string path = null;

#if UNITY_EDITOR
        path = UnityEditor.EditorUtility.OpenFilePanel("Select SVG", Application.dataPath, "svg");
#elif UNITY_STANDALONE_WIN
        try
        {
            // Use WinForms OpenFileDialog on Windows Standalone
            path = OpenFileDialogWindows("SVG files (*.svg)|*.svg|All files (*.*)|*.*");
        }
        catch (Exception ex)
        {
            Log($"OpenFileDialog failed: {ex.Message}");
        }
#endif

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            ImportSVGFromPath(path);
        }
        else
        {
            Log("No file selected or file does not exist");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ImportSVGFromDialog();
        }
    }

    public void ImportSVGFromPath(string filePath)
    {
        if (whiteboardPlane == null || lineRendererPrefab == null)
        {
            Debug.LogError("SVGImporter: Assign whiteboardPlane and lineRendererPrefab");
            return;
        }

        try
        {
            string svgContent = File.ReadAllText(filePath);
            var commands = ParseSVG(svgContent);
            if (commands.Count == 0)
            {
                Log("SVG contained no drawable elements");
                return;
            }

            DrawOnWhiteboard(commands);
            Log($"Imported {commands.Count} drawing commands from {Path.GetFileName(filePath)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"SVGImporter: Error reading SVG: {e.Message}");
        }
    }

    // ===== Parsing =====
    private enum CommandType { MoveTo, LineTo, Circle }
    private struct DrawingCommand
    {
        public CommandType type;
        public Vector2 position;
        public float radius;
    }

    private List<DrawingCommand> ParseSVG(string svgContent)
    {
        List<DrawingCommand> commands = new List<DrawingCommand>();
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(svgContent);

        // <line>
        foreach (XmlNode node in xmlDoc.GetElementsByTagName("line"))
        {
            if (TryF(node.Attributes["x1"], out float x1) &&
                TryF(node.Attributes["y1"], out float y1) &&
                TryF(node.Attributes["x2"], out float x2) &&
                TryF(node.Attributes["y2"], out float y2))
            {
                commands.Add(new DrawingCommand { type = CommandType.MoveTo, position = new Vector2(x1, -y1) });
                commands.Add(new DrawingCommand { type = CommandType.LineTo, position = new Vector2(x2, -y2) });
            }
        }

        // <rect> -> 4 edges
        foreach (XmlNode node in xmlDoc.GetElementsByTagName("rect"))
        {
            if (TryF(node.Attributes["x"], out float x) &&
                TryF(node.Attributes["y"], out float y) &&
                TryF(node.Attributes["width"], out float w) &&
                TryF(node.Attributes["height"], out float h) &&
                w > 0f && h > 0f)
            {
                Vector2 tl = new Vector2(x, -y);
                Vector2 tr = new Vector2(x + w, -y);
                Vector2 br = new Vector2(x + w, -(y + h));
                Vector2 bl = new Vector2(x, -(y + h));

                commands.Add(new DrawingCommand { type = CommandType.MoveTo, position = tl });
                commands.Add(new DrawingCommand { type = CommandType.LineTo, position = tr });
                commands.Add(new DrawingCommand { type = CommandType.LineTo, position = br });
                commands.Add(new DrawingCommand { type = CommandType.LineTo, position = bl });
                commands.Add(new DrawingCommand { type = CommandType.LineTo, position = tl }); // close
            }
        }

        // <circle>
        foreach (XmlNode node in xmlDoc.GetElementsByTagName("circle"))
        {
            if (TryF(node.Attributes["cx"], out float cx) &&
                TryF(node.Attributes["cy"], out float cy) &&
                TryF(node.Attributes["r"], out float r) && r > 0f)
            {
                commands.Add(new DrawingCommand { type = CommandType.Circle, position = new Vector2(cx, -cy), radius = r });
            }
        }

        // <polyline>
        foreach (XmlNode node in xmlDoc.GetElementsByTagName("polyline"))
        {
            string pointsAttr = node.Attributes["points"]?.Value;
            var pts = ParseSvgPoints(pointsAttr);
            if (pts.Count > 0)
            {
                commands.Add(new DrawingCommand { type = CommandType.MoveTo, position = new Vector2(pts[0].x, -pts[0].y) });
                for (int i = 1; i < pts.Count; i++)
                    commands.Add(new DrawingCommand { type = CommandType.LineTo, position = new Vector2(pts[i].x, -pts[i].y) });
            }
        }

        // <polygon>
        foreach (XmlNode node in xmlDoc.GetElementsByTagName("polygon"))
        {
            string pointsAttr = node.Attributes["points"]?.Value;
            var pts = ParseSvgPoints(pointsAttr);
            if (pts.Count > 0)
            {
                commands.Add(new DrawingCommand { type = CommandType.MoveTo, position = new Vector2(pts[0].x, -pts[0].y) });
                for (int i = 1; i < pts.Count; i++)
                    commands.Add(new DrawingCommand { type = CommandType.LineTo, position = new Vector2(pts[i].x, -pts[i].y) });
                // Close polygon
                commands.Add(new DrawingCommand { type = CommandType.LineTo, position = new Vector2(pts[0].x, -pts[0].y) });
            }
        }

        // <path>
        foreach (XmlNode node in xmlDoc.GetElementsByTagName("path"))
        {
            string d = node.Attributes["d"]?.Value;
            ParseSvgPathImproved(d, commands);
        }

        return commands;
    }

    private bool TryF(XmlAttribute attr, out float val)
    {
        val = 0f;
        if (attr == null) return false;
        return float.TryParse(attr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out val);
    }

    private List<Vector2> ParseSvgPoints(string pointsAttr)
    {
        var points = new List<Vector2>();
        if (string.IsNullOrEmpty(pointsAttr)) return points;
        string cleaned = pointsAttr.Replace(',', ' ');
        string[] values = cleaned.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < values.Length - 1; i += 2)
        {
            if (float.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(values[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                points.Add(new Vector2(x, y));
            }
        }
        return points;
    }

    // Similar to FileListManager's improved parser. Supports M/L/H/V/C/Z with cubic bezier approx.
    private void ParseSvgPathImproved(string d, List<DrawingCommand> commands)
    {
        if (string.IsNullOrEmpty(d)) return;

        d = d.Replace(",", " ");
        d = Regex.Replace(d, @"([MmLlHhVvCcSsQqTtAaZz])", " $1 ");
        string[] tokens = d.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        Vector2 currentPos = Vector2.zero;
        Vector2 lastControlPoint = Vector2.zero;
        Vector2 pathStart = Vector2.zero;
        char cmd = 'M';
        bool isRel = false;

        int i = 0;
        while (i < tokens.Length)
        {
            string tk = tokens[i].Trim();
            if (tk.Length == 1 && char.IsLetter(tk[0]))
            {
                cmd = char.ToUpperInvariant(tk[0]);
                isRel = char.IsLower(tk[0]);
                i++;
                continue;
            }

            switch (cmd)
            {
                case 'M':
                    if (i + 1 < tokens.Length &&
                        float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float mx) &&
                        float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float my))
                    {
                        Vector2 pos = new Vector2(mx, -my);
                        if (isRel) pos += currentPos;
                        commands.Add(new DrawingCommand { type = CommandType.MoveTo, position = pos });
                        currentPos = pathStart = pos;
                        cmd = 'L';
                        i += 2;
                    }
                    else i++;
                    break;
                case 'L':
                    if (i + 1 < tokens.Length &&
                        float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float lx) &&
                        float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ly))
                    {
                        Vector2 pos = new Vector2(lx, -ly);
                        if (isRel) pos += currentPos;
                        commands.Add(new DrawingCommand { type = CommandType.LineTo, position = pos });
                        currentPos = pos;
                        i += 2;
                    }
                    else i++;
                    break;
                case 'H':
                    if (float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float hx))
                    {
                        Vector2 pos = new Vector2(isRel ? currentPos.x + hx : hx, currentPos.y);
                        commands.Add(new DrawingCommand { type = CommandType.LineTo, position = pos });
                        currentPos = pos;
                        i++;
                    }
                    else i++;
                    break;
                case 'V':
                    if (float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float vy))
                    {
                        Vector2 pos = new Vector2(currentPos.x, isRel ? currentPos.y - vy : -vy);
                        commands.Add(new DrawingCommand { type = CommandType.LineTo, position = pos });
                        currentPos = pos;
                        i++;
                    }
                    else i++;
                    break;
                case 'Z':
                    commands.Add(new DrawingCommand { type = CommandType.LineTo, position = pathStart });
                    currentPos = pathStart;
                    i++;
                    break;
                case 'C':
                    if (i + 5 < tokens.Length &&
                        float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float c1x) &&
                        float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float c1y) &&
                        float.TryParse(tokens[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out float c2x) &&
                        float.TryParse(tokens[i + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out float c2y) &&
                        float.TryParse(tokens[i + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx) &&
                        float.TryParse(tokens[i + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out float cy))
                    {
                        Vector2 p1 = new Vector2(c1x, -c1y);
                        Vector2 p2 = new Vector2(c2x, -c2y);
                        Vector2 p3 = new Vector2(cx, -cy);
                        if (isRel) { p1 += currentPos; p2 += currentPos; p3 += currentPos; }
                        ApproximateCubicBezier(currentPos, p1, p2, p3, commands);
                        currentPos = p3;
                        lastControlPoint = p2;
                        i += 6;
                    }
                    else i++;
                    break;
                default:
                    i++;
                    break;
            }
        }
    }

    private void ApproximateCubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, List<DrawingCommand> commands)
    {
        const int segments = 12;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float omt = 1f - t;
            Vector2 pt = (omt * omt * omt) * p0 + 3f * (omt * omt * t) * p1 + 3f * (omt * t * t) * p2 + (t * t * t) * p3;
            commands.Add(new DrawingCommand { type = CommandType.LineTo, position = pt });
        }
    }

    // ===== Drawing =====
    private void DrawOnWhiteboard(List<DrawingCommand> commands)
    {
        // Compute bounds for scaling
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in commands)
        {
            if (c.type == CommandType.Circle)
            {
                Vector2 a = c.position + new Vector2(-c.radius, -c.radius);
                Vector2 b = c.position + new Vector2(c.radius, c.radius);
                min = Vector2.Min(min, a);
                max = Vector2.Max(max, b);
            }
            else
            {
                min = Vector2.Min(min, c.position);
                max = Vector2.Max(max, c.position);
            }
        }

        Vector2 size = max - min;
        float safeW = Mathf.Max(size.x, 1e-6f);
        float safeH = Mathf.Max(size.y, 1e-6f);
        float scale = Mathf.Min((whiteboardWidth * 0.8f) / safeW, (whiteboardHeight * 0.8f) / safeH);
        scale = Mathf.Clamp(scale, 0.0001f, 1000f);

        // Split commands into paths
        List<List<DrawingCommand>> paths = SeparatePaths(commands);

        foreach (var path in paths)
        {
            if (path.Count == 0) continue;
            List<Vector3> worldPoints = new List<Vector3>();
            bool closed = false;
            Vector3? firstPoint = null;

            foreach (var cmd in path)
            {
                if (cmd.type == CommandType.Circle)
                {
                    DrawCircleWorld((cmd.position - (min + size * 0.5f)) * scale + whiteboardCenter, cmd.radius * scale);
                }
                else
                {
                    Vector2 local = (cmd.position - (min + size * 0.5f)) * scale + whiteboardCenter;
                    Vector3 wp = whiteboardPlane.TransformPoint(new Vector3(local.x, local.y, importedZOffset));
                    if (firstPoint == null) firstPoint = wp;
                    worldPoints.Add(wp);
                }
            }

            // Decide loop if path ends where it started
            if (worldPoints.Count >= 3 && firstPoint.HasValue)
            {
                float d = Vector3.Distance(worldPoints[worldPoints.Count - 1], firstPoint.Value);
                closed = d <= 1e-4f; // tolerance
                if (closed)
                {
                    // remove duplicate last point to avoid tiny spurs
                    worldPoints.RemoveAt(worldPoints.Count - 1);
                }
            }

            if (worldPoints.Count >= 2)
            {
                CreateLineRenderer(worldPoints, closed);
            }
        }
    }

    private List<List<DrawingCommand>> SeparatePaths(List<DrawingCommand> commands)
    {
        List<List<DrawingCommand>> paths = new List<List<DrawingCommand>>();
        List<DrawingCommand> current = new List<DrawingCommand>();
        foreach (var c in commands)
        {
            if (c.type == CommandType.Circle)
            {
                if (current.Count > 0) { paths.Add(new List<DrawingCommand>(current)); current.Clear(); }
                paths.Add(new List<DrawingCommand> { c });
            }
            else if (c.type == CommandType.MoveTo)
            {
                if (current.Count > 0) { paths.Add(new List<DrawingCommand>(current)); current.Clear(); }
                current.Add(c);
            }
            else
            {
                current.Add(c);
            }
        }
        if (current.Count > 0) paths.Add(current);
        return paths;
    }

    private void CreateLineRenderer(List<Vector3> worldPoints, bool loop)
    {
        GameObject lineObj = Instantiate(lineRendererPrefab.gameObject, whiteboardPlane);
        lineObj.name = loop ? "VR_ImportedShape" : "VR_ImportedPath";
        LineRenderer lr = lineObj.GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = importedLineWidth;
        if (lr.material != null) lr.material.color = importedColor;
        lr.positionCount = worldPoints.Count;
        lr.loop = loop;
        lr.SetPositions(worldPoints.ToArray());

        if (redoUndoManager != null)
        {
            redoUndoManager.RegisterLine(lineObj);
        }

        // Update current design for attachment tool (if present)
        var attachTool = FindObjectOfType<AttachDesignToController>();
        if (attachTool != null)
            attachTool.SetCurrentDesign(lineObj);
    }

    private void DrawCircleWorld(Vector2 localCenter, float radius)
    {
        int segments = 48;
        List<Vector3> pts = new List<Vector3>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            float ang = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 p = localCenter + new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius);
            pts.Add(whiteboardPlane.TransformPoint(new Vector3(p.x, p.y, importedZOffset)));
        }
        CreateLineRenderer(pts, true);
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    // Windows file dialog (STA thread)
    private string OpenFileDialogWindows(string filter)
    {
        string selected = null;
        System.Threading.Thread t = new System.Threading.Thread(() =>
        {
            try
            {
                using (var ofd = new System.Windows.Forms.OpenFileDialog())
                {
                    ofd.Filter = filter;
                    ofd.Multiselect = false;
                    ofd.CheckFileExists = true;
                    ofd.CheckPathExists = true;
                    var result = ofd.ShowDialog();
                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        selected = ofd.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpenFileDialog error: {ex.Message}");
            }
        });
        t.SetApartmentState(System.Threading.ApartmentState.STA);
        t.Start();
        t.Join();
        return selected;
    }
#endif

    private void Log(string msg)
    {
        if (enableLogs) Debug.Log($"[SVGImporter] {msg}");
    }
}
