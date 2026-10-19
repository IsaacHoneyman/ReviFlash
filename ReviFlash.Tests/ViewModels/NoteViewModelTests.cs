using ReviFlash.Data.Local;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class NoteViewModelTests
{
    private static NoteViewModel Open(ulong id, string content)
    {
        NoteRepository.SaveContent(id, content);
        var note = NoteRepository.GetAllNotes().Single(n => n.ID == id);
        return new NoteViewModel(note, () => { });
    }

    private static ulong NewNote() => NoteRepository.CreateNote("Test", null).ID;

    [Fact]
    public void Splitting_a_paragraph_and_leaving_saves_both_halves()
    {
        using var database = new TestDatabase();
        var id = NewNote();
        var vm = Open(id, "helloworld");

        vm.BeginEdit(vm.Blocks[0]);
        vm.SplitEditingAt(5);
        vm.Close();

        Assert.Equal("hello\n\nworld", NoteRepository.GetContent(id));
    }

    [Fact]
    public void Splitting_at_the_start_of_the_only_paragraph_keeps_the_text()
    {
        using var database = new TestDatabase();
        var id = NewNote();
        var vm = Open(id, "hello");

        vm.BeginEdit(vm.Blocks[0]);
        vm.SplitEditingAt(0);
        vm.Close();

        Assert.Equal("hello", NoteRepository.GetContent(id));
    }

    [Fact]
    public void Joining_onto_the_previous_paragraph_saves_the_joined_text()
    {
        using var database = new TestDatabase();
        var id = NewNote();
        var vm = Open(id, "one\n\ntwo");

        vm.BeginEdit(vm.Blocks[1], caret: 0);
        Assert.True(vm.MergeEditingIntoPrevious());
        vm.Close();

        Assert.Equal("one\ntwo", NoteRepository.GetContent(id));
    }

    [Fact]
    public void Joining_the_next_paragraph_onto_the_edited_one_saves_the_joined_text()
    {
        using var database = new TestDatabase();
        var id = NewNote();
        var vm = Open(id, "one\n\ntwo\n\nthree");

        vm.BeginEdit(vm.Blocks[0]);
        Assert.True(vm.MergeNextIntoEditing());
        Assert.Same(vm.Blocks[0], vm.EditingBlock);
        vm.Close();

        Assert.Equal("one\ntwo\n\nthree", NoteRepository.GetContent(id));
    }

    [Fact]
    public void Joining_the_next_paragraph_does_nothing_on_the_last_paragraph()
    {
        using var database = new TestDatabase();
        var vm = Open(NewNote(), "one\n\ntwo");

        vm.BeginEdit(vm.Blocks[1]);

        Assert.False(vm.MergeNextIntoEditing());
        Assert.Equal(2, vm.Blocks.Count);
    }

    [Fact]
    public void A_block_set_to_null_text_reads_as_empty_and_saves()
    {
        using var database = new TestDatabase();
        var id = NewNote();
        var vm = Open(id, "one\n\ntwo");

        vm.Blocks[0].Text = null!;
        vm.SaveNow();

        Assert.Equal("", vm.Blocks[0].Text);
        Assert.Equal("two", NoteRepository.GetContent(id));
    }

    [Fact]
    public void Deleting_the_only_paragraph_being_edited_stops_editing_it()
    {
        using var database = new TestDatabase();
        var id = NewNote();
        var vm = Open(id, "hello");
        var block = vm.Blocks[0];

        vm.BeginEdit(block);
        vm.DeleteBlock(block);

        Assert.False(block.IsEditing);
        Assert.False(vm.IsEditingBlock);
        Assert.Equal("", block.Text);
    }
}
