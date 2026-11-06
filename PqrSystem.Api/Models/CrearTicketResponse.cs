namespace PqrSystem.Api.Models;

public sealed record CrearTicketResponse(
    int Id,
    string DocumentoEstudiante,
    string NombreEstudiante,
    string Categoria,
    string Descripcion,
    string Estado,
    string? RespuestaAdmin,
    DateTime FechaCreacion);
