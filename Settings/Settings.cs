namespace BookStore.Api.Settings;

public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 1440;
}

public class PricingOptions
{
    /// <summary>0.05 = 5% tax.</summary>
    public decimal TaxRate { get; set; } = 0.05m;

    /// <summary>0 = free delivery.</summary>
    public decimal ShippingFee { get; set; } = 0m;
}