using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> One selectable destination in the move dialog. </summary>
public sealed class FolderChoice(ulong? folderID, string name, int depth, string pathText) : ISearchable
{
    /// <summary> Destination folder, or null for the main menu. </summary>
    public ulong? FolderID { get; } = folderID;
    public string Name { get; } = name;
    public int Depth { get; } = depth;
    public string PathText { get; } = pathText;

    public bool IsCurrentLocation { get; init; }

    public Avalonia.Thickness Indent => new(Depth * 20, 0, 0, 0);
    public string Subtitle => IsCurrentLocation ? "Current location" : PathText;
    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);

    public string SearchName => Name;
    public IEnumerable<string?> SearchKeywords => [PathText];
}

/// <summary>
/// Destination picker for moving a set, group or folder. Folders that would create a
/// loop (the folder being moved, and everything inside it) are left out entirely.
/// </summary>
public partial class MoveToFolderViewModel : ViewModelBase
{
    private readonly List<FolderChoice> _allChoices = [];

    public ObservableCollection<FolderChoice> Choices { get; } = [];

    [ObservableProperty] private string _searchText = "";
    partial void OnSearchTextChanged(string value) => RefreshChoices();

    [NotifyPropertyChangedFor(nameof(CanMove))]
    [ObservableProperty] private FolderChoice? _selectedChoice;

    public string ItemName { get; }
    public string ItemKind { get; }
    public string WindowTitle => $"Move {ItemKind}";
    public string Header => $"Move \"{ItemName}\" to...";

    public bool CanMove => SelectedChoice is not null && !SelectedChoice.IsCurrentLocation;

    public MoveToFolderViewModel(FolderTree tree, object item, ulong? currentFolderID)
    {
        (ItemName, ItemKind) = item switch
        {
            Folder folder => (folder.Name, "Folder"),
            StudyGroup group => (group.Name, "Group"),
            FlashCardDeck deck => (deck.Name, "Set"),
            Note note => (note.Name, "Note"),
            _ => ("item", "Item"),
        };

        // Moving a folder into itself, or into one of its own subfolders, would orphan
        // the branch; those destinations are never offered.
        var excluded = item is Folder movedFolder ? tree.SubtreeIds(movedFolder.ID) : [];

        _allChoices.Add(new FolderChoice(null, FolderTree.RootLabel, 0, "")
        {
            IsCurrentLocation = currentFolderID is null,
        });

        AddChoices(tree, null, 1, currentFolderID, excluded);

        RefreshChoices();
        SelectedChoice = _allChoices.Find(choice => !choice.IsCurrentLocation);
    }

    private void AddChoices(FolderTree tree, ulong? parentID, int depth, ulong? currentFolderID, HashSet<ulong> excluded)
    {
        foreach (var folder in tree.ChildrenOf(parentID))
        {
            if (excluded.Contains(folder.ID)) continue;

            _allChoices.Add(new FolderChoice(folder.ID, folder.Name, depth, tree.PathOf(folder.ParentFolderID))
            {
                IsCurrentLocation = currentFolderID == folder.ID,
            });

            AddChoices(tree, folder.ID, depth + 1, currentFolderID, excluded);
        }
    }

    private void RefreshChoices()
    {
        Choices.Clear();

        // With no query the list keeps its tree order, which the indentation depends on;
        // searching flattens it into ranked matches like every other list in the app.
        var visible = SearchUtility.IsEmptyQuery(SearchText)
            ? _allChoices
            : _allChoices.SearchAndSort(SearchText, SortMode.Relevance);

        foreach (var choice in visible) Choices.Add(choice);

        if (SelectedChoice is not null && !Choices.Contains(SelectedChoice)) SelectedChoice = null;
    }
}
