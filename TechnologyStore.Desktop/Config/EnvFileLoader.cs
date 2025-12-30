using System;
using System.IO;
using System.Collections.Generic;

namespace TechnologyStore.Desktop
{
    /// <summary>
    /// Environment variable loader for local development.
    /// Loads variables from .env file and provides validation utilities.
    /// </summary>
    public static class EnvFileLoader
    {
        private static readonly HashSet<string> _loadedKeys = new();
        
        /// <summary>
        /// Loads environment variables from a .env file (for local development only).
        /// Safely ignores missing files.
        /// </summary>
        public static void LoadFromFile(string path = ".env")
        {
            try
            {
                if (!File.Exists(path)) return;

                foreach (var rawLine in File.ReadAllLines(path))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = line.Substring(0, idx).Trim();
                    var val = line.Substring(idx + 1).Trim().Trim('"');

                    // Do not overwrite variables already set in the environment
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, val);
                        _loadedKeys.Add(key);
                    }
                }
            }
            catch
            {
                // Non-fatal; if env loading fails, the application will later fail fast when required.
            }
        }

        /// <summary>
        /// Gets a required environment variable. Throws if not set or empty.
        /// </summary>
        /// <param name="key">The environment variable name</param>
        /// <returns>The environment variable value</returns>
        /// <exception cref="InvalidOperationException">Thrown when the variable is not set or empty</exception>
        public static string GetRequired(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Required environment variable '{key}' is not set. " +
                    "Please configure it in your environment or .env file.");
            }
            return value;
        }

        /// <summary>
        /// Gets an optional environment variable with a default value.
        /// </summary>
        /// <param name="key">The environment variable name</param>
        /// <param name="defaultValue">Default value if not set</param>
        /// <returns>The environment variable value or default</returns>
        public static string GetOptional(string key, string defaultValue = "")
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        /// <summary>
        /// Gets an integer environment variable with a default value.
        /// </summary>
        public static int GetInt(string key, int defaultValue = 0)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Gets a boolean environment variable (supports "true", "1", "yes").
        /// </summary>
        public static bool GetBool(string key, bool defaultValue = false)
        {
            var value = Environment.GetEnvironmentVariable(key)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            return value == "true" || value == "1" || value == "yes";
        }

        /// <summary>
        /// Validates that all required environment variables are set.
        /// </summary>
        /// <param name="requiredKeys">Array of required environment variable names</param>
        /// <exception cref="InvalidOperationException">Thrown when any required variable is missing</exception>
        public static void ValidateRequired(params string[] requiredKeys)
        {
            var missingKeys = requiredKeys
                .Where(key => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                .ToList();

            if (missingKeys.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Missing required environment variables: {string.Join(", ", missingKeys)}. " +
                    "Please configure them in your environment or .env file.");
            }
        }

        /// <summary>
        /// Gets a connection string from various environment variable formats.
        /// Supports DB_CONNECTION_STRING, DATABASE_URL (Heroku format), or individual DB_* variables.
        /// </summary>
        public static string GetConnectionString()
        {
            if (TryGetDirectConnectionString(out var conn))
                return conn;

            if (TryGetDatabaseUrlConnectionString(out conn))
                return conn;

            if (TryBuildFromIndividualVariables(out conn))
                return conn;

            throw new InvalidOperationException(
                "Database connection string not configured. Please set DB_CONNECTION_STRING, DATABASE_URL, " +
                "or individual DB_HOST, DB_NAME, DB_USER, DB_PASSWORD environment variables.");
        }

        private static bool TryGetDirectConnectionString(out string connectionString)
        {
            connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ?? string.Empty;
            return !string.IsNullOrWhiteSpace(connectionString);
        }

        private static bool TryGetDatabaseUrlConnectionString(out string connectionString)
        {
            connectionString = string.Empty;
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrWhiteSpace(databaseUrl))
                return false;

            if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri))
                return false;

            if (string.IsNullOrWhiteSpace(uri.UserInfo))
                return false;

            var userInfo = uri.UserInfo.Split(':');
            var user = Uri.UnescapeDataString(userInfo[0]);
            var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port.ToString() : "5432";
            var db = uri.AbsolutePath.TrimStart('/');

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass) || string.IsNullOrWhiteSpace(db))
                return false;

            connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
            return true;
        }

        private static bool TryBuildFromIndividualVariables(out string connectionString)
        {
            connectionString = string.Empty;

            var hostEnv = Environment.GetEnvironmentVariable("DB_HOST") ?? Environment.GetEnvironmentVariable("PGHOST");
            var dbEnv = Environment.GetEnvironmentVariable("DB_NAME") ?? Environment.GetEnvironmentVariable("PGDATABASE");
            var userEnv = Environment.GetEnvironmentVariable("DB_USER") ?? Environment.GetEnvironmentVariable("PGUSER");
            var passEnv = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? Environment.GetEnvironmentVariable("PGPASSWORD");
            var portEnv = Environment.GetEnvironmentVariable("DB_PORT") ?? Environment.GetEnvironmentVariable("PGPORT") ?? "5432";

            if (string.IsNullOrWhiteSpace(hostEnv) || string.IsNullOrWhiteSpace(dbEnv) ||
                string.IsNullOrWhiteSpace(userEnv) || string.IsNullOrWhiteSpace(passEnv))
                return false;

            connectionString = $"Host={hostEnv};Port={portEnv};Database={dbEnv};Username={userEnv};Password={passEnv};";
            return true;
        }
    }
}
