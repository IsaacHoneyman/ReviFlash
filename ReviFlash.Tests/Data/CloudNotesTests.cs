using System.Net;
using System.Text.Json;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class CloudNotesTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, Uri.UnescapeDataString(request.RequestUri!.PathAndQuery), body));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(request)) };
        }
    }

    private static (SupabaseConnection Client, FakeHandler Handler) Connect(string reply = "[]")
    {
        var handler = new FakeHandler(_ => reply);
        return (new SupabaseConnection("token", handler), handler);
    }

    [Fact]
    public async Task Uploading_a_note_uses_the_notes_bucket_and_table_with_its_word_count()
    {
        var (client, handler) = Connect();

        var (success, message) = await client.UploadCloudDeckAsync("user-1", "Lecture 1", "Year 1", 250, "{}", isPrivate: true, CloudKind.Notes);

        Assert.True(success);
        Assert.Equal("Note uploaded privately!", message);
        Assert.StartsWith("/storage/v1/object/notes/user-1/", handler.Requests[0].Url);
        Assert.Equal("/rest/v1/notes", handler.Requests[1].Url);
        using var row = JsonDocument.Parse(handler.Requests[1].Body!);
        Assert.Equal(250, row.RootElement.GetProperty("word_count").GetInt32());
        Assert.False(row.RootElement.TryGetProperty("card_count", out _));
    }

    [Fact]
    public async Task Sets_still_use_the_decks_bucket_and_table_with_a_card_count()
    {
        var (client, handler) = Connect();

        await client.UploadCloudDeckAsync("user-1", "Biology", "", 12, "{}", isPrivate: false);

        Assert.StartsWith("/storage/v1/object/decks/user-1/", handler.Requests[0].Url);
        Assert.Equal("/rest/v1/decks", handler.Requests[1].Url);
        using var row = JsonDocument.Parse(handler.Requests[1].Body!);
        Assert.Equal(12, row.RootElement.GetProperty("card_count").GetInt32());
    }

    [Fact]
    public async Task Every_note_call_goes_to_the_notes_side()
    {
        var (client, handler) = Connect("""[{"id":"6f1c1c3e-0000-0000-0000-000000000001","title":"Lecture","word_count":3,"storage_path":"user-1/a.json"}]""");
        var notes = CloudKind.Notes;

        var listed = await client.GetUserCloudDecksAsync("user-1", notes);
        await client.GetPublicDecksAsync("graphs", 25, notes);
        await client.UpdateCloudDeckAsync("user-1/a.json", "Lecture", "", 3, "{}", notes);
        await client.SetCloudDeckVisibilityAsync("user-1", "user-1/a.json", makePrivate: false, notes);
        await client.RecordDownloadAsync(listed[0].Id, notes);
        await client.DownloadCloudDeckJsonAsync("user-1/a.json", notes);
        await client.DeleteCloudDeckAsync("user-1/a.json", notes);

        Assert.All(handler.Requests, request => Assert.DoesNotContain("deck", request.Url));
        Assert.Contains(handler.Requests, request => request.Url.StartsWith("/rest/v1/notes?select=") && request.Url.Contains("word_count"));
        Assert.Contains(handler.Requests, request => request.Url.StartsWith("/rest/v1/rpc/search_public_notes"));
        Assert.Contains(handler.Requests, request => request.Url == "/rest/v1/rpc/record_note_download" && request.Body!.Contains("p_note_id"));
        Assert.Contains(handler.Requests, request => request.Url == "/storage/v1/object/public/notes/user-1/a.json");
        Assert.Equal("3 words", Assert.Single(listed).SizeText);
        Assert.True(listed[0].IsNote);
    }

    [Fact]
    public void A_downloaded_note_becomes_a_new_note_in_its_folders()
    {
        using var database = new TestDatabase();
        var json = DeckTransferManager.GenerateCloudNoteJson("Graphs", "\\H1{Graphs}\n\nA graph has vertices.", ["Year 1", "Algorithms"]);

        var note = DeckTransferManager.TryImportCloudNote(json, targetFolderID: null, recreateFolders: true);
        var again = DeckTransferManager.TryImportCloudNote(json);

        Assert.Equal("Graphs", note.Name);
        Assert.Equal("\\H1{Graphs}\n\nA graph has vertices.", NoteRepository.GetContent(note.ID));
        Assert.Equal("Year 1 / Algorithms", DeckTransferManager.GetFolderPathNames(note.FolderID) is var path ? string.Join(" / ", path) : "");
        Assert.NotEqual(note.ID, again.ID);
        Assert.Null(again.FolderID);
    }

    [Fact]
    public void A_download_without_text_is_refused()
    {
        using var database = new TestDatabase();

        Assert.Throws<InvalidDataException>(() => DeckTransferManager.TryImportCloudNote("""{"NoteName":"Empty"}"""));
    }

    [Fact]
    public void Notes_that_are_empty_or_show_an_image_cant_be_uploaded()
    {
        using var database = new TestDatabase();
        var vm = new OnlineExportViewModel { ShowNotes = true };
        var empty = NoteRepository.CreateNote("Empty", null);
        var pictured = NoteRepository.CreateNote("Pictured", null);
        NoteRepository.SaveContent(pictured.ID, "Look: \\img{0123456789abcdef0123456789abcdef.webp}");
        var plain = NoteRepository.CreateNote("Plain", null);
        NoteRepository.SaveContent(plain.ID, "Three words here");

        Assert.Null(vm.PrepareUpload(empty));
        Assert.Contains("empty note", vm.StatusMessage);
        Assert.Null(vm.PrepareUpload(pictured));
        Assert.Contains("notes with images", vm.StatusMessage);

        var upload = Assert.IsType<CloudUpload>(vm.PrepareUpload(plain));
        Assert.Equal(3, upload.Size);
        Assert.Contains("\"Content\": \"Three words here\"", upload.Json);
    }
}
