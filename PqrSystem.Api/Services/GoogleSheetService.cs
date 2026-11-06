using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PqrSystem.Api.Services;

public sealed class GoogleSheetService(
    HttpClient httpClient,
    ILogger<GoogleSheetService> logger,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public async Task EnviarRespaldoAsync(
        long idTicket,
        string documento,
        string nombreEstudiante,
        string categoria,
        string descripcion,
        string estado)
    {
        var webhookUrl = GetWebhookUrl();
        if (webhookUrl is null)
        {
            return;
        }

        var datos = new TicketBackupPayload(
            idTicket,
            documento,
            nombreEstudiante,
            categoria,
            descripcion,
            estado);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(webhookUrl, datos, JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "El respaldo del ticket {IdTicket} en Google Sheets devolvió HTTP {StatusCode}.",
                    idTicket,
                    (int)response.StatusCode);
                Console.Error.WriteLine(
                    $"El respaldo del ticket {idTicket} en Google Sheets devolvió HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "No se pudo respaldar el ticket {IdTicket} en Google Sheets; se conserva en PostgreSQL.", idTicket);
            Console.Error.WriteLine(
                $"No se pudo respaldar el ticket {idTicket} en Google Sheets; se conserva en PostgreSQL: {exception}");
        }
    }

    public async Task ActualizarRespaldoAsync(long idTicket, string estado, string respuesta)
    {
        var webhookUrl = GetWebhookUrl();
        if (webhookUrl is null)
        {
            return;
        }

        var datos = new
        {
            Accion = "Actualizar",
            IdTicket = idTicket,
            Estado = estado,
            Respuesta = respuesta
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync(webhookUrl, datos, JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "La actualización del ticket {IdTicket} en Google Sheets devolvió HTTP {StatusCode}.",
                    idTicket,
                    (int)response.StatusCode);
                Console.Error.WriteLine(
                    $"La actualización del ticket {idTicket} en Google Sheets devolvió HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "No se pudo actualizar el ticket {IdTicket} en Google Sheets.", idTicket);
            Console.Error.WriteLine(
                $"No se pudo actualizar el ticket {idTicket} en Google Sheets: {exception}");
        }
    }

    private string? GetWebhookUrl()
    {
        var webhookUrl = configuration["GoogleSheets:WebhookUrl"];
        if (!string.IsNullOrWhiteSpace(webhookUrl))
        {
            return webhookUrl;
        }

        logger.LogWarning("Google Sheets no está configurado; se omite el respaldo del ticket.");
        return null;
    }

    private sealed record TicketBackupPayload(
        [property: JsonPropertyName("IdTicket")] long IdTicket,
        [property: JsonPropertyName("Documento")] string Documento,
        [property: JsonPropertyName("Nombre")] string Nombre,
        [property: JsonPropertyName("Categoria")] string Categoria,
        [property: JsonPropertyName("Descripcion")] string Descripcion,
        [property: JsonPropertyName("Estado")] string Estado);
}
