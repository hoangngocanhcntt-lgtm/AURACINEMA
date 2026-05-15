using System.ComponentModel.DataAnnotations;

namespace AuraCinema.Web.Areas.Admin.ViewModels;

public class PromotionFormViewModel
{
    public int PromoID { get; set; }

    [Display(Name = "Mã khuyến mãi")]
    public string? PromoCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mức giảm giá")]
    [Range(0, 1000000, ErrorMessage = "Mức giảm giá không hợp lệ")]
    [Display(Name = "Mức giảm (VNĐ)")]
    public int DiscountValue { get; set; }

    [Display(Name = "Điều kiện")]
    public string Condition { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
    [Display(Name = "Ngày bắt đầu")]
    public DateTime StartDate { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
    [Display(Name = "Ngày kết thúc")]
    public DateTime EndDate { get; set; } = DateTime.Now.AddMonths(1);

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Hoat dong";
}
