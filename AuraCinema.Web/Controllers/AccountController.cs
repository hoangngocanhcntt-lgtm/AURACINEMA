using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuraCinema.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;

    public AccountController(IAuthService auth) => _auth = auth;

    // ─────────────── ĐĂNG NHẬP ───────────────
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            return RedirectByRole(role);
        }
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var user = await _auth.LoginAsync(model.Email, model.Password);
            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role)
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var props = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            TempData["Success"] = "Đăng nhập thành công!";
            return RedirectByRole(user.Role);
        }
        catch (System.Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // ─────────────── ĐĂNG XUẤT ───────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Đăng xuất thành công!";
        return RedirectToAction("Index", "Home");
    }

    // ─────────────── ĐĂNG KÝ ───────────────
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            return RedirectByRole(role);
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (success, message) = await _auth.RegisterAsync(
            model.FullName, model.Email, model.Password, model.Phone);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        TempData["Success"] = "Đăng ký thành công! Vui lòng đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    // ─────────────── QUÊN MẬT KHẨU — Bước 1 ───────────────
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Luôn thông báo gửi thành công dù email không tồn tại (tránh enumerate)
        await _auth.SendOtpAsync(model.Email);

        TempData["OtpEmail"] = model.Email;
        TempData["Info"] = "Nếu email tồn tại, mã OTP đã được gửi. Vui lòng kiểm tra hộp thư.";
        return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
    }

    // ─────────────── QUÊN MẬT KHẨU — Bước 2: Nhập OTP ───────────────
    [HttpGet]
    public IActionResult VerifyOtp(string? email)
        => View(new VerifyOtpViewModel { Email = email ?? string.Empty });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var valid = await _auth.VerifyOtpAsync(model.Email, model.Otp);
        if (!valid)
        {
            ModelState.AddModelError(string.Empty, "Mã OTP không đúng hoặc đã hết hạn.");
            return View(model);
        }

        TempData["VerifiedEmail"] = model.Email;
        return RedirectToAction(nameof(ResetPassword), new { email = model.Email });
    }

    // ─────────────── QUÊN MẬT KHẨU — Bước 3: Đặt mật khẩu mới ───────────────
    [HttpGet]
    public IActionResult ResetPassword(string? email)
        => View(new ResetPasswordViewModel { Email = email ?? string.Empty });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var ok = await _auth.ResetPasswordAsync(model.Email, model.NewPassword);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "Có lỗi xảy ra. Vui lòng thử lại từ đầu.");
            return View(model);
        }

        TempData["Success"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    // ─────────────── ACCESS DENIED ───────────────
    public IActionResult AccessDenied() => View();

    // ─────────────── HELPER ───────────────
    private IActionResult RedirectByRole(string? role)
    {
        if (role == "Admin")
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
        if (role == "Staff")
        {
            return RedirectToAction("Index", "TicketScanner", new { area = "Admin" });
        }
        return RedirectToAction("Index", "Home");
    }
}
