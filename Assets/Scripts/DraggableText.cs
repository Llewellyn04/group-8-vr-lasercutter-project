using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class DraggableText : MonoBehaviour
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private RectTransform whiteboardRect;
    private bool isDragging = false;
    private Vector2 dragStartOffset;
    private Camera eventCamera;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            eventCamera = Camera.main;
        }
    }

    public void Initialize(RectTransform whiteboard)
    {
        whiteboardRect = whiteboard;
        Debug.Log($"[DraggableText] Whiteboard size: {whiteboard.rect.size}");
        Debug.Log($"[DraggableText] Text size: {rectTransform.sizeDelta}");
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        // Check if input field is active - if so, don't drag
        TMP_InputField activeInput = UnityEngine.Object.FindFirstObjectByType<TMP_InputField>();
        if (activeInput != null && activeInput.isFocused)
        {
            isDragging = false;
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        bool mouseDown = Mouse.current.leftButton.isPressed;
        bool mouseJustPressed = Mouse.current.leftButton.wasPressedThisFrame;

        Camera cam = (canvas.renderMode == RenderMode.WorldSpace) ? eventCamera : null;
        bool isOver = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, mousePos, cam);

        if (mouseJustPressed && isOver && !isDragging)
        {
            isDragging = true;

            Vector2 localMousePoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent as RectTransform,
                mousePos,
                cam,
                out localMousePoint
            );

            dragStartOffset = rectTransform.anchoredPosition - localMousePoint;
        }

        // Stop dragging when mouse released OR when mouse leaves the screen
        if ((!mouseDown || !isOver) && isDragging)
        {
            isDragging = false;
        }

        if (isDragging && mouseDown)
        {
            Vector2 localMousePoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent as RectTransform,
                mousePos,
                cam,
                out localMousePoint
            );

            Vector2 newPosition = localMousePoint + dragStartOffset;

            // Clamp with better math
            if (whiteboardRect != null)
            {
                Vector2 whiteboardSize = whiteboardRect.rect.size;
                Vector2 textSize = rectTransform.sizeDelta;

                // Calculate actual boundaries
                float minX = -whiteboardSize.x / 2f + textSize.x / 2f;
                float maxX = whiteboardSize.x / 2f - textSize.x / 2f;
                float minY = -whiteboardSize.y / 2f + textSize.y / 2f;
                float maxY = whiteboardSize.y / 2f - textSize.y / 2f;

                newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
                newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);

                Debug.Log($"[DraggableText] Position: {newPosition}, Bounds: X[{minX}, {maxX}] Y[{minY}, {maxY}]");
            }

            rectTransform.anchoredPosition = newPosition;
        }
    }
}