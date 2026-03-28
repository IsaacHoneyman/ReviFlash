using System;
using System.Collections.Generic;
using System.Linq;

namespace ReviFlash.Models;

public class MultiFlashCard : FlashCard
{
    public List<(string optionText, bool isCorrect)> Options { get; set; } = [];
    public override IReadOnlyList<MultiChoicePreviewOption> MultiChoiceOptionsPreview =>
        Options.Select(o => new MultiChoicePreviewOption { Text = o.optionText, IsCorrect = o.isCorrect }).ToList();

    public MultiFlashCard(string front, string back, List<(string optionText, bool isCorrect)> options) : base(front, back)
    {
        Options = options;
    }

    public MultiFlashCard(string front, string back, List<(string optionText, bool isCorrect)> options, ulong id) : this(front, back, options)
    {
        ID = id;
    }

    public override bool VerifyAnswer(object answer)
    {
        if (answer is not List<string> selectedOptions) return false;
        
        foreach (var (optionText, isCorrect) in Options)
        {
            if (isCorrect && !selectedOptions.Contains(optionText))
                return false; 
            if (!isCorrect && selectedOptions.Contains(optionText))
                return false; 
        }
        
        return true;
    }
}