using System;

namespace ReviFlash.Models;

/// <summary> One answer given in a review: which card and part of it, when, whether it was right, and how long it took. </summary>
public readonly record struct CardResult(ulong CardID, string Part, DateTime AnsweredAt, bool Correct, int Seconds);
