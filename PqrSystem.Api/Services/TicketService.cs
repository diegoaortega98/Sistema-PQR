using PqrSystem.Api.Models;

namespace PqrSystem.Api.Services;

public sealed class TicketService(ITicketRepository repository)
{
    private static readonly HashSet<string> CategoriasPermitidas =
    [
        "Infraestructura",
        "Vehiculos",
        "Servicio",
        "Academico",
        "Sugerencia",
        "Felicitación",
        "Otro"
    ];

    public Task<Ticket> CrearTicketAsync(
        string documentoEstudiante,
        string nombreEstudiante,
        string categoria,
        string descripcion)
    {
        ValidarRequerido(documentoEstudiante, nameof(documentoEstudiante));
        ValidarRequerido(nombreEstudiante, nameof(nombreEstudiante));
        ValidarRequerido(categoria, nameof(categoria));
        ValidarRequerido(descripcion, nameof(descripcion));

        var categoriaNormalizada = categoria.Trim();
        if (!CategoriasPermitidas.Contains(categoriaNormalizada))
        {
            throw new PqrValidationException(
                "La categoría debe ser Infraestructura, Vehiculos, Servicio, Academico, Sugerencia, Felicitación o Otro.");
        }

        return repository.CrearAsync(
            documentoEstudiante.Trim(),
            nombreEstudiante.Trim(),
            categoriaNormalizada,
            descripcion.Trim());
    }

    public Task<IReadOnlyList<Ticket>> ObtenerTicketsPendientesAsync() =>
        repository.ObtenerPendientesAsync();

    public Task<IReadOnlyList<Ticket>> ObtenerTicketsPorDocumentoAsync(string documentoEstudiante)
    {
        ValidarRequerido(documentoEstudiante, nameof(documentoEstudiante));
        return repository.ObtenerPorDocumentoAsync(documentoEstudiante.Trim());
    }

    public Task<string> ResponderTicketAsync(int id, string respuesta)
    {
        if (id <= 0)
        {
            throw new PqrValidationException("El Id del ticket debe ser mayor que cero.");
        }

        ValidarRequerido(respuesta, nameof(respuesta));
        return repository.ResponderAsync(id, respuesta.Trim());
    }

    private static void ValidarRequerido(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new PqrValidationException($"El campo {campo} es obligatorio.");
        }
    }
}
