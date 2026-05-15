# ReviFlash Code Quality Analysis

## Summary
Identified **29 issues** across ViewModels, Models, Data, and Views layers. Most issues are **medium severity**, with some **high-severity** null safety and design problems that should be addressed first.

---

## HIGH SEVERITY ISSUES

### 1. Null Safety in DeckEditorWindow.axaml.cs
**File:** [Views/DeckEditorWindow.axaml.cs](Views/DeckEditorWindow.axaml.cs#L18)  
**Severity:** HIGH  
**Issue:** Multiple `throw new InvalidOperationException()` calls for null checks instead of proper null handling.
```csharp
var card = (FlashCard)(button.DataContext ?? throw new InvalidOperationException("..."));
var option = (MultiChoiceOptionEditor)(button.DataContext ?? 
    throw new InvalidOperationException("..."));
```
**Problem:** If button.DataContext is null, the app crashes. This should be handled gracefully.  
**Suggestion:** Use null-conditional operators and return early:
```csharp
if (button.DataContext is not FlashCard card) {
    // Show user error or return
    return;
}
```

### 2. Potential NullReferenceException in ReviewViewModel
**File:** [ViewModels/ReviewViewModel.cs](ViewModels/ReviewViewModel.cs#L59)  
**Severity:** HIGH  
**Issue:** `CurrentCard` property directly accesses `_sessionCards[_currentIndex]` without bounds checking.
```csharp
public FlashCard CurrentCard => _sessionCards[_currentIndex];
```
**Problem:** If the card list is empty or index is out of bounds, an IndexOutOfRangeException is thrown.  
**Suggestion:** Add bounds check:
```csharp
public FlashCard? CurrentCard => _currentIndex < _sessionCards.Count 
    ? _sessionCards[_currentIndex] 
    : null;
```

### 3. Missing Return Type Check in ReviewViewModel
**File:** [ViewModels/ReviewViewModel.cs](ViewModels/ReviewViewModel.cs#L180)  
**Severity:** HIGH  
**Issue:** Multiple `Check*Answer()` methods return early with type checks but don't prevent UI from showing answers.
```csharp
public void CheckMultiChoiceAnswer() {
    if (CurrentCard is not MultiFlashCard) {
        return;
    }
    // ... rest of logic
}
```
**Problem:** Early return without user feedback. UI might appear broken if wrong card type.  
**Suggestion:** Use nullable returns and handle in views or show error message.

---

## MEDIUM SEVERITY ISSUES

### 4. Hardcoded Magic Strings (Multiple Files)
**Severity:** MEDIUM  
**Files:**
- [ViewModels/MainWindowViewModel.cs](ViewModels/MainWindowViewModel.cs#L115) - Grade thresholds (90, 80, 70, 60, 50)
- [ViewModels/DeckEditorViewModel.cs](ViewModels/DeckEditorViewModel.cs#L151) - Card type names ("Match", "True/False", "True", "False")
- [ViewModels/SummaryViewModel.cs](ViewModels/SummaryViewModel.cs#L15) - Grade switch (A*, A, B, C, D, U)

**Issue:** Magic numbers and strings scattered throughout code.
```csharp
grade = Percentage switch {
    >= 90 => "A*",
    >= 80 => "A",
    >= 70 => "B",
    >= 60 => "C",
    >= 50 => "D",
    _ => "U"
};
```
**Suggestion:** Create constants class:
```csharp
public static class GradingConstants {
    public const int GRADE_A_STAR_THRESHOLD = 90;
    public const int GRADE_A_THRESHOLD = 80;
    public const string GRADE_A_STAR = "A*";
    // ... etc
}
```

### 5. Duplicate Grade Calculation Logic
**Severity:** MEDIUM  
**Files:** [MainWindowViewModel.cs](ViewModels/MainWindowViewModel.cs#L370), [SummaryViewModel.cs](ViewModels/SummaryViewModel.cs#L15)  
**Issue:** Same grade calculation logic duplicated in multiple places.
```csharp
// MainWindowViewModel.cs (line ~370)
Grade = Percentage switch { >= 90 => "A*", >= 80 => "A", ... };

// SummaryViewModel.cs (line ~15)
public string Grade => Total == 0 ? "-" : Percentage switch { >= 90 => "A*", ... };
```
**Suggestion:** Extract to shared utility class:
```csharp
public static class GradeCalculator {
    public static string CalculateGrade(double percentage, int totalQuestions) {
        if (totalQuestions == 0) return "-";
        return percentage switch { /* ... */ };
    }
}
```

### 6. Long Method - DeckEditorViewModel.AddNewCard()
**Severity:** MEDIUM  
**File:** [ViewModels/DeckEditorViewModel.cs](ViewModels/DeckEditorViewModel.cs#L220)  
**Issue:** Method is extremely long (~150+ lines), handles validation, card creation, type switching, and database updates.
**Suggestion:** Break into smaller methods:
```csharp
public void AddNewCard() {
    if (!ValidateCardInput()) return;
    
    var cardData = PrepareCardData();
    var newCard = CreateCardFromData(cardData);
    SaveCard(newCard);
    ClearEditor();
}

private bool ValidateCardInput() { /* ... */ }
private CardData PrepareCardData() { /* ... */ }
private FlashCard CreateCardFromData(CardData data) { /* ... */ }
```

### 7. Long Method - DeckEditorViewModel.BeginEditCard()
**Severity:** MEDIUM  
**File:** [ViewModels/DeckEditorViewModel.cs](ViewModels/DeckEditorViewModel.cs#L400)  
**Issue:** Large conditional chain for different card types (~120 lines).
**Suggestion:** Use polymorphic pattern - move card type setup to card classes.

### 8. Incomplete Error Handling in MetaDataManager
**Severity:** MEDIUM  
**File:** [Data/MetaDataManager.cs](Data/MetaDataManager.cs#L40)  
**Issue:** Exception caught but silently ignored:
```csharp
try {
    string json = File.ReadAllText(GetFilePath());
    return JsonSerializer.Deserialize<AppMetaData>(json) ?? new AppMetaData();
} catch (Exception) {  // ← Silent catch
    return new AppMetaData();
}
```
**Problem:** Any error (JSON corruption, file permission, etc.) silently creates default. User won't know data was lost.  
**Suggestion:** Log the error or notify user:
```csharp
catch (Exception ex) {
    System.Diagnostics.Debug.WriteLine($"Failed to load metadata: {ex.Message}");
    return new AppMetaData();
}
```

### 9. Silent Exception in BackupManager
**Severity:** MEDIUM  
**File:** [Data/BackupManager.cs](Data/BackupManager.cs#L80)  
**Issue:** Method `AddFileToArchiveSafely()` silently returns if file doesn't exist:
```csharp
private static void AddFileToArchiveSafely(ZipArchive archive, string sourceFilePath) {
    if (!File.Exists(sourceFilePath)) {
        return;  // ← Silent return
    }
    // ...
}
```
**Problem:** Backup created without metadata.json or database, silently failing.  
**Suggestion:** Return bool and log result:
```csharp
private static bool TryAddFileToArchive(...) {
    if (!File.Exists(sourceFilePath)) {
        Debug.WriteLine($"File not found: {sourceFilePath}");
        return false;
    }
    return true;
}
```

### 10. Multiple Property Changed Calls in ReviewViewModel.NextCard()
**Severity:** MEDIUM  
**File:** [ViewModels/ReviewViewModel.cs](ViewModels/ReviewViewModel.cs#L290)  
**Issue:** 10+ consecutive `OnPropertyChanged()` calls:
```csharp
OnPropertyChanged(nameof(CurrentCard));
OnPropertyChanged(nameof(IsAnswerRevealed));
OnPropertyChanged(nameof(IsAnswerChecked));
OnPropertyChanged(nameof(UserTypedAnswer));
// ... 6 more
```
**Problem:** Inefficient, hard to maintain. Batching would be better.  
**Suggestion:** Create a refresh method:
```csharp
private void RefreshCardDisplay() {
    OnPropertyChanged(nameof(CurrentCard));
    OnPropertyChanged(nameof(IsAnswerRevealed));
    // ... batch all together
}
```

### 11. Tight Coupling: Static App.CurrentMetaData
**Severity:** MEDIUM  
**Files:** Multiple (DeckEditorViewModel, MainWindowViewModel, ReviewViewModel, SettingsViewModel, AppMetaData)  
**Issue:** Direct dependency on static `App.CurrentMetaData`:
```csharp
public bool ShowAdditionalFieldLatexPreviews => 
    App.CurrentMetaData.ShowAdditionalFieldLatexPreviews;

public bool ShouldShowTimer => App.CurrentMetaData.ShowTimer;
```
**Problem:** Hard to test, couples all ViewModels to App class, fragile.  
**Suggestion:** Inject IAppSettings interface:
```csharp
public DeckEditorViewModel(FlashCardDeck deck, IAppSettings settings) {
    _settings = settings;
}
public bool ShowAdditionalFieldLatexPreviews => 
    _settings.ShowAdditionalFieldLatexPreviews;
```

### 12. Static String Version in MainWindowViewModel
**Severity:** MEDIUM  
**File:** [ViewModels/MainWindowViewModel.cs](ViewModels/MainWindowViewModel.cs#L56)  
**Issue:** Hardcoded version string that needs manual updates:
```csharp
private static string _versionText = "Version B-0.7.1";
```
**Problem:** Easy to forget updating. Should come from assembly/config.  
**Suggestion:** Read from assembly attributes:
```csharp
public static string VersionText => 
    typeof(App).Assembly.GetName().Version?.ToString() ?? "Unknown";
```

### 13. Disabled Null Forgiving Operators Without Justification
**Severity:** MEDIUM  
**File:** [ViewModels/DeckEditorViewModel.cs](ViewModels/DeckEditorViewModel.cs#L189)  
**Issue:** Multiple `null!` assertions without explanation:
```csharp
private record EditorSnapshot(..., List<(string optionText, bool isCorrect)> MultiOptions, ...);
// Used with null! in constructor
```
**Problem:** Code smell suggesting unclear nullability contract.  
**Suggestion:** Use proper nullable types and document why null is safe.

### 14. Dictionary Used Instead of Sealed Record for Attempt Tracking
**Severity:** MEDIUM  
**File:** [ViewModels/ReviewViewModel.cs](ViewModels/ReviewViewModel.cs#L50)  
**Issue:** Using plain `Dictionary<ulong, int>` instead of typed structure:
```csharp
private readonly Dictionary<ulong, ulong>? _cardDeckMap;
private readonly Dictionary<ulong, int> _attemptsByDeck = [];
private readonly Dictionary<ulong, int> _correctByDeck = [];
```
**Problem:** No type safety, unclear what values mean.  
**Suggestion:** Create typed structure:
```csharp
private sealed record DeckStats(Dictionary<ulong, int> Attempts, 
                                 Dictionary<ulong, int> CorrectCounts);
```

### 15. RecordCurrentCardResult Not Implemented
**Severity:** MEDIUM  
**File:** [ViewModels/ReviewViewModel.cs](ViewModels/ReviewViewModel.cs#L450)  
**Issue:** Method called but not defined in visible code:
```csharp
RecordCurrentCardResult(IsAnswerCorrect);
```
**Problem:** Either missing or defined elsewhere, unclear implementation.  
**Suggestion:** Define clearly or search for implementation.

---

## DESIGN/ARCHITECTURE ISSUES

### 16. FlashCard Abstract Base Class Has Incomplete Abstraction
**Severity:** MEDIUM  
**File:** [Models/FlashCard.cs](Models/FlashCard.cs#L1)  
**Issue:** Abstract class with many type-checking properties:
```csharp
public bool IsMultiChoiceCard => this is MultiFlashCard;
public bool IsMatchCard => this is MatchFlashCard;
public bool IsTrueFalseCard => this is TrueFalseFlashCard;
```
**Problem:** Violates Open/Closed principle. Requires casting everywhere.  
**Suggestion:** Use visitor pattern or sealed types with proper method dispatch.

### 17. No Validation for Empty Card Lists
**Severity:** MEDIUM  
**File:** [ViewModels/ReviewViewModel.cs](ViewModels/ReviewViewModel.cs#L130)  
**Issue:** Constructor doesn't validate card enumerable:
```csharp
public ReviewViewModel(IEnumerable<FlashCard> cards, ulong deckID, ...) {
    _sessionCards = [.. cards.OrderBy(_ => Guid.NewGuid()).ToList()];
    // No check if _sessionCards.Count == 0
}
```
**Problem:** Can create session with 0 cards, causing crashes.  
**Suggestion:** Validate in constructor:
```csharp
if (!cards.Any()) throw new ArgumentException("Cannot start review with empty card list");
```

### 18. DatabaseManager.GetConnection() Not Disposed in All Cases
**Severity:** MEDIUM  
**Files:** Multiple repository methods  
**Issue:** Some methods correctly use `using`, but pattern not enforced. Example:
```csharp
public static void SaveNewDeck(FlashCardDeck deck) {
    using var connection = DatabaseManager.GetConnection();
    // Proper disposal
}
```
**Problem:** Potential connection leak if exception occurs before using statement.  
**Suggestion:** Consider using connection pooling or repository factory pattern.

### 19. FlashCard Type Casting Scattered Throughout Code
**Severity:** MEDIUM  
**Files:** [ReviewViewModel.cs](ViewModels/ReviewViewModel.cs#L175), [DeckEditorViewModel.cs](ViewModels/DeckEditorViewModel.cs#L445)  
**Issue:** Many `is` checks followed by casts:
```csharp
if (card is TypeFlashCard)
{
    SelectedCardType = "Type to Answer";
    NewTypeAnswer = ((TypeFlashCard)card).Answer;
}
else if (card is MultiFlashCard multiCard)
{
    // ...
}
```
**Problem:** Duplicates type information, hard to maintain.  
**Suggestion:** Use pattern matching everywhere:
```csharp
SelectedCardType = card switch {
    TypeFlashCard t => "Type to Answer",
    MultiFlashCard m => "Multi Choice",
    // ...
};
```

### 20. ReviewViewModel Timer Not Disposed
**Severity:** MEDIUM  
**File:** [ViewModels/ReviewViewModel.cs](ViewModels/ReviewViewModel.cs#L160)  
**Issue:** `_displayTimer` created in constructor but no cleanup:
```csharp
_displayTimer = new Timer(100);
_displayTimer.Elapsed += (_, _) => { ... };
_displayTimer.Start();
// No IDisposable implementation
```
**Problem:** Timer keeps running, holding memory.  
**Suggestion:** Implement IDisposable:
```csharp
public void Dispose() {
    _displayTimer?.Dispose();
}
```

---

## LOW-MEDIUM SEVERITY ISSUES

### 21. Inconsistent Property Naming in EditorSnapshot
**Severity:** LOW-MEDIUM  
**File:** [ViewModels/DeckEditorViewModel.cs](ViewModels/DeckEditorViewModel.cs#L30)  
**Issue:** Record fields don't follow naming convention:
```csharp
private record EditorSnapshot(
    string SelectedCardType,
    string NewFront,
    string NewBack,
    string NewTypeAnswer,  // ← "New" prefix inconsistent
    bool NewTrueFalseAnswerIsTrue,  // ← Very long name
    string NewTrueOptionText,
    string NewFalseOptionText,
    List<(string optionText, bool isCorrect)> MultiOptions,
    List<(string leftText, string rightText)> MatchPairs,
    string ValidationMessage
);
```
**Suggestion:** Use consistent naming:
```csharp
private record EditorSnapshot(
    string CardType,
    string Front,
    string Back,
    string TypeAnswer,
    bool TrueFalseAnswerIsTrue,
    // ...
);
```

### 22. Inconsistent Pattern - Some Collections Initialized, Some Not
**Severity:** LOW-MEDIUM  
**Files:** [ViewModels/DeckEditorViewModel.cs](ViewModels/DeckEditorViewModel.cs#L56)  
**Issue:** Observable collections initialized inline:
```csharp
public ObservableCollection<FlashCard> Cards { get; set; } = new();
public ObservableCollection<MultiChoiceOptionEditor> MultiChoiceOptions { get; } = new();
```
**Suggestion:** Consistent pattern - all use `= new()` initialization.

### 23. No Input Validation for Database IDs
**Severity:** LOW-MEDIUM  
**Files:** [Data/FlashCardRepository.cs](Data/FlashCardRepository.cs)  
**Issue:** Methods accept `ulong deckID` without validation:
```csharp
public static List<FlashCard> GetCardsForDeck(ulong deckID) {
    // No validation that deckID > 0
}
```
**Suggestion:** Add validation:
```csharp
if (deckID == 0 || deckID == ulong.MaxValue)
    throw new ArgumentException("Invalid deck ID");
```

### 24. String Normalization Inconsistent
**Severity:** LOW-MEDIUM  
**Issue:** Different methods trim/normalize differently:
- `TypeFlashCard.UpdateAnswer()` calls `.Trim()`
- `MainWindowViewModel.LoadStats()` doesn't normalize percentage before grading
  
**Suggestion:** Create string normalization utility class with consistent rules.

### 25. Method ClearEditor() Has Side Effects Based on Card Type
**Severity:** LOW-MEDIUM  
**File:** [ViewModels/DeckEditorViewModel.cs](ViewModels/DeckEditorViewModel.cs#L550)  
**Issue:** Clearing editor sets different defaults based on `SelectedCardType`:
```csharp
private void ClearEditor() {
    if (IsMatchCardType) {
        NewFront = "Match The Cards";
        NewBack = "Match The Cards";
    } else {
        NewFront = "";
        NewBack = "";
    }
    // ...
}
```
**Problem:** Hidden behavior, confusing.  
**Suggestion:** Explicit parameter or separate methods:
```csharp
private void ClearEditor(string cardType) { /* ... */ }
```

### 26. NullReferenceException Risk in Binding
**Severity:** LOW-MEDIUM  
**File:** [ViewModels/MainWindowViewModel.cs](ViewModels/MainWindowViewModel.cs#L330)  
**Issue:** Properties accessed that could be null:
```csharp
public FlashCardDeck? SelectedDeckForStats
{
    get => _selectedDeckForStats;
    set { _selectedDeckForStats = value; ... }
}

// Then used without null check:
ShowDeckStats(SelectedDeckForStats);  // ← Could be null
```
**Suggestion:** Add null check before use.

### 27. No Logging Infrastructure
**Severity:** LOW-MEDIUM  
**Issue:** No logging system for debugging production issues.
**Suggestion:** Add Serilog or Microsoft.Extensions.Logging.

### 28. Observer Pattern for MetaData Could Be Improved
**Severity:** LOW  
**File:** [App.axaml.cs](App.axaml.cs#L11)  
**Issue:** Custom event instead of standard INotifyPropertyChanged:
```csharp
public static event Action<AppMetaData>? CurrentMetaDataChanged;
```
**Suggestion:** Use reactive libraries (ReactiveUI) or MVVM Toolkit's relay commands.

### 29. No Configuration for Database Path
**Severity:** LOW-MEDIUM  
**File:** [Data/DatabaseManager.cs](Data/DatabaseManager.cs#L19)  
**Issue:** Hardcoded database path:
```csharp
private static string GetDatabasePath() {
    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reviflash.db");
}
```
**Problem:** Can't change at runtime, not portable.  
**Suggestion:** Make configurable via settings.

---

## PERFORMANCE ISSUES

### 30. Inefficient Grade Calculation Logic
**Severity:** LOW  
**File:** Multiple files  
**Issue:** Uses switch expression for grading - fine, but duplicated.
**Suggestion:** Already covered in issue #5 (duplicate logic).

### 31. String Concatenation in Loops
**Severity:** LOW  
**File:** [ViewModels/ReviewViewModel.cs](ViewModels/ReviewViewModel.cs#L260)  
**Issue:** Adding strings to ObservableCollection repeatedly:
```csharp
foreach (var row in MatchRows) {
    if (!string.Equals(row.SelectedRightText, row.CorrectRightText, ...)) {
        WrongMatches.Add($"{row.LeftText} -> {selected} (correct: {row.CorrectRightText})");
    }
}
```
**Suggestion:** Build list first, then add to collection:
```csharp
var wrongList = MatchRows
    .Where(row => row.SelectedRightText != row.CorrectRightText)
    .Select(row => $"{row.LeftText} -> ...")
    .ToList();
WrongMatches.Clear();
foreach (var item in wrongList) WrongMatches.Add(item);
```

---

## SUMMARY BY SEVERITY

| Severity | Count | Issues |
|----------|-------|--------|
| HIGH     | 3     | Null safety, NullReferenceException, type checks without feedback |
| MEDIUM   | 18    | Hardcoded strings, duplicate logic, long methods, error handling, tight coupling |
| LOW      | 8     | Naming, logging, configuration, performance micro-optimizations |

## RECOMMENDED FIX ORDER

1. **Phase 1 (HIGH):** Fix null safety issues in Views and ReviewViewModel (Issues 1-3)
2. **Phase 2 (MEDIUM):** Extract constants and utilities (Issues 4-5, 12, 27)
3. **Phase 3 (MEDIUM):** Refactor long methods and duplicate logic (Issues 6-7, 19-20)
4. **Phase 4 (MEDIUM):** Improve error handling (Issues 8-9, 23)
5. **Phase 5 (MEDIUM):** Reduce coupling with App static (Issue 11)
6. **Phase 6 (LOW):** Code cleanup, naming, configuration (remaining issues)

