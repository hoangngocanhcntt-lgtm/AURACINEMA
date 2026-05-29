using AuraCinema.Domain.Models.Chat;

namespace AuraCinema.Domain.Interfaces.Services;

public interface IChatService
{
    Task<ChatResponse> HandleAsync(int? userId, List<ChatMessage> history, string message, CancellationToken cancellationToken = default);
}
