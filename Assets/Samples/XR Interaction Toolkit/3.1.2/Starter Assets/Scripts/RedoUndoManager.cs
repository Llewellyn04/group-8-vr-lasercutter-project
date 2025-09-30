using System.Collections.Generic;
using UnityEngine;

public class RedoUndoManager : MonoBehaviour
{
    private Stack<GameObject> undoStack = new Stack<GameObject>();
    private Stack<GameObject> redoStack = new Stack<GameObject>();

    /// <summary>
    /// Call this when a drawing is finished
    /// </summary>
    public void RegisterLine(GameObject lineObj)
    {
        undoStack.Push(lineObj);
        redoStack.Clear(); // Once you draw something new, redo history resets
    }

    /// <summary>
    /// Undo last drawing
    /// </summary>
    public void Undo()
    {
        if (undoStack.Count > 0)
        {
            GameObject lastLine = undoStack.Pop();
            lastLine.SetActive(false); // Hide instead of destroying
            redoStack.Push(lastLine);
        }
    }

    /// <summary>
    /// Redo last undone drawing
    /// </summary>
    public void Redo()
    {
        if (redoStack.Count > 0)
        {
            GameObject redoLine = redoStack.Pop();
            redoLine.SetActive(true);
            undoStack.Push(redoLine);
        }
    }

    /// <summary>
    /// Optional: clear everything
    /// </summary>
    public void ClearAll()
    {
        foreach (var line in undoStack) Destroy(line);
        foreach (var line in redoStack) Destroy(line);
        undoStack.Clear();
        redoStack.Clear();
    }
}

