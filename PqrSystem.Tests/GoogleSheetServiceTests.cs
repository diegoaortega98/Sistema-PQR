using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PqrSystem.Api.Services;

namespace PqrSystem.Tests;

public sealed class GoogleSheetServiceTests
{
    private const string WebhookUrl = "https://webhook.example.test/exec";

    [Fact]
    public async Task EnviarRespaldoAsync_EnviaUrlYPropiedadesExactas()
    {
        HttpRequestMessage? sentRequest = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            sentRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        var service = CreateService(httpClient);

        await service.EnviarRespaldoAsync(
            17,
            "DOC-017",
            "ESTUDIANTE DE PRUEBA",
            "Sugerencia",
            "Detalle",
            "Pendiente");

        Assert.NotNull(sentRequest);
        Assert.Equal(HttpMethod.Post, sentRequest.Method);
        Assert.Equal(WebhookUrl, sentRequest.RequestUri!.ToString());

        using var json = JsonDocument.Parse(await sentRequest.Content!.ReadAsStringAsync());
        Assert.Equal(
            ["IdTicket", "Documento", "Nombre", "Categoria", "Descripcion", "Estado"],
            json.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(17, json.RootElement.GetProperty("IdTicket").GetInt64());
        Assert.Equal("DOC-017", json.RootElement.GetProperty("Documento").GetString());
        Assert.Equal("ESTUDIANTE DE PRUEBA", json.RootElement.GetProperty("Nombre").GetString());
        Assert.Equal("Sugerencia", json.RootElement.GetProperty("Categoria").GetString());
        Assert.Equal("Detalle", json.RootElement.GetProperty("Descripcion").GetString());
        Assert.Equal("Pendiente", json.RootElement.GetProperty("Estado").GetString());

        sentRequest.Dispose();
    }

    [Fact]
    public async Task EnviarRespaldoAsync_CuandoHttpClientFalla_NoPropagaExcepcion()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            (_, _) => throw new HttpRequestException("Fallo de prueba")));
        var service = CreateService(httpClient);

        var exception = await Record.ExceptionAsync(
            () => service.EnviarRespaldoAsync(
                18,
                "DOC-018",
                "ESTUDIANTE DE PRUEBA",
                "Otro",
                "Detalle",
                "Pendiente"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ActualizarRespaldoAsync_EnviaUrlYPropiedadesExactas()
    {
        HttpRequestMessage? sentRequest = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            sentRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        var service = CreateService(httpClient);

        await service.ActualizarRespaldoAsync(27, "Resuelto", "Solicitud atendida");

        Assert.NotNull(sentRequest);
        Assert.Equal(HttpMethod.Post, sentRequest.Method);
        Assert.Equal(WebhookUrl, sentRequest.RequestUri!.ToString());

        using var json = JsonDocument.Parse(await sentRequest.Content!.ReadAsStringAsync());
        Assert.Equal(
            ["Accion", "IdTicket", "Estado", "Respuesta"],
            json.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("Actualizar", json.RootElement.GetProperty("Accion").GetString());
        Assert.Equal(27, json.RootElement.GetProperty("IdTicket").GetInt64());
        Assert.Equal("Resuelto", json.RootElement.GetProperty("Estado").GetString());
        Assert.Equal("Solicitud atendida", json.RootElement.GetProperty("Respuesta").GetString());

        sentRequest.Dispose();
    }

    [Fact]
    public async Task ActualizarRespaldoAsync_CuandoHttpClientFalla_NoPropagaExcepcion()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            (_, _) => throw new HttpRequestException("Fallo de prueba")));
        var service = CreateService(httpClient);

        var exception = await Record.ExceptionAsync(
            () => service.ActualizarRespaldoAsync(28, "Resuelto", "Respuesta"));

        Assert.Null(exception);
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

    private static GoogleSheetService CreateService(HttpClient httpClient)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheets:WebhookUrl"] = WebhookUrl
            })
            .Build();

        return new GoogleSheetService(
            httpClient,
            NullLogger<GoogleSheetService>.Instance,
            configuration);
    }
}
