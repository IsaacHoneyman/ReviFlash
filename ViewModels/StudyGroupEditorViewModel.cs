using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Data.Local;
using ReviFlash.Models;

namespace ReviFlash.ViewModels;

public partial class StudyGroupEditorViewModel : ViewModelBase
{
    private readonly StudyGroup? _editingGroup;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _groupName = string.Empty;
    [ObservableProperty] private string _deckSearchText = string.Empty;

    partial void OnDeckSearchTextChanged(string value)
    {
        RefreshFilteredAvailableDecks();
    }

    public ObservableCollection<FlashCardDeck> AvailableDecks { get; } = [];
    public ObservableCollection<FlashCardDeck> FilteredAvailableDecks { get; } = [];
    public ObservableCollection<FlashCardDeck> SelectedDecks { get; } = [];

    public bool IsEditing => _editingGroup != null;
    public string WindowTitle => IsEditing ? "Edit Group" : "Create Group";
    public bool CanSave => !string.IsNullOrWhiteSpace(GroupName);

    public StudyGroupEditorViewModel(StudyGroup? group = null)
    {
        _editingGroup = group;
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
        var fDecks = AvailableDecks.FilterBySearch(DeckSearchText);
        foreach (var d in fDecks) FilteredAvailableDecks.Add(d);
    }
}