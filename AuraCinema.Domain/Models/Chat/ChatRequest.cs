namespace AuraCinema.Domain.Models.Chat;

public class ChatRequest
{
    public List<ChatMessage> History { get; set; } = new();
    public string Message { get; set; } = "";
}
