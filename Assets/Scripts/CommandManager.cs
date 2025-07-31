using UnityEngine;
using System.Collections.Generic;
public class CommandManager: MonoBehaviour//allowing interaction with objects in Unity
{
    private Stack<ICommand> undoStack = new Stack<ICommand>();
    private Stack<ICommand> redoStack = new Stack<ICommand>();

    public static CommandManager Instance { get; private set; }
    private void Awake() //ensuring that there is only one instance of CommandManager
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep this instance across scenes
        }
        
    }

    public void ExecuteCommand(ICommand command)//method for executing command/action
    {
        command.Execute();
        undoStack.Push(command);
        redoStack.Clear(); // Clear redo stack after a new command is executed
    }

    public void Undo() //method for undoing the last command
    {
        if (undoStack.Count > 0)//checking if there is anything in the stack
        {
            ICommand command = undoStack.Pop();
            command.Undo();
            redoStack.Push(command);
        }
        else
        {
            Debug.LogWarning("No commands to undo.");
        }
    }

    public void Redo() //method for redoing the last undone command
    {
        if (redoStack.Count > 0)//checking if there is anything in the stack
        {
            ICommand command = redoStack.Pop();
            command.Execute();
            undoStack.Push(command);
        }
        else
        {
            Debug.LogWarning("No commands to redo.");
        }
    }
}
