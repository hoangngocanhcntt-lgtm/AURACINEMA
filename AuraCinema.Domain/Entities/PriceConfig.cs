namespace AuraCinema.Domain.Entities;
public class PriceConfig {
    public int ConfigID { get; set; }
    public string ConfigType { get; set; } = string.Empty;
    public string ConfigCode { get; set; } = string.Empty;
    public string ConfigName { get; set; } = string.Empty;
    public int SurchargeAmount { get; set; }
}
