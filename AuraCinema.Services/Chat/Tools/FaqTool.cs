using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AuraCinema.Services.Chat.Tools;

public class FaqTool : IChatTool
{
    private static readonly Dictionary<string, string> _faq = new(StringComparer.OrdinalIgnoreCase)
    {
        // TODO[user]: Thay nội dung dưới bằng chính sách thật của rạp AuraCinema.
        ["hoan_ve"]      = "[PLACEHOLDER] Mô tả chính sách hoàn vé.",
        ["doi_ve"]       = "[PLACEHOLDER] Mô tả chính sách đổi vé.",
        ["do_an"]        = "[PLACEHOLDER] Quy định mang đồ ăn ngoài.",
        ["do_tuoi"]      = "[PLACEHOLDER] Quy định độ tuổi (P, K, T13, T16, T18, C).",
        ["den_muon"]     = "[PLACEHOLDER] Quy định đến muộn.",
        ["mat_ve"]       = "[PLACEHOLDER] Quy định khi mất vé/QR.",
        ["thanh_toan"]   = "[PLACEHOLDER] Các phương thức thanh toán.",
        ["hotline"]      = "[PLACEHOLDER] Hotline + giờ hỗ trợ.",
        ["dia_chi"]      = "[PLACEHOLDER] Địa chỉ rạp + giờ mở cửa."
    };

    public string Name => "get_faq";

    public string Description => "Trả lời câu hỏi thường gặp về chính sách (hoàn vé, mang đồ ăn, đổi vé, độ tuổi...).";

    public object Schema => new
    {
        type = "object",
        properties = new
        {
            topic = new
            {
                type = "string",
                description = "Mã chủ đề FAQ. PHẢI là 1 trong các giá trị: hoan_ve, doi_ve, do_an, do_tuoi, den_muon, mat_ve, thanh_toan, hotline, dia_chi.",
                @enum = new[] { "hoan_ve", "doi_ve", "do_an", "do_tuoi", "den_muon", "mat_ve", "thanh_toan", "hotline", "dia_chi" }
            }
        },
        required = new[] { "topic" }
    };

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Replace("đ", "d")
            .Replace(" ", "_");
    }

    public Task<object> ExecuteAsync(JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        var topic = args.TryGetProperty("topic", out var tProp) ? tProp.GetString() ?? "" : "";
        var normalizedTopic = RemoveDiacritics(topic);

        foreach (var kvp in _faq)
        {
            if (normalizedTopic.Contains(kvp.Key) || kvp.Key.Contains(normalizedTopic))
            {
                return Task.FromResult<object>(new { ok = true, topic = kvp.Key, answer = kvp.Value });
            }
        }

        return Task.FromResult<object>(new
        {
            ok = false,
            error = "FAQ_NOT_FOUND",
            suggestion = "Bạn có thể hỏi về: hoàn vé, đổi vé, đồ ăn, độ tuổi, đến muộn, mất vé, thanh toán, hotline, địa chỉ.",
            fallback = "Mình chưa có thông tin này, bạn liên hệ hotline để được hỗ trợ nhé."
        });
    }
}
