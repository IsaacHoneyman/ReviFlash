using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ReviFlash.Data.Local;
using ReviFlash.Utilities;

namespace ReviFlash.Views;

/// <summary> Puts images into card fields and note paragraphs: pasted, dropped in, or picked from a file. </summary>
public static class ImageInsertion
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp"];

    private static readonly FilePickerFileType ImageFiles = new("Images")
    {
        Patterns = [.. ImageExtensions.Select(extension => "*" + extension)],
        MimeTypes = ["image/png", "image/jpeg", "image/webp", "image/gif", "image/bmp"],
    };

    /// <summary> Set while a paste with no image is handed back to the box, so its own text paste runs. </summary>
    private static TextBox? _plainPasteBox;

    /// <summary> Lets the boxes under <paramref name="root"/> that <paramref name="accepts"/> take pasted and dropped images; <paramref name="showError"/> says why one couldn't be added. </summary>
    public static void Attach(Control root, Func<TextBox, bool> accepts, Action<string> showError)
    {
        DragDrop.SetAllowDrop(root, true);

        root.AddHandler(TextBox.PastingFromClipboardEvent, (_, e) =>
        {
            if (e.Source is not TextBox box || !accepts(box)) return;
            if (box == _plainPasteBox)
            {
                _plainPasteBox = null;
                return;
            }

            e.Handled = true;
            _ = PasteAsync(box, showError);
        });

        root.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = TargetOf(e, accepts) is not null && DroppedImages(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        });

        root.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (TargetOf(e, accepts) is not { } box) return;
            var paths = DroppedImages(e);
            if (paths.Count == 0) return;

            e.Handled = true;
            box.Focus();
            _ = InsertFilesAsync(box, paths, showError);
        });
    }

    /// <summary> Asks for image files and adds them at the cursor in <paramref name="box"/>. </summary>
    public static async Task PickAsync(TextBox box, Action<string> showError)
    {
        if (TopLevel.GetTopLevel(box)?.StorageProvider is not { } storage) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add images",
            AllowMultiple = true,
            FileTypeFilter = [ImageFiles],
        });
        await InsertFilesAsync(box, [.. files.Select(file => file.TryGetLocalPath()).OfType<string>()], showError);
    }

    private static async Task PasteAsync(TextBox box, Action<string> showError)
    {
        try
        {
            if (TopLevel.GetTopLevel(box)?.Clipboard is { } clipboard)
            {
                if (ImagePaths(await clipboard.TryGetFilesAsync()) is { Count: > 0 } paths)
                {
                    await InsertFilesAsync(box, paths, showError);
                    return;
                }

                if (await clipboard.TryGetBitmapAsync() is { } bitmap)
                {
                    byte[] png;
                    using (bitmap)
                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream);
                        png = stream.ToArray();
                    }
                    await InsertAsync(box, [() => ImageStore.Add(png)], showError);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Couldn't read an image from the clipboard.", ex);
        }

        _plainPasteBox = box;
        box.Paste();
    }

    private static Task InsertFilesAsync(TextBox box, IReadOnlyList<string> paths, Action<string> showError) =>
        InsertAsync(box, [.. paths.Select(path => (Func<string>)(() => ImageStore.AddFile(path)))], showError);

    /// <summary> Stores each image off the UI thread, then puts the ones that worked at the cursor, each on its own line. </summary>
    private static async Task InsertAsync(TextBox box, IReadOnlyList<Func<string>> adds, Action<string> showError)
    {
        var names = new List<string>();
        string? error = null;
        foreach (var add in adds)
        {
            try { names.Add(await Task.Run(add)); }
            catch (ImageStoreException ex) { error = ex.Message; }
            catch (Exception ex)
            {
                Logger.LogError("Couldn't add an image.", ex);
                error = "That image couldn't be added.";
            }
        }

        if (names.Count > 0)
        {
            var edit = InsertOnOwnLine(box.Text ?? "", box.SelectionStart, box.SelectionEnd, string.Join('\n', names.Select(name => ImageToken.Format(name))));
            box.Text = edit.Text;
            TextFormatting.Select(box, edit.SelectionStart, edit.SelectionEnd);
            box.Focus();
        }
        if (error is not null) showError(error);
    }

    /// <summary> Replaces the selection with <paramref name="tokens"/>, adding line breaks so they sit on lines of their own; the cursor goes after them. </summary>
    internal static TextFormatting.TextEdit InsertOnOwnLine(string text, int selectionStart, int selectionEnd, string tokens)
    {
        var start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);

        var before = start > 0 && text[start - 1] != '\n' ? "\n" : "";
        var after = end < text.Length && text[end] != '\n' ? "\n" : "";
        var inserted = before + tokens + after;

        var caret = start + before.Length + tokens.Length;
        return new TextFormatting.TextEdit(text[..start] + inserted + text[end..], caret, caret);
    }

    private static TextBox? TargetOf(DragEventArgs e, Func<TextBox, bool> accepts) =>
        (e.Source as Avalonia.Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is { } box && accepts(box) ? box : null;

    private static List<string> DroppedImages(DragEventArgs e) => ImagePaths(e.DataTransfer.TryGetFiles());

    private static List<string> ImagePaths(IEnumerable<IStorageItem>? items) =>
        [.. (items ?? []).Select(item => item.TryGetLocalPath()).OfType<string>()
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))];
}
