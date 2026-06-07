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
        var toolCallResults = new List<(string ToolName, string ResultJson)>();

        var loopCount = 0;
        const int MAX_LOOPS = 8;
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
                    await ExecuteToolCallsAsync(choice.Message.ToolCalls!, messages, ctx, collectedNames, cancellationToken, toolCallResults);
                    loopCount++;
                    continue;
                }

                var content = choice.Message.Content ?? "";
                var leaked = ExtractLeakedToolCalls(content);
                if (leaked.Count > 0)
                {
                    messages.Add(new LlmMessage { Role = "assistant", ToolCalls = leaked });
                    await ExecuteToolCallsAsync(leaked, messages, ctx, collectedNames, cancellationToken, toolCallResults);
                    loopCount++;
                    continue;
                }

                var cleaned = CleanContent(content);
                if (!string.IsNullOrEmpty(cleaned))
                {
                    // Chỉ check fabricated data khi KHÔNG có bất kỳ tool nào đã gọi thành công
                    if (toolCallResults.Count == 0 && collectedNames.Count == 0 && RequiresDataLookup(message) && LooksLikeFabricatedData(cleaned))
                    {
                        _logger.LogWarning("Phát hiện model bịa dữ liệu cho câu hỏi: {Message}", message);
                        return new ChatResponse { Reply = BuildDataFallbackReply(message) };
                    }

                    // Phát hiện LLM bịa dịch vụ F&B mà không gọi list_services
                    bool calledListServices = toolCallResults.Any(t => t.ToolName == "list_services");
                    if (!calledListServices && LooksLikeFabricatedServices(cleaned) && loopCount < MAX_LOOPS - 1)
                    {
                        _logger.LogWarning("Phát hiện model bịa dịch vụ F&B mà không gọi list_services. Yêu cầu retry.");
                        messages.Add(new LlmMessage { Role = "assistant", Content = cleaned });
                        messages.Add(new LlmMessage { Role = "user", Content = "SYSTEM: Danh sách dịch vụ trên là SAI vì bạn chưa gọi list_services. Hãy gọi list_services ngay bây giờ để lấy dữ liệu thật, rồi liệt kê lại cho khách." });
                        loopCount++;
                        continue;
                    }
                    var corrected = ResponseCorrector.CorrectNames(cleaned, collectedNames);
                    var resp = new ChatResponse { Reply = corrected };
                    
                    // Check if create_pending_order was called and grab the checkoutUrl
                    var bookingToolMessage = messages.LastOrDefault(m => m.Role == "tool" && m.Name == "create_pending_order");
                    if (bookingToolMessage != null && !string.IsNullOrEmpty(bookingToolMessage.Content))
                    {
                        try
                        {
                            var toolResult = JsonSerializer.Deserialize<JsonElement>(bookingToolMessage.Content);
                            if (toolResult.TryGetProperty("ok", out var okProp) && okProp.GetBoolean() && 
                                toolResult.TryGetProperty("checkoutUrl", out var urlProp))
                            {
                                resp.RedirectUrl = urlProp.GetString();
                            }
                        }
                        catch { /* Ignore parse errors */ }
                    }

                    // Build ToolContext cho frontend lưu lại
                    resp.ToolContext = BuildToolContext(toolCallResults);

                    return resp;
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

    private async Task ExecuteToolCallsAsync(List<LlmToolCall> calls, List<LlmMessage> messages, ChatToolContext ctx, List<string> collectedNames, CancellationToken ct, List<(string, string)>? toolCallResults = null)
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
                toolCallResults?.Add((call.Function.Name, resultJson));
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
        // Chỉ kiểm tra các dấu hiệu bịa dữ liệu rõ ràng
        string[] commonHallucinations = { "sát thủ bóng đêm", "tình yêu trong bóng tối", "cuộc chiến không giới hạn", "đại bàng bay cao", "câu chuyện bí ẩn", "bóng ma học đường", "ngôi nhà ma ám" };
        var lowerReply = reply.ToLowerInvariant();
        foreach (var fakeInfo in commonHallucinations) if (lowerReply.Contains(fakeInfo)) return true;

        // Check numbered list + quotes heuristic — nhưng yêu cầu nhiều dấu hiệu hơn
        bool hasNumberedList = reply.Contains("1.") && reply.Contains("2.");
        bool hasListIntro = reply.Contains("danh sách") || reply.Contains("dưới đây là");
        int quoteCount = reply.Count(c => c == '"' || c == '\'');
        bool hasManyQuotes = quoteCount >= 6;

        // Cần ít nhất 2 dấu hiệu để xác định là bịa
        int signals = (hasNumberedList ? 1 : 0) + (hasListIntro ? 1 : 0) + (hasManyQuotes ? 1 : 0);
        if (signals >= 2) return true;

        return false;
    }

    private static string BuildDataFallbackReply(string userMessage)
    {
        return "Xin lỗi bạn, hiện tại mình không thể lấy dữ liệu trực tiếp lúc này. 😅 Bạn vui lòng xem thông tin chi tiết về phim, lịch chiếu và giá vé ngay trên trang chủ của AuraCinema nhé!";
    }

    private static bool LooksLikeFabricatedServices(string text)
    {
        var lower = text.ToLower();
        // Kiểm tra xem có chứa các từ khóa dịch vụ phổ biến hoặc giá tiền bịa không
        bool hasServiceNames = lower.Contains("bắp rang bơ") || 
                               lower.Contains("nước ngọt") || 
                               lower.Contains("combo bắp");
        
        bool hasPriceFormat = System.Text.RegularExpressions.Regex.IsMatch(lower, @"\d{1,3}(\.|\,)\d{3}\s*(đ|vnd|vnđ)");

        return hasServiceNames || hasPriceFormat;
    }

    /// <summary>
    /// Xây dựng BOOKING_CONTEXT từ kết quả tool calls để frontend lưu lại.
    /// LLM sẽ nhìn thấy context này trong history ở tin nhắn tiếp theo.
    /// </summary>
    private static string? BuildToolContext(List<(string ToolName, string ResultJson)> toolCallResults)
    {
        if (toolCallResults.Count == 0) return null;

        var parts = new List<string>();

        foreach (var (toolName, resultJson) in toolCallResults)
        {
            try
            {
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("ok", out var okProp) || !okProp.GetBoolean())
                    continue;

                switch (toolName)
                {
                    case "list_services":
                        if (root.TryGetProperty("services", out var svcArr) && svcArr.ValueKind == JsonValueKind.Array)
                        {
                            var svcLines = new List<string> { "services:" };
                            int stt = 1;
                            foreach (var svc in svcArr.EnumerateArray())
                            {
                                var svcId = svc.TryGetProperty("serviceId", out var idP) ? idP.GetInt32() : 0;
                                var svcName = svc.TryGetProperty("serviceName", out var nameP) ? nameP.GetString() : "";
                                var svcPrice = svc.TryGetProperty("price", out var priceP) ? priceP.GetString() : "";
                                svcLines.Add($"  {stt}→serviceId={svcId}({svcName},{svcPrice})");
                                stt++;
                            }
                            parts.Add(string.Join("\n", svcLines));
                        }
                        break;

                    case "get_available_adjacent_seats":
                        if (root.TryGetProperty("groups", out var grpArr) && grpArr.ValueKind == JsonValueKind.Array)
                        {
                            var seatLines = new List<string>();
                            // Lưu showtime info
                            if (root.TryGetProperty("showtime", out var stInfo))
                            {
                                var stId = stInfo.TryGetProperty("showtimeId", out var stIdP) ? stIdP.GetInt32() : 0;
                                seatLines.Add($"showtimeId={stId}");
                            }
                            seatLines.Add("seat_groups:");
                            int grpIdx = 1;
                            foreach (var grp in grpArr.EnumerateArray())
                            {
                                var label = grp.TryGetProperty("label", out var lblP) ? lblP.GetString() : "";
                                var seatIds = new List<int>();
                                if (grp.TryGetProperty("seatIds", out var idsP) && idsP.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var id in idsP.EnumerateArray())
                                        if (id.TryGetInt32(out var sid)) seatIds.Add(sid);
                                }
                                seatLines.Add($"  {grpIdx}→{label}(seatIds=[{string.Join(",", seatIds)}])");
                                grpIdx++;
                            }
                            parts.Add(string.Join("\n", seatLines));
                        }
                        break;

                    case "get_showtimes":
                        if (root.TryGetProperty("movie", out var movieEl))
                        {
                            var movieId = movieEl.TryGetProperty("id", out var mIdP) ? mIdP.GetInt32() : 0;
                            var movieTitle = movieEl.TryGetProperty("title", out var mTitleP) ? mTitleP.GetString() : "";
                            parts.Add($"movie: id={movieId}, title={movieTitle}");
                        }
                        // Lưu showtimeIds để LLM có thể tham chiếu khi tạo đơn
                        if (root.TryGetProperty("groups", out var dateGroups) && dateGroups.ValueKind == JsonValueKind.Array)
                        {
                            var stLines = new List<string> { "showtimes:" };
                            foreach (var dg in dateGroups.EnumerateArray())
                            {
                                var dayLabel = dg.TryGetProperty("dayLabel", out var dlP) ? dlP.GetString() : "";
                                if (dg.TryGetProperty("showtimes", out var stArr) && stArr.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var st in stArr.EnumerateArray())
                                    {
                                        var stId = st.TryGetProperty("showtimeId", out var stIdP) ? stIdP.GetInt32() : 0;
                                        var startTime = st.TryGetProperty("startTime", out var stTimeP) ? stTimeP.GetString() : "";
                                        var endTime = st.TryGetProperty("endTime", out var etP) ? etP.GetString() : "";
                                        var roomName = st.TryGetProperty("roomName", out var rnP) ? rnP.GetString() : "";
                                        var available = st.TryGetProperty("availableSeats", out var avP) ? avP.GetInt32() : 0;
                                        stLines.Add($"  {dayLabel} {startTime}-{endTime} {roomName} ({available} ghế trống) → showtimeId={stId}");
                                    }
                                }
                            }
                            if (stLines.Count > 1) // có ít nhất 1 showtime
                                parts.Add(string.Join("\n", stLines));
                        }
                        break;
                }
            }
            catch { /* skip */ }
        }

        return parts.Count > 0 ? "[BOOKING_CONTEXT]\n" + string.Join("\n", parts) : null;
    }
}
