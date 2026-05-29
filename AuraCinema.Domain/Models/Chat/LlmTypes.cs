using System.Text.Json.Serialization;

namespace AuraCinema.Domain.Models.Chat;

public class LlmMessage
{
    public string Role { get; set; } = string.Empty;
    public string? Content { get; set; }
    public List<LlmToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }
}

public class LlmToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public LlmFunctionCall Function { get; set; } = new();
}

public class LlmFunctionCall
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty; // string JSON
}

public class LlmTool
{
    public string Type { get; set; } = "function";
    public LlmFunctionDeclaration Function { get; set; } = new();
}

public class LlmFunctionDeclaration
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? Parameters { get; set; } // object schema
}

public class LlmRequest
{
    public string Model { get; set; } = string.Empty;
    public List<LlmMessage> Messages { get; set; } = new();
    public List<LlmTool>? Tools { get; set; }
    public string ToolChoice { get; set; } = "auto";
    public double Temperature { get; set; }
    public double? TopP { get; set; }
    public int MaxTokens { get; set; }
}

public class LlmResponse
{
    public List<LlmChoice> Choices { get; set; } = new();
}

public class LlmChoice
{
    public LlmMessage Message { get; set; } = new();
    public string? FinishReason { get; set; }
}
