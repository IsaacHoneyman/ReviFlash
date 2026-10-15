using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ReviFlash.Utilities;
using SkiaSharp;

namespace ReviFlash.Data.Local;

/// <summary> Why an image couldn't be added, in words for the user. </summary>
public sealed class ImageStoreException(string message) : Exception(message);

/// <summary> Images in the app data folder, saved as WebP and named by a hash of their contents, so adding one twice stores it once. </summary>
public static partial class ImageStore
{
    public const int MaxSide = 1600;
    private const int WebpQuality = 85;
    private const long MaxSourceBytes = 25 * 1024 * 1024;

    [GeneratedRegex(@"^[0-9a-f]{32}\.webp$")]
    private static partial Regex NameRegex();

    public static string Folder => Path.Combine(TextUtility.BaseDirectory, "images");

    /// <summary> Only names this store makes are accepted, so a token can't point outside the folder. </summary>
    public static bool IsValidName(string name) => NameRegex().IsMatch(name);

    public static string? PathOf(string name) => IsValidName(name) ? Path.Combine(Folder, name) : null;

    public static string AddFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new ImageStoreException("That file couldn't be found.");
        if (info.Length > MaxSourceBytes) throw new ImageStoreException("That image is too big (over 25 MB).");
        return Add(File.ReadAllBytes(path));
    }

    /// <summary> Saves the image, shrunk to fit <see cref="MaxSide"/> and upright, and returns its name. </summary>
    public static string Add(byte[] source)
    {
        if (source.Length > MaxSourceBytes) throw new ImageStoreException("That image is too big (over 25 MB).");

        using var bitmap = Decode(source) ?? throw new ImageStoreException("That file isn't an image ReviFlash can read (PNG, JPEG, WebP, GIF or BMP).");
        using var resized = Shrink(bitmap);
        using var image = SKImage.FromBitmap(resized ?? bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, WebpQuality)
            ?? throw new ImageStoreException("That image couldn't be saved.");

        var bytes = data.ToArray();
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes))[..32] + ".webp";
        var path = Path.Combine(Folder, name);
        if (File.Exists(path))
        {
            // A fresh time keeps a startup cleanup that's already running from deleting it.
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            return name;
        }

        Directory.CreateDirectory(Folder);
        var partial = path + ".partial";
        File.WriteAllBytes(partial, bytes);
        File.Move(partial, path, overwrite: true);
        return name;
    }

    /// <summary> Every stored image's name, skipping anything else in the folder. </summary>
    public static List<string> StoredNames() =>
        Directory.Exists(Folder)
            ? [.. Directory.EnumerateFiles(Folder).Select(Path.GetFileName).OfType<string>().Where(IsValidName)]
            : [];

    /// <summary> The names of the images some card, option, pair or note still shows. </summary>
    public static HashSet<string> ReferencedNames()
    {
        var texts = Db.Query(@"
            SELECT Front FROM Cards UNION ALL SELECT Back FROM Cards UNION ALL SELECT Answer FROM Cards WHERE Answer IS NOT NULL
            UNION ALL SELECT OptionText FROM CardOptions
            UNION ALL SELECT LeftText FROM MatchCardPairs UNION ALL SELECT RightText FROM MatchCardPairs
            UNION ALL SELECT Content FROM Notes;", reader => reader.GetString(0));

        var names = new HashSet<string>();
        foreach (var text in texts)
        {
            foreach (Match token in ImageToken.TokenRegex().Matches(text))
            {
                if (ImageToken.TryRead(token.Value, 0, out var image, out _)) names.Add(image.Name);
            }
        }
        return names;
    }

    /// <summary> Deletes images nothing refers to any more; ones written after <paramref name="startedAt"/> (just added) are kept. Returns how many went. </summary>
    public static int RemoveUnused(DateTime startedAt)
    {
        var stored = StoredNames();
        if (stored.Count == 0) return 0;

        var referenced = ReferencedNames();
        var removed = 0;
        foreach (var name in stored.Where(name => !referenced.Contains(name)))
        {
            var path = Path.Combine(Folder, name);
            try
            {
                if (File.GetLastWriteTimeUtc(path) >= startedAt.ToUniversalTime()) continue;
                File.Delete(path);
                removed++;
            }
            catch (IOException ex) { Logger.LogError($"Couldn't remove unused image {name}", ex); }
        }
        return removed;
    }

    /// <summary> Decodes the first frame, turned upright by its EXIF orientation (photos from phones). </summary>
    private static SKBitmap? Decode(byte[] source)
    {
        using var stream = new SKMemoryStream(source);
        using var codec = SKCodec.Create(stream);
        if (codec is null) return null;

        var decoded = new SKBitmap(codec.Info.WithColorType(SKImageInfo.PlatformColorType).WithAlphaType(SKAlphaType.Premul));
        var result = codec.GetPixels(decoded.Info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            decoded.Dispose();
            return null;
        }

        var degrees = codec.EncodedOrigin switch
        {
            SKEncodedOrigin.RightTop => 90,
            SKEncodedOrigin.BottomRight => 180,
            SKEncodedOrigin.LeftBottom => 270,
            _ => 0,
        };
        if (degrees == 0) return decoded;

        using (decoded) return Rotate(decoded, degrees);
    }

    private static SKBitmap Rotate(SKBitmap source, int degrees)
    {
        var sideways = degrees != 180;
        var rotated = new SKBitmap(sideways ? source.Height : source.Width, sideways ? source.Width : source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(rotated);
        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawBitmap(source, 0, 0);
        return rotated;
    }

    /// <summary> A copy no larger than <see cref="MaxSide"/> on its longest side, or null when it already fits. </summary>
    private static SKBitmap? Shrink(SKBitmap bitmap)
    {
        var longest = Math.Max(bitmap.Width, bitmap.Height);
        if (longest <= MaxSide) return null;

        var scale = (double)MaxSide / longest;
        var size = new SKImageInfo(Math.Max(1, (int)Math.Round(bitmap.Width * scale)), Math.Max(1, (int)Math.Round(bitmap.Height * scale)), bitmap.ColorType, bitmap.AlphaType);
        return bitmap.Resize(size, SKFilterQuality.High) ?? throw new ImageStoreException("That image couldn't be resized.");
    }
}
