using Npgsql;

namespace PqrSystem.Api.Services;

public static class DatabaseConnectionString
{
    public static string Resolve(string? databaseUrl, string fallbackConnectionString)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            return fallbackConnectionString;
        }

        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return new NpgsqlConnectionStringBuilder(databaseUrl).ConnectionString;
        }

        var credentials = Uri.UnescapeDataString(uri.UserInfo).Split(':', 2);
        if (credentials.Length != 2 || string.IsNullOrEmpty(uri.Host))
        {
            throw new ArgumentException("DATABASE_URL debe incluir host, usuario, contraseña y base de datos.", nameof(databaseUrl));
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port == -1 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = credentials[0],
            Password = credentials[1],
            SslMode = SslMode.Require
        };

        foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = parameter.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]);
            var value = pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
            if (string.Equals(key, "sslmode", StringComparison.OrdinalIgnoreCase))
            {
                var mode = value.Replace("-", string.Empty, StringComparison.Ordinal);
                if (!Enum.TryParse<SslMode>(mode, ignoreCase: true, out var sslMode))
                {
                    throw new ArgumentException("DATABASE_URL contiene un valor sslmode no reconocido.", nameof(databaseUrl));
                }

                builder.SslMode = sslMode;
            }
            else if (string.Equals(key, "timeout", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(value, out var timeout))
            {
                builder.Timeout = timeout;
            }
        }

        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new ArgumentException("DATABASE_URL debe indicar el nombre de la base de datos.", nameof(databaseUrl));
        }

        return builder.ConnectionString;
    }
}
