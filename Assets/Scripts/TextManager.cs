using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class WhiteboardTextManager : MonoBehaviour
{
    [System.Serializable]
    public struct VectorTextEntry
    {
        public string content;
        public Vector2 position;
        public int fontSize;

        public VectorTextEntry(string c, Vector2 p, int f)
        {
            content = c;
            position = p;
            fontSize = f;
        }
    }

    [Header("UI References")]
    public Button openInputButton;              // Button to add text
    public TMP_InputField textInputField;       // Input field
    public RectTransform whiteboardArea;        // Parent where text objects go
    public TextMeshProUGUI textPrefab;          // Prefab for new text objects

    [Header("VR Controller + Plane References")]
    public Transform whiteboardPlane;           // Same plane used by drawing tools
    public Transform drawingTip;                // Controller tip used for ray/plane hit
    public RedoUndoManager redoUndoManager;     // Shared undo/redo manager (lines + text)

    [Header("Controller Actions (match SplineVRDraw)")]
    public InputActionProperty dragAction;
    public InputActionProperty resizeAction;
    public InputActionProperty undoAction;
    public InputActionProperty redoAction;

    [Header("Export Handling")]
    public List<VectorTextEntry> textHistory = new List<VectorTextEntry>();

    void Start()
    {
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
    if (listeners.Length > 1)
    {
        for (int i = 1; i < listeners.Length; i++)
        {
            listeners[i].enabled = false;
        }
        Debug.Log($"[TextManager] Disabled {listeners.Length - 1} extra Audio Listeners");
    }
    
    VerifySetup();
    
    textInputField.gameObject.SetActive(false);
    openInputButton.onClick.AddListener(ShowInputField);
    textInputField.onEndEdit.AddListener(OnTextEntered);
    }

    void OnEnable()
    {
        if (dragAction.action != null) dragAction.action.Enable();
        if (resizeAction.action != null) resizeAction.action.Enable();
        if (undoAction.action != null) undoAction.action.Enable();
        if (redoAction.action != null) redoAction.action.Enable();
    }

    void OnDisable()
    {
        if (dragAction.action != null) dragAction.action.Disable();
        if (resizeAction.action != null) resizeAction.action.Disable();
        if (undoAction.action != null) undoAction.action.Disable();
        if (redoAction.action != null) redoAction.action.Disable();
    }
    public void VerifySetup()
    {
        Debug.Log("=== WHITEBOARD SETUP CHECK ===");

        // Check Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        Debug.Log($"Canvas found: {canvas != null}");
        if (canvas != null)
        {
            Debug.Log($"Canvas render mode: {canvas.renderMode}");
        }

        // Check GraphicRaycaster
        GraphicRaycaster raycaster = GetComponentInParent<GraphicRaycaster>();
        Debug.Log($"GraphicRaycaster found: {raycaster != null}");

        // Check EventSystem
        Debug.Log($"EventSystem exists: {EventSystem.current != null}");

        // Check prefab
        Debug.Log($"Text prefab assigned: {textPrefab != null}");
        if (textPrefab != null)
        {
            Debug.Log($"Text prefab has TMP: {textPrefab.GetComponent<TextMeshProUGUI>() != null}");
        }

        // Check whiteboard
        Debug.Log($"Whiteboard area assigned: {whiteboardArea != null}");

        Debug.Log("=== END SETUP CHECK ===");
    }

    private void ShowInputField()
    {
        textInputField.gameObject.SetActive(true);
        textInputField.ActivateInputField();
    }

    private void OnTextEntered(string newText)
    {
        if (!string.IsNullOrWhiteSpace(newText))
        {
            TextMeshProUGUI newTextObj = Instantiate(textPrefab, whiteboardArea);
            newTextObj.text = newText;

            // Make text smaller - adjust these values
            newTextObj.rectTransform.sizeDelta = new Vector2(50, 20);  // Even smaller
            newTextObj.fontSize = 12;
            newTextObj.color = Color.black;
            newTextObj.rectTransform.anchoredPosition = Vector2.zero;
            newTextObj.raycastTarget = true;
            newTextObj.transform.SetAsLastSibling();

            DraggableText draggable = newTextObj.gameObject.AddComponent<DraggableText>();
            draggable.Initialize(whiteboardArea);
            // Provide controller + plane references so text can be dragged/resized like splines
            draggable.whiteboardPlane = whiteboardPlane;
            draggable.drawingTip = drawingTip;
            draggable.dragAction = dragAction;
            draggable.resizeAction = resizeAction;

            textHistory.Add(new VectorTextEntry(
                newText,
                newTextObj.rectTransform.anchoredPosition,
                Mathf.RoundToInt(newTextObj.fontSize)
            ));

            // Register with the shared RedoUndoManager so Undo/Redo/Clear affects text too
            if (redoUndoManager != null)
            {
                redoUndoManager.RegisterLine(newTextObj.gameObject);
            }
        }

        textInputField.text = string.Empty;
        textInputField.gameObject.SetActive(false);

    }
    // Export to SVG
    public string ExportToSVG(int width = 800, int height = 600)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\">");

        foreach (var entry in textHistory)
        {
            float svgX = entry.position.x + (width / 2f);
            float svgY = height / 2f - entry.position.y;

            sb.AppendLine(
                $"  <text x=\"{svgX}\" y=\"{svgY}\" font-size=\"{entry.fontSize}\" font-family=\"Arial\">{entry.content}</text>"
            );
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }
    void Update()
    {
        // Optionally mirror Undo/Redo controller buttons for text-only scenes
        if (redoUndoManager != null)
        {
            bool undoPressed = false;
            bool redoPressed = false;
            try { if (undoAction.action != null) undoPressed = undoAction.action.ReadValue<float>() > 0.5f; } catch { undoPressed = false; }
            try { if (redoAction.action != null) redoPressed = redoAction.action.ReadValue<float>() > 0.5f; } catch { redoPressed = false; }

            // Local edge detection so actions fire once per press
            if (undoPressed && !_lastUndo)
                redoUndoManager.Undo();
            if (redoPressed && !_lastRedo)
                redoUndoManager.Redo();

            _lastUndo = undoPressed;
            _lastRedo = redoPressed;
        }
    }

    // Local press edge tracking for fallback bindings
    private bool _lastUndo = false;
    private bool _lastRedo = false;
}
