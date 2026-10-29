using System;
using System.Collections.Generic;
using System.Linq;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

public class MatchFlashCard(string front, string back, List<(string leftText, string rightText)> options, bool keepLeftOrder = false) : FlashCard(front, back)
{
    public override string TypeName => CardUtility.CARD_TYPE_MATCH;

    /// <summary> Reviews show the left side in its written order; otherwise it is shuffled. </summary>
    public bool KeepLeftOrder { get; } = keepLeftOrder;

    public List<(string leftText, string rightText)> Options { get; set; } = options;
    public override IReadOnlyList<MatchPreviewPair> MatchPairsPreview =>
        [.. Options.Select(o => new MatchPreviewPair { LeftText = o.leftText, RightText = o.rightText })];

    public MatchFlashCard(string front, string back, List<(string leftText, string rightText)> options, ulong id, bool keepLeftOrder = false) : this(front, back, options, keepLeftOrder)
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