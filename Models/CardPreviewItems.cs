namespace ReviFlash.Models;

public sealed class MultiChoicePreviewOption
{
    public required string Text { get; init; }
    public required bool IsCorrect { get; init; }
}

public sealed class MatchPreviewPair
{
    public required string LeftText { get; init; }
    public required string RightText { get; init; }
}
