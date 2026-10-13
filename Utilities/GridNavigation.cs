namespace ReviFlash.Utilities;

public enum GridMove { Left, Right, Up, Down, First, Last }

/// <summary> Keyboard movement through items laid out left to right in rows of equal width. </summary>
public static class GridNavigation
{
    /// <summary> The index focus moves to, or -1 when Up leaves the top row; with nothing focused every move lands on the first item. </summary>
    public static int Move(int index, int count, int columns, GridMove move)
    {
        if (count == 0) return -1;
        if (index < 0 || index >= count) return 0;
        if (columns < 1) columns = 1;

        return move switch
        {
            GridMove.Left => System.Math.Max(0, index - 1),
            GridMove.Right => System.Math.Min(count - 1, index + 1),
            GridMove.Up => index >= columns ? index - columns : -1,
            GridMove.Down when index + columns < count => index + columns,
            GridMove.Down when index / columns < (count - 1) / columns => count - 1,
            GridMove.Down => index,
            GridMove.First => 0,
            GridMove.Last => count - 1,
            _ => index,
        };
    }
}
