using System.Text.Json;
using System.Text.RegularExpressions;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Chat;
using AuraCinema.Services.Chat.Tools;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat;

public class ChatService : IChatService
{
    private readonly ILlmClient _llm;
    private readonly ToolRegistry _toolRegistry;
    private readonly ILogger<ChatService> _logger; private readonly LlmOptions _options;
    private static readonly Regex LeakedToolRegex = new(@"<function\s*=\s*(.*?)\s*>(.*?)</function>", RegexOptions.Singleline | RegexOptions.Compiled);

    public ChatService(ILlmClient llm, ToolRegistry toolRegistry, ILogger<ChatService> logger, Microsoft.Extensions.Options.IOptions<LlmOptions> options)
    {
        _llm = llm;
        _toolRegistry = toolRegistry;
        _logger = logger; _options = options.Value;
    }

    public async Task<ChatResponse> HandleAsync(int? userId, List<ChatMessage> history, string message, CancellationToken cancellationToken = default)
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = SystemPrompt.Build(DateTime.Now) }
        };
        messages.AddRange(history.Select(h => new LlmMessage { Role = h.Role, Content = h.Content }));
        messages.Add(new LlmMessage { Role = "user", Content = message });

        var tools = _toolRegistry.GetAllDeclarations();
        var ctx = new ChatToolContext(userId, null);
        var collectedNames = new List<string>();

        var loopCount = 0;
        const int MAX_LOOPS = 3;
        var disableTools = false;

        try
        {
            while (loopCount < MAX_LOOPS)
            {
                var useTools = !disableTools && tools.Count > 0;
                var req = new LlmRequest
                {
                    Messages = messages,
                    Tools = useTools ? tools : null,
                    ToolChoice = useTools ? "auto" : "none", Model = _options.Model, Temperature = _options.Temperature, TopP = _options.TopP, MaxTokens = _options.MaxTokens
                };

                LlmResponse llmResp;
                try
                {
                    llmResp = await _llm.GenerateAsync(req, cancellationToken);
                }
                catch (Exception ex) when (ex.GetType().Name == "GroqToolUseFailedException")
                {
                    if (!disableTools)
                    {
                        disableTools = true;
                        loopCount++;
                        continue;
                    }
                    return new ChatResponse { Reply = "Mình chưa lấy được dữ liệu này, bạn hỏi lại theo cách khác giúp mình nhé." };
                }

                var choice = llmResp.Choices.FirstOrDefault();
                if (choice == null) break;

                if (choice.Message.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
                {
                    messages.Add(choice.Message);
                    await ExecuteToolCallsAsync(choice.Message.ToolCalls!, messages, ctx, collectedNames, cancellationToken);
                    loopCount++;
                    continue;
                }

                var content = choice.Message.Content ?? "";
                var leaked = ExtractLeakedToolCalls(content);
                if (leaked.Count > 0)
                {
                    messages.Add(new LlmMessage { Role = "assistant", ToolCalls = leaked });
                    await ExecuteToolCallsAsync(leaked, messages, ctx, collectedNames, cancellationToken);
                    loopCount++;
                    continue;
                }

                var cleaned = CleanContent(content);
                if (!string.IsNullOrEmpty(cleaned))
                {
                    if (collectedNames.Count == 0 && RequiresDataLookup(message) && LooksLikeFabricatedData(cleaned))
                    {
                        _logger.LogWarning("Phát hiện model bịa dữ liệu cho câu hỏi: {Message}", message);
                        return new ChatResponse { Reply = BuildDataFallbackReply(message) };
                    }
                    var corrected = ResponseCorrector.CorrectNames(cleaned, collectedNames);
                    return new ChatResponse { Reply = corrected };
                }

                break;
            }

            return new ChatResponse { Reply = "Xin lỗi, mình không xử lý được, bạn mô tả lại nhé." };
        }
        catch (Exception ex) when (ex.GetType().Name == "RateLimitedException" || ex.Message.Contains("Rate Limit") || ex.Message.Contains("429"))
        {
            return new ChatResponse { Reply = BuildFallbackReply(message) };
        }
        catch (Exception ex) when (ex.Message.Contains("ServiceUnavailable") || ex.Message.Contains("503"))
        {
            _logger.LogWarning("Gemini API bị quá tải: {Message}", ex.Message);
            return new ChatResponse { Reply = "Hiện tại server AI của Google đang bị quá tải đột xuất. Bạn đợi khoảng 1 phút rồi hỏi lại giúp mình nhé!" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định trong ChatService");
            return new ChatResponse { Reply = "Đã có lỗi xảy ra, bạn thử lại sau nhé." };
        }
    }

    private async Task ExecuteToolCallsAsync(List<LlmToolCall> calls, List<LlmMessage> messages, ChatToolContext ctx, List<string> collectedNames, CancellationToken ct)
    {
        foreach (var call in calls)
        {
            var tool = _toolRegistry.Get(call.Function.Name);
            if (tool == null)
            {
                messages.Add(new LlmMessage { Role = "tool", ToolCallId = call.Id, Name = call.Function.Name, Content = JsonSerializer.Serialize(new { error = "Tool not found" }) });
                continue;
            }
            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(call.Function.Arguments);
                var result = await tool.ExecuteAsync(args, ctx, ct);
                var resultJson = JsonSerializer.Serialize(result);
                collectedNames.AddRange(ResponseCorrector.ExtractNames(call.Function.Name, resultJson));
                messages.Add(new LlmMessage { Role = "tool", ToolCallId = call.Id, Name = call.Function.Name, Content = resultJson });
            }
            catch (Exception ex)
            {
                messages.Add(new LlmMessage { Role = "tool", ToolCallId = call.Id, Name = call.Function.Name, Content = JsonSerializer.Serialize(new { error = ex.Message }) });
            }
        }
    }

    private static List<LlmToolCall> ExtractLeakedToolCalls(string content)
    {
        var calls = new List<LlmToolCall>();
        if (string.IsNullOrEmpty(content) || !content.Contains("<function", StringComparison.OrdinalIgnoreCase)) return calls;
        var index = 0;
        foreach (Match m in LeakedToolRegex.Matches(content))
        {
            try { JsonSerializer.Deserialize<JsonElement>(m.Groups[2].Value); } catch { continue; }
            calls.Add(new LlmToolCall { Id = $"leaked_{index++}", Function = new LlmFunctionCall { Name = m.Groups[1].Value, Arguments = m.Groups[2].Value } });
        }
        return calls;
    }

    private static string CleanContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        var cleaned = LeakedToolRegex.Replace(content, "");
        return cleaned.Replace("</function>", "").Replace("<|python_tag|>", "").Trim();
    }

    private static string BuildFallbackReply(string userMessage)
    {
        var msg = userMessage.ToLowerInvariant();
        if (msg.Contains("phim") || msg.Contains("movie") || msg.Contains("xem")) return "Mình đang bận xíu! 🎬 Bạn có thể xem danh sách phim đang chiếu ngay trên trang chủ nhé. Nếu cần hỏi thêm, hãy nhắn lại sau vài giây!";
        if (msg.Contains("lịch") || msg.Contains("suất") || msg.Contains("giờ") || msg.Contains("chiếu") || msg.Contains("showtime")) return "Mình đang xử lý tin nhắn khác! 🕐 Bạn có thể xem lịch chiếu ngay trên trang phim nhé. Nhắn lại mình sau vài giây nha!";
        if (msg.Contains("giá") || msg.Contains("vé") || msg.Contains("price") || msg.Contains("ticket") || msg.Contains("bao nhiêu")) return "Mình đang bận chút! 🎫 Giá vé tuỳ theo loại ghế và suất chiếu, bạn có thể xem chi tiết khi chọn phim và suất chiếu nhé. Nhắn lại mình sau vài giây!";
        if (msg.Contains("khuyến") || msg.Contains("mã") || msg.Contains("giảm") || msg.Contains("ưu đãi") || msg.Contains("promo")) return "Mình đang bận xíu! 🎁 Bạn có thể xem các khuyến mãi mới nhất ngay trên trang chủ nhé. Nhắn lại mình sau vài giây nha!";
        if (msg.Contains("đặt") || msg.Contains("book") || msg.Contains("mua")) return "Mình đang xử lý tin nhắn khác! 🎟️ Bạn có thể đặt vé trực tiếp bằng cách chọn phim → chọn suất → chọn ghế nhé. Nhắn lại mình sau vài giây!";
        if (msg.Contains("chào") || msg.Contains("hi") || msg.Contains("hello") || msg.Contains("xin chào")) return "Chào bạn! 👋 Tôi là Bé Aura, trợ lý AI của rạp AuraCinema. Mình đang hơi bận, bạn nhắn lại sau vài giây nhé! 😊";
        
        var replies = new[] {
            "Mình đang xử lý nhiều tin nhắn, bạn nhắn lại sau vài giây nhé! 😊 Mình có thể giúp bạn tìm phim, xem lịch chiếu, hoặc thông tin khuyến mãi.",
            "Rạp mình đang có nhiều phim hay lắm! 🎬 Bạn nhắn lại sau vài giây để mình tư vấn chi tiết nhé!",
            "Xin lỗi bạn, mình đang bận chút! Bạn có thể xem thông tin phim và lịch chiếu trên trang chủ, hoặc nhắn lại mình sau vài giây nha! 🌟"
        };
        return replies[Math.Abs(Environment.TickCount) % replies.Length];
    }

    private static bool RequiresDataLookup(string userMessage)
    {
        var msg = userMessage.ToLowerInvariant();
        string[] keywords = { "phim", "movie", "lịch chiếu", "suất chiếu", "giờ chiếu", "giá vé", "khuyến mãi", "ưu đãi", "đặt vé", "vé" };
        foreach (var kw in keywords) if (msg.Contains(kw)) return true;
        return false;
    }

    private static bool LooksLikeFabricatedData(string reply)
    {
        if (reply.Contains("1.") && reply.Contains("2.")) return true;
        if (reply.Contains("danh sách") || reply.Contains("dưới đây là")) return true;
        int quoteCount = reply.Count(c => c == '"' || c == '\'');
        if (quoteCount >= 4) return true;
        string[] commonHallucinations = { "sát thủ bóng đêm", "tình yêu trong bóng tối", "cuộc chiến không giới hạn", "đại bàng bay cao", "câu chuyện bí ẩn", "bóng ma học đường", "ngôi nhà ma ám", "2.000đ", "2000đ", "50.000đ", "45.000" };
        var lowerReply = reply.ToLowerInvariant();
        foreach (var fakeInfo in commonHallucinations) if (lowerReply.Contains(fakeInfo)) return true;
        return false;
    }

    private static string BuildDataFallbackReply(string userMessage)
    {
        return "Xin lỗi bạn, hiện tại mình không thể lấy dữ liệu trực tiếp lúc này. 😅 Bạn vui lòng xem thông tin chi tiết về phim, lịch chiếu và giá vé ngay trên trang chủ của AuraCinema nhé!";
    }
}
