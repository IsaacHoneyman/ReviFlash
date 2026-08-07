using System.Runtime.CompilerServices;

namespace ReviFlash.Utilities;

/// <summary> Utility for grade calculations and card types </summary>
public static class CardUtility
{
    public const int GRADE_A_STAR_THRESHOLD = 90;
    public const int GRADE_A_THRESHOLD = 80;
    public const int GRADE_B_THRESHOLD = 70;
    public const int GRADE_C_THRESHOLD = 60;
    public const int GRADE_D_THRESHOLD = 50;

    public const string GRADE_A_STAR = "A*";
    public const string GRADE_A = "A";
    public const string GRADE_B = "B";
    public const string GRADE_C = "C";
    public const string GRADE_D = "D";
    public const string GRADE_U = "U";
    public const string GRADE_UNGRADED = "-";

    public const string CARD_TYPE_FLIP = "Flip";
    public const string CARD_TYPE_TYPE = "Type to Answer";
    public const string CARD_TYPE_MULTI_CHOICE = "Multi Choice";
    public const string CARD_TYPE_MATCH = "Match";
    public const string CARD_TYPE_TRUE_FALSE = "True/False";
    public const string CARD_TYPE_MATCH_PLACEHOLDER = "Match The Cards";

    public const string TRUE_LABEL = "True";
    public const string FALSE_LABEL = "False";

    public static string CalculateLetterGrade(double percentage)
    {
        return percentage switch
        {
            >= GRADE_A_STAR_THRESHOLD => GRADE_A_STAR,
            >= GRADE_A_THRESHOLD => GRADE_A,
            >= GRADE_B_THRESHOLD => GRADE_B,
            >= GRADE_C_THRESHOLD => GRADE_C,
            >= GRADE_D_THRESHOLD => GRADE_D,
            _ => GRADE_U,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string CalculateGradeWithDefault(int correct, int total)
    {
        if (total == 0) return GRADE_UNGRADED;
        return CalculateLetterGrade((correct * 100.0) / total);
    }
}
