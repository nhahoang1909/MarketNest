using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     A static utility for safely generating raw PostgreSQL queries with parameterized values.
///     <para>
///         Use this ONLY when EF Core cannot express the needed SQL — e.g. complex multi-schema
///         joins, DDL commands (CREATE INDEX / SEQUENCE / SCHEMA), or PostgreSQL-specific features.
///     </para>
///     <para>
///         All value interpolation goes through positional parameters ($1, $2, …) to prevent
///         SQL injection. Identifier quoting handles untrusted table/column names.
///     </para>
/// </summary>
public static partial class PgQueryBuilder
{
    // =========================================================================
    // CORE: Parameterized Query Result
    // =========================================================================

    /// <summary>
    ///     Represents a fully built SQL query with its positional parameters.
    /// </summary>
    public sealed record PgQuery(string Sql, IReadOnlyList<object?> Parameters)
    {
        public override string ToString() => Sql;
    }

    // =========================================================================
    // INTERPOLATED QUERY BUILDER (FormattableString)
    // =========================================================================

    /// <summary>
    ///     Builds a parameterized <see cref="PgQuery" /> from an interpolated string.
    ///     Values are automatically extracted as $1, $2, … parameters.
    ///     <see cref="RawSqlFragment" /> values are inlined as-is (use only for trusted input).
    /// </summary>
    /// <example>
    ///     <code>
    ///     var q = PgQueryBuilder.Query($"SELECT * FROM users WHERE id = {userId} AND active = {true}");
    ///     // Sql:        "SELECT * FROM users WHERE id = $1 AND active = $2"
    ///     // Parameters: [userId, true]
    ///     </code>
    /// </example>
    public static PgQuery Query(FormattableString formattable)
    {
        ArgumentNullException.ThrowIfNull(formattable);

        object?[] args = formattable.GetArguments();
        List<object?> parameters = [];
        string format = formattable.Format;

        string sql = FormatPlaceholderRegex().Replace(format, match =>
        {
            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            object? value = index < args.Length ? args[index] : null;

            // RawSqlFragment is inlined verbatim — never parameterized
            if (value is RawSqlFragment raw)
                return raw.Value;

            parameters.Add(value);
            return "$" + parameters.Count.ToString(CultureInfo.InvariantCulture);
        });

        return new PgQuery(sql, parameters);
    }

    /// <summary>
    ///     Marks a string as a raw SQL fragment that will NOT be parameterized.
    ///     Use only for trusted, developer-controlled values (column names, keywords).
    ///     <strong>NEVER use with user input.</strong>
    /// </summary>
    public static RawSqlFragment Raw(string sql) => new(sql);

    /// <summary>
    ///     A trusted raw SQL fragment that bypasses parameterization.
    /// </summary>
    public sealed record RawSqlFragment(string Value)
    {
        public override string ToString() => Value;
    }

    // =========================================================================
    // IDENTIFIER QUOTING
    // =========================================================================

    /// <summary>
    ///     Safely quotes a PostgreSQL identifier (table, column, schema name).
    ///     Internal double-quotes are escaped by doubling.
    /// </summary>
    public static string Identifier(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return "\"" + name.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    ///     Quotes a schema-qualified identifier: <c>"schema"."table"</c>.
    /// </summary>
    public static string Identifier(string schema, string name)
        => Identifier(schema) + "." + Identifier(name);

    /// <summary>
    ///     Returns a <see cref="RawSqlFragment" /> of a quoted identifier for use inside
    ///     <see cref="Query(FormattableString)" />.
    /// </summary>
    public static RawSqlFragment IdentifierRaw(string name) => Raw(Identifier(name));

    /// <inheritdoc cref="IdentifierRaw(string)" />
    public static RawSqlFragment IdentifierRaw(string schema, string name) => Raw(Identifier(schema, name));

    // =========================================================================
    // SELECT BUILDER
    // =========================================================================

    /// <summary>
    ///     Builds a parameterized SELECT query with optional WHERE, ORDER BY, LIMIT, OFFSET.
    /// </summary>
    public static PgQuery Select(
        string table,
        IEnumerable<string>? columns = null,
        IDictionary<string, object?>? where = null,
        string? orderBy = null,
        bool orderDesc = false,
        int? limit = null,
        int? offset = null,
        string? schema = null)
    {
        List<object?> parameters = [];
        StringBuilder sb = new();

        List<string>? cols = columns?.ToList();
        string colSql = (cols is null || cols.Count == 0)
            ? "*"
            : string.Join(", ", cols.Select(Identifier));

        string tableSql = schema is not null ? Identifier(schema, table) : Identifier(table);

        sb.Append("SELECT ").Append(colSql).Append(" FROM ").Append(tableSql);

        AppendWhereClause(sb, parameters, where);

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            string direction = orderDesc ? "DESC" : "ASC";
            sb.Append(" ORDER BY ").Append(Identifier(orderBy)).Append(' ').Append(direction);
        }

        if (limit.HasValue)
        {
            parameters.Add(limit.Value);
            sb.Append(" LIMIT $").Append(parameters.Count.ToString(CultureInfo.InvariantCulture));
        }

        if (offset.HasValue)
        {
            parameters.Add(offset.Value);
            sb.Append(" OFFSET $").Append(parameters.Count.ToString(CultureInfo.InvariantCulture));
        }

        return new PgQuery(sb.ToString(), parameters);
    }

    // =========================================================================
    // INSERT BUILDER
    // =========================================================================

    /// <summary>
    ///     Builds a parameterized INSERT for a single row.
    /// </summary>
    public static PgQuery Insert(
        string table,
        IDictionary<string, object?> values,
        string? returning = null,
        string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ArgumentException("Values cannot be empty.", nameof(values));

        List<object?> parameters = [];
        string tableSql = schema is not null ? Identifier(schema, table) : Identifier(table);

        string columnsSql = string.Join(", ", values.Keys.Select(Identifier));
        string placeholders = string.Join(", ", values.Values.Select(v =>
        {
            parameters.Add(v);
            return "$" + parameters.Count.ToString(CultureInfo.InvariantCulture);
        }));

        string sql = "INSERT INTO " + tableSql + " (" + columnsSql + ") VALUES (" + placeholders + ")";

        if (!string.IsNullOrWhiteSpace(returning))
            sql += " RETURNING " + Identifier(returning);

        return new PgQuery(sql, parameters);
    }

    /// <summary>
    ///     Builds a parameterized bulk INSERT for multiple rows.
    ///     All rows must share the same column set (taken from the first row).
    /// </summary>
    public static PgQuery InsertMany(
        string table,
        IEnumerable<IDictionary<string, object?>> rows,
        string? returning = null,
        string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        List<IDictionary<string, object?>> rowList = rows.ToList();
        if (rowList.Count == 0)
            throw new ArgumentException("Rows cannot be empty.", nameof(rows));

        List<string> keys = rowList[0].Keys.ToList();
        List<object?> parameters = [];
        string tableSql = schema is not null ? Identifier(schema, table) : Identifier(table);
        string columnsSql = string.Join(", ", keys.Select(Identifier));

        IEnumerable<string> valueSets = rowList.Select(row =>
        {
            IEnumerable<string> placeholders = keys.Select(k =>
            {
                parameters.Add(row.TryGetValue(k, out object? val) ? val : null);
                return "$" + parameters.Count.ToString(CultureInfo.InvariantCulture);
            });
            return "(" + string.Join(", ", placeholders) + ")";
        });

        string sql = "INSERT INTO " + tableSql + " (" + columnsSql + ") VALUES " + string.Join(", ", valueSets);

        if (!string.IsNullOrWhiteSpace(returning))
            sql += " RETURNING " + Identifier(returning);

        return new PgQuery(sql, parameters);
    }

    // =========================================================================
    // UPDATE BUILDER
    // =========================================================================

    /// <summary>
    ///     Builds a parameterized UPDATE query.
    /// </summary>
    public static PgQuery Update(
        string table,
        IDictionary<string, object?> set,
        IDictionary<string, object?>? where = null,
        string? returning = null,
        string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (set.Count == 0)
            throw new ArgumentException("SET values cannot be empty.", nameof(set));

        List<object?> parameters = [];
        string tableSql = schema is not null ? Identifier(schema, table) : Identifier(table);

        IEnumerable<string> setClauses = set.Select(kv =>
        {
            parameters.Add(kv.Value);
            return Identifier(kv.Key) + " = $" + parameters.Count.ToString(CultureInfo.InvariantCulture);
        });

        StringBuilder sb = new();
        sb.Append("UPDATE ").Append(tableSql).Append(" SET ").Append(string.Join(", ", setClauses));

        AppendWhereClause(sb, parameters, where);

        if (!string.IsNullOrWhiteSpace(returning))
            sb.Append(" RETURNING ").Append(Identifier(returning));

        return new PgQuery(sb.ToString(), parameters);
    }

    // =========================================================================
    // DELETE BUILDER
    // =========================================================================

    /// <summary>
    ///     Builds a parameterized DELETE query.
    /// </summary>
    public static PgQuery Delete(
        string table,
        IDictionary<string, object?>? where = null,
        string? returning = null,
        string? schema = null)
    {
        List<object?> parameters = [];
        string tableSql = schema is not null ? Identifier(schema, table) : Identifier(table);

        StringBuilder sb = new();
        sb.Append("DELETE FROM ").Append(tableSql);

        AppendWhereClause(sb, parameters, where);

        if (!string.IsNullOrWhiteSpace(returning))
            sb.Append(" RETURNING ").Append(Identifier(returning));

        return new PgQuery(sb.ToString(), parameters);
    }

    // =========================================================================
    // UPSERT (INSERT ON CONFLICT)
    // =========================================================================

    /// <summary>
    ///     Builds a PostgreSQL UPSERT (INSERT … ON CONFLICT DO UPDATE).
    /// </summary>
    public static PgQuery Upsert(
        string table,
        IDictionary<string, object?> values,
        IEnumerable<string> conflictColumns,
        IEnumerable<string>? updateColumns = null,
        string? returning = null,
        string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ArgumentException("Values cannot be empty.", nameof(values));

        List<object?> parameters = [];
        string tableSql = schema is not null ? Identifier(schema, table) : Identifier(table);
        string columnsSql = string.Join(", ", values.Keys.Select(Identifier));
        string placeholders = string.Join(", ", values.Values.Select(v =>
        {
            parameters.Add(v);
            return "$" + parameters.Count.ToString(CultureInfo.InvariantCulture);
        }));

        string conflict = string.Join(", ", conflictColumns.Select(Identifier));

        List<string> updateCols = updateColumns?.ToList() ?? values.Keys.ToList();
        string updateSet = string.Join(", ", updateCols.Select(c =>
            Identifier(c) + " = EXCLUDED." + Identifier(c)));

        string sql = "INSERT INTO " + tableSql + " (" + columnsSql + ") VALUES (" + placeholders + ") " +
                     "ON CONFLICT (" + conflict + ") DO UPDATE SET " + updateSet;

        if (!string.IsNullOrWhiteSpace(returning))
            sql += " RETURNING " + Identifier(returning);

        return new PgQuery(sql, parameters);
    }

    // =========================================================================
    // IN CLAUSE BUILDER
    // =========================================================================

    /// <summary>
    ///     Generates a safe IN clause using PostgreSQL array: <c>"col" = ANY($N)</c>.
    /// </summary>
    public static (string Clause, object Parameter, int NextParamIndex) InClause<T>(
        string column,
        IEnumerable<T> values,
        int paramIndex = 1)
    {
        ArgumentNullException.ThrowIfNull(values);
        T[] arr = values.ToArray();
        if (arr.Length == 0)
            throw new ArgumentException("IN clause values cannot be empty.", nameof(values));

        return (Identifier(column) + " = ANY($" + paramIndex.ToString(CultureInfo.InvariantCulture) + ")",
            arr, paramIndex + 1);
    }

    /// <summary>
    ///     Generates a safe NOT IN clause: <c>"col" &lt;&gt; ALL($N)</c>.
    /// </summary>
    public static (string Clause, object Parameter, int NextParamIndex) NotInClause<T>(
        string column,
        IEnumerable<T> values,
        int paramIndex = 1)
    {
        ArgumentNullException.ThrowIfNull(values);
        T[] arr = values.ToArray();
        if (arr.Length == 0)
            throw new ArgumentException("NOT IN clause values cannot be empty.", nameof(values));

        return (Identifier(column) + " <> ALL($" + paramIndex.ToString(CultureInfo.InvariantCulture) + ")",
            arr, paramIndex + 1);
    }

    // =========================================================================
    // QUERY COMPOSITION
    // =========================================================================

    /// <summary>
    ///     Merges multiple <see cref="PgQuery" /> fragments into one, re-indexing parameters.
    /// </summary>
    public static PgQuery Combine(params PgQuery[] queries)
    {
        List<object?> parameters = [];
        StringBuilder sb = new();

        foreach (PgQuery query in queries)
        {
            int offset = parameters.Count;
            parameters.AddRange(query.Parameters);

            string reindexed = ParameterPlaceholderRegex().Replace(query.Sql, m =>
            {
                int n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                return "$" + (n + offset).ToString(CultureInfo.InvariantCulture);
            });

            sb.Append(reindexed);
        }

        return new PgQuery(sb.ToString(), parameters);
    }

    // =========================================================================
    // LIKE PATTERN ESCAPING
    // =========================================================================

    /// <summary>
    ///     Escapes a value for use in a PostgreSQL LIKE pattern,
    ///     escaping <c>%</c>, <c>_</c>, and the escape character itself.
    ///     Append <c>%</c> after the result for prefix matching.
    /// </summary>
    public static string EscapeLike(string value, char escapeChar = '\\')
    {
        ArgumentNullException.ThrowIfNull(value);
        string e = escapeChar.ToString();
        return value
            .Replace(e, e + e)
            .Replace("%", e + "%")
            .Replace("_", e + "_");
    }

    // =========================================================================
    // DEBUG HELPER (development only — NOT for execution)
    // =========================================================================

    /// <summary>
    ///     Returns an approximate interpolated version of the query for debugging / logging.
    ///     <strong>DO NOT execute the output — values are not safely embedded.</strong>
    /// </summary>
    public static string ToDebugString(PgQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        string sql = query.Sql;

        // Replace from highest index downward to avoid $1 matching inside $10
        for (int i = query.Parameters.Count; i >= 1; i--)
        {
            object? value = query.Parameters[i - 1];
            string literal = value switch
            {
                null => "NULL",
                string s => "'" + s.Replace("'", "''") + "'",
                bool b => b ? "TRUE" : "FALSE",
                DateTime dt => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'",
                DateTimeOffset dto => "'" +
                                      dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + "'",
                _ => value.ToString() ?? "NULL"
            };
            sql = sql.Replace("$" + i.ToString(CultureInfo.InvariantCulture), literal);
        }

        return sql;
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    /// <summary>
    ///     Appends a WHERE clause from a dictionary of column → value conditions.
    ///     Null values emit <c>IS NULL</c>; all others emit <c>= $N</c>.
    /// </summary>
    private static void AppendWhereClause(
        StringBuilder sb,
        List<object?> parameters,
        IDictionary<string, object?>? where)
    {
        if (where is null || where.Count == 0)
            return;

        IEnumerable<string> conditions = where.Select(kv =>
        {
            if (kv.Value is null)
                return Identifier(kv.Key) + " IS NULL";

            parameters.Add(kv.Value);
            return Identifier(kv.Key) + " = $" + parameters.Count.ToString(CultureInfo.InvariantCulture);
        });

        sb.Append(" WHERE ").Append(string.Join(" AND ", conditions));
    }

    // ── Source-generated regexes (compile-time, zero allocation) ──────────

    [GeneratedRegex(@"\{(\d+)(?::[^}]*)?\}")]
    private static partial Regex FormatPlaceholderRegex();

    [GeneratedRegex(@"\$(\d+)")]
    private static partial Regex ParameterPlaceholderRegex();
}

