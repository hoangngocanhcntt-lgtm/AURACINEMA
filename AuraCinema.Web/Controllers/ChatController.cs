using System.Security.Claims;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Chat;
using AuraCinema.Services.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCinema.Web.Controllers;

[Route("api/chat")]
[ApiController]
[AllowAnonymous]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;
    private readonly ChatRateLimiter _rateLimiter;
    
    public ChatController(IChatService chat, ChatRateLimiter rateLimiter)
    {
        _chat = chat;
        _rateLimiter = rateLimiter;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest req)
    {
        // Xác định identifier: userId (nếu đã đăng nhập) hoặc IP
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdStr, out var uid) ? uid : null;

        var identifier = userId.HasValue
            ? $"user:{userId}"
            : $"ip:{HttpContext.Connection.RemoteIpAddress}";

        // Kiểm tra rate limit
        if (!_rateLimiter.TryAcquire(identifier))
        {
            var retryAfter = _rateLimiter.GetRetryAfterSeconds(identifier);
            return Ok(new ChatResponse
            {
                Reply = $"Bạn chat nhanh quá, hãy chờ {Math.Ceiling(retryAfter)} giây rồi thử lại nhé! 😊"
            });
        }

        var resp = await _chat.HandleAsync(userId, req.History, req.Message, HttpContext.RequestAborted);
        return Ok(resp);
    }
}
