namespace Signavex.Domain.Configuration;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = "";
    public string PublishableKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string ProPriceId { get; set; } = "";
}
