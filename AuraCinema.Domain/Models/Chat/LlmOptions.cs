namespace AuraCinema.Domain.Models.Chat;

public class LlmOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "llama-3.1-8b-instant";
    public int MaxTokens { get; set; } = 1024;
    public double Temperature { get; set; } = 0.3;
    public double TopP { get; set; } = 0.9;
    public int TimeoutSeconds { get; set; } = 30;
}
