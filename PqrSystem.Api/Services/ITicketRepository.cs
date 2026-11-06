using PqrSystem.Api.Models;

namespace PqrSystem.Api.Services;

public interface ITicketRepository
{
    Task<Ticket> CrearAsync(
        string documentoEstudiante,
        string nombreEstudiante,
        string categoria,
        string descripcion);

    Task<IReadOnlyList<Ticket>> ObtenerPendientesAsync();

    Task<IReadOnlyList<Ticket>> ObtenerPorDocumentoAsync(string documentoEstudiante);

    Task<string> ResponderAsync(int id, string respuesta);
}
