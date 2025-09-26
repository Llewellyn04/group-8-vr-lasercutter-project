using UnityEngine;
using UnityEngine.UI;

public class ClearCanvas : MonoBehaviour
{
    public Canvas mainCanvas;

    public void ClearCanvas()
    {
        if (mainCanvas != null)
        {
            Debug.LogWarning("Main Canvas has not been assigned in the inspector.");
            return;
        }

        for(int i = mainCanvas.transform.childCount-1; i >= 0; i--)//looping backwards to avoid issues with changing child count while iterating
        {
            Transform child = mainCanvas.transform.GetChild(i);
            if(child.gameObject != mainCanvas.gameObject)
            {
                Destroy(mainCanvas.transform.GetChild(i).gameObject);
            }
        }
    }


}