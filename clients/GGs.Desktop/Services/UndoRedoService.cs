using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GGs.Shared.Tweaks;

namespace GGs.Desktop.Services;

public class UndoRedoService
{
    private readonly TweakExecutionService _executor;
    private readonly Stack<TweakDefinition> _undoStack = new();
    private readonly Stack<TweakDefinition> _redoStack = new();

    public UndoRedoService(TweakExecutionService executor)
    {
        _executor = executor;
    }

    public async Task<bool> UndoAsync()
    {
        if (_undoStack.Count == 0) return false;

        var tweak = _undoStack.Pop();
        _redoStack.Push(tweak);
        return await _executor.UndoTweakAsync(tweak);
    }

    public async Task<bool> RedoAsync()
    {
        if (_redoStack.Count == 0) return false;

        var tweak = _redoStack.Pop();
        _undoStack.Push(tweak);
        var result = await _executor.ExecuteTweakAsync(tweak);
        return result?.Success ?? false;
    }

    public void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void RecordAction(string action, TweakApplicationLog log)
    {
        // Stub implementation for compilation
    }

    public void RecordAction(string action, bool success)
    {
        // Stub implementation for compilation
    }

    public void RecordAction(string action, bool success, string error)
    {
        // Stub implementation for compilation
    }
}