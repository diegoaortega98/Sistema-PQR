using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PqrSystem.Api.Services;

public sealed class GoogleSheetService(
    HttpClient httpClient,
    ILogger<GoogleSheetService> logger,
    IConfiguration configuration)
{
    public async Task EnviarRespaldoAsync(
        string nombre,
        string documento,
        string categoria,
        string descripcion)
    {
        var formUrl = configuration["GoogleFormSettings:FormUrl"];
        if (string.IsNullOrWhiteSpace(formUrl))
        {
            logger.LogWarning("Google Forms no está configurado; se omite el respaldo del ticket.");
            return;
        }

        var formData = new Dictionary<string, string>
        {
            ["entry.323429411"] = categoria,
            ["entry.1533825319"] = "Estudiante",
            ["entry.1918136615"] = "Fundación Elyon Yireh - Cartagena",
            ["entry.289807416"] = nombre,
            ["entry.411497220"] = documento,
            ["entry.1459353141"] = "3000000000",
            ["entry.1931416965"] = "correo@elyonyireh.edu.co",
            ["entry.510921678"] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["entry.879910079"] = "1",
            ["entry.1299815972"] = "Automovilismo",
            ["entry.563078625"] = "Coordinación",
            ["entry.1081510727"] = "N/A",
            ["entry.95054066"] = descripcion
        };

        try
        {
            using var content = new FormUrlEncodedContent(formData);
            using var response = await httpClient.PostAsync(formUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "El envío a Google Forms devolvió HTTP {StatusCode}.",
                    (int)response.StatusCode);
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Error de red al enviar el ticket a Google Forms.");
        }
        catch (TaskCanceledException exception)
        {
            logger.LogError(exception, "Se agotó el tiempo de espera al enviar el ticket a Google Forms.");
        }
    }
}
