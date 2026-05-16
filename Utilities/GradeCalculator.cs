namespace ReviFlash.Utilities;

/// <summary>
/// Utility class for grade calculations. Consolidates grading logic to a single,
/// testable location and ensures consistency across the application.
/// </summary>
public static class GradeCalculator
{
    /// <summary>
    /// Calculates a letter grade based on percentage score.
    /// </summary>
    /// <param name="percentage">The percentage score (0-100)</param>
    /// <returns>A letter grade (A*, A, B, C, D, U)</returns>
    public static string CalculateLetterGrade(double percentage)
    {
        return percentage switch
        {
            >= GradingConstants.GRADE_A_STAR_THRESHOLD => GradingConstants.GRADE_A_STAR,
            >= GradingConstants.GRADE_A_THRESHOLD => GradingConstants.GRADE_A,
            >= GradingConstants.GRADE_B_THRESHOLD => GradingConstants.GRADE_B,
            >= GradingConstants.GRADE_C_THRESHOLD => GradingConstants.GRADE_C,
            >= GradingConstants.GRADE_D_THRESHOLD => GradingConstants.GRADE_D,
            _ => GradingConstants.GRADE_U,
        };
    }

    /// <summary>
    /// Calculates a letter grade based on correct and total attempts.
    /// Returns "-" for ungraded (no attempts).
    /// </summary>
    public static string CalculateGradeWithDefault(int correct, int total)
    {
        if (total == 0)
        {
            return GradingConstants.GRADE_UNGRADED;
        }

        double percentage = (correct * 100.0) / total;
        return CalculateLetterGrade(percentage);
    }

    /// <summary>
    /// Calculates a percentage score.
    /// </summary>
    public static double CalculatePercentage(int correct, int total)
    {
        return total == 0 ? 0 : (correct * 100.0) / total;
    }
}
