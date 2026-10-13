using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class GridNavigationTests
{
    // 10 cards in rows of 4:  0 1 2 3 / 4 5 6 7 / 8 9
    private static int Move(int index, GridMove move) => GridNavigation.Move(index, 10, 4, move);

    [Theory]
    [InlineData(5, GridMove.Left, 4)]
    [InlineData(5, GridMove.Right, 6)]
    [InlineData(5, GridMove.Up, 1)]
    [InlineData(5, GridMove.Down, 9)]
    [InlineData(5, GridMove.First, 0)]
    [InlineData(5, GridMove.Last, 9)]
    public void Arrows_move_one_card_or_one_row(int from, GridMove move, int to)
    {
        Assert.Equal(to, Move(from, move));
    }

    [Fact]
    public void Left_and_right_run_on_across_rows()
    {
        Assert.Equal(3, Move(4, GridMove.Left));
        Assert.Equal(4, Move(3, GridMove.Right));
    }

    [Fact]
    public void Left_and_right_stop_at_the_ends()
    {
        Assert.Equal(0, Move(0, GridMove.Left));
        Assert.Equal(9, Move(9, GridMove.Right));
    }

    [Fact]
    public void Up_from_the_top_row_leaves_the_grid()
    {
        Assert.Equal(-1, Move(2, GridMove.Up));
    }

    [Fact]
    public void Down_into_a_short_last_row_lands_on_its_last_card()
    {
        Assert.Equal(9, Move(6, GridMove.Down));
    }

    [Fact]
    public void Down_on_the_last_row_stays_put()
    {
        Assert.Equal(8, Move(8, GridMove.Down));
    }

    [Fact]
    public void With_nothing_focused_any_move_lands_on_the_first_card()
    {
        Assert.Equal(0, Move(-1, GridMove.Down));
        Assert.Equal(0, Move(-1, GridMove.Last));
    }

    [Fact]
    public void An_empty_grid_has_nowhere_to_go()
    {
        Assert.Equal(-1, GridNavigation.Move(-1, 0, 4, GridMove.Down));
    }
}
