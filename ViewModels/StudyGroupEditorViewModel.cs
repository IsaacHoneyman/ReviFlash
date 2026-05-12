using System.Collections.ObjectModel;
using System.Linq;
using ReviFlash.Data;
using ReviFlash.Models;

namespace ReviFlash.ViewModels;

public class StudyGroupEditorViewModel : ViewModelBase
{
    private readonly StudyGroup? _editingGroup;
    private string _groupName = string.Empty;
    private string _deckSearchText = string.Empty;

    public ObservableCollection<FlashCardDeck> AvailableDecks { get; } = [];
    public ObservableCollection<FlashCardDeck> FilteredAvailableDecks { get; } = [];
    public ObservableCollection<FlashCardDeck> SelectedDecks { get; } = [];

    public string GroupName
    {
        get => _groupName;
        set
        {
            _groupName = value;
            OnPropertyChanged(nameof(GroupName));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public bool IsEditing => _editingGroup != null;
    public string WindowTitle => IsEditing ? "Edit Group" : "Create Group";
    public bool CanSave => !string.IsNullOrWhiteSpace(GroupName);

    public string DeckSearchText
    {
        get => _deckSearchText;
        set
        {
            _deckSearchText = value;
            OnPropertyChanged(nameof(DeckSearchText));
            RefreshFilteredAvailableDecks();
        }
    }

    public StudyGroupEditorViewModel(StudyGroup? group = null)
    {
        _editingGroup = group;

        foreach (var deck in FlashCardRepository.GetAllDecks())
        {
            AvailableDecks.Add(deck);
        }

        RefreshFilteredAvailableDecks();

        if (group != null)
        {
            GroupName = group.Name;

            var existingDecks = FlashCardRepository.GetDecksForStudyGroup(group.ID);
            foreach (var deck in existingDecks)
            {
                MoveDeckToSelected(deck);
            }
        }
    }

    public void AddDeck(FlashCardDeck deck)
    {
        MoveDeckToSelected(deck);
        RefreshFilteredAvailableDecks();
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
        if (!CanSave)
        {
            return _editingGroup ?? new StudyGroup(string.Empty);
        }

        var deckIds = SelectedDecks.Select(deck => deck.ID).ToList();

        if (_editingGroup is null)
        {
            var newGroup = new StudyGroup(GroupName.Trim());
            FlashCardRepository.SaveNewStudyGroup(newGroup);
            FlashCardRepository.SetStudyGroupDecks(newGroup.ID, deckIds);
            return newGroup;
        }

        _editingGroup.Name = GroupName.Trim();
        FlashCardRepository.UpdateStudyGroup(_editingGroup);
        FlashCardRepository.SetStudyGroupDecks(_editingGroup.ID, deckIds);
        return _editingGroup;
    }

    private void MoveDeckToSelected(FlashCardDeck deck)
    {
        var existing = AvailableDecks.FirstOrDefault(d => d.ID == deck.ID);
        if (existing != null)
        {
            AvailableDecks.Remove(existing);
        }

        if (SelectedDecks.All(d => d.ID != deck.ID))
        {
            SelectedDecks.Add(deck);
        }
    }

    private void RefreshFilteredAvailableDecks()
    {
        var searchText = DeckSearchText.Trim().ToLowerInvariant();

        FilteredAvailableDecks.Clear();

        foreach (var deck in AvailableDecks)
        {
            if (string.IsNullOrWhiteSpace(searchText) ||
                deck.Name.ToLowerInvariant().Contains(searchText) ||
                deck.CardCount.ToString().Contains(searchText))
            {
                FilteredAvailableDecks.Add(deck);
            }
        }
    }
}