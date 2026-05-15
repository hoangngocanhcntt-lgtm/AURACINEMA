using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace AuraCinema.Web.Areas.Admin.ViewModels;

public class MovieFormViewModel
{
    public int MovieID { get; set; }

    [Display(Name = "Mã phim")]
    public string? MovieCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập Tên phim")]
    [Display(Name = "Tên phim")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập Thể loại")]
    [Display(Name = "Thể loại")]
    public string Genre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập Đạo diễn")]
    [Display(Name = "Đạo diễn")]
    public string Director { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập Diễn viên")]
    [Display(Name = "Diễn viên")]
    public string Actors { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập Thời lượng (phút)")]
    [Range(1, 500, ErrorMessage = "Thời lượng không hợp lệ")]
    [Display(Name = "Thời lượng (phút)")]
    public int Duration { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn Ngày khởi chiếu")]
    [Display(Name = "Ngày khởi chiếu")]
    public DateOnly ReleaseDate { get; set; }

    [Display(Name = "Link Trailer")]
    public string Trailer { get; set; } = string.Empty;

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Dang chieu";

    // Used to hold the current poster URL when editing
    public string CurrentPoster { get; set; } = string.Empty;

    // Used for uploading a new poster
    [Display(Name = "Poster Phim")]
    public IFormFile? PosterFile { get; set; }
}
