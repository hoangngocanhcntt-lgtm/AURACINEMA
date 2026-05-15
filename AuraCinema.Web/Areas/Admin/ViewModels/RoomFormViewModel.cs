using System.ComponentModel.DataAnnotations;

namespace AuraCinema.Web.Areas.Admin.ViewModels;

public class RoomFormViewModel
{
    public int RoomID { get; set; }

    [Display(Name = "Mã phòng")]
    public string? RoomCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên phòng")]
    [Display(Name = "Tên phòng")]
    public string RoomName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số lượng ghế")]
    [Range(1, 500, ErrorMessage = "Sức chứa từ 1 đến 500")]
    [Display(Name = "Sức chứa")]
    public int Capacity { get; set; } = 100;

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Hoat dong";
}
