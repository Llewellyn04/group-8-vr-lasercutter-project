using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEngine.EventSystems;
using System.Linq;

public class FileListManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject fileSelectionPanel;
    public Button togglePanelButton;
    public Transform scrollViewContent;
    public GameObject fileButtonPrefab;

    [Header("Settings")]
    public string importFolderPath = "ImportsDxfSVG134567";

    [Header("Whiteboard Integration")]
    public GameObject drawingManagerObject;
    public GameObject drawingPlane;
    public LineRenderer lineRendererPrefab;
    public float whiteboardWidth = 5f;
    public float whiteboardHeight = 3f;
    public Vector2 whiteboardCenter = Vector2.zero;
    public Color importedDrawingColor = Color.red;

    [Header("Alternative Display Methods")]
    public bool useSimplifiedParsing = true;
    public bool showDXFAsPoints = false;
    public GameObject pointPrefab; // Optional: prefab for showing points
    public bool logDXFContent = false; // For debugging DXF structure

    [Header("Debug Settings")]
    public bool enableDebugLogs = true;

    private List<GameObject> instantiatedButtons = new List<GameObject>();
    private List<GameObject> currentDrawings = new List<GameObject>();
    private bool isPanelOpen = false;

    // ---- NEW: robust number parsing / filtering (minimal, local helper only) ----
    private const double COORD_ABS_MAX = 1e8; // filter obviously bad coordinates (e.g., 1e20 from malformed DXF)

    private bool TryParseDxfNumber(string s, out float value)
    {
        value = 0f;
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return false;
        if (double.IsNaN(d) || double.IsInfinity(d) || System.Math.Abs(d) > COORD_ABS_MAX)
            return false;
        value = (float)d;
        return true;
    }

    private bool IsFinite(Vector2 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y);
    }

    void Start()
    {
        DebugLog("=== FileListManager Start() ===");
        ValidateComponents();

        if (fileSelectionPanel != null)
        {
            fileSelectionPanel.SetActive(false);
            DebugLog("File selection panel set to inactive at start");
        }
        else DebugLog("ERROR: fileSelectionPanel is not assigned!", true);

        if (togglePanelButton != null)
        {
            togglePanelButton.onClick.AddListener(TogglePanel);
            DebugLog("Toggle button listener added");
        }
        else DebugLog("ERROR: togglePanelButton is not assigned!", true);
    }

    private void ValidateComponents()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            DebugLog("ERROR: No EventSystem found! Adding one...", true);
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();
        }
    }

    public void TogglePanel()
    {
        DebugLog("=== TogglePanel CALLED ===");
        isPanelOpen = !isPanelOpen;
        DebugLog($"Panel open state now: {isPanelOpen}");

        if (fileSelectionPanel != null)
            fileSelectionPanel.SetActive(isPanelOpen);

        if (isPanelOpen)
        {
            DebugLog("Panel is opening, populating file list...");
            PopulateFileList();
        }
    }

    private void PopulateFileList()
    {
        DebugLog("=== PopulateFileList CALLED ===");
        ClearExistingButtons();

        string fullPath = Path.Combine(Application.dataPath, importFolderPath);
        DebugLog($"Looking for files in: {fullPath}");

        if (!Directory.Exists(fullPath))
        {
            DebugLog($"ERROR: Import folder not found: {fullPath}", true);
            return;
        }

        string[] dxfFiles = Directory.GetFiles(fullPath, "*.dxf", SearchOption.TopDirectoryOnly);
        string[] svgFiles = Directory.GetFiles(fullPath, "*.svg", SearchOption.TopDirectoryOnly);

        DebugLog($"Found {dxfFiles.Length} DXF and {svgFiles.Length} SVG files");

        List<string> allFiles = new List<string>();
        allFiles.AddRange(dxfFiles);
        allFiles.AddRange(svgFiles);

        foreach (string filePath in allFiles)
        {
            DebugLog($"Creating button for {filePath}");
            CreateFileButton(filePath);
        }
    }

    private void CreateFileButton(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        DebugLog($"=== CreateFileButton for {fileName} ===");

        if (fileButtonPrefab == null || scrollViewContent == null)
        {
            DebugLog("ERROR: fileButtonPrefab or scrollViewContent not assigned!", true);
            return;
        }

        GameObject newButton = Instantiate(fileButtonPrefab, scrollViewContent);

        // Set up text
        TextMeshProUGUI tmpText = newButton.GetComponentInChildren<TextMeshProUGUI>();
        if (tmpText != null)
        {
            tmpText.text = fileName;
        }
        else
        {
            Text txt = newButton.GetComponentInChildren<Text>();
            if (txt != null) txt.text = fileName;
        }

        // Set up click listener
        Button button = newButton.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OnFileButtonClicked(fileName, filePath));
            DebugLog("Click listener added to button");
        }

        instantiatedButtons.Add(newButton);
    }

    private void OnFileButtonClicked(string fileName, string fullFilePath)
    {
        DebugLog("=== OnFileButtonClicked CALLED ===");
        DebugLog($"User clicked: {fileName} | Path: {fullFilePath}");

        ClearCurrentDrawings();

        if (!File.Exists(fullFilePath))
        {
            DebugLog($"ERROR: File does not exist: {fullFilePath}", true);
            return;
        }

        string ext = Path.GetExtension(fileName).ToLower();
        DebugLog($"File extension detected: {ext}");

        switch (ext)
        {
            case ".dxf":
                LoadDXFFile(fullFilePath);
                break;
            case ".svg":
                LoadSVGFile(fullFilePath);
                break;
            default:
                DebugLog($"WARNING: Unsupported file type: {ext}", true);
                break;
        }

        TogglePanel();
    }

    private void LoadDXFFile(string filePath)
    {
        DebugLog($"=== LoadDXFFile CALLED: {filePath} ===");
        try
        {
            string fileContent = File.ReadAllText(filePath);
            DebugLog($"DXF file size: {fileContent.Length} characters");

            if (logDXFContent)
            {
                LogDXFStructure(fileContent);
            }

            List<DrawingCommand> commands;

            if (useSimplifiedParsing)
            {
                commands = ParseDXFSimplified(fileContent);
            }
            else
            {
                commands = ParseDXFComplete(fileContent);
            }

            DebugLog($"DXF produced {commands.Count} drawing commands");

            if (commands.Count > 0)
            {
                DrawOnWhiteboard(commands);
            }
            else
            {
                DebugLog("WARNING: DXF had no drawable commands - trying alternative methods", true);
                TryAlternativeDXFDisplay(fileContent);
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"ERROR reading DXF: {e.Message}", true);
        }
    }

    private void LoadSVGFile(string filePath)
    {
        DebugLog($"=== LoadSVGFile CALLED: {filePath} ===");
        // Your existing SVG loading code...
    }

    // COMPLETE DXF PARSER
    private List<DrawingCommand> ParseDXFComplete(string content)
    {
        DebugLog("=== ParseDXFComplete ===");
        List<DrawingCommand> commands = new List<DrawingCommand>();
        string[] lines = content.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        DebugLog($"DXF has {lines.Length} lines");

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            // Look for entity section
            if (line == "ENTITIES" || line == "0")
            {
                if (i + 1 < lines.Length)
                {
                    string entityType = lines[i + 1].Trim();

                    switch (entityType)
                    {
                        case "LINE":
                            commands.AddRange(ParseDXFLine(lines, i));
                            break;
                        case "CIRCLE":
                            commands.AddRange(ParseDXFCircle(lines, i));
                            break;
                        case "ARC":
                            commands.AddRange(ParseDXFArc(lines, i));
                            break;
                        case "LWPOLYLINE":
                            commands.AddRange(ParseDXFPolyline(lines, i));
                            break;
                        case "POLYLINE":
                            commands.AddRange(ParseDXFPolyline(lines, i));
                            break;
                        case "SPLINE":
                            commands.AddRange(ParseDXFSpline(lines, i));
                            break;
                        case "ELLIPSE":
                            commands.AddRange(ParseDXFEllipse(lines, i));
                            break;
                    }
                }
            }
        }

        DebugLog($"Complete parsing found {commands.Count} commands");
        return commands;
    }

    // SIMPLIFIED DXF PARSER - More Robust
    private List<DrawingCommand> ParseDXFSimplified(string content)
    {
        DebugLog("=== ParseDXFSimplified ===");
        List<DrawingCommand> commands = new List<DrawingCommand>();

        // Extract all coordinate pairs using robust parsing
        List<Vector2> allPoints = ExtractAllCoordinates(content);
        DebugLog($"Found {allPoints.Count} coordinate points");

        if (allPoints.Count > 0)
        {
            // Connect all points as a continuous path
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = allPoints[0] });

            for (int i = 1; i < allPoints.Count; i++)
            {
                commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = allPoints[i] });
            }
        }

        // Also look for circles
        List<CircleData> circles = ExtractCircles(content);
        DebugLog($"Found {circles.Count} circles");

        foreach (var circle in circles)
        {
            if (IsFinite(circle.center) && float.IsFinite(circle.radius) && circle.radius > 0f)
            {
                commands.Add(new DrawingCommand
                {
                    type = DrawingCommand.CommandType.Circle,
                    position = circle.center,
                    radius = circle.radius
                });
            }
        }

        return commands;
    }

    private List<Vector2> ExtractAllCoordinates(string content)
    {
        List<Vector2> points = new List<Vector2>();
        string[] lines = content.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length - 3; i++)
        {
            string code = lines[i].Trim();
            string value = lines[i + 1].Trim();

            // Look for X coordinates (code 10, 11, etc.)
            if ((code == "10" || code == "11"))
            {
                string nextCode = lines[i + 2].Trim();
                string nextValue = lines[i + 3].Trim();

                // Check if next is Y coordinate (code 20, 21, etc.)
                if ((nextCode == "20" || nextCode == "21"))
                {
                    if (TryParseDxfNumber(value, out float x) &&
                        TryParseDxfNumber(nextValue, out float y))
                    {
                        Vector2 p = new Vector2(x, y);
                        if (IsFinite(p))
                        {
                            points.Add(p);
                            DebugLog($"Found coordinate: ({x}, {y})");
                        }
                    }
                }
            }
        }

        return points;
    }

    private struct CircleData
    {
        public Vector2 center;
        public float radius;
    }

    private List<CircleData> ExtractCircles(string content)
    {
        List<CircleData> circles = new List<CircleData>();
        string[] lines = content.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "CIRCLE")
            {
                Vector2 center = Vector2.zero;
                float radius = 0f;
                bool hasX = false, hasY = false, hasR = false;

                // Look for circle parameters in next 20 lines
                for (int j = i + 1; j < i + 20 && j < lines.Length - 1; j++)
                {
                    string code = lines[j].Trim();
                    string value = lines[j + 1].Trim();

                    if (code == "10" && TryParseDxfNumber(value, out float cx)) { center.x = cx; hasX = true; }
                    if (code == "20" && TryParseDxfNumber(value, out float cy)) { center.y = cy; hasY = true; }
                    if (code == "40" && TryParseDxfNumber(value, out float r)) { radius = r; hasR = true; }
                }

                if (hasX && hasY && hasR && radius > 0f && IsFinite(center))
                {
                    circles.Add(new CircleData { center = center, radius = radius });
                }
            }
        }

        return circles;
    }

    // Individual entity parsers
    private List<DrawingCommand> ParseDXFLine(string[] lines, int startIndex)
    {
        List<DrawingCommand> commands = new List<DrawingCommand>();
        Vector2 start = Vector2.zero, end = Vector2.zero;
        bool sx = false, sy = false, ex = false, ey = false;

        for (int i = startIndex + 1; i < startIndex + 20 && i < lines.Length - 1; i++)
        {
            string code = lines[i].Trim();
            string value = lines[i + 1].Trim();

            if (code == "10" && TryParseDxfNumber(value, out float v10)) { start.x = v10; sx = true; }
            if (code == "20" && TryParseDxfNumber(value, out float v20)) { start.y = v20; sy = true; }
            if (code == "11" && TryParseDxfNumber(value, out float v11)) { end.x = v11; ex = true; }
            if (code == "21" && TryParseDxfNumber(value, out float v21)) { end.y = v21; ey = true; }
        }

        if (sx && sy && ex && ey && start != end && IsFinite(start) && IsFinite(end))
        {
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = start });
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = end });
        }

        return commands;
    }

    private List<DrawingCommand> ParseDXFCircle(string[] lines, int startIndex)
    {
        List<DrawingCommand> commands = new List<DrawingCommand>();
        Vector2 center = Vector2.zero;
        float radius = 0f;
        bool hasX = false, hasY = false, hasR = false;

        for (int i = startIndex + 1; i < startIndex + 15 && i < lines.Length - 1; i++)
        {
            string code = lines[i].Trim();
            string value = lines[i + 1].Trim();

            if (code == "10" && TryParseDxfNumber(value, out float v10)) { center.x = v10; hasX = true; }
            if (code == "20" && TryParseDxfNumber(value, out float v20)) { center.y = v20; hasY = true; }
            if (code == "40" && TryParseDxfNumber(value, out float v40)) { radius = v40; hasR = true; }
        }

        if (hasX && hasY && hasR && radius > 0f && IsFinite(center))
        {
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.Circle, position = center, radius = radius });
        }

        return commands;
    }

    private List<DrawingCommand> ParseDXFArc(string[] lines, int startIndex)
    {
        // Convert arc to line segments
        List<DrawingCommand> commands = new List<DrawingCommand>();
        Vector2 center = Vector2.zero;
        float radius = 0f, startAngle = 0f, endAngle = 0f;
        bool hasX = false, hasY = false, hasR = false, hasSA = false, hasEA = false;

        for (int i = startIndex + 1; i < startIndex + 20 && i < lines.Length - 1; i++)
        {
            string code = lines[i].Trim();
            string value = lines[i + 1].Trim();

            if (code == "10" && TryParseDxfNumber(value, out float v10)) { center.x = v10; hasX = true; }
            if (code == "20" && TryParseDxfNumber(value, out float v20)) { center.y = v20; hasY = true; }
            if (code == "40" && TryParseDxfNumber(value, out float v40)) { radius = v40; hasR = true; }
            if (code == "50" && TryParseDxfNumber(value, out float v50)) { startAngle = v50; hasSA = true; }
            if (code == "51" && TryParseDxfNumber(value, out float v51)) { endAngle = v51; hasEA = true; }
        }

        if (hasX && hasY && hasR && hasSA && hasEA && radius > 0f && IsFinite(center))
        {
            int segments = 16;
            float currentAngle = startAngle * Mathf.Deg2Rad;

            Vector2 firstPoint = center + new Vector2(radius * Mathf.Cos(currentAngle), radius * Mathf.Sin(currentAngle));
            if (!IsFinite(firstPoint)) return commands;
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = firstPoint });

            for (int i = 1; i <= segments; i++)
            {
                currentAngle = (startAngle + (endAngle - startAngle) * i / segments) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(radius * Mathf.Cos(currentAngle), radius * Mathf.Sin(currentAngle));
                if (IsFinite(point))
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = point });
            }
        }

        return commands;
    }

    private List<DrawingCommand> ParseDXFPolyline(string[] lines, int startIndex)
    {
        List<DrawingCommand> commands = new List<DrawingCommand>();
        List<Vector2> points = new List<Vector2>();

        for (int i = startIndex + 1; i < lines.Length && i < startIndex + 500; i++)
        {
            if (i + 3 >= lines.Length) break;

            string code = lines[i].Trim();
            string value = lines[i + 1].Trim();

            // Stop if we hit another entity
            if (value == "ENDSEC" || value == "LINE" || value == "CIRCLE" || value == "ARC" || value == "LWPOLYLINE" || value == "POLYLINE") break;

            if (code == "10" && lines[i + 2].Trim() == "20")
            {
                if (TryParseDxfNumber(value, out float x) && TryParseDxfNumber(lines[i + 3].Trim(), out float y))
                {
                    Vector2 p = new Vector2(x, y);
                    if (IsFinite(p)) points.Add(p);
                }
            }
        }

        if (points.Count > 1)
        {
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = points[0] });
            for (int i = 1; i < points.Count; i++)
            {
                if (IsFinite(points[i]))
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = points[i] });
            }
        }

        return commands;
    }

    private List<DrawingCommand> ParseDXFSpline(string[] lines, int startIndex)
    {
        // Similar to polyline but for splines
        return ParseDXFPolyline(lines, startIndex);
    }

    private List<DrawingCommand> ParseDXFEllipse(string[] lines, int startIndex)
    {
        // Simplified ellipse as circle for now
        return ParseDXFCircle(lines, startIndex);
    }

    // ALTERNATIVE DISPLAY METHOD
    private void TryAlternativeDXFDisplay(string content)
    {
        DebugLog("=== Trying Alternative DXF Display ===");

        // Method 1: Show all coordinate points as dots
        if (showDXFAsPoints)
        {
            ShowDXFAsPoints(content);
        }

        // Method 2: Create a simple text visualization
        CreateTextVisualization(content);

        // Method 3: Log structure for manual inspection
        LogDXFStructure(content);
    }

    private void ShowDXFAsPoints(string content)
    {
        List<Vector2> points = ExtractAllCoordinates(content);

        if (pointPrefab == null)
        {
            // Create simple sphere points
            foreach (var point in points)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.parent = drawingPlane.transform;
                sphere.transform.localPosition = new Vector3(point.x * 0.01f, point.y * 0.01f, 0);
                sphere.transform.localScale = Vector3.one * 0.02f;
                sphere.GetComponent<Renderer>().material.color = importedDrawingColor;
                currentDrawings.Add(sphere);
            }
        }

        DebugLog($"Displayed {points.Count} points from DXF");
    }

    private void CreateTextVisualization(string content)
    {
        GameObject textObj = new GameObject("DXF_Info");
        textObj.transform.parent = drawingPlane.transform;
        textObj.transform.localPosition = Vector3.zero;

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = $"DXF File Loaded\nSize: {content.Length} chars\nClick 'Clear' to remove";
        textMesh.color = importedDrawingColor;
        textMesh.fontSize = 10;
        textMesh.anchor = TextAnchor.MiddleCenter;

        currentDrawings.Add(textObj);
    }

    private void LogDXFStructure(string content)
    {
        DebugLog("=== DXF Structure Analysis ===");
        string[] lines = content.Split('\n');

        Dictionary<string, int> entityCounts = new Dictionary<string, int>();

        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].Trim() == "0")
            {
                string entity = lines[i + 1].Trim();
                if (!entityCounts.ContainsKey(entity))
                    entityCounts[entity] = 0;
                entityCounts[entity]++;
            }
        }

        DebugLog("Entities found in DXF:");
        foreach (var kvp in entityCounts.OrderByDescending(x => x.Value))
        {
            DebugLog($"  {kvp.Key}: {kvp.Value} times");
        }

        // Look for specific sections
        if (content.Contains("ENTITIES"))
            DebugLog("✓ ENTITIES section found");
        else
            DebugLog("✗ ENTITIES section missing");

        if (content.Contains("LINE"))
            DebugLog("✓ LINE entities found");
        if (content.Contains("CIRCLE"))
            DebugLog("✓ CIRCLE entities found");
        if (content.Contains("LWPOLYLINE"))
            DebugLog("✓ LWPOLYLINE entities found");
    }

    [System.Serializable]
    public class DrawingCommand
    {
        public enum CommandType { MoveTo, LineTo, Circle }
        public CommandType type;
        public Vector2 position;
        public float radius;
    }

    private void DrawOnWhiteboard(List<DrawingCommand> commands)
    {
        if (drawingPlane == null || lineRendererPrefab == null)
        {
            DebugLog("ERROR: drawingPlane or lineRendererPrefab not assigned!", true);
            return;
        }

        DebugLog($"Drawing {commands.Count} commands onto whiteboard");

        // Filter out any non-finite data just in case
        List<DrawingCommand> safe = new List<DrawingCommand>();
        foreach (var c in commands)
        {
            if (IsFinite(c.position) && (c.type != DrawingCommand.CommandType.Circle || (float.IsFinite(c.radius) && c.radius > 0f)))
                safe.Add(c);
        }
        if (safe.Count == 0) return;

        // Calculate bounds and scale
        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;

        foreach (var cmd in safe)
        {
            min = Vector2.Min(min, cmd.position);
            max = Vector2.Max(max, cmd.position);
        }

        Vector2 drawingSize = max - min;
        // Guard against zero-size to avoid division by zero
        if (drawingSize.x == 0f) drawingSize.x = 1e-6f;
        if (drawingSize.y == 0f) drawingSize.y = 1e-6f;
        Vector2 drawingCenter = (min + max) / 2f;

        float scaleX = (whiteboardWidth * 0.8f) / drawingSize.x;
        float scaleY = (whiteboardHeight * 0.8f) / drawingSize.y;
        float scale = Mathf.Min(Mathf.Abs(scaleX), Mathf.Abs(scaleY));

        if (!float.IsFinite(scale) || scale <= 0)
            scale = 0.001f; // fallback

        DebugLog($"Drawing bounds: min={min}, max={max}, size={drawingSize}, scale={scale}");

        LineRenderer currentLine = null;
        List<Vector3> points = new List<Vector3>();

        foreach (var cmd in safe)
        {
            Vector2 scaledPos = (cmd.position - drawingCenter) * scale + whiteboardCenter;
            Vector3 local = new Vector3(scaledPos.x, scaledPos.y, 0);

            if (cmd.type == DrawingCommand.CommandType.MoveTo)
            {
                if (currentLine != null && points.Count > 1)
                    FinishLineRenderer(currentLine, points);

                currentLine = CreateNewLineRenderer();
                points.Clear();
                points.Add(local);
            }
            else if (cmd.type == DrawingCommand.CommandType.LineTo)
            {
                if (currentLine == null)
                    currentLine = CreateNewLineRenderer();
                points.Add(local);
            }
            else if (cmd.type == DrawingCommand.CommandType.Circle)
            {
                float scaledRadius = cmd.radius * scale;
                if (scaledRadius > 0f && float.IsFinite(scaledRadius))
                    DrawCircleAsLineRenderer(scaledPos, scaledRadius);
            }
        }

        if (currentLine != null && points.Count > 1)
            FinishLineRenderer(currentLine, points);
    }

    private LineRenderer CreateNewLineRenderer()
    {
        GameObject lineObj = Instantiate(lineRendererPrefab.gameObject, drawingPlane.transform);
        LineRenderer lr = lineObj.GetComponent<LineRenderer>();

        if (lr.material != null)
            lr.material.color = importedDrawingColor;

        lr.startWidth = lr.endWidth = 0.02f;
        lr.useWorldSpace = false;

        currentDrawings.Add(lineObj);
        return lr;
    }

    private void FinishLineRenderer(LineRenderer lr, List<Vector3> pts)
    {
        if (pts.Count > 1)
        {
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
        }
    }

    private void DrawCircleAsLineRenderer(Vector2 center, float radius)
    {
        LineRenderer lr = CreateNewLineRenderer();
        int segments = 32;
        Vector3[] points = new Vector3[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            points[i] = new Vector3(
                center.x + radius * Mathf.Cos(angle),
                center.y + radius * Mathf.Sin(angle),
                0
            );
        }

        lr.positionCount = points.Length;
        lr.SetPositions(points);
    }

    private void ClearCurrentDrawings()
    {
        foreach (GameObject drawing in currentDrawings)
            if (drawing != null) Destroy(drawing);
        currentDrawings.Clear();
    }

    private void ClearExistingButtons()
    {
        foreach (GameObject btn in instantiatedButtons)
            if (btn != null) Destroy(btn);
        instantiatedButtons.Clear();
    }

    private void DebugLog(string message, bool isError = false)
    {
        if (!enableDebugLogs) return;

        if (isError)
            Debug.LogError($"[FileListManager] {message}");
        else
            Debug.Log($"[FileListManager] {message}");
    }

    // Public method to clear all drawings
    public void ClearWhiteboard()
    {
        ClearCurrentDrawings();
    }
}