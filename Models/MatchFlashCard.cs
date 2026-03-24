using System;
using System.Collections.Generic;

namespace ReviFlash.Models;

public class MatchFlashCard : FlashCard
{
    public List<(string leftText, string rightText)> Options { get; set; } = [];

    public MatchFlashCard(string front, string back, List<(string leftText, string rightText)> options) : base(front, back)
    {
        Options = options;
    }

    public MatchFlashCard(string front, string back, List<(string leftText, string rightText)> options, ulong id) : this(front, back, options)
    {
        ID = id;
    }

    public override bool VerifyAnswer(object answer)
    {
        if (answer is not List<(string leftText, string rightText)> selectedOptions) return false;
        
        foreach (var (leftText, rightText) in Options)
        {
            if (!selectedOptions.Contains((leftText, rightText)))
                return false; 
        }
        
        return true;
    }
}