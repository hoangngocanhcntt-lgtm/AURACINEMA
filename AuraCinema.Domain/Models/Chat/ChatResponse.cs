namespace AuraCinema.Domain.Models.Chat;

public class ChatResponse
{
    public string Reply { get; set; } = "";
    public bool RequireLogin { get; set; }
    public string? RedirectUrl { get; set; }
    public List<ChatMessage> UpdatedHistory { get; set; } = new();
    /// <summary>
    /// Tóm tắt kết quả tool calls (danh sách dịch vụ, ghế đã chọn, showtimeId...)
    /// để frontend lưu lại và gửi kèm ở tin nhắn tiếp theo.
    /// </summary>
    public string? ToolContext { get; set; }
}
