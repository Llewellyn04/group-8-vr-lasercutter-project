using UnityEngine;
using UnityEngine.EventSystems;

public class ClickTest : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"*** CLICK TEST SUCCESS on {gameObject.name} ***");
    }

    private void OnEnable()
    {
        Debug.Log($"[ClickTest] Enabled on {gameObject.name}");
    }
}