using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ReviFlash.Data.Local;

/// <summary> Runs parameterised SQL against the library database; null parameter values are stored as NULL. </summary>
internal static class Db
{
    // --- Connections ---

    public static SqliteConnection Open()
    {
        var connection = DatabaseManager.GetConnection();
        connection.Open();
        return connection;
    }

    /// <summary> Opens a database file outside the pool, so the file can be deleted as soon as the connection is disposed. </summary>
    public static SqliteConnection OpenUnpooled(string databasePath, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = mode,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    public static void InTransaction(Action<SqliteConnection, SqliteTransaction> work)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        work(connection, transaction);
        transaction.Commit();
    }

    public static T InTransaction<T>(Func<SqliteConnection, SqliteTransaction, T> work)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var result = work(connection, transaction);
        transaction.Commit();
        return result;
    }

    // --- Commands ---

    public static int Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = Open();
        return Execute(connection, null, sql, parameters);
    }

    public static int Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, transaction, sql, parameters);
        return command.ExecuteNonQuery();
    }

    /// <summary> The first column of the first row, or default when there is no row or it is NULL. </summary>
    public static T? Scalar<T>(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = Open();
        return Scalar<T>(connection, null, sql, parameters);
    }

    public static T? Scalar<T>(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, transaction, sql, parameters);
        var result = command.ExecuteScalar();
        if (result is null or DBNull) return default;

        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(result, type, CultureInfo.InvariantCulture);
    }

    /// <summary> Runs an INSERT (ending in a semicolon) and returns the new row's ID. </summary>
    public static ulong Insert(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = Open();
        return Insert(connection, null, sql, parameters);
    }

    public static ulong Insert(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, transaction, sql + "\nSELECT last_insert_rowid();", parameters);
        return (ulong)(long)(command.ExecuteScalar() ?? throw new InvalidOperationException("The insert returned no row ID."));
    }

    public static List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] parameters)
    {
        using var connection = Open();
        return Query(connection, null, sql, map, parameters);
    }

    public static List<T> Query<T>(SqliteConnection connection, SqliteTransaction? transaction, string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, transaction, sql, parameters);
        using var reader = command.ExecuteReader();

        var rows = new List<T>();
        while (reader.Read()) rows.Add(map(reader));
        return rows;
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction, string sql, (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    // --- Schema ---

    public static bool TableExists(SqliteConnection connection, string table) =>
        Scalar<long>(connection, null, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;", ("$name", table)) > 0;

    // --- Stats ---

    /// <summary> " AND ..." conditions for an optional ID ($id in <paramref name="idCondition"/>) and an optional SQLite date-modifier period. </summary>
    public static (string Sql, (string Name, object? Value)[] Parameters) StatsFilter(string dateColumn, string? timeModifier, string? idCondition = null, ulong? id = null)
    {
        var sql = "";
        var parameters = new List<(string Name, object? Value)>();

        if (id.HasValue && idCondition is not null)
        {
            sql += $" AND {idCondition}";
            parameters.Add(("$id", id.Value));
        }
        if (!string.IsNullOrEmpty(timeModifier))
        {
            sql += $" AND {dateColumn} >= DATE('now', $timeModifier)";
            parameters.Add(("$timeModifier", timeModifier));
        }

        return (sql, parameters.ToArray());
    }

    // --- Dates ---

    /// <summary> A stats day, stored as ISO "yyyy-MM-dd" text. </summary>
    public static DateOnly ParseDay(string raw) => DateOnly.Parse(raw, CultureInfo.InvariantCulture);

    /// <summary> Dates are stored as ISO text; anything unparseable reads as null. </summary>
    public static DateTime? ReadNullableDate(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index)) return null;

        var raw = reader.GetValue(index)?.ToString();
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
    }
}
