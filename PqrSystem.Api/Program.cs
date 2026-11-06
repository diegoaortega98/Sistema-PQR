using PqrSystem.Api.Models;
using PqrSystem.Api.Services;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var connectionString = DatabaseConnectionString.Resolve(
    builder.Configuration["DATABASE_URL"],
    builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("No se configuró la cadena ConnectionStrings:DefaultConnection."));
var adminPassword = builder.Configuration["AdminSettings:Password"];
if (string.IsNullOrEmpty(adminPassword))
{
    throw new InvalidOperationException("La contraseña administrativa no puede estar vacía.");
}

var ticketRepository = new TicketRepository(connectionString);
await ticketRepository.InitializeAsync();
builder.Services.AddSingleton<ITicketRepository>(ticketRepository);
builder.Services.AddSingleton<TicketService>();
builder.Services.AddHttpClient<GoogleSheetService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

static bool HasValidAdminPassword(HttpRequest request, string expectedPassword)
{
    var providedPassword = request.Headers["X-Admin-Password"].ToString();
    var expectedBytes = Encoding.UTF8.GetBytes(expectedPassword);
    var providedBytes = Encoding.UTF8.GetBytes(providedPassword);
    return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
}

app.MapPost("/api/tickets", async (
    CrearTicketRequest request,
    TicketService service,
    GoogleSheetService googleSheetService) =>
{
    try
    {
        var ticket = await service.CrearTicketAsync(
            request.DocumentoEstudiante,
            request.NombreEstudiante,
            request.Categoria,
            request.Descripcion);
        Console.WriteLine($"ENVIANDO A GOOGLE SHEETS EL TICKET ID: {ticket.Id}");
        await googleSheetService.EnviarRespaldoAsync(
            ticket.Id,
            ticket.DocumentoEstudiante,
            ticket.NombreEstudiante,
            ticket.Categoria,
            ticket.Descripcion,
            ticket.Estado);
        return Results.Created(
            $"/api/tickets/{ticket.Id}",
            new CrearTicketResponse(
                ticket.Id,
                ticket.DocumentoEstudiante,
                ticket.NombreEstudiante,
                ticket.Categoria,
                ticket.Descripcion,
                ticket.Estado,
                ticket.RespuestaAdmin,
                ticket.FechaCreacion));
    }
    catch (PqrValidationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapGet("/api/tickets/estudiante/{documento}", async (string documento, TicketService service) =>
{
    try
    {
        return Results.Ok(await service.ObtenerTicketsPorDocumentoAsync(documento));
    }
    catch (PqrValidationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapGet("/api/tickets/pendientes", async (HttpRequest request, TicketService service) =>
{
    if (!HasValidAdminPassword(request, adminPassword))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(await service.ObtenerTicketsPendientesAsync());
});

app.MapPut("/api/tickets/{id:int}/respuesta", async (
    HttpRequest httpRequest,
    int id,
    ResponderTicketRequest request,
    TicketService service,
    GoogleSheetService googleSheetService) =>
{
    if (!HasValidAdminPassword(httpRequest, adminPassword))
    {
        return Results.Unauthorized();
    }

    try
    {
        var documentoEstudiante = await service.ResponderTicketAsync(id, request.Respuesta);
        await googleSheetService.ActualizarRespaldoAsync(id, "Resuelto", request.Respuesta.Trim());
        return Results.Ok(new ResponderTicketResponse(documentoEstudiante));
    }
    catch (PqrValidationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (TicketNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (TicketAlreadyResolvedException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.Run();

public partial class Program;
