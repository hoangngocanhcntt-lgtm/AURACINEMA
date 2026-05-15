using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AuraCinema.Web.Areas.Admin.ViewModels;

public class ShowtimeFormViewModel
{
    public int ShowtimeID { get; set; }

    [Display(Name = "Mã suất chiếu")]
    public string? ShowtimeCode { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phim")]
    [Display(Name = "Phim")]
    public int MovieID { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phòng")]
    [Display(Name = "Phòng chiếu")]
    public int RoomID { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu")]
    [Display(Name = "Thời gian bắt đầu")]
    public DateTime StartTime { get; set; } = DateTime.Now;

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Sap chieu";

    // SelectLists for Dropdowns
    public IEnumerable<SelectListItem>? MovieList { get; set; }
    public IEnumerable<SelectListItem>? RoomList { get; set; }
}
