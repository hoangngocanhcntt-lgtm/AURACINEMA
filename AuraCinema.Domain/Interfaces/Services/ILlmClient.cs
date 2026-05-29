using AuraCinema.Domain.Models.Chat;

namespace AuraCinema.Domain.Interfaces.Services;

public interface ILlmClient
{
    Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default);
}
