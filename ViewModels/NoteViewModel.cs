using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> A note paragraph; RenderedText only updates when editing finishes so the maths isn't re-rendered per key. </summary>
public partial class NoteBlock : ObservableObject
{
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [ObservableProperty] private string _text;

    [ObservableProperty] private string _renderedText;
    [ObservableProperty] private bool _isEditing;

    /// <summary> Where to put the cursor when editing starts: 0 for the start, -1 for the end. </summary>
    public int PendingCaret { get; set; } = -1;

    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);

    public NoteBlock(string text)
    {
        _text = text;
        _renderedText = text;
    }
}

/// <summary> A line in the note's contents panel: a heading and the paragraph it's in. </summary>
public sealed record ContentsEntry(int Level, string Source, NoteBlock Block)
{
    public Thickness Indent => new((Level - 1) * 14, 0, 0, 0);
    public double FontSize => Level switch { 1 => 15, 2 => 14, _ => 13 };
}

/// <summary> A note open as a page: paragraphs edited one at a time, a contents panel from its headings, and autosave. </summary>
public partial class NoteViewModel : ViewModelBase
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    // Study time stops counting after this long with no typing, clicking or scrolling.
    private static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(2);
    private const int StudyTickSeconds = 5;
    private const int StudyFlushSeconds = 60;

    private readonly Note _note;
    private readonly Action _onClose;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _studyTimer;
    private bool _dirty;
    private bool _closed;
    private DateTime _lastActivity = DateTime.Now;
    private int _unsavedStudySeconds;

    /// <summary> Set by the view from its window, so time only counts while ReviFlash is the focused window. </summary>
    public bool IsWindowActive { get; set; } = true;

    public ObservableCollection<NoteBlock> Blocks { get; } = [];
    public ObservableCollection<ContentsEntry> Contents { get; } = [];

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _statusText;
    [ObservableProperty] private bool _showContents = true;

    [NotifyPropertyChangedFor(nameof(IsEditingBlock))]
    [ObservableProperty] private NoteBlock? _editingBlock;

    public bool IsEditingBlock => EditingBlock is not null;
    public bool HasContents => Contents.Count > 0;

    public NoteViewModel(Note note, Action onClose)
    {
        _note = note;
        _onClose = onClose;
        _name = note.Name;

        var content = NoteRepository.GetContent(note.ID);
        foreach (var text in NoteText.SplitBlocks(content)) AddBlock(Blocks.Count, text);

        _statusText = string.IsNullOrWhiteSpace(content) ? "New note" : $"Edited {TextUtility.FormatWhen(note.UpdatedAt)}";

        _saveTimer = new DispatcherTimer { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => SaveNow();

        _studyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(StudyTickSeconds) };
        _studyTimer.Tick += (_, _) => StudyTick();
        _studyTimer.Start();

        RefreshContents();
    }

    // --- Study time ---

    /// <summary> Typing, clicking or scrolling in the note: keeps its study time counting. </summary>
    public void NoteActivity() => _lastActivity = DateTime.Now;

    private void StudyTick()
    {
        if (IsWindowActive && DateTime.Now - _lastActivity < IdleAfter) _unsavedStudySeconds += StudyTickSeconds;
        if (_unsavedStudySeconds >= StudyFlushSeconds) FlushStudyTime();
    }

    private void FlushStudyTime()
    {
        if (_unsavedStudySeconds <= 0) return;
        try
        {
            NoteRepository.AddStudyTime(_note.ID, _unsavedStudySeconds);
            _unsavedStudySeconds = 0;
        }
        catch (Exception ex)
        {
            Logger.LogError("Saving note study time failed", ex);
        }
    }

    // --- Editing ---

    /// <summary> Starts editing a paragraph, finishing whichever one was being edited. </summary>
    public void BeginEdit(NoteBlock block, int caret = -1)
    {
        if (EditingBlock == block) return;
        CommitEditing();
        if (!Blocks.Contains(block)) return;

        block.PendingCaret = caret;
        EditingBlock = block;
        block.IsEditing = true;
    }

    /// <summary> Finishes editing: blank lines split the paragraph, an emptied one is removed (unless it's the only one), and the note saves. </summary>
    public void CommitEditing()
    {
        if (EditingBlock is not { } block) return;

        // Cleared first: hiding the editor makes it lose focus, which would otherwise commit again.
        EditingBlock = null;
        block.IsEditing = false;

        var index = Blocks.IndexOf(block);
        var parts = NoteText.SplitBlocks(block.Text);

        if (parts.Count == 1 && string.IsNullOrWhiteSpace(parts[0]))
        {
            if (Blocks.Count > 1) RemoveBlock(block);
            else block.Text = block.RenderedText = "";
        }
        else
        {
            block.Text = parts[0];
            block.RenderedText = parts[0];
            for (var i = 1; i < parts.Count; i++) AddBlock(index + i, parts[i]);
        }

        RefreshContents();
        SaveNow();
    }

    /// <summary> Splits the edited paragraph at the caret, rendering the text before and editing the rest as a new paragraph. </summary>
    public void SplitEditingAt(int caret)
    {
        if (EditingBlock is not { } block) return;

        var text = block.Text;
        caret = Math.Clamp(caret, 0, text.Length);
        var after = text[caret..].TrimStart('\r', '\n');

        var index = Blocks.IndexOf(block);
        var countBefore = Blocks.Count;
        block.Text = text[..caret].TrimEnd('\r', '\n');
        CommitEditing();

        // Committing can split the text before into several paragraphs, or remove it if it was empty.
        var next = AddBlock(Math.Clamp(index + 1 + (Blocks.Count - countBefore), 0, Blocks.Count), after);
        BeginEdit(next, caret: 0);
    }

    /// <summary> Joins the edited paragraph onto the one before, with the cursor where the two meet. </summary>
    public bool MergeEditingIntoPrevious()
    {
        if (EditingBlock is not { } block) return false;
        var index = Blocks.IndexOf(block);
        if (index <= 0) return false;

        var previous = Blocks[index - 1];
        var before = previous.Text.TrimEnd('\r', '\n');
        var after = block.Text;

        string joined;
        int caret;
        if (string.IsNullOrWhiteSpace(before)) (joined, caret) = (after, 0);
        else if (string.IsNullOrWhiteSpace(after)) (joined, caret) = (before, before.Length);
        else (joined, caret) = (before + "\n" + after, before.Length + 1);

        // Not committed: committing would re-split and save a paragraph that's about to go.
        EditingBlock = null;
        block.IsEditing = false;
        RemoveBlock(block);

        previous.Text = joined;
        BeginEdit(previous, caret);
        RefreshContents();
        return true;
    }

    /// <summary> Edits the paragraph before the one being edited, with the cursor at its end. </summary>
    public bool EditPrevious()
    {
        if (EditingBlock is not { } block) return false;
        var index = Blocks.IndexOf(block);
        if (index <= 0) return false;

        BeginEdit(Blocks[index - 1], caret: -1);
        return true;
    }

    /// <summary> Edits the paragraph after the one being edited, with the cursor at its start. </summary>
    public bool EditNext()
    {
        if (EditingBlock is not { } block) return false;
        var index = Blocks.IndexOf(block);
        if (index < 0 || index >= Blocks.Count - 1) return false;

        BeginEdit(Blocks[index + 1], caret: 0);
        return true;
    }

    /// <summary> Starts writing at the end of the note: in the last paragraph if it's empty, otherwise a new one. </summary>
    public void EditEnd()
    {
        CommitEditing();
        var last = Blocks.LastOrDefault();
        if (last is null || !last.IsEmpty) last = AddBlock(Blocks.Count, "");
        BeginEdit(last);
    }

    public void DeleteBlock(NoteBlock block)
    {
        if (EditingBlock == block) EditingBlock = null;

        if (Blocks.Count > 1) RemoveBlock(block);
        else block.Text = block.RenderedText = "";

        MarkDirty();
        RefreshContents();
        SaveNow();
    }

    private NoteBlock AddBlock(int index, string text)
    {
        var block = new NoteBlock(text);
        block.PropertyChanged += Block_PropertyChanged;
        Blocks.Insert(index, block);
        return block;
    }

    private void RemoveBlock(NoteBlock block)
    {
        block.PropertyChanged -= Block_PropertyChanged;
        Blocks.Remove(block);
        MarkDirty();
    }

    private void Block_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NoteBlock.Text)) MarkDirty();
    }

    // --- Headings ---

    private void RefreshContents()
    {
        Contents.Clear();
        foreach (var block in Blocks)
        {
            foreach (var heading in NoteText.Headings(block.RenderedText))
                Contents.Add(new ContentsEntry(heading.Level, heading.Source, block));
        }
        OnPropertyChanged(nameof(HasContents));
    }

    /// <summary> What's typed inside the last heading before <paramref name="caret"/> in <paramref name="block"/>, for a new card's front. </summary>
    public string? NearestHeading(NoteBlock? block, int caret)
    {
        var index = block is null ? Blocks.Count : Blocks.IndexOf(block);
        if (block is not null && index >= 0)
        {
            var before = block.Text[..Math.Clamp(caret, 0, block.Text.Length)];
            if (NoteText.LastHeading(before) is { } inBlock) return inBlock;
        }

        for (var i = Math.Min(index, Blocks.Count) - 1; i >= 0; i--)
        {
            if (NoteText.LastHeading(Blocks[i].Text) is { } heading) return heading;
        }
        return null;
    }

    // --- Saving ---

    private void MarkDirty()
    {
        if (_closed) return;
        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary> Writes the note now if anything changed since the last save. </summary>
    public void SaveNow()
    {
        _saveTimer.Stop();
        if (!_dirty) return;
        _dirty = false;

        var content = NoteText.JoinBlocks(Blocks.Select(block => block.Text));
        try
        {
            _note.UpdatedAt = NoteRepository.SaveContent(_note.ID, content);
            _note.WordCount = NoteText.WordCount(content);
            StatusText = "Saved";
        }
        catch (Exception ex)
        {
            Logger.LogError("Saving a note failed", ex);
            _dirty = true;
            StatusText = "Couldn't save. Your changes are still here; try again.";
        }
    }

    /// <summary> Renames the note to what's in the name box, or puts the old name back if it was cleared. </summary>
    public void CommitName()
    {
        var name = Name.Trim();
        if (name.Length == 0)
        {
            Name = _note.Name;
            return;
        }
        if (name == _note.Name) return;

        NoteRepository.RenameNote(_note.ID, name);
        _note.Name = name;
        Name = name;
    }

    /// <summary> Saves everything and goes back to the main menu. </summary>
    public void Close()
    {
        if (_closed) return;
        CommitEditing();
        CommitName();
        SaveNow();
        _studyTimer.Stop();
        FlushStudyTime();
        _closed = true;
        _onClose();
    }

    /// <summary> Called when the app is closing with the note open. </summary>
    public void SaveBeforeExit()
    {
        if (EditingBlock is not null) CommitEditing();
        CommitName();
        SaveNow();
        _studyTimer.Stop();
        FlushStudyTime();
    }
}
