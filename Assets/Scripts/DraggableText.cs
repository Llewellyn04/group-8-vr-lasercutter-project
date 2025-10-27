using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class DraggableText : MonoBehaviour
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private RectTransform whiteboardRect;
    private Camera eventCamera;

    // Mouse dragging state
    private bool isMouseDragging = false;
    private Vector2 dragStartOffset;

    // Controller-based drag/resize (match SplineVRDraw interaction style)
    [Header("Controller + Plane References")]
    public Transform whiteboardPlane;      // Used to project controller tip onto plane
    public Transform drawingTip;           // Controller tip transform
    public InputActionProperty dragAction; // Press to drag
    public InputActionProperty resizeAction; // Press to resize

    [Header("Resize Settings")]
    public float minScale = 0.2f;
    public float maxScale = 5f;

    private bool isControllerDragging = false;
    private Vector2 controllerDragStartLocal; // local to whiteboardRect

    private bool isResizing = false;
    private Vector2 resizePivotLocal; // local to whiteboardRect
    private float resizeStartRadius = 0f; // distance from pivot to pointer at start
    private Vector3 startLocalScale = Vector3.one;

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

    private void OnEnable()
    {
        if (dragAction.action != null) dragAction.action.Enable();
        if (resizeAction.action != null) resizeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (dragAction.action != null) dragAction.action.Disable();
        if (resizeAction.action != null) resizeAction.action.Disable();
    }

    private void Update()
    {
        // If an input field is focused, stop interactions
        TMP_InputField activeInput = UnityEngine.Object.FindFirstObjectByType<TMP_InputField>();
        if (activeInput != null && activeInput.isFocused)
        {
            isMouseDragging = false;
            EndControllerDrag();
            EndResize();
            return;
        }

        HandleMouseDrag();
        HandleControllerDragAndResize();
    }

    // ================= Mouse drag (existing) =================
    private void HandleMouseDrag()
    {
        if (Mouse.current == null || whiteboardRect == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        bool mouseDown = Mouse.current.leftButton.isPressed;
        bool mouseJustPressed = Mouse.current.leftButton.wasPressedThisFrame;

        Camera cam = (canvas != null && canvas.renderMode == RenderMode.WorldSpace) ? eventCamera : null;
        bool isOver = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, mousePos, cam);

        if (mouseJustPressed && isOver && !isMouseDragging)
        {
            isMouseDragging = true;

            Vector2 localMousePoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent as RectTransform,
                mousePos,
                cam,
                out localMousePoint
            );

            dragStartOffset = rectTransform.anchoredPosition - localMousePoint;
        }

        if ((!mouseDown) && isMouseDragging)
        {
            isMouseDragging = false;
        }

        if (isMouseDragging && mouseDown)
        {
            Vector2 localMousePoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent as RectTransform,
                mousePos,
                cam,
                out localMousePoint
            );

            Vector2 newPosition = localMousePoint + dragStartOffset;
            rectTransform.anchoredPosition = ClampToWhiteboard(newPosition);
        }
    }

    // ================= Controller drag/resize =================
    private void HandleControllerDragAndResize()
    {
        if (whiteboardPlane == null || drawingTip == null || whiteboardRect == null)
            return;

        // Project controller tip onto the whiteboard plane
        if (!TryGetPointerOnBoard(out Vector3 hitWorld, out Vector2 hitLocalRect))
        {
            if (!IsActionPressed(dragAction) && isControllerDragging) EndControllerDrag();
            if (!IsActionPressed(resizeAction) && isResizing) EndResize();
            return;
        }

        // Drag
        if (IsActionPressed(dragAction))
        {
            if (!isControllerDragging)
            {
                if (IsPointerOverThisText(hitLocalRect))
                {
                    isControllerDragging = true;
                    controllerDragStartLocal = hitLocalRect;
                    dragStartOffset = rectTransform.anchoredPosition - controllerDragStartLocal;
                }
            }
            else
            {
                Vector2 newPos = hitLocalRect + dragStartOffset;
                rectTransform.anchoredPosition = ClampToWhiteboard(newPos);
            }
        }
        else if (isControllerDragging)
        {
            EndControllerDrag();
        }

        // Resize
        if (IsActionPressed(resizeAction))
        {
            if (!isResizing)
            {
                if (IsPointerOverThisText(hitLocalRect))
                {
                    isResizing = true;
                    resizePivotLocal = rectTransform.anchoredPosition; // scale around center
                    resizeStartRadius = Mathf.Max(Vector2.Distance(hitLocalRect, resizePivotLocal), 0.0001f);
                    startLocalScale = rectTransform.localScale;
                }
            }
            else
            {
                float currentRadius = Mathf.Max(Vector2.Distance(hitLocalRect, resizePivotLocal), 0.0001f);
                float scale = currentRadius / resizeStartRadius;
                float clamped = Mathf.Clamp(scale, minScale, maxScale);
                rectTransform.localScale = startLocalScale * clamped;
            }
        }
        else if (isResizing)
        {
            EndResize();
        }
    }

    private bool TryGetPointerOnBoard(out Vector3 worldPoint, out Vector2 localPointRect)
    {
        worldPoint = Vector3.zero;
        localPointRect = Vector2.zero;
        Plane plane = new Plane(whiteboardPlane.forward, whiteboardPlane.position);
        Ray ray = new Ray(drawingTip.position - drawingTip.forward * 0.05f, drawingTip.forward);
        if (plane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            Vector3 localPlane = whiteboardPlane.InverseTransformPoint(worldPoint);
            // Map plane local XY to whiteboard Rect local XY (assumes aligned/scaled setup)
            // Whiteboard Rect is centered, anchoredPosition is in its local space
            localPointRect = new Vector2(localPlane.x, localPlane.y);
            return true;
        }
        return false;
    }

    private bool IsPointerOverThisText(Vector2 pointerLocalRect)
    {
        Vector2 half = rectTransform.sizeDelta * 0.5f;
        Vector2 rel = pointerLocalRect - rectTransform.anchoredPosition;
        return Mathf.Abs(rel.x) <= half.x && Mathf.Abs(rel.y) <= half.y;
    }

    private Vector2 ClampToWhiteboard(Vector2 pos)
    {
        Vector2 whiteboardSize = whiteboardRect.rect.size;
        Vector2 textSize = rectTransform.sizeDelta * rectTransform.localScale;
        float minX = -whiteboardSize.x / 2f + textSize.x / 2f;
        float maxX = whiteboardSize.x / 2f - textSize.x / 2f;
        float minY = -whiteboardSize.y / 2f + textSize.y / 2f;
        float maxY = whiteboardSize.y / 2f - textSize.y / 2f;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        return pos;
    }

    private bool IsActionPressed(InputActionProperty action)
    {
        if (action.action == null) return false;
        try { return action.action.ReadValue<float>() > 0.5f; } catch { return false; }
    }

    private void EndControllerDrag()
    {
        isControllerDragging = false;
    }

    private void EndResize()
    {
        isResizing = false;
        resizeStartRadius = 0f;
    }
}
