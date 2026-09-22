using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

public partial class StudyGroupEditorViewModel : ViewModelBase
{
    private readonly StudyGroup? _editingGroup;
    private readonly ulong? _targetFolderID;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _groupName = string.Empty;
    [ObservableProperty] private string _deckSearchText = string.Empty;

    partial void OnDeckSearchTextChanged(string value)
    {
        RefreshFilteredAvailableDecks();
    }

    /// <summary> The same orderings the dashboard offers. </summary>
    public List<SortOption> SortOptions { get; } = SearchUtility.CreateSortOptions();

    [ObservableProperty] private SortOption? _selectedSortOption;
    partial void OnSelectedSortOptionChanged(SortOption? value) => RefreshFilteredAvailableDecks();

    public ObservableCollection<FlashCardDeck> AvailableDecks { get; } = [];
    public ObservableCollection<FlashCardDeck> FilteredAvailableDecks { get; } = [];
    public ObservableCollection<FlashCardDeck> SelectedDecks { get; } = [];

    public bool IsEditing => _editingGroup != null;
    public string WindowTitle => IsEditing ? "Edit Group" : "Create Group";
    public bool CanSave => !string.IsNullOrWhiteSpace(GroupName);

    /// <param name="targetFolderID"> Folder a newly created group is filed into; null for the main menu. </param>
    public StudyGroupEditorViewModel(StudyGroup? group = null, ulong? targetFolderID = null)
    {
        _editingGroup = group;
        _targetFolderID = targetFolderID;
        SelectedSortOption = SortOptions[0];

        var allDecks = FlashCardRepository.GetAllDecks();

        // Show which folder each set lives in: two sets can easily share a name once
        // they are filed apart.
        FolderTree.Load().ApplyPaths(allDecks, []);

        if (group != null)
        {
            GroupName = group.Name;
            var existingDeckIds = FlashCardRepository.GetDecksForStudyGroup(group.ID)
            .Select(d => d.ID).ToHashSet();

            foreach (var deck in allDecks)
            {
                if (existingDeckIds.Contains(deck.ID)) SelectedDecks.Add(deck);
                else AvailableDecks.Add(deck);
            }
        }
        else { foreach (var deck in allDecks) AvailableDecks.Add(deck); }

        RefreshFilteredAvailableDecks();
    }

    public void AddDeck(FlashCardDeck deck)
    {
        if (AvailableDecks.Remove(deck))
        {
            SelectedDecks.Add(deck);
            RefreshFilteredAvailableDecks();
        }
    }

    public void RemoveDeck(FlashCardDeck deck)
    {
        if (SelectedDecks.Remove(deck))
        {
            AvailableDecks.Add(deck);
            RefreshFilteredAvailableDecks();
        }
    }

    public StudyGroup SaveGroup()
    {
        if (!CanSave) return _editingGroup ?? new StudyGroup(string.Empty);

        var deckIds = SelectedDecks.Select(deck => deck.ID).ToList();
        var trimmedName = GroupName.Trim();

        if (_editingGroup is null)
        {
            var newGroup = new StudyGroup(trimmedName);
            FlashCardRepository.SaveNewStudyGroup(newGroup);
            FlashCardRepository.SetStudyGroupDecks(newGroup.ID, deckIds);

            if (_targetFolderID is ulong folderID) FolderRepository.MoveStudyGroup(newGroup.ID, folderID);

            return newGroup;
        }

        _editingGroup.Name = trimmedName;
        FlashCardRepository.UpdateStudyGroup(_editingGroup);
        FlashCardRepository.SetStudyGroupDecks(_editingGroup.ID, deckIds);
        return _editingGroup;
    }

    private void RefreshFilteredAvailableDecks()
    {
        FilteredAvailableDecks.Clear();

        var fDecks = AvailableDecks.SearchAndSort(DeckSearchText, SelectedSortOption?.Mode ?? SortMode.Relevance);
        foreach (var d in fDecks) FilteredAvailableDecks.Add(d);
    }
}