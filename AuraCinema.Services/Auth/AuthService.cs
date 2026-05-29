using AuraCinema.Domain.Entities;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Helpers;
using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Services.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public AuthService(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(
        string fullName, string email, string password, string phone)
    {
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return (false, "Email đã được sử dụng.");

        var user = new User
        {
            UserCode = CodeGenerator.GenerateUserCode(),
            FullName = fullName,
            Email    = email.ToLower().Trim(),
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Phone    = phone,
            Role     = "Khach hang",
            Status   = "Hoat dong"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return (true, "Đăng ký thành công.");
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim());

        if (user is null) return null;
        if (user.Status == "Da khoa") 
            throw new System.Exception("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Admin.");
        if (!BCrypt.Net.BCrypt.Verify(password, user.Password)) return null;

        return user;
    }

    public async Task<bool> SendOtpAsync(string email)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim());
        if (user is null) return false;

        var otp = new Random().Next(100000, 999999).ToString();
        user.OtpCode   = otp;
        user.OtpExpiry = DateTime.UtcNow.AddMinutes(15);
        await _db.SaveChangesAsync();

        await _email.SendOtpEmailAsync(user.Email, user.FullName, otp);
        return true;
    }

    public async Task<bool> VerifyOtpAsync(string email, string otp)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim());

        if (user is null) return false;
        if (user.OtpCode != otp) return false;
        if (user.OtpExpiry < DateTime.UtcNow) return false;

        // OTP dùng 1 lần
        user.OtpCode   = null;
        user.OtpExpiry = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(string email, string newPassword)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim());
        if (user is null) return false;

        user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsEmailExistsAsync(string email)
        => await _db.Users.AnyAsync(u => u.Email == email.ToLower().Trim());
}
