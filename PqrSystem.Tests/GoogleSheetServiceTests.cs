using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PqrSystem.Api.Services;

namespace PqrSystem.Tests;

public sealed class GoogleSheetServiceTests
{
    private const string FormUrl =
        "https://docs.google.com/forms/d/e/1FAIpQLSd1uPDKOe7vFLVf3k7r3GP8M2eES5dpKVrlwqfFpHkLMUjd6w/formResponse";

    [Fact]
    public async Task EnviarRespaldoAsync_EnviaLosTreceCamposDelFormulario()
    {
        HttpRequestMessage? sentRequest = null;
        string? sentBody = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (request, _) =>
        {
            sentRequest = request;
            sentBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var service = CreateService(httpClient);

        await service.EnviarRespaldoAsync(
            "ESTUDIANTE DE PRUEBA",
            "DOC-017",
            "Sugerencia",
            "Detalle de prueba");

        Assert.NotNull(sentRequest);
        Assert.Equal(HttpMethod.Post, sentRequest.Method);
        Assert.Equal(FormUrl, sentRequest.RequestUri!.ToString());
        Assert.Equal("application/x-www-form-urlencoded", sentRequest.Content!.Headers.ContentType!.MediaType);

        var fields = ParseForm(sentBody!);
        Assert.Equal(13, fields.Count);
        Assert.Equal("Sugerencia", fields["entry.323429411"]);
        Assert.Equal("Estudiante", fields["entry.1533825319"]);
        Assert.Equal("Fundación Elyon Yireh - Cartagena", fields["entry.1918136615"]);
        Assert.Equal("ESTUDIANTE DE PRUEBA", fields["entry.289807416"]);
        Assert.Equal("DOC-017", fields["entry.411497220"]);
        Assert.Equal("3000000000", fields["entry.1459353141"]);
        Assert.Equal("correo@elyonyireh.edu.co", fields["entry.1931416965"]);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", fields["entry.510921678"]);
        Assert.Equal("1", fields["entry.879910079"]);
        Assert.Equal("Automovilismo", fields["entry.1299815972"]);
        Assert.Equal("Coordinación", fields["entry.563078625"]);
        Assert.Equal("N/A", fields["entry.1081510727"]);
        Assert.Equal("Detalle de prueba", fields["entry.95054066"]);
    }

    [Fact]
    public async Task EnviarRespaldoAsync_CuandoHttpClientFalla_NoPropagaExcepcion()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            (_, _) => throw new HttpRequestException("Fallo de prueba")));
        var service = CreateService(httpClient);

        var exception = await Record.ExceptionAsync(
            () => service.EnviarRespaldoAsync("Nombre", "DOC-018", "Otro", "Detalle"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task EnviarRespaldoAsync_SinUrlConfigurada_NoEnviaSolicitud()
    {
        var requestSent = false;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            requestSent = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        var service = CreateService(httpClient, includeFormUrl: false);

        await service.EnviarRespaldoAsync("Nombre", "DOC-019", "Otro", "Detalle");

        Assert.False(requestSent);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private static Dictionary<string, string> ParseForm(string content) =>
        content.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));

    private static GoogleSheetService CreateService(HttpClient httpClient, bool includeFormUrl = true)
    {
        var values = new Dictionary<string, string?>();
        if (includeFormUrl)
        {
            values["GoogleFormSettings:FormUrl"] = FormUrl;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new GoogleSheetService(
            httpClient,
            NullLogger<GoogleSheetService>.Instance,
            configuration);
    }
}
