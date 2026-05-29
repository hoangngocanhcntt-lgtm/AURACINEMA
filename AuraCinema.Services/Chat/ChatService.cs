using System.Text.Json;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Chat;
using AuraCinema.Services.Chat.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraCinema.Services.Chat;

public class ChatService : IChatService
{
    private readonly ILlmClient _llm;
    private readonly ToolRegistry _toolRegistry;
    private readonly LlmOptions _options;
    private readonly ILogger<ChatService> _logger;

    public ChatService(ILlmClient llm, ToolRegistry toolRegistry, IOptions<LlmOptions> options, ILogger<ChatService> logger)
    {
        _llm = llm;
        _toolRegistry = toolRegistry;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatResponse> HandleAsync(int? userId, List<ChatMessage> history, string message, CancellationToken cancellationToken = default)
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = SystemPrompt.Prompt }
        };

        foreach (var msg in history.TakeLast(6))
        {
            messages.Add(new LlmMessage { Role = msg.Role, Content = msg.Content });
        }

        messages.Add(new LlmMessage { Role = "user", Content = message });

        var tools = _toolRegistry.GetAllDeclarations();
        var ctx = new ChatToolContext(userId, null);

        var loopCount = 0;
        const int MAX_LOOPS = 5;

        try
        {
            while (loopCount < MAX_LOOPS)
            {
                var req = new LlmRequest
                {
                    Model = _options.Model,
                    Messages = messages,
                    Tools = tools.Count > 0 ? tools : null,
                    ToolChoice = "auto",
                    Temperature = _options.Temperature,
                    TopP = _options.TopP,
                    MaxTokens = _options.MaxTokens
                };

                var llmResp = await _llm.GenerateAsync(req, cancellationToken);
                var choice = llmResp.Choices.FirstOrDefault();

                if (choice == null)
                {
                    break;
                }

                messages.Add(choice.Message);

                if (choice.Message.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
                {
                    foreach (var call in choice.Message.ToolCalls)
                    {
                        var tool = _toolRegistry.Get(call.Function.Name);
                        if (tool != null)
                        {
                            try
                            {
                                var args = JsonSerializer.Deserialize<JsonElement>(call.Function.Arguments);
                                var result = await tool.ExecuteAsync(args, ctx, cancellationToken);
                                
                                messages.Add(new LlmMessage
                                {
                                    Role = "tool",
                                    ToolCallId = call.Id,
                                    Name = call.Function.Name,
                                    Content = JsonSerializer.Serialize(result)
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Lỗi khi gọi tool {ToolName}", call.Function.Name);
                                messages.Add(new LlmMessage
                                {
                                    Role = "tool",
                                    ToolCallId = call.Id,
                                    Name = call.Function.Name,
                                    Content = JsonSerializer.Serialize(new { error = ex.Message })
                                });
                            }
                        }
                        else
                        {
                            messages.Add(new LlmMessage
                            {
                                Role = "tool",
                                ToolCallId = call.Id,
                                Name = call.Function.Name,
                                Content = JsonSerializer.Serialize(new { error = "Tool not found" })
                            });
                        }
                    }
                    loopCount++;
                    continue;
                }

                if (!string.IsNullOrEmpty(choice.Message.Content))
                {
                    return new ChatResponse { Reply = choice.Message.Content };
                }

                break;
            }

            return new ChatResponse { Reply = "Xin lỗi, mình không xử lý được, bạn mô tả lại nhé." };
        }
        catch (InvalidOperationException ex)
        {
            return new ChatResponse { Reply = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định trong ChatService");
            return new ChatResponse { Reply = "Đã có lỗi xảy ra, bạn thử lại sau nhé." };
        }
    }
}
