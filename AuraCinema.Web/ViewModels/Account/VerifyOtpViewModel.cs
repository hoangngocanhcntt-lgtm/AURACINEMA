using System.ComponentModel.DataAnnotations;
namespace AuraCinema.Web.ViewModels.Account;

public class VerifyOtpViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm 6 chữ số.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ gồm chữ số.")]
    [Display(Name = "Mã OTP")]
    public string Otp { get; set; } = string.Empty;
}
