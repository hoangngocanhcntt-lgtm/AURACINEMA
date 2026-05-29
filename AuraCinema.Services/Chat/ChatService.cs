using System.Text.Json;
using System.Text.RegularExpressions;
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

    // Bắt cú pháp tool call mà model nhỏ "rò rỉ" ra dạng text: <function=ten>{...json...}</function>
    private static readonly Regex LeakedToolRegex = new(
        @"<function\s*=\s*([a-zA-Z_][\w]*)\s*>?\s*(\{.*?\})\s*(?:</function>)?",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public async Task<ChatResponse> HandleAsync(int? userId, List<ChatMessage> history, string message, CancellationToken cancellationToken = default)
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = SystemPrompt.Build(DateTime.Now) }
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
        var disableTools = false; // bật khi gặp tool_use_failed để model trả lời bằng text

        try
        {
            while (loopCount < MAX_LOOPS)
            {
                var useTools = !disableTools && tools.Count > 0;
                var req = new LlmRequest
                {
                    Model = _options.Model,
                    Messages = messages,
                    Tools = useTools ? tools : null,
                    ToolChoice = useTools ? "auto" : "none",
                    Temperature = _options.Temperature,
                    TopP = _options.TopP,
                    MaxTokens = _options.MaxTokens
                };

                LlmResponse llmResp;
                try
                {
                    llmResp = await _llm.GenerateAsync(req, cancellationToken);
                }
                catch (GroqToolUseFailedException)
                {
                    // Model sinh tool call hỏng -> thử lại 1 lần không dùng tool để vẫn có câu trả lời.
                    if (!disableTools)
                    {
                        disableTools = true;
                        loopCount++;
                        continue;
                    }
                    return new ChatResponse { Reply = "Mình chưa lấy được dữ liệu này, bạn hỏi lại theo cách khác giúp mình nhé." };
                }

                var choice = llmResp.Choices.FirstOrDefault();
                if (choice == null)
                {
                    break;
                }

                // 1) Tool call chuẩn (native).
                if (choice.Message.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
                {
                    messages.Add(choice.Message);
                    await ExecuteToolCallsAsync(choice.Message.ToolCalls!, messages, ctx, cancellationToken);
                    loopCount++;
                    continue;
                }

                // 2) Tool call bị rò rỉ ra content dạng text -> bóc ra, thực thi như tool call thật.
                var content = choice.Message.Content ?? "";
                var leaked = ExtractLeakedToolCalls(content);
                if (leaked.Count > 0)
                {
                    messages.Add(new LlmMessage { Role = "assistant", ToolCalls = leaked });
                    await ExecuteToolCallsAsync(leaked, messages, ctx, cancellationToken);
                    loopCount++;
                    continue;
                }

                // 3) Câu trả lời thường — dọn rác còn sót rồi trả về.
                var cleaned = CleanContent(content);
                if (!string.IsNullOrEmpty(cleaned))
                {
                    return new ChatResponse { Reply = cleaned };
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

    private async Task ExecuteToolCallsAsync(List<LlmToolCall> calls, List<LlmMessage> messages, ChatToolContext ctx, CancellationToken ct)
    {
        foreach (var call in calls)
        {
            var tool = _toolRegistry.Get(call.Function.Name);
            if (tool == null)
            {
                messages.Add(new LlmMessage
                {
                    Role = "tool",
                    ToolCallId = call.Id,
                    Name = call.Function.Name,
                    Content = JsonSerializer.Serialize(new { error = "Tool not found" })
                });
                continue;
            }

            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(call.Function.Arguments);
                var result = await tool.ExecuteAsync(args, ctx, ct);
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
    }

    private static List<LlmToolCall> ExtractLeakedToolCalls(string content)
    {
        var calls = new List<LlmToolCall>();
        if (string.IsNullOrEmpty(content) || !content.Contains("<function", StringComparison.OrdinalIgnoreCase))
        {
            return calls;
        }

        var index = 0;
        foreach (Match m in LeakedToolRegex.Matches(content))
        {
            var name = m.Groups[1].Value;
            var json = m.Groups[2].Value;

            try
            {
                JsonSerializer.Deserialize<JsonElement>(json); // chỉ nhận khi JSON hợp lệ
            }
            catch
            {
                continue;
            }

            calls.Add(new LlmToolCall
            {
                Id = $"leaked_{index++}",
                Function = new LlmFunctionCall { Name = name, Arguments = json }
            });
        }

        return calls;
    }

    private static string CleanContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        // Lớp chặn cuối: không để cú pháp tool call lọt ra UI dù vì lý do gì.
        var cleaned = LeakedToolRegex.Replace(content, "");
        cleaned = cleaned.Replace("</function>", "").Replace("<|python_tag|>", "");
        return cleaned.Trim();
    }
}
