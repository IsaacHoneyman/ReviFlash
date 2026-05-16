namespace ReviFlash.Utilities;

/// <summary>
/// Constants for grade thresholds and labels used throughout the application.
/// Extracted to a single location to avoid magic numbers and ensure consistency.
/// </summary>
public static class GradingConstants
{
    // Grade thresholds (percentage)
    public const int GRADE_A_STAR_THRESHOLD = 90;
    public const int GRADE_A_THRESHOLD = 80;
    public const int GRADE_B_THRESHOLD = 70;
    public const int GRADE_C_THRESHOLD = 60;
    public const int GRADE_D_THRESHOLD = 50;

    // Grade labels
    public const string GRADE_A_STAR = "A*";
    public const string GRADE_A = "A";
    public const string GRADE_B = "B";
    public const string GRADE_C = "C";
    public const string GRADE_D = "D";
    public const string GRADE_U = "U";
    public const string GRADE_UNGRADED = "-";

    // Card type labels used by the editor UI
    public const string CARD_TYPE_FLIP = "Flip";
    public const string CARD_TYPE_TYPE = "Type to Answer";
    public const string CARD_TYPE_MULTI_CHOICE = "Multi Choice";
    public const string CARD_TYPE_MATCH = "Match";
    public const string CARD_TYPE_TRUE_FALSE = "True/False";
    public const string CARD_TYPE_MATCH_PLACEHOLDER = "Match The Cards";

    // True/False labels (default)
    public const string TRUE_LABEL = "True";
    public const string FALSE_LABEL = "False";
}
