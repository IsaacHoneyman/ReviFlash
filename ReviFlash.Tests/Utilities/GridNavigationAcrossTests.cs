using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

/// <summary> Folders (5, in rows of 4) above the other cards (6, in rows of 3). </summary>
public class GridNavigationAcrossTests
{
    private static readonly (int Count, int Columns)[] Sections = [(5, 4), (6, 3)];

    private static (int, int) Move(int section, int index, GridMove move) => GridNavigation.MoveAcross(Sections, section, index, move);

    [Fact]
    public void Within_a_section_it_moves_like_one_grid()
    {
        Assert.Equal((1, 4), Move(1, 1, GridMove.Down));
        Assert.Equal((0, 1), Move(0, 0, GridMove.Right));
    }

    [Fact]
    public void Down_from_the_last_folder_row_lands_on_the_cards_below()
    {
        Assert.Equal((1, 0), Move(0, 4, GridMove.Down));
        Assert.Equal((0, 4), Move(0, 2, GridMove.Down));

        (int, int)[] oneFolderRow = [(3, 4), (6, 3)];
        Assert.Equal((1, 2), GridNavigation.MoveAcross(oneFolderRow, 0, 2, GridMove.Down));
    }

    [Fact]
    public void Up_from_the_top_card_row_lands_on_the_last_folder_row()
    {
        Assert.Equal((0, 4), Move(1, 0, GridMove.Up));
        Assert.Equal((0, 4), Move(1, 2, GridMove.Up));
    }

    [Fact]
    public void Left_and_right_step_across_the_boundary()
    {
        Assert.Equal((1, 0), Move(0, 4, GridMove.Right));
        Assert.Equal((0, 4), Move(1, 0, GridMove.Left));
    }

    [Fact]
    public void Up_from_the_top_section_leaves_the_grid()
    {
        Assert.Equal((-1, -1), Move(0, 2, GridMove.Up));
    }

    [Fact]
    public void Home_and_end_reach_the_very_first_and_last_items()
    {
        Assert.Equal((0, 0), Move(1, 3, GridMove.First));
        Assert.Equal((1, 5), Move(0, 1, GridMove.Last));
    }

    [Fact]
    public void An_empty_section_is_stepped_over_and_nothing_focused_starts_at_the_first_item()
    {
        (int, int)[] onlyCards = [(0, 4), (6, 3)];
        Assert.Equal((1, 0), GridNavigation.MoveAcross(onlyCards, -1, -1, GridMove.Down));
        Assert.Equal((-1, -1), GridNavigation.MoveAcross(onlyCards, 1, 1, GridMove.Up));
    }
}
