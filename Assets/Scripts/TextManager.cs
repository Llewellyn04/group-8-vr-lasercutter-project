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

            textHistory.Add(new VectorTextEntry(
                newText,
                newTextObj.rectTransform.anchoredPosition,
                Mathf.RoundToInt(newTextObj.fontSize)
            ));
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
        // Check for mouse click using new Input System
        if (UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = mousePos;

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            Debug.Log($"[TextManager] === CLICK at {mousePos} - Hit {results.Count} objects ===");
            foreach (var result in results)
            {
                Debug.Log($"[TextManager] Hit: {result.gameObject.name} (has DraggableText: {result.gameObject.GetComponent<DraggableText>() != null})");
            }
        }
    }
}
