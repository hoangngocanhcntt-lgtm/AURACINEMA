namespace AuraCinema.Domain.Interfaces.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string toName, string otp);
    Task SendTicketConfirmationAsync(string toEmail, string toName, int orderId);
}
