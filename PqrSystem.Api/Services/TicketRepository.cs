using System.Globalization;
using Npgsql;
using PqrSystem.Api.Models;

namespace PqrSystem.Api.Services;

public sealed class TicketRepository(string connectionString) : ITicketRepository
{
    private const string SelectColumns = """
        "Id", "DocumentoEstudiante", "NombreEstudiante", "Categoria",
        "Descripcion", "Estado", "RespuestaAdmin", "FechaCreacion"
        """;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "Tickets" (
                "Id" SERIAL PRIMARY KEY,
                "DocumentoEstudiante" TEXT NOT NULL,
                "NombreEstudiante" TEXT NOT NULL,
                "Categoria" TEXT NOT NULL CHECK ("Categoria" IN (
                    'Infraestructura', 'Vehiculos', 'Servicio', 'Academico',
                    'Sugerencia', 'Felicitación', 'Otro')),
                "Descripcion" TEXT NOT NULL,
                "Estado" TEXT NOT NULL CHECK ("Estado" IN ('Pendiente', 'Resuelto')),
                "RespuestaAdmin" TEXT NULL,
                "FechaCreacion" TIMESTAMP WITH TIME ZONE NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_Tickets_DocumentoEstudiante"
                ON "Tickets" ("DocumentoEstudiante");
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Ticket> CrearAsync(
        string documentoEstudiante,
        string nombreEstudiante,
        string categoria,
        string descripcion)
    {
        var fechaCreacion = DateTime.UtcNow;
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "Tickets"
                ("DocumentoEstudiante", "NombreEstudiante", "Categoria", "Descripcion", "Estado", "RespuestaAdmin", "FechaCreacion")
            VALUES
                (@documento, @nombre, @categoria, @descripcion, 'Pendiente', NULL, @fecha)
            RETURNING "Id";
            """;
        command.Parameters.AddWithValue("documento", documentoEstudiante);
        command.Parameters.AddWithValue("nombre", nombreEstudiante);
        command.Parameters.AddWithValue("categoria", categoria);
        command.Parameters.AddWithValue("descripcion", descripcion);
        command.Parameters.AddWithValue("fecha", fechaCreacion);

        var idResult = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("PostgreSQL no devolvió el Id del ticket creado.");
        var id = Convert.ToInt32(idResult, CultureInfo.InvariantCulture);
        if (id <= 0)
        {
            throw new InvalidOperationException("PostgreSQL devolvió un Id inválido para el ticket creado.");
        }

        return new Ticket(
            id,
            documentoEstudiante,
            nombreEstudiante,
            categoria,
            descripcion,
            "Pendiente",
            null,
            fechaCreacion);
    }

    public Task<IReadOnlyList<Ticket>> ObtenerPendientesAsync() =>
        ObtenerTicketsAsync(
            $"""
            SELECT {SelectColumns}
            FROM "Tickets"
            WHERE "Estado" = 'Pendiente'
            ORDER BY "Id";
            """);

    public Task<IReadOnlyList<Ticket>> ObtenerPorDocumentoAsync(string documentoEstudiante) =>
        ObtenerTicketsAsync(
            $"""
            SELECT {SelectColumns}
            FROM "Tickets"
            WHERE "DocumentoEstudiante" = @documento
            ORDER BY "Id";
            """,
            ("documento", documentoEstudiante));

    public async Task<string> ResponderAsync(int id, string respuesta)
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = """
            SELECT "DocumentoEstudiante", "Estado"
            FROM "Tickets"
            WHERE "Id" = @id
            FOR UPDATE;
            """;
        selectCommand.Parameters.AddWithValue("id", id);

        string documentoEstudiante;
        string estado;
        await using (var reader = await selectCommand.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new TicketNotFoundException(id);
            }

            documentoEstudiante = reader.GetString(0);
            estado = reader.GetString(1);
        }

        if (estado == "Resuelto")
        {
            throw new TicketAlreadyResolvedException(id);
        }

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            UPDATE "Tickets"
            SET "Estado" = 'Resuelto', "RespuestaAdmin" = @respuesta
            WHERE "Id" = @id AND "Estado" = 'Pendiente';
            """;
        updateCommand.Parameters.AddWithValue("respuesta", respuesta);
        updateCommand.Parameters.AddWithValue("id", id);

        if (await updateCommand.ExecuteNonQueryAsync() != 1)
        {
            throw new TicketAlreadyResolvedException(id);
        }

        await transaction.CommitAsync();
        return documentoEstudiante;
    }

    private async Task<IReadOnlyList<Ticket>> ObtenerTicketsAsync(
        string sql,
        (string Name, object Value)? parameter = null)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameter is { } value)
        {
            command.Parameters.AddWithValue(value.Name, value.Value);
        }

        await using var reader = await command.ExecuteReaderAsync();
        var tickets = new List<Ticket>();
        while (await reader.ReadAsync())
        {
            tickets.Add(LeerTicket(reader));
        }

        return tickets;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static Ticket LeerTicket(NpgsqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetDateTime(7));
}
