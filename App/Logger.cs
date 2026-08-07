using System;
using System.Diagnostics;

namespace ReviFlash;

/// <summary> Logging for debugging ReviFlash. </summary>
public static class Logger
{
    public static void LogInfo<T>(T? message) =>
        Write(message?.ToString() ?? "", ConsoleColor.White, false);

    public static void LogWarning<T>(T? warning) =>
        Write($"[Warning] {warning?.ToString() ?? ""}", ConsoleColor.Yellow, false);

    public static void LogError<T>(T? error) =>
       Write($"[Error] {error?.ToString() ?? ""}", ConsoleColor.Red, true);

    public static void LogError<T>(T? error, Exception e) => 
        LogError($"{error} - {e}");

    private static void Write(string message, ConsoleColor colour, bool isError)
    {
        string output = $"[{DateTime.Now:HH:mm:ss.ffff}] {message}";
        Trace.WriteLine(output);
        Debug.WriteLine(output);

        ConsoleColor oColour = Console.ForegroundColor;
        Console.ForegroundColor = colour;
        try
        {
            if (isError) Console.Error.WriteLine(output);
            else { Console.WriteLine(output); }
        }
        finally { Console.ForegroundColor = oColour; }
    }
}