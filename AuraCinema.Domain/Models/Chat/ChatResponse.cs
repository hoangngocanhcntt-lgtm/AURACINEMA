namespace AuraCinema.Domain.Models.Chat;

public class ChatResponse
{
    public string Reply { get; set; } = "";
    public bool RequireLogin { get; set; }
    public string? RedirectUrl { get; set; }
    public List<ChatMessage> UpdatedHistory { get; set; } = new();
}
