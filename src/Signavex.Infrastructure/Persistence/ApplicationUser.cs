using Microsoft.AspNetCore.Identity;

namespace Signavex.Infrastructure.Persistence;

public class ApplicationUser : IdentityUser
{
    public string SubscriptionPlan { get; set; } = "Free";
    public string? StripeCustomerId { get; set; }
}
