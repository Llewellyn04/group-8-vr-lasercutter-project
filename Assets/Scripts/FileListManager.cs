using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static Unity.Burst.Intrinsics.Arm;

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
    public bool useSimplifiedParsing = false;
    public bool showDXFAsPoints = false;
    public GameObject pointPrefab; // Optional: prefab for showing points
    public bool logDXFContent = true; // For debugging DXF structure

    [Header("Debug Settings")]
    public bool enableDebugLogs = true;

    [Header("Z-Position Control")]
    public float importedDrawingZOffset = 0.1f; // Positive value brings drawings forward

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

        // Configure Canvas for VR properly
        Canvas mainCanvas = null;
        if (fileSelectionPanel != null)
        {
            mainCanvas = fileSelectionPanel.GetComponentInParent<Canvas>();
        }

        if (mainCanvas != null)
        {
            DebugLog($"Canvas found - Current render mode: {mainCanvas.renderMode}");

            // For VR, ensure proper canvas setup
            if (mainCanvas.renderMode == RenderMode.WorldSpace)
            {
                // Make sure the canvas scale is appropriate for VR
                if (mainCanvas.transform.localScale.x < 0.001f)
                {
                    mainCanvas.transform.localScale = Vector3.one * 0.001f;
                    DebugLog("Adjusted canvas scale for VR visibility");
                }
            }

            // Ensure GraphicRaycaster is present and configured
            GraphicRaycaster raycaster = mainCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = mainCanvas.gameObject.AddComponent<GraphicRaycaster>();
                DebugLog("Added GraphicRaycaster to canvas");
            }

            // Important for VR: Make sure raycaster can detect interactions
            raycaster.ignoreReversedGraphics = false;
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }

        // Handle prefab positioning
        if (fileButtonPrefab != null && fileButtonPrefab.transform.parent == scrollViewContent)
        {
            fileButtonPrefab.transform.SetAsLastSibling();
            Button prefabButton = fileButtonPrefab.GetComponent<Button>();
            if (prefabButton != null)
            {
                prefabButton.interactable = false;
            }
            DebugLog("Configured prefab button");
        }

        // Rest of your existing code...
        if (fileSelectionPanel != null)
        {
            fileSelectionPanel.SetActive(false);
        }

        if (togglePanelButton != null)
        {
            togglePanelButton.onClick.AddListener(TogglePanel);
        }
    }

    private void ValidateComponents()
    {
        EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            DebugLog("ERROR: No EventSystem found! Adding one...", true);
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        else
        {
            // Check for Input System UI Input Module
            var inputSystemModule = eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            if (inputSystemModule != null)
            {
                DebugLog("Input System UI Module found - configuring for VR UI interaction");

                // Enable all interaction modes
                inputSystemModule.enabled = true;

                DebugLog("Input System UI Module configured");
            }
            else
            {
                DebugLog("No Input System UI Module found, adding one...");
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
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

        if (scrollViewContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollViewContent.GetComponent<RectTransform>());
        }

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

        if (scrollViewContent != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollViewContent.GetComponent<RectTransform>());
        }

        DebugButtonBlocking();
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
        newButton.name = $"FileButton_{fileName}";

        // Ensure proper VR UI setup
        Button button = newButton.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;

            // Ensure the button's Image component can receive raycasts
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.raycastTarget = true;
            }

            // Clear existing listeners and add new one
            button.onClick.RemoveAllListeners();

            // Capture variables
            string capturedName = fileName;
            string capturedPath = filePath;

            button.onClick.AddListener(() => {
                DebugLog($"BUTTON CLICKED: {capturedName}");
                OnFileButtonClicked(capturedName, capturedPath);
            });
        }

        // Set button text
        TextMeshProUGUI tmpText = newButton.GetComponentInChildren<TextMeshProUGUI>();
        if (tmpText != null) tmpText.text = fileName;
        else
        {
            Text txt = newButton.GetComponentInChildren<Text>();
            if (txt != null) txt.text = fileName;
        }

        // Force layout update
        LayoutRebuilder.MarkLayoutForRebuild(scrollViewContent.GetComponent<RectTransform>());

        instantiatedButtons.Add(newButton);
        DebugLog($"Created button for {fileName}, total buttons: {instantiatedButtons.Count}");
    }

    public void DebugButtonBlocking()
    {
        if (instantiatedButtons.Count > 0)
        {
            for (int i = 0; i < Mathf.Min(3, instantiatedButtons.Count); i++)
            {
                Button btn = instantiatedButtons[i].GetComponent<Button>();
                if (btn != null)
                {
                    bool interactable = btn.interactable;
                    bool raycastTarget = btn.GetComponent<Image>()?.raycastTarget ?? false;

                    DebugLog($"Button {i}: Interactable={interactable}, RaycastTarget={raycastTarget}");

                    // Check if something is covering this button
                    RectTransform rectTransform = btn.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        Vector3[] corners = new Vector3[4];
                        rectTransform.GetWorldCorners(corners);
                        DebugLog($"Button {i} world bounds: {corners[0]} to {corners[2]}");
                    }
                }
            }
        }
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

        // TogglePanel();
    }

    private void LoadDXFFile(string filePath)
    {
        DebugLog($"=== LoadDXFFile CALLED: {filePath} ===");
        try
        {
            string fileContent = File.ReadAllText(filePath);
            DebugLog($"DXF file size: {fileContent.Length} characters");

            List<DrawingCommand> commands = new List<DrawingCommand>();

            // Try complete parsing first
            commands = ParseDXFComplete(fileContent);
            DebugLog($"Complete parsing produced {commands.Count} commands");

            // If that fails, try simplified
            if (commands.Count == 0)
            {
                DebugLog("Complete parsing failed, trying simplified...");
                commands = ParseDXFSimplified(fileContent);
                DebugLog($"Simplified parsing produced {commands.Count} commands");
            }

            // If both fail, try extracting raw coordinates
            if (commands.Count == 0)
            {
                DebugLog("Both parsers failed, extracting raw coordinates...");
                List<Vector2> rawCoords = ExtractAllCoordinates(fileContent);
                if (rawCoords.Count > 1)
                {
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = rawCoords[0] });
                    for (int i = 1; i < rawCoords.Count; i++)
                    {
                        commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = rawCoords[i] });
                    }
                    DebugLog($"Raw coordinate extraction found {commands.Count} commands");
                }
            }

            if (commands.Count > 0)
            {
                DrawOnWhiteboard(commands);
                DebugLog($"Successfully drew {commands.Count} commands");
            }
            else
            {
                DebugLog("ERROR: Could not extract any drawable data from DXF", true);
                // Don't call TryAlternativeDXFDisplay - it creates that text message
                CreateSimpleErrorIndicator("DXF: No drawable data found");
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"ERROR reading DXF: {e.Message}", true);
            CreateSimpleErrorIndicator($"DXF Error: {e.Message}");
        }
    }

    private void CreateSimpleErrorIndicator(string message)
    {
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicator.transform.parent = drawingPlane.transform;
        indicator.transform.localPosition = new Vector3(0, 0, importedDrawingZOffset);
        indicator.transform.localScale = Vector3.one * 0.1f;
        indicator.GetComponent<Renderer>().material.color = Color.red;
        currentDrawings.Add(indicator);
        DebugLog($"Created error indicator: {message}");
    }

    private void LoadSVGFile(string filePath)
    {
        DebugLog($"=== LoadSVGFile CALLED: {filePath} ===");
        try
        {
            string svgContent = File.ReadAllText(filePath);

            // Parse XML
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(svgContent);

            List<DrawingCommand> commands = new List<DrawingCommand>();

            // Parse <line> elements
            foreach (System.Xml.XmlNode node in xmlDoc.GetElementsByTagName("line"))
            {
                if (float.TryParse(node.Attributes["x1"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x1) &&
                    float.TryParse(node.Attributes["y1"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float y1) &&
                    float.TryParse(node.Attributes["x2"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x2) &&
                    float.TryParse(node.Attributes["y2"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float y2))
                {
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = new Vector2(x1, -y1) });
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = new Vector2(x2, -y2) });
                }
            }

            // Parse <rect> elements (FIXED: Convert rectangles to lines)
            foreach (System.Xml.XmlNode node in xmlDoc.GetElementsByTagName("rect"))
            {
                if (float.TryParse(node.Attributes["x"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(node.Attributes["y"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(node.Attributes["width"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float width) &&
                    float.TryParse(node.Attributes["height"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float height))
                {
                    // Draw rectangle as 4 lines
                    Vector2 topLeft = new Vector2(x, -y);
                    Vector2 topRight = new Vector2(x + width, -y);
                    Vector2 bottomRight = new Vector2(x + width, -(y + height));
                    Vector2 bottomLeft = new Vector2(x, -(y + height));

                    // Top line
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = topLeft });
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = topRight });
                    // Right line
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = topRight });
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = bottomRight });
                    // Bottom line
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = bottomRight });
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = bottomLeft });
                    // Left line
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = bottomLeft });
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = topLeft });
                }
            }

            // Parse <circle> elements
            foreach (System.Xml.XmlNode node in xmlDoc.GetElementsByTagName("circle"))
            {
                if (float.TryParse(node.Attributes["cx"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float cx) &&
                    float.TryParse(node.Attributes["cy"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float cy) &&
                    float.TryParse(node.Attributes["r"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float r))
                {
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.Circle, position = new Vector2(cx, -cy), radius = r });
                }
            }

            // Parse <polyline> elements (FIXED: Better handling)
            foreach (System.Xml.XmlNode node in xmlDoc.GetElementsByTagName("polyline"))
            {
                string pointsAttr = node.Attributes["points"]?.Value;
                if (!string.IsNullOrEmpty(pointsAttr))
                {
                    var points = ParseSvgPoints(pointsAttr);
                    if (points.Count > 0)
                    {
                        commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = new Vector2(points[0].x, -points[0].y) });
                        for (int i = 1; i < points.Count; i++)
                        {
                            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = new Vector2(points[i].x, -points[i].y) });
                        }
                    }
                }
            }

            // Parse <polygon> elements (FIXED: Similar to polyline but close the shape)
            foreach (System.Xml.XmlNode node in xmlDoc.GetElementsByTagName("polygon"))
            {
                string pointsAttr = node.Attributes["points"]?.Value;
                if (!string.IsNullOrEmpty(pointsAttr))
                {
                    var points = ParseSvgPoints(pointsAttr);
                    if (points.Count > 0)
                    {
                        commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = new Vector2(points[0].x, -points[0].y) });
                        for (int i = 1; i < points.Count; i++)
                        {
                            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = new Vector2(points[i].x, -points[i].y) });
                        }
                        // Close the polygon
                        commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = new Vector2(points[0].x, -points[0].y) });
                    }
                }
            }

            // Parse <path> elements (IMPROVED)
            foreach (System.Xml.XmlNode node in xmlDoc.GetElementsByTagName("path"))
            {
                string d = node.Attributes["d"]?.Value;
                if (!string.IsNullOrEmpty(d))
                    ParseSvgPathImproved(d, commands);
            }

            DebugLog($"SVG produced {commands.Count} drawing commands");

            if (commands.Count > 0)
            {
                DrawOnWhiteboard(commands);
            }
            else
            {
                DebugLog("WARNING: SVG had no drawable elements", true);
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"ERROR reading SVG: {e.Message}", true);
        }
    }

    // NEW: Helper method to parse SVG points attribute
    private List<Vector2> ParseSvgPoints(string pointsAttr)
    {
        List<Vector2> points = new List<Vector2>();

        // Replace commas with spaces and split
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

    // COMPLETE DXF PARSER (IMPROVED to handle entity separation better)
    private List<DrawingCommand> ParseDXFComplete(string content)
    {
        DebugLog("=== ParseDXFComplete ===");
        List<DrawingCommand> commands = new List<DrawingCommand>();
        string[] lines = content.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        bool inEntitiesSection = false;

        for (int i = 0; i < lines.Length - 1; i++)
        {
            string line = lines[i].Trim();

            if (line == "ENTITIES")
            {
                inEntitiesSection = true;
                continue;
            }

            if (inEntitiesSection && (line == "ENDSEC" || line == "EOF"))
            {
                break;
            }

            // Only parse entities within ENTITIES section
            if (inEntitiesSection && line == "0" && i + 1 < lines.Length)
            {
                string entityType = lines[i + 1].Trim().ToUpperInvariant();

                switch (entityType)
                {
                    case "LINE":
                        var lineCommands = ParseDXFLineEntity(lines, i);
                        commands.AddRange(lineCommands);
                        break;
                    case "CIRCLE":
                        var circleCommands = ParseDXFCircleEntity(lines, i);
                        commands.AddRange(circleCommands);
                        break;
                    case "LWPOLYLINE":
                        var polyCommands = ParseDXFPolylineEntity(lines, i);
                        commands.AddRange(polyCommands);
                        break;
                    case "ARC":
                        var arcCommands = ParseDXFArcEntity(lines, i);
                        commands.AddRange(arcCommands);
                        break;
                }
            }
        }

        DebugLog($"Complete parsing found {commands.Count} commands");
        return commands;
    }
    private List<DrawingCommand> ParseDXFLineEntity(string[] lines, int startIndex)
    {
        List<DrawingCommand> commands = new List<DrawingCommand>();

        // Find the end of this entity
        int endIndex = FindEntityEnd(lines, startIndex);

        Vector2 start = Vector2.zero, end = Vector2.zero;
        bool hasStart = false, hasEnd = false;

        for (int i = startIndex + 2; i < endIndex && i < lines.Length - 1; i++)
        {
            string code = lines[i].Trim();
            if (i + 1 >= lines.Length) break;
            string value = lines[i + 1].Trim();

            switch (code)
            {
                case "10": // Start X
                    if (TryParseDxfNumber(value, out float sx)) { start.x = sx; }
                    break;
                case "20": // Start Y  
                    if (TryParseDxfNumber(value, out float sy)) { start.y = sy; hasStart = true; }
                    break;
                case "11": // End X
                    if (TryParseDxfNumber(value, out float ex)) { end.x = ex; }
                    break;
                case "21": // End Y
                    if (TryParseDxfNumber(value, out float ey)) { end.y = ey; hasEnd = true; }
                    break;
            }
        }

        if (hasStart && hasEnd && IsFinite(start) && IsFinite(end))
        {
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = start });
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = end });
            DebugLog($"LINE: ({start.x}, {start.y}) to ({end.x}, {end.y})");
        }

        return commands;
    }

    private List<DrawingCommand> ParseDXFCircleEntity(string[] lines, int startIndex)
    {
        List<DrawingCommand> commands = new List<DrawingCommand>();

        int endIndex = FindEntityEnd(lines, startIndex);

        Vector2 center = Vector2.zero;
        float radius = 0f;
        bool hasCenter = false, hasRadius = false;

        for (int i = startIndex + 2; i < endIndex && i < lines.Length - 1; i++)
        {
            string code = lines[i].Trim();
            if (i + 1 >= lines.Length) break;
            string value = lines[i + 1].Trim();

            switch (code)
            {
                case "10": // Center X
                    if (TryParseDxfNumber(value, out float cx)) { center.x = cx; }
                    break;
                case "20": // Center Y
                    if (TryParseDxfNumber(value, out float cy)) { center.y = cy; hasCenter = true; }
                    break;
                case "40": // Radius
                    if (TryParseDxfNumber(value, out float r)) { radius = r; hasRadius = true; }
                    break;
            }
        }

        if (hasCenter && hasRadius && IsFinite(center) && radius > 0)
        {
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.Circle, position = center, radius = radius });
            DebugLog($"CIRCLE: Center({center.x}, {center.y}) Radius={radius}");
        }

        return commands;
    }

    private List<DrawingCommand> ParseDXFPolylineEntity(string[] lines, int startIndex)
    {
        List<DrawingCommand> commands = new List<DrawingCommand>();

        int endIndex = FindEntityEnd(lines, startIndex);
        List<Vector2> vertices = new List<Vector2>();

        for (int i = startIndex + 2; i < endIndex && i < lines.Length - 3; i++)
        {
            string code = lines[i].Trim();
            if (code == "10" && i + 2 < lines.Length && lines[i + 2].Trim() == "20")
            {
                string xValue = lines[i + 1].Trim();
                string yValue = lines[i + 3].Trim();

                if (TryParseDxfNumber(xValue, out float x) && TryParseDxfNumber(yValue, out float y))
                {
                    Vector2 vertex = new Vector2(x, y);
                    if (IsFinite(vertex))
                    {
                        vertices.Add(vertex);
                        DebugLog($"POLYLINE vertex: ({x}, {y})");
                    }
                }
            }
        }

        if (vertices.Count > 1)
        {
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = vertices[0] });
            for (int i = 1; i < vertices.Count; i++)
            {
                commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = vertices[i] });
            }
            DebugLog($"POLYLINE: {vertices.Count} vertices");
        }

        return commands;
    }

    private List<DrawingCommand> ParseDXFArcEntity(string[] lines, int startIndex)
    {
        List<DrawingCommand> commands = new List<DrawingCommand>();

        int endIndex = FindEntityEnd(lines, startIndex);

        Vector2 center = Vector2.zero;
        float radius = 0f, startAngle = 0f, endAngle = 0f;
        bool hasCenter = false, hasRadius = false, hasAngles = false;

        for (int i = startIndex + 2; i < endIndex && i < lines.Length - 1; i++)
        {
            string code = lines[i].Trim();
            if (i + 1 >= lines.Length) break;
            string value = lines[i + 1].Trim();

            switch (code)
            {
                case "10":
                    if (TryParseDxfNumber(value, out float cx)) { center.x = cx; }
                    break;
                case "20":
                    if (TryParseDxfNumber(value, out float cy)) { center.y = cy; hasCenter = true; }
                    break;
                case "40":
                    if (TryParseDxfNumber(value, out float r)) { radius = r; hasRadius = true; }
                    break;
                case "50":
                    if (TryParseDxfNumber(value, out float sa)) { startAngle = sa; }
                    break;
                case "51":
                    if (TryParseDxfNumber(value, out float ea)) { endAngle = ea; hasAngles = true; }
                    break;
            }
        }

        if (hasCenter && hasRadius && hasAngles && IsFinite(center) && radius > 0)
        {
            // Convert arc to line segments
            int segments = 16;
            Vector2 firstPoint = center + new Vector2(radius * Mathf.Cos(startAngle * Mathf.Deg2Rad),
                                                     radius * Mathf.Sin(startAngle * Mathf.Deg2Rad));
            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = firstPoint });

            for (int i = 1; i <= segments; i++)
            {
                float angle = (startAngle + (endAngle - startAngle) * i / segments) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
                commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = point });
            }

            DebugLog($"ARC: Center({center.x}, {center.y}) R={radius} {startAngle}° to {endAngle}°");
        }

        return commands;
    }

    // Helper method to find where an entity ends
    private int FindEntityEnd(string[] lines, int startIndex)
    {
        for (int i = startIndex + 2; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "0")
            {
                return i;
            }
        }
        return lines.Length;
    }



    // SIMPLIFIED DXF PARSER - More Robust (IMPROVED to avoid connecting unrelated points)
    private List<DrawingCommand> ParseDXFSimplified(string content)
    {
        DebugLog("=== ParseDXFSimplified ===");
        List<DrawingCommand> commands = new List<DrawingCommand>();

        // Extract coordinates by entity to avoid connecting unrelated points
        List<List<Vector2>> entityGroups = ExtractCoordinatesByEntity(content);

        foreach (var group in entityGroups)
        {
            if (group.Count > 0)
            {
                commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = group[0] });
                for (int i = 1; i < group.Count; i++)
                {
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = group[i] });
                }
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

    // NEW: Extract coordinates grouped by entity to avoid connecting unrelated points
    private List<List<Vector2>> ExtractCoordinatesByEntity(string content)
    {
        List<List<Vector2>> entityGroups = new List<List<Vector2>>();
        string[] lines = content.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        List<Vector2> currentGroup = new List<Vector2>();
        bool inEntitySection = false;

        for (int i = 0; i < lines.Length - 3; i++)
        {
            string line = lines[i].Trim();

            // Check for new entity
            if (line == "0" && i + 1 < lines.Length)
            {
                string entityType = lines[i + 1].Trim().ToUpperInvariant();

                // If we were building a group, save it
                if (currentGroup.Count > 0)
                {
                    entityGroups.Add(new List<Vector2>(currentGroup));
                    currentGroup.Clear();
                }

                // Check if this is a drawable entity
                inEntitySection = (entityType == "LINE" || entityType == "LWPOLYLINE" ||
                                 entityType == "POLYLINE" || entityType == "ARC" ||
                                 entityType == "SPLINE");
                continue;
            }

            if (!inEntitySection) continue;

            string code = line;
            string value = lines[i + 1].Trim();

            // Look for X coordinates (code 10, 11, etc.)
            if ((code == "10" || code == "11") && i + 2 < lines.Length)
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
                            currentGroup.Add(p);
                            DebugLog($"Found coordinate in entity: ({x}, {y})");
                        }
                    }
                }
            }
        }

        // Don't forget the last group
        if (currentGroup.Count > 0)
        {
            entityGroups.Add(currentGroup);
        }

        return entityGroups;
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

    // Individual entity parsers (IMPROVED to handle entity boundaries better)
    private List<DrawingCommand> ParseDXFLine(string[] lines, int startIndex)
    {
        List<DrawingCommand> commands = new List<DrawingCommand>();
        Vector2 start = Vector2.zero, end = Vector2.zero;
        bool sx = false, sy = false, ex = false, ey = false;

        // Look for the end of this entity
        int endIndex = startIndex + 30;
        for (int i = startIndex + 2; i < startIndex + 50 && i < lines.Length; i++)
        {
            if (lines[i].Trim() == "0")
            {
                endIndex = i;
                break;
            }
        }

        for (int i = startIndex + 1; i < endIndex && i < lines.Length - 1; i++)
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

        // Look for the end of this entity
        int endIndex = startIndex + 20;
        for (int i = startIndex + 2; i < startIndex + 30 && i < lines.Length; i++)
        {
            if (lines[i].Trim() == "0")
            {
                endIndex = i;
                break;
            }
        }

        for (int i = startIndex + 1; i < endIndex && i < lines.Length - 1; i++)
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

        // Look for the end of this entity
        int endIndex = startIndex + 25;
        for (int i = startIndex + 2; i < startIndex + 40 && i < lines.Length; i++)
        {
            if (lines[i].Trim() == "0")
            {
                endIndex = i;
                break;
            }
        }

        for (int i = startIndex + 1; i < endIndex && i < lines.Length - 1; i++)
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

        // Look for the end of this entity - polylines can be quite long
        int endIndex = startIndex + 500;
        for (int i = startIndex + 2; i < startIndex + 1000 && i < lines.Length; i++)
        {
            if (lines[i].Trim() == "0")
            {
                endIndex = i;
                break;
            }
        }

        for (int i = startIndex + 1; i < endIndex && i < lines.Length - 3; i++)
        {
            string code = lines[i].Trim();
            string value = lines[i + 1].Trim();

            // Look for coordinate pairs
            if (code == "10" && i + 2 < lines.Length)
            {
                string nextCode = lines[i + 2].Trim();
                if (nextCode == "20" && i + 3 < lines.Length)
                {
                    if (TryParseDxfNumber(value, out float x) && TryParseDxfNumber(lines[i + 3].Trim(), out float y))
                    {
                        Vector2 p = new Vector2(x, y);
                        if (IsFinite(p)) points.Add(p);
                    }
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
        // Similar to polyline but for splines - extract control points
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
       // CreateTextVisualization(content);

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
                // Use positive Z offset to bring points in front of whiteboard
                sphere.transform.localPosition = new Vector3(point.x * 0.01f, point.y * 0.01f, importedDrawingZOffset);
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
        // Use positive Z offset to bring text in front of whiteboard
        textObj.transform.localPosition = new Vector3(0, 0, importedDrawingZOffset);

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = $"DXF File Loaded\nSize: {content.Length} chars\nClick 'Clear' to remove";
        textMesh.color = importedDrawingColor;
        textMesh.fontSize = 10;
        textMesh.anchor = TextAnchor.MiddleCenter;

        currentDrawings.Add(textObj);
    }

    // IMPROVED SVG Path Parser
    private void ParseSvgPathImproved(string d, List<DrawingCommand> commands)
    {
        if (string.IsNullOrEmpty(d)) return;

        // Clean the path data
        d = d.Replace(",", " ");
        d = Regex.Replace(d, @"([MmLlHhVvCcSsQqTtAaZz])", " $1 ");
        string[] tokens = d.Split(new char[] { ' ', '\t', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        Vector2 currentPos = Vector2.zero;
        Vector2 lastControlPoint = Vector2.zero;
        Vector2 pathStart = Vector2.zero;
        char currentCommand = 'M';
        bool isRelative = false;

        int i = 0;
        while (i < tokens.Length)
        {
            string token = tokens[i].Trim();

            // Check if it's a command
            if (char.IsLetter(token[0]) && token.Length == 1)
            {
                currentCommand = token[0];
                isRelative = char.IsLower(currentCommand);
                currentCommand = char.ToUpperInvariant(currentCommand);
                i++;
                continue;
            }

            // Parse based on current command
            switch (currentCommand)
            {
                case 'M': // MoveTo
                    if (i + 1 < tokens.Length &&
                        float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float mx) &&
                        float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float my))
                    {
                        Vector2 pos = new Vector2(mx, -my);
                        if (isRelative) pos += currentPos;

                        commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.MoveTo, position = pos });
                        currentPos = pos;
                        pathStart = pos;
                        currentCommand = 'L'; // After moveto, subsequent coordinates are lineto
                        i += 2;
                    }
                    else i++;
                    break;

                case 'L': // LineTo
                    if (i + 1 < tokens.Length &&
                        float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float lx) &&
                        float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ly))
                    {
                        Vector2 pos = new Vector2(lx, -ly);
                        if (isRelative) pos += currentPos;

                        commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = pos });
                        currentPos = pos;
                        i += 2;
                    }
                    else i++;
                    break;

                case 'H': // Horizontal LineTo
                    if (float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float hx))
                    {
                        Vector2 pos = new Vector2(isRelative ? currentPos.x + hx : hx, currentPos.y);
                        commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = pos });
                        currentPos = pos;
                        i++;
                    }
                    else i++;
                    break;

                case 'V': // Vertical LineTo
                    if (float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float vy))
                    {
                        Vector2 pos = new Vector2(currentPos.x, isRelative ? currentPos.y - vy : -vy);
                        commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = pos });
                        currentPos = pos;
                        i++;
                    }
                    else i++;
                    break;

                case 'Z': // ClosePath
                    commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = pathStart });
                    currentPos = pathStart;
                    i++;
                    break;

                case 'C': // CubicBezier - approximate with line segments
                    if (i + 5 < tokens.Length &&
                        float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float c1x) &&
                        float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float c1y) &&
                        float.TryParse(tokens[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out float c2x) &&
                        float.TryParse(tokens[i + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out float c2y) &&
                        float.TryParse(tokens[i + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx) &&
                        float.TryParse(tokens[i + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out float cy))
                    {
                        Vector2 cp1 = new Vector2(c1x, -c1y);
                        Vector2 cp2 = new Vector2(c2x, -c2y);
                        Vector2 endPos = new Vector2(cx, -cy);

                        if (isRelative)
                        {
                            cp1 += currentPos;
                            cp2 += currentPos;
                            endPos += currentPos;
                        }

                        // Approximate cubic bezier with line segments
                        ApproximateCubicBezier(currentPos, cp1, cp2, endPos, commands);
                        currentPos = endPos;
                        lastControlPoint = cp2;
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
        int segments = 10;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float oneMinusT = 1f - t;

            Vector2 point = oneMinusT * oneMinusT * oneMinusT * p0 +
                           3f * oneMinusT * oneMinusT * t * p1 +
                           3f * oneMinusT * t * t * p2 +
                           t * t * t * p3;

            commands.Add(new DrawingCommand { type = DrawingCommand.CommandType.LineTo, position = point });
        }
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
            Vector2 pos = cmd.position;
            if (cmd.type == DrawingCommand.CommandType.Circle)
            {
                // Include circle bounds
                min = Vector2.Min(min, pos - Vector2.one * cmd.radius);
                max = Vector2.Max(max, pos + Vector2.one * cmd.radius);
            }
            else
            {
                min = Vector2.Min(min, pos);
                max = Vector2.Max(max, pos);
            }
        }

        Vector2 drawingSize = max - min;
        // Guard against zero-size to avoid division by zero
        if (drawingSize.x == 0f) drawingSize.x = 1e-6f;
        if (drawingSize.y == 0f) drawingSize.y = 1e-6f;
        Vector2 drawingCenter = (min + max) / 2f;

        float safeWidth = Mathf.Max(drawingSize.x, 0.001f);
        float safeHeight = Mathf.Max(drawingSize.y, 0.001f);

        float scaleX = (whiteboardWidth * 0.8f) / safeWidth;
        float scaleY = (whiteboardHeight * 0.8f) / safeHeight;
        float scale = Mathf.Min(scaleX, scaleY);

        // Clamp scale to a sane range
        scale = Mathf.Clamp(scale, 0.001f, 10f);

        if (!float.IsFinite(scale) || scale <= 0)
            scale = 0.001f; // fallback

        DebugLog($"Drawing bounds: min={min}, max={max}, size={drawingSize}, scale={scale}");

        // IMPROVED: Separate continuous paths to avoid unwanted lines
        List<List<DrawingCommand>> paths = SeparatePaths(safe);

        foreach (var path in paths)
        {
            if (path.Count == 0) continue;

            LineRenderer currentLine = null;
            List<Vector3> points = new List<Vector3>();

            foreach (var cmd in path)
            {
                Vector2 scaledPos = (cmd.position - drawingCenter) * scale + whiteboardCenter;
                Vector3 worldPos = drawingPlane.transform.TransformPoint(
                    new Vector3(scaledPos.x, scaledPos.y, importedDrawingZOffset)
                );

                if (cmd.type == DrawingCommand.CommandType.MoveTo)
                {
                    // Finish previous line if it exists
                    if (currentLine != null && points.Count > 1)
                        FinishLineRenderer(currentLine, points);

                    // Start new line
                    currentLine = CreateNewLineRenderer();
                    points.Clear();
                    points.Add(worldPos);
                }
                else if (cmd.type == DrawingCommand.CommandType.LineTo)
                {
                    if (currentLine == null)
                    {
                        currentLine = CreateNewLineRenderer();
                        points.Clear();
                    }
                    points.Add(worldPos);
                }
                else if (cmd.type == DrawingCommand.CommandType.Circle)
                {
                    float scaledRadius = cmd.radius * scale;
                    if (scaledRadius > 0f && float.IsFinite(scaledRadius))
                        DrawCircleAsLineRenderer(scaledPos, scaledRadius);
                }
            }

            // Finish the last line in this path
            if (currentLine != null && points.Count > 1)
            {
                FinishLineRenderer(currentLine, points);
            }
        }
    }

    // NEW: Separate drawing commands into distinct paths to avoid connecting unrelated elements
    private List<List<DrawingCommand>> SeparatePaths(List<DrawingCommand> commands)
    {
        List<List<DrawingCommand>> paths = new List<List<DrawingCommand>>();
        List<DrawingCommand> currentPath = new List<DrawingCommand>();

        foreach (var cmd in commands)
        {
            if (cmd.type == DrawingCommand.CommandType.Circle)
            {
                // Circles are standalone - finish current path and add circle as separate path
                if (currentPath.Count > 0)
                {
                    paths.Add(new List<DrawingCommand>(currentPath));
                    currentPath.Clear();
                }
                paths.Add(new List<DrawingCommand> { cmd });
            }
            else if (cmd.type == DrawingCommand.CommandType.MoveTo)
            {
                // MoveTo starts a new path - finish current and start new
                if (currentPath.Count > 0)
                {
                    paths.Add(new List<DrawingCommand>(currentPath));
                    currentPath.Clear();
                }
                currentPath.Add(cmd);
            }
            else if (cmd.type == DrawingCommand.CommandType.LineTo)
            {
                // LineTo continues current path
                currentPath.Add(cmd);
            }
        }

        // Don't forget the last path
        if (currentPath.Count > 0)
        {
            paths.Add(currentPath);
        }

        return paths;
    }

    private LineRenderer CreateNewLineRenderer()
    {
        GameObject lineObj = Instantiate(lineRendererPrefab.gameObject, drawingPlane.transform);
        LineRenderer lr = lineObj.GetComponent<LineRenderer>();

        if (lr.material != null)
            lr.material.color = importedDrawingColor;

        lr.startWidth = lr.endWidth = 0.02f;
        lr.useWorldSpace = true;
        lineObj.transform.SetParent(drawingPlane.transform, false);

        // Set sorting layer or render queue to ensure it renders on top
        if (lr.material != null)
        {
            lr.material.renderQueue = 3000; // Render after most objects
        }

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
        else if (pts.Count == 1)
        {
            // Single point - don't display or make it a tiny line
            lr.positionCount = 0;
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
            points[i] = drawingPlane.transform.TransformPoint(new Vector3(
                center.x + radius * Mathf.Cos(angle),
                center.y + radius * Mathf.Sin(angle),
                importedDrawingZOffset
            ));
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
            if (btn != null && btn != fileButtonPrefab.gameObject)
            {
                Destroy(btn);
            }
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