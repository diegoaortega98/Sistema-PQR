using System.Text.Json;
using Npgsql;
using PqrSystem.Api.Models;
using PqrSystem.Api.Services;

namespace PqrSystem.Tests;

public sealed class TicketServiceTests
{
    private readonly FakeTicketRepository _repository = new();
    private readonly TicketService _service;

    public TicketServiceTests()
    {
        _service = new TicketService(_repository);
    }

    [Fact]
    public async Task CrearTicket_CreaTicketPendienteConNombreYId()
    {
        var ticket = await _service.CrearTicketAsync(
            "DOC-001",
            "ESTUDIANTE DE PRUEBA",
            "Infraestructura",
            "Solicitud de mantenimiento");

        Assert.True(ticket.Id > 0);
        Assert.Equal("DOC-001", ticket.DocumentoEstudiante);
        Assert.Equal("ESTUDIANTE DE PRUEBA", ticket.NombreEstudiante);
        Assert.Equal("Infraestructura", ticket.Categoria);
        Assert.Equal("Pendiente", ticket.Estado);
        Assert.Null(ticket.RespuestaAdmin);
    }

    [Fact]
    public async Task Ticket_NoIncluyePinEnSuModeloNiSerializacion()
    {
        var ticket = await _service.CrearTicketAsync(
            "DOC-008",
            "ESTUDIANTE DE PRUEBA",
            "Servicio",
            "Consulta de privacidad");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ticket));

        Assert.False(json.RootElement.TryGetProperty("PinAcceso", out _));
    }

    [Fact]
    public async Task ResponderTicket_CuandoYaFueResuelto_LanzaExcepcionControlada()
    {
        var ticket = await _service.CrearTicketAsync(
            "DOC-002",
            "ESTUDIANTE DE PRUEBA",
            "Servicio",
            "Solicitud de revisión");
        await _service.ResponderTicketAsync(ticket.Id, "Solicitud atendida");

        await Assert.ThrowsAsync<TicketAlreadyResolvedException>(
            () => _service.ResponderTicketAsync(ticket.Id, "Segunda respuesta"));
    }

    [Fact]
    public async Task ObtenerTicketsPendientes_RetornaSoloTicketsPendientes()
    {
        var ticketPendiente = await _service.CrearTicketAsync(
            "DOC-003",
            "ESTUDIANTE DE PRUEBA",
            "Academico",
            "Consulta académica");
        var ticketResuelto = await _service.CrearTicketAsync(
            "DOC-004",
            "ESTUDIANTE DE PRUEBA",
            "Vehiculos",
            "Consulta de vehículo");
        await _service.ResponderTicketAsync(ticketResuelto.Id, "Consulta atendida");

        var ticket = Assert.Single(await _service.ObtenerTicketsPendientesAsync());

        Assert.Equal(ticketPendiente.Id, ticket.Id);
    }

    [Fact]
    public async Task ObtenerTicketsPorDocumento_RetornaTodosLosTicketsDelDocumento()
    {
        var ticketPrimero = await _service.CrearTicketAsync(
            "DOC-005",
            "ESTUDIANTE DE PRUEBA",
            "Servicio",
            "Solicitud pendiente");
        var ticketSegundo = await _service.CrearTicketAsync(
            "DOC-005",
            "ESTUDIANTE DE PRUEBA",
            "Academico",
            "Solicitud resuelta");
        await _service.ResponderTicketAsync(ticketSegundo.Id, "Respuesta administrativa");
        await _service.CrearTicketAsync(
            "DOC-006",
            "ESTUDIANTE DE PRUEBA",
            "Vehiculos",
            "Solicitud de otro documento");

        var resultado = await _service.ObtenerTicketsPorDocumentoAsync("DOC-005");

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, ticket => ticket.Id == ticketSegundo.Id);
        Assert.Contains(resultado, ticket => ticket.Estado == "Resuelto" &&
            ticket.RespuestaAdmin == "Respuesta administrativa");
        Assert.Contains(resultado, ticket => ticket.Id == ticketPrimero.Id);
        Assert.DoesNotContain(resultado, ticket => ticket.DocumentoEstudiante == "DOC-006");
    }

    [Theory]
    [InlineData("Sugerencia")]
    [InlineData("Felicitación")]
    [InlineData("Otro")]
    public async Task CrearTicket_AceptaLasNuevasCategorias(string categoria)
    {
        var ticket = await _service.CrearTicketAsync(
            "DOC-007",
            "ESTUDIANTE DE PRUEBA",
            categoria,
            "Solicitud de prueba");

        Assert.Equal(categoria, ticket.Categoria);
    }

    [Fact]
    public void DatabaseConnectionString_ConvierteUrlDeNeonAConexionNpgsqlConTls()
    {
        var connectionString = DatabaseConnectionString.Resolve(
            "postgresql://qa-user:p%40ss%3Aword@db.example.test:5433/pqr?sslmode=require",
            "Host=localhost;Database=pqr;Username=postgres;Password=postgres");
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal("db.example.test", parsed.Host);
        Assert.Equal(5433, parsed.Port);
        Assert.Equal("pqr", parsed.Database);
        Assert.Equal("qa-user", parsed.Username);
        Assert.Equal("p@ss:word", parsed.Password);
        Assert.Equal(SslMode.Require, parsed.SslMode);
    }

    [Fact]
    public void DatabaseConnectionString_UsaFallbackCuandoNoHayDatabaseUrl()
    {
        const string fallback = "Host=localhost;Database=pqr;Username=postgres;Password=postgres";

        Assert.Equal(fallback, DatabaseConnectionString.Resolve(null, fallback));
    }

    [Fact]
    public void DatabaseConnectionString_UsaPuertoPredeterminadoCuandoUrlNoLoIndica()
    {
        var connectionString = DatabaseConnectionString.Resolve(
            "postgresql://user:password@db.example.test/pqr",
            "Host=localhost;Database=pqr;Username=postgres;Password=postgres");
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal(5432, parsed.Port);
    }

    private sealed class FakeTicketRepository : ITicketRepository
    {
        private readonly List<Ticket> _tickets = [];
        private int _nextId = 1;

        public Task<Ticket> CrearAsync(
            string documentoEstudiante,
            string nombreEstudiante,
            string categoria,
            string descripcion)
        {
            var ticket = new Ticket(
                _nextId++,
                documentoEstudiante,
                nombreEstudiante,
                categoria,
                descripcion,
                "Pendiente",
                null,
                DateTime.UtcNow);
            _tickets.Add(ticket);
            return Task.FromResult(ticket);
        }

        public Task<IReadOnlyList<Ticket>> ObtenerPendientesAsync() =>
            Task.FromResult<IReadOnlyList<Ticket>>(
                _tickets.Where(ticket => ticket.Estado == "Pendiente").ToArray());

        public Task<IReadOnlyList<Ticket>> ObtenerPorDocumentoAsync(string documentoEstudiante) =>
            Task.FromResult<IReadOnlyList<Ticket>>(
                _tickets.Where(ticket => ticket.DocumentoEstudiante == documentoEstudiante).ToArray());

        public Task<string> ResponderAsync(int id, string respuesta)
        {
            var index = _tickets.FindIndex(ticket => ticket.Id == id);
            if (index < 0)
            {
                throw new TicketNotFoundException(id);
            }

            var ticket = _tickets[index];
            if (ticket.Estado == "Resuelto")
            {
                throw new TicketAlreadyResolvedException(id);
            }

            _tickets[index] = ticket with { Estado = "Resuelto", RespuestaAdmin = respuesta };
            return Task.FromResult(ticket.DocumentoEstudiante);
        }
    }
}
