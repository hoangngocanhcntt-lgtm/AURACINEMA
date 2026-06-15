namespace AuraCinema.Domain.Entities;
public class PriceConfig {
    public int ConfigID { get; set; }
    public string ConfigType { get; set; } = string.Empty;
    public string ConfigCode { get; set; } = string.Empty;
    public string ConfigName { get; set; } = string.Empty;
    public int SurchargeAmount { get; set; }
    public int? NewSurchargeAmount { get; set; }
    public DateTime? EffectiveDate { get; set; }
    
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int ActiveSurchargeAmount => (EffectiveDate.HasValue && EffectiveDate.Value.Date <= DateTime.Now.Date && NewSurchargeAmount.HasValue) 
        ? NewSurchargeAmount.Value 
        : SurchargeAmount;
}
