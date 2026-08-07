using System.Collections.Generic;

namespace ReviFlash.Data.Local;

public sealed record TrueFalseAnswerPayload(bool CorrectAnswerIsTrue, string TrueLabel, string FalseLabel);
public sealed record BackupManifest(bool IncludeStats);
public sealed record FlashCardExportPackage(List<DeckExportEntry> Decks);
public sealed record DeckExportEntry(string Name, List<CardExportEntry> Cards, List<DeckStatEntry> Stats);
public sealed record CardExportEntry(string CardType, string Front, string Back, string? Answer, bool? CorrectAnswerIsTrue,
    string? TrueLabel, string? FalseLabel, List<MultiChoiceOptionEntry>? Options, List<MatchPairEntry>? Pairs);
public sealed record MultiChoiceOptionEntry(string OptionText, bool IsCorrect);
public sealed record MatchPairEntry(string LeftText, string RightText);
public sealed record DeckStatEntry(int CorrectCount, int TotalAttempts, int TimeTakenSeconds, string DateChecked);