using System.Collections.Generic;
using UnityEngine;

public class RedoUndoManager : MonoBehaviour
{
    private Stack<GameObject> undoStack = new Stack<GameObject>();
    private Stack<GameObject> redoStack = new Stack<GameObject>();

    public int UndoCount => undoStack.Count;
    public int RedoCount => redoStack.Count;

    // Register a newly drawn line
    public void RegisterLine(GameObject line)
    {
        if (line == null)
        {
            Debug.LogError("Attempted to register a null line!");
            return;
        }

        undoStack.Push(line);
        redoStack.Clear(); // Clear redo when a new action occurs
        Debug.Log($"✓ Registered line: {line.name}. Undo stack count: {undoStack.Count}");
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("Undo stack is empty!");
            return;
        }

        GameObject line = undoStack.Pop();

        if (line != null)
        {
            line.SetActive(false);
            redoStack.Push(line);
            Debug.Log($"✓ Undo: Hid line '{line.name}'. Undo stack: {undoStack.Count}, Redo stack: {redoStack.Count}");
        }
        else
        {
            Debug.LogWarning("Undo: Line was null (already destroyed?)");
        }
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            Debug.Log("Redo stack is empty!");
            return;
        }

        GameObject line = redoStack.Pop();

        if (line != null)
        {
            line.SetActive(true);
            undoStack.Push(line);
            Debug.Log($"✓ Redo: Showed line '{line.name}'. Undo stack: {undoStack.Count}, Redo stack: {redoStack.Count}");
        }
        else
        {
            Debug.LogWarning("Redo: Line was null (already destroyed?)");
        }
    }

    public void ClearAll()
    {
        int clearedCount = 0;

        foreach (var line in undoStack)
        {
            if (line != null)
            {
                line.SetActive(false);
                clearedCount++;
            }
        }

        foreach (var line in redoStack)
        {
            if (line != null)
            {
                line.SetActive(false);
                clearedCount++;
            }
        }

        undoStack.Clear();
        redoStack.Clear();

        Debug.Log($"✓ Cleared {clearedCount} lines. Stacks cleared.");
    }
}