using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

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
        textInputField.gameObject.SetActive(false);
        openInputButton.onClick.AddListener(ShowInputField);
        textInputField.onEndEdit.AddListener(OnTextEntered);
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
            // Spawn a new TMP text object on the whiteboard
            TextMeshProUGUI newTextObj = Instantiate(textPrefab, whiteboardArea);
            newTextObj.text = newText;

            // Position: center for now, can later be moved around
            newTextObj.rectTransform.anchoredPosition = Vector2.zero;

            // Save entry for export
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
}
