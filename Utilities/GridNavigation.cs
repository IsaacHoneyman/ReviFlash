using System.Collections.Generic;

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

    /// <summary> Movement through sections stacked one above another, each a grid like <see cref="Move"/>'s; Up off the top section gives (-1, -1). </summary>
    /// <param name="sections"> Each section's item count and columns, top to bottom; empty sections are stepped over. </param>
    public static (int Section, int Index) MoveAcross(IReadOnlyList<(int Count, int Columns)> sections, int section, int index, GridMove move)
    {
        int first = NextFilled(sections, -1, 1), last = NextFilled(sections, sections.Count, -1);
        if (first < 0) return (-1, -1);
        if (section < 0 || section >= sections.Count || index < 0 || index >= sections[section].Count) return (first, 0);

        var (count, columns) = sections[section];
        columns = System.Math.Max(1, columns);
        int above = NextFilled(sections, section, -1), below = NextFilled(sections, section, 1);

        switch (move)
        {
            case GridMove.First:
                return (first, 0);
            case GridMove.Last:
                return (last, sections[last].Count - 1);
            case GridMove.Left when index == 0 && above >= 0:
                return (above, sections[above].Count - 1);
            case GridMove.Right when index == count - 1 && below >= 0:
                return (below, 0);
            case GridMove.Up when index < columns:
                if (above < 0) return (-1, -1);
                var (aboveCount, aboveColumns) = sections[above];
                aboveColumns = System.Math.Max(1, aboveColumns);
                return (above, System.Math.Min(aboveCount - 1, (aboveCount - 1) / aboveColumns * aboveColumns + index % aboveColumns));
            case GridMove.Down when index / columns == (count - 1) / columns && below >= 0:
                return (below, System.Math.Min(sections[below].Count - 1, index % columns));
            default:
                return (section, Move(index, count, columns, move));
        }
    }

    private static int NextFilled(IReadOnlyList<(int Count, int Columns)> sections, int from, int step)
    {
        for (int i = from + step; i >= 0 && i < sections.Count; i += step)
            if (sections[i].Count > 0) return i;
        return -1;
    }
}
