using System;
using System.Collections.Generic;
using System.Linq;

namespace ReviFlash.Models;

public class MatchFlashCard(string front, string back, List<(string leftText, string rightText)> options) : FlashCard(front, back)
{
    public List<(string leftText, string rightText)> Options { get; set; } = options;
    public override IReadOnlyList<MatchPreviewPair> MatchPairsPreview =>
        [.. Options.Select(o => new MatchPreviewPair { LeftText = o.leftText, RightText = o.rightText })];

    public MatchFlashCard(string front, string back, List<(string leftText, string rightText)> options, ulong id) : this(front, back, options)
    {
        ID = id;
    }

    public override bool VerifyAnswer(object answer)
    {
        if (answer is not List<(string leftText, string rightText)> selectedOptions) return false;
        
        foreach (var (leftText, rightText) in Options)
            if (!selectedOptions.Contains((leftText, rightText)))
                return false; 
        
        return true;
    }
}