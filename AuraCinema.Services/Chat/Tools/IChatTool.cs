using System.Text.Json;

namespace AuraCinema.Services.Chat.Tools;

public interface IChatTool
{
    string Name { get; }
    string Description { get; }
    object Schema { get; }
    Task<object> ExecuteAsync(JsonElement args, ChatToolContext ctx, CancellationToken ct);
}
