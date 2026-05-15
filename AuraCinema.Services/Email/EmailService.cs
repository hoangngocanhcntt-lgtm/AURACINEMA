using AuraCinema.Domain.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace AuraCinema.Services.Email;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config) => _config = config;

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(
            _config["Email:FromName"] ?? "Aura Cinema",
            _config["Email:Username"]!));
        msg.To.Add(new MailboxAddress(toName, toEmail));
        msg.Subject = subject;
        msg.Body = new TextPart("html") { Text = htmlBody };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(
            _config["Email:SmtpHost"]!,
            int.Parse(_config["Email:SmtpPort"] ?? "587"),
            SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(
            _config["Email:Username"]!,
            _config["Email:Password"]!);
        await smtp.SendAsync(msg);
        await smtp.DisconnectAsync(true);
    }

    public async Task SendOtpEmailAsync(string toEmail, string toName, string otp)
    {
        var html = $@"
        <div style='font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:32px;
                    border-radius:12px;background:#0f0c1a;color:#fff;'>
            <h2 style='color:#e91e8c;text-align:center;'>🎬 AURA CINEMA</h2>
            <p>Xin chào <strong>{toName}</strong>,</p>
            <p>Mã OTP đặt lại mật khẩu của bạn là:</p>
            <div style='font-size:36px;font-weight:bold;letter-spacing:12px;
                        text-align:center;color:#e91e8c;padding:20px 0;'>{otp}</div>
            <p style='color:#aaa;font-size:13px;'>Mã có hiệu lực trong <strong>15 phút</strong>.
            Không chia sẻ mã này với bất kỳ ai.</p>
            <hr style='border-color:#333;'/>
            <p style='color:#666;font-size:12px;text-align:center;'>
                © 2025 Aura Cinema. All rights reserved.</p>
        </div>";

        await SendAsync(toEmail, toName, "🔐 Mã OTP xác thực — Aura Cinema", html);
    }

    public async Task SendTicketConfirmationAsync(string toEmail, string toName, int orderId)
    {
        var html = $@"
        <div style='font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:32px;
                    border-radius:12px;background:#0f0c1a;color:#fff;'>
            <h2 style='color:#e91e8c;text-align:center;'>🎬 AURA CINEMA</h2>
            <p>Xin chào <strong>{toName}</strong>,</p>
            <p>Đặt vé <strong>#{orderId}</strong> của bạn đã được xác nhận thành công!</p>
            <p>Vui lòng vào mục <strong>Vé của tôi</strong> để xem chi tiết và mã QR.</p>
            <hr style='border-color:#333;'/>
            <p style='color:#666;font-size:12px;text-align:center;'>
                © 2025 Aura Cinema. All rights reserved.</p>
        </div>";

        await SendAsync(toEmail, toName, $"✅ Xác nhận đặt vé #{orderId} — Aura Cinema", html);
    }
}
