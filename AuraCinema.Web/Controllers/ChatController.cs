using System.Security.Claims;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCinema.Web.Controllers;

[Route("api/chat")]
[ApiController]
[AllowAnonymous]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;
    
    public ChatController(IChatService chat)
    {
        _chat = chat;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest req)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdStr, out var uid) ? uid : null;
        var resp = await _chat.HandleAsync(userId, req.History, req.Message, HttpContext.RequestAborted);
        return Ok(resp);
    }
}
