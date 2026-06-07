using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AuraCinema.Services.Chat;

/// <summary>
/// Post-processor: sửa tên phim/thực thể bị LLM viết sai dấu tiếng Việt
/// bằng cách so khớp mờ (fuzzy match) với tên đúng lấy từ Database.
/// </summary>
public static class ResponseCorrector
{
    // ───────────────────────────────────────────────
    // 1) Trích xuất tên thực thể đúng từ tool result
    // ───────────────────────────────────────────────

    /// <summary>
    /// Đọc JSON kết quả tool, trả về danh sách tên thực thể (title, promoCode...).
    /// </summary>
    public static List<string> ExtractNames(string toolName, string resultJson)
    {
        var names = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            switch (toolName)
            {
                case "search_movies":
                    ExtractFromArray(root, "movies", "title", names);
                    break;

                case "get_showtimes":
                    // movie.title nằm trong object "movie"
                    if (root.TryGetProperty("movie", out var movie) &&
                        movie.TryGetProperty("title", out var mt) &&
                        mt.GetString() is string movieTitle)
                    {
                        names.Add(movieTitle);
                    }
                    break;

                case "list_promotions":
                    ExtractFromArray(root, "promotions", "title", names);
                    break;

                case "list_services":
                    ExtractFromArray(root, "services", "serviceName", names);
                    break;

                case "get_available_adjacent_seats":
                    // Extract seat group labels (e.g. "D1-D3") to mark that real data was used
                    ExtractFromArray(root, "groups", "label", names);
                    break;
            }
        }
        catch { /* ignore parse errors */ }

        return names.Where(n => !string.IsNullOrWhiteSpace(n) && n.Length >= 3)
                    .Distinct()
                    .ToList();
    }

    private static void ExtractFromArray(JsonElement root, string arrayProp, string fieldName, List<string> names)
    {
        if (root.TryGetProperty(arrayProp, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty(fieldName, out var val) && val.GetString() is string s)
                    names.Add(s);
            }
        }
    }

    // ───────────────────────────────────────────────
    // 2) Sửa tên bị sai trong response của LLM
    // ───────────────────────────────────────────────

    /// <summary>
    /// Thay thế tên bị sai dấu trong response bằng tên đúng từ DB.
    /// </summary>
    public static string CorrectNames(string response, IReadOnlyList<string> correctNames)
    {
        if (string.IsNullOrEmpty(response) || correctNames == null || correctNames.Count == 0)
            return response;

        // Xử lý tên dài trước để tránh replace chồng chéo
        foreach (var name in correctNames.OrderByDescending(n => n.Length))
        {
            if (name.Length < 3) continue;

            // Đã đúng rồi → skip
            if (response.Contains(name, StringComparison.Ordinal)) continue;

            response = FindAndReplace(response, name);
        }

        return response;
    }

    private static string FindAndReplace(string text, string correctName)
    {
        var correctAscii = RemoveDiacritics(correctName).ToUpperInvariant();
        var textAscii = RemoveDiacritics(text).ToUpperInvariant();

        // ── Fast path: ASCII exact match ──
        // Trường hợp phổ biến: LLM chỉ sai dấu thanh nhưng đúng base vowel
        int exactIdx = textAscii.IndexOf(correctAscii, StringComparison.Ordinal);
        if (exactIdx >= 0)
        {
            return text[..exactIdx] + correctName + text[(exactIdx + correctAscii.Length)..];
        }

        // ── Slow path: Fuzzy word-aligned match ──
        // Trường hợp LLM sai cả base vowel (VD: ĐƠN → ĐẢN, HÀNG → HƯƠNG)
        return FuzzyReplace(text, correctName);
    }

    private static string FuzzyReplace(string text, string correctName)
    {
        var textWords = GetWordSpans(text);
        var nameWords = correctName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int nameWC = nameWords.Length;

        if (nameWC == 0 || textWords.Count < nameWC) return text;

        int bestStart = -1;
        int bestEnd = -1;
        double bestSim = 0;

        var correctAscii = RemoveDiacritics(correctName).ToUpperInvariant();

        // Thử các cửa sổ có số từ ±1 so với tên đúng
        int minWC = Math.Max(1, nameWC - 1);
        int maxWC = Math.Min(nameWC + 1, textWords.Count);

        for (int wc = minWC; wc <= maxWC; wc++)
        {
            for (int i = 0; i <= textWords.Count - wc; i++)
            {
                int spanStart = textWords[i].start;
                var last = textWords[i + wc - 1];
                int spanEnd = last.start + last.length;

                var candidate = text[spanStart..spanEnd];

                // Bỏ dấu câu trailing để so sánh chính xác hơn
                var candidateClean = candidate.TrimEnd(',', '.', ';', '!', '?', '\n', '\r', ' ', ')');
                if (candidateClean.Length == 0) continue;

                var candidateAscii = RemoveDiacritics(candidateClean).ToUpperInvariant();

                // Quick filter: chênh lệch độ dài > 40% → skip
                if (Math.Abs(candidateAscii.Length - correctAscii.Length) > correctAscii.Length * 0.4)
                    continue;

                var sim = Similarity(correctAscii, candidateAscii);
                if (sim > bestSim && sim >= 0.60)
                {
                    bestSim = sim;
                    bestStart = spanStart;
                    bestEnd = spanStart + candidateClean.Length;
                }
            }
        }

        if (bestStart >= 0 && bestEnd > bestStart)
        {
            return text[..bestStart] + correctName + text[bestEnd..];
        }

        return text;
    }

    // ───────────────────────────────────────────────
    // 3) Utilities
    // ───────────────────────────────────────────────

    private static List<(int start, int length)> GetWordSpans(string text)
    {
        var spans = new List<(int, int)>();
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) break;
            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            spans.Add((start, i - start));
        }
        return spans;
    }

    private static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        int dist = LevenshteinDistance(a, b);
        return 1.0 - (double)dist / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int n = a.Length, m = b.Length;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    /// <summary>Bỏ toàn bộ dấu tiếng Việt, đ→d, Đ→D.</summary>
    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D');
    }
}
