using System;

namespace ReviFlash.Models;

public abstract class FlashCard
{
    private static ulong IDCounter = 0;
    
    private static ulong GenerateID()
    {
        return IDCounter++;
    }

    public ulong ID {get; protected set; }
    public string Front { get; private set; }
    public string Back { get; private set; } 
    public FlashCardMetaData MetaData { get; private set; } = new FlashCardMetaData();
    
    public FlashCard(string front, string back, bool generateID = true)
    {
        Front = front;
        Back = back;
        if (generateID) ID = GenerateID();
    }

    public abstract bool VerifyAnswer(object answer);

    public class FlashCardMetaData
    {
        public DateTime CreationDate { get; private set; }
        public DateTime LastReviewDate { get; private set; }

        public ulong TimesReviewed { get; private set; } = 0;
        public ulong TimesCorrect { get; private set; } = 0;

        public FlashCardMetaData()
        {
            this.CreationDate = DateTime.Now;
            this.LastReviewDate = DateTime.Now;
        }

        public void UpdateReviewData(bool wasCorrect)
        {
            TimesReviewed++;
            if (wasCorrect) TimesCorrect++;
            LastReviewDate = DateTime.Now;
        }
    }
}