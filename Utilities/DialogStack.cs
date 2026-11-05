using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReviFlash.Utilities;

/// <summary> The dialogs open over a page, newest on top; each one's task finishes with the result it closes with. </summary>
public sealed class DialogStack<T> where T : class
{
    private readonly List<(T Dialog, TaskCompletionSource<object?> Result)> _open = [];

    public bool IsOpen => _open.Count > 0;

    public T? Top => _open.Count > 0 ? _open[^1].Dialog : null;

    public bool Contains(T dialog) => _open.Exists(entry => entry.Dialog == dialog);

    /// <summary> Whether something is hidden behind a dialog: the page (<paramref name="within"/> null) while any is open, or a dialog that isn't on top. </summary>
    public bool IsCovered(T? within) => IsOpen && Top != within;

    public Task<object?> Push(T dialog)
    {
        if (Contains(dialog)) throw new InvalidOperationException("That dialog is already open.");

        // Callers carry on only after the dialog has left the screen.
        var result = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _open.Add((dialog, result));
        return result.Task;
    }

    /// <returns> False if the dialog wasn't open. </returns>
    public bool Close(T dialog, object? result)
    {
        int index = _open.FindIndex(entry => entry.Dialog == dialog);
        if (index < 0) return false;

        var pending = _open[index].Result;
        _open.RemoveAt(index);
        pending.SetResult(result);
        return true;
    }
}
