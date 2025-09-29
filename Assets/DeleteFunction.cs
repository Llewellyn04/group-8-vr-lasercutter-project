using UnityEngine;

using UnityEngine.UI;

using UnityEngine.EventSystems;

// Handles selection & outline

public class SelectorManager : MonoBehaviour, IPointerClickHandler

{

    public static GameObject selectedObject;

    private static Outline lastOutline;

    public void OnPointerClick(PointerEventData eventData)

    {

        // Remove previous outline

        if (lastOutline != null)

            Destroy(lastOutline);

        // Set this object as selected

        selectedObject = gameObject;

        // Add yellow outline

        Outline outline = gameObject.AddComponent<Outline>();

        outline.effectColor = Color.yellow;

        outline.effectDistance = new Vector2(5f, 5f);

        lastOutline = outline;

    }



}

// Handles deleting the selected object

public class DeleteFunction : MonoBehaviour

{

    public void DeleteSelectedObject()

    {

        if (SelectorManager.selectedObject != null)

        {

            Destroy(SelectorManager.selectedObject);

            SelectorManager.selectedObject = null;

        }

        else

        {

            Debug.Log("No object selected!");

        }

    }

}


