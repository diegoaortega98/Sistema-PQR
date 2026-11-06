namespace PqrSystem.Api.Services;

public sealed class PqrValidationException(string message) : Exception(message);

public sealed class TicketNotFoundException(int id)
    : Exception($"No existe un ticket con Id {id}.");

public sealed class TicketAlreadyResolvedException(int id)
    : Exception($"El ticket con Id {id} ya fue resuelto.");
