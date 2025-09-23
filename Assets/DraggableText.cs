using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class DraggableText : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] private RectTransform dragBounds; // Assign WhiteBoard RectTransform
    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2 pointerOffset;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    private Camera UICamera =>
        (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

    public void OnPointerDown(PointerEventData eventData)
    {
        // Get pointer offset relative to object
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            eventData.position,
            UICamera,
            out var localPoint
        );

        pointerOffset = rectTransform.localPosition - (Vector3)localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragBounds,
            eventData.position,
            UICamera,
            out var localPoint))
        {
            Vector3 newPos = localPoint + (Vector2)pointerOffset;

            // Clamp inside whiteboard
            Vector3 min = dragBounds.rect.min;
            Vector3 max = dragBounds.rect.max;

            // Adjust for text pivot and size
            float halfWidth = rectTransform.rect.width * rectTransform.pivot.x;
            float halfHeight = rectTransform.rect.height * rectTransform.pivot.y;

            newPos.x = Mathf.Clamp(newPos.x, min.x + halfWidth, max.x - halfWidth);
            newPos.y = Mathf.Clamp(newPos.y, min.y + halfHeight, max.y - halfHeight);

            rectTransform.localPosition = newPos;
        }
    }
}
