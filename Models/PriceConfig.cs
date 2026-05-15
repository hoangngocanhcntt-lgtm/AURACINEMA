using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuraCinemaWeb.Models
{
    public class PriceConfig
    {
        [Key]
        [MaxLength(50)]
        public string ConfigID { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ConfigType { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? SeatType { get; set; }

        [Required]
        [MaxLength(50)]
        public string ConfigCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ConfigName { get; set; } = string.Empty;

        public int Amount { get; set; }

        [MaxLength(20)]
        public string? DayOfWeek { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        public DateOnly EffectiveDate { get; set; }
    }
}
