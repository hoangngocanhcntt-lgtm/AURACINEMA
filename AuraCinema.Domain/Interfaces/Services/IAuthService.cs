namespace AuraCinema.Domain.Interfaces.Services;

public interface IAuthService
{
    Task<(bool Success, string Message)> RegisterAsync(string fullName, string email, string password, string phone);
    Task<Domain.Entities.User?> LoginAsync(string email, string password);
    Task<bool> SendOtpAsync(string email);
    Task<bool> VerifyOtpAsync(string email, string otp);
    Task<bool> ResetPasswordAsync(string email, string newPassword);
    Task<bool> IsEmailExistsAsync(string email);
}
