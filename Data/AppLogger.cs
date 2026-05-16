using System;
using System.Diagnostics;

namespace ReviFlash.Data;

public static class AppLogger
{
    public static void Info(string message)
    {
        Trace.WriteLine(message);
        Debug.WriteLine(message);
    }

    public static void Error(string message)
    {
        Trace.WriteLine(message);
        Debug.WriteLine(message);
        Console.Error.WriteLine(message);
    }

    public static void Error(string message, Exception exception)
    {
        Error($"{message}: {exception.Message}");
    }
}