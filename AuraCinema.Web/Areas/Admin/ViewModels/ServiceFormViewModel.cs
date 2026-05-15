using System.ComponentModel.DataAnnotations;

namespace AuraCinema.Web.Areas.Admin.ViewModels;

public class ServiceFormViewModel
{
    public int ServiceID { get; set; }

    [Display(Name = "Mã dịch vụ")]
    public string? ServiceCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ")]
    [Display(Name = "Tên dịch vụ")]
    public string ServiceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập giá")]
    [Range(0, 1000000, ErrorMessage = "Giá không hợp lệ")]
    [Display(Name = "Giá (VNĐ)")]
    public int Price { get; set; }

    [Display(Name = "Hình ảnh")]
    public string? Image { get; set; }

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Hoat dong";
}
