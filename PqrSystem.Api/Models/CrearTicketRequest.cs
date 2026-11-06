namespace PqrSystem.Api.Models;

public sealed record CrearTicketRequest(
    string DocumentoEstudiante,
    string NombreEstudiante,
    string Categoria,
    string Descripcion);
