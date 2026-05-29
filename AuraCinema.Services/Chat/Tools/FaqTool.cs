using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AuraCinema.Services.Chat.Tools;

public class FaqTool : IChatTool
{
    // TODO[user]: Đây là nội dung mẫu hợp lý cho rạp — chỉnh lại theo chính sách thật
    // (đặc biệt hotline, địa chỉ, giờ mở cửa) trước khi demo.
    private static readonly Dictionary<string, string> _faq = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hoan_ve"]      = "Vé đã thanh toán được hoàn 100% nếu hủy trước giờ chiếu ít nhất 2 tiếng. Trong vòng 2 tiếng trước suất chiếu thì không hỗ trợ hoàn. Tiền hoàn về lại phương thức thanh toán ban đầu trong 3-5 ngày làm việc.",
        ["doi_ve"]       = "Bạn có thể đổi sang suất khác của cùng phim, trước giờ chiếu ít nhất 2 tiếng và tối đa 1 lần mỗi vé. Phần chênh lệch giá (nếu có) sẽ được thu thêm hoặc hoàn lại. Thực hiện trong mục 'Vé của tôi' hoặc tại quầy.",
        ["do_an"]        = "Rạp có quầy bắp nước riêng nên không nhận đồ ăn, thức uống mang từ ngoài vào, trừ nước lọc và đồ ăn cho trẻ nhỏ hoặc theo nhu cầu sức khỏe.",
        ["do_tuoi"]      = "Phim được phân loại độ tuổi: P (mọi lứa tuổi), K (dưới 13 cần người lớn đi kèm), T13/T16/T18 (đủ 13/16/18 tuổi trở lên). Vui lòng mang giấy tờ tùy thân khi xem phim có giới hạn tuổi.",
        ["den_muon"]     = "Bạn vẫn được vào phòng chiếu khi đến muộn, nhưng rạp không chiếu lại phần đầu và không hoàn/đổi vé vì lý do đến muộn. Nên đến trước giờ chiếu khoảng 15 phút nhé.",
        ["mat_ve"]       = "Vé điện tử được lưu trong mục 'Vé của tôi' nên không lo bị mất, bạn chỉ cần mở mã QR khi vào cổng. Nếu không mở được mã, mang CCCD trùng với tài khoản đặt vé ra quầy để được hỗ trợ.",
        ["thanh_toan"]   = "Rạp hỗ trợ thanh toán online qua PayOS: quét mã QR ngân hàng, ví điện tử (MoMo, ZaloPay...) và thẻ ATM/Visa/MasterCard. Tại quầy có thể thanh toán tiền mặt hoặc chuyển khoản.",
        ["hotline"]      = "Hotline hỗ trợ của AuraCinema: 1900 1234, phục vụ từ 8:00 đến 22:00 hằng ngày. Bạn cũng có thể nhắn ngay tại đây, mình hỗ trợ bạn nha.",
        ["dia_chi"]      = "AuraCinema mở cửa 8:00-23:00 hằng ngày. Bạn xem địa chỉ chi tiết và chỉ đường của từng rạp trong mục 'Hệ thống rạp' nhé."
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
