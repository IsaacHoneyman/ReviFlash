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

    public ObservableCollection<FlashCardDeck> AvailableDecks { get; } = [];
    public ObservableCollection<FlashCardDeck> SelectedDecks { get; } = [];

    public DeckFolderBrowser AvailableBrowser { get; }

    public bool IsEditing => _editingGroup != null;
    public string WindowTitle => IsEditing ? "Edit Group" : "Create Group";
    public bool CanSave => !string.IsNullOrWhiteSpace(GroupName);

    /// <param name="targetFolderID"> Folder a newly created group is filed into; null for the main menu. </param>
    public StudyGroupEditorViewModel(StudyGroup? group = null, ulong? targetFolderID = null)
    {
        _editingGroup = group;
        _targetFolderID = targetFolderID;
        AvailableBrowser = new DeckFolderBrowser("Add", AddDeck);

        var allDecks = FlashCardRepository.GetAllDecks();

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

        // Start where the group lives, so its neighbouring sets are the first on offer.
        AvailableBrowser.NavigateTo(group?.FolderID ?? targetFolderID);
        AvailableBrowser.SetDecks(AvailableDecks);
    }

    public void AddDeck(FlashCardDeck deck)
    {
        if (AvailableDecks.Remove(deck))
        {
            SelectedDecks.Add(deck);
            AvailableBrowser.SetDecks(AvailableDecks);
        }
    }

    public void RemoveDeck(FlashCardDeck deck)
    {
        if (SelectedDecks.Remove(deck))
        {
            AvailableDecks.Add(deck);
            AvailableBrowser.SetDecks(AvailableDecks);
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
            FlashCardRepository.SaveNewStudyGroup(newGroup, deckIds, _targetFolderID);
            return newGroup;
        }

        _editingGroup.Name = trimmedName;
        FlashCardRepository.UpdateStudyGroup(_editingGroup, deckIds);
        return _editingGroup;
    }

}