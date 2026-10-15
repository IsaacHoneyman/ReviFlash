using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using ReviFlash.Data.Local;

namespace ReviFlash.Views;

/// <summary> Decoded images shared by every MathText; the last few stay loaded, since an editor preview redraws on each key. </summary>
internal static class ImageCache
{
    private const int KeptRecent = 8;
    private const int PruneAbove = 256;

    private static readonly Dictionary<string, WeakReference<Bitmap>> Loaded = new();
    private static readonly LinkedList<(string Name, Bitmap Bitmap)> Recent = new();

    /// <summary> The image, or null when the name isn't one the store makes or its file is missing or unreadable. </summary>
    public static Bitmap? Get(string name)
    {
        if (Loaded.TryGetValue(name, out var weak) && weak.TryGetTarget(out var cached))
        {
            KeepRecent(name, cached);
            return cached;
        }

        if (ImageStore.PathOf(name) is not { } path || !File.Exists(path)) return null;

        Bitmap bitmap;
        try { bitmap = new Bitmap(path); }
        catch (Exception ex)
        {
            Logger.LogError($"Couldn't load image {name}", ex);
            return null;
        }

        if (Loaded.Count >= PruneAbove)
        {
            foreach (var dead in Loaded.Where(entry => !entry.Value.TryGetTarget(out _)).Select(entry => entry.Key).ToList())
                Loaded.Remove(dead);
        }
        Loaded[name] = new WeakReference<Bitmap>(bitmap);
        KeepRecent(name, bitmap);
        return bitmap;
    }

    private static void KeepRecent(string name, Bitmap bitmap)
    {
        var existing = Recent.FirstOrDefault(entry => entry.Name == name);
        if (existing.Bitmap is not null) Recent.Remove(existing);
        Recent.AddFirst((name, bitmap));
        if (Recent.Count > KeptRecent) Recent.RemoveLast();
    }
}
