namespace TechnologyStoreAutomation;

/// <summary>
/// Centralized database configuration utility.
/// Builds connection strings from various environment variable formats.
/// </summary>
public static class DatabaseConfig
{
    /// <summary>
    /// Builds a PostgreSQL connection string from environment variables.
    /// Supports multiple formats:
    /// 1) DB_CONNECTION_STRING - Full connection string
    /// 2) DATABASE_URL - Heroku-style postgres://user:pass@host:port/dbname
    /// 3) Individual variables: DB_HOST, DB_NAME, DB_USER, DB_PASSWORD, DB_PORT
    ///    (also supports PG* variants: PGHOST, PGDATABASE, PGUSER, PGPASSWORD, PGPORT)
    /// </summary>
    /// <returns>A valid connection string, or empty string if configuration is missing</returns>
    public static string BuildConnectionStringFromEnv()
    {
        // 1) Full connection string provided directly
        var direct = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;

        // 2) Heroku-style DATABASE_URL: postgres://user:pass@host:port/dbname
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            var connectionString = ParseDatabaseUrl(databaseUrl);
            if (!string.IsNullOrEmpty(connectionString)) return connectionString;
        }

        // 3) Individual environment variables
        return BuildFromIndividualVariables();
    }

    /// <summary>
    /// Parses a Heroku-style DATABASE_URL into a connection string
    /// </summary>
    private static string ParseDatabaseUrl(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            return string.Empty;
        }

        var userInfo = uri.UserInfo.Split(':');
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port.ToString() : "5432";
        var db = uri.AbsolutePath.TrimStart('/');

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass) ||
            string.IsNullOrWhiteSpace(db))
        {
            return string.Empty;
        }

        return $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
    }

    /// <summary>
    /// Builds connection string from individual environment variables
    /// </summary>
    private static string BuildFromIndividualVariables()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? 
                   Environment.GetEnvironmentVariable("PGHOST");
        var database = Environment.GetEnvironmentVariable("DB_NAME") ??
                       Environment.GetEnvironmentVariable("PGDATABASE");
        var user = Environment.GetEnvironmentVariable("DB_USER") ?? 
                   Environment.GetEnvironmentVariable("PGUSER");
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ??
                       Environment.GetEnvironmentVariable("PGPASSWORD");
        var port = Environment.GetEnvironmentVariable("DB_PORT") ??
                   Environment.GetEnvironmentVariable("PGPORT") ?? "5432";

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            return string.Empty;
        }

        return $"Host={host};Port={port};Database={database};Username={user};Password={password};";
    }

    /// <summary>
    /// Gets the connection string or throws an exception with helpful message
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when connection string cannot be built</exception>
    public static string GetRequiredConnectionString()
    {
        var connectionString = BuildConnectionStringFromEnv();
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string not configured. Please set one of the following environment variables:\n" +
                "1) DB_CONNECTION_STRING\n" +
                "2) DATABASE_URL\n" +
                "3) DB_HOST, DB_NAME, DB_USER, DB_PASSWORD (and optional DB_PORT)");
        }

        return connectionString;
    }
}

