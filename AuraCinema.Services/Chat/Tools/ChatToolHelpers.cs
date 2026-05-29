using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AuraCinema.Services.Chat.Tools;

public static class VietnameseText
{
    /// <summary>Bỏ dấu, lowercase, đổi "đ"->"d". Dùng để so khớp tên phim bất kể sai dấu.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var formD = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Replace("đ", "d")
            .Trim();
    }
}

public static class JsonArgExtensions
{
    /// <summary>Đọc 1 property theo nhiều tên (vd "minOrderAmount" hoặc "min_order_amount") — 8B hay đổi camel/snake.</summary>
    public static bool TryGetAny(this JsonElement args, out JsonElement value, params string[] names)
    {
        if (args.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in names)
            {
                if (args.TryGetProperty(name, out value))
                {
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}
