using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Signavex.Domain.Configuration;
using Signavex.Engine;
using Signavex.Infrastructure;
using Signavex.Infrastructure.Email;
using Signavex.Infrastructure.Persistence;
using Signavex.Signals;
using Stripe;
using Stripe.Checkout;
using Signavex.Web.Components;
using Signavex.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Bind configuration options
builder.Services.Configure<SignavexOptions>(
    builder.Configuration.GetSection(SignavexOptions.SectionName));

builder.Services.Configure<DataProviderOptions>(
    builder.Configuration.GetSection(DataProviderOptions.SectionName));

builder.Services.Configure<AnthropicOptions>(
    builder.Configuration.GetSection(AnthropicOptions.SectionName));

builder.Services.Configure<StripeOptions>(
    builder.Configuration.GetSection(StripeOptions.SectionName));

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));

var providerOptions = builder.Configuration
    .GetSection(DataProviderOptions.SectionName)
    .Get<DataProviderOptions>() ?? new DataProviderOptions();

var signavexOptions = builder.Configuration
    .GetSection(SignavexOptions.SectionName)
    .Get<SignavexOptions>() ?? new SignavexOptions();

// Register domain layers
builder.Services
    .AddSignavexSignals()
    .AddSignavexEngine()
    .AddSignavexInfrastructure(providerOptions, signavexOptions.ConnectionString);

// ASP.NET Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<SignavexDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SendGridEmailSender>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/login";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// Application services
builder.Services.AddSingleton<ScanDashboardService>();
builder.Services.AddSingleton<BacktestRunnerService>();
builder.Services.AddSingleton<ApiKeyValidationService>();
builder.Services.AddSingleton<EconomicDashboardService>();
builder.Services.AddSingleton<DailyBriefService>();

// Authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ProRequired", policy => policy.RequireRole("Pro"));

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SignavexDbContext>();

// Rate limiting for public pages
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Blazor — Static SSR only, no interactive server mode (no SignalR circuit)
builder.Services.AddRazorComponents();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider,
    Microsoft.AspNetCore.Components.Server.ServerAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Configure Stripe API key
var stripeOptions = builder.Configuration
    .GetSection(StripeOptions.SectionName)
    .Get<StripeOptions>() ?? new StripeOptions();
StripeConfiguration.ApiKey = stripeOptions.SecretKey;

// Initialize database — migrations handle all schema and seed data
using (var scope = app.Services.CreateScope())
{
    await using var db = await scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<SignavexDbContext>>()
        .CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapHealthChecks("/health");

app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/account/login");
}).DisableAntiforgery();

// Stripe: Create checkout session for Pro upgrade
app.MapPost("/account/upgrade", async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    Microsoft.Extensions.Options.IOptions<StripeOptions> stripeOpts) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null) return Results.Redirect("/account/login");

    // Create or reuse Stripe customer
    if (string.IsNullOrEmpty(user.StripeCustomerId))
    {
        var customerService = new CustomerService();
        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = user.Email,
            Metadata = new Dictionary<string, string> { { "UserId", user.Id } }
        });
        user.StripeCustomerId = customer.Id;
        await userManager.UpdateAsync(user);
    }

    var opts = stripeOpts.Value;
    var sessionService = new SessionService();
    var session = await sessionService.CreateAsync(new SessionCreateOptions
    {
        Customer = user.StripeCustomerId,
        PaymentMethodTypes = ["card"],
        LineItems = [new SessionLineItemOptions { Price = opts.ProPriceId, Quantity = 1 }],
        Mode = "subscription",
        SuccessUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/account/upgrade-success",
        CancelUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/account/upgrade-cancelled"
    });

    return Results.Redirect(session.Url);
}).RequireAuthorization();

// Stripe: Webhook endpoint
app.MapPost("/webhooks/stripe", async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    Microsoft.Extensions.Options.IOptions<StripeOptions> stripeOpts,
    ILogger<Program> logger) =>
{
    var json = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
    try
    {
        var stripeEvent = EventUtility.ConstructEvent(
            json,
            httpContext.Request.Headers["Stripe-Signature"],
            stripeOpts.Value.WebhookSecret);

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
            {
                var session = stripeEvent.Data.Object as Session;
                if (session?.CustomerId is not null)
                {
                    var user = await FindUserByStripeCustomerId(userManager, session.CustomerId);
                    if (user is not null)
                    {
                        user.SubscriptionPlan = "Pro";
                        await userManager.UpdateAsync(user);
                        if (!await userManager.IsInRoleAsync(user, "Pro"))
                            await userManager.AddToRoleAsync(user, "Pro");
                        if (await userManager.IsInRoleAsync(user, "Free"))
                            await userManager.RemoveFromRoleAsync(user, "Free");
                        logger.LogInformation("User {UserId} upgraded to Pro", user.Id);
                    }
                }
                break;
            }
            case EventTypes.CustomerSubscriptionDeleted:
            {
                var subscription = stripeEvent.Data.Object as Subscription;
                if (subscription?.CustomerId is not null)
                {
                    var user = await FindUserByStripeCustomerId(userManager, subscription.CustomerId);
                    if (user is not null)
                    {
                        user.SubscriptionPlan = "Free";
                        await userManager.UpdateAsync(user);
                        if (await userManager.IsInRoleAsync(user, "Pro"))
                            await userManager.RemoveFromRoleAsync(user, "Pro");
                        if (!await userManager.IsInRoleAsync(user, "Free"))
                            await userManager.AddToRoleAsync(user, "Free");
                        logger.LogInformation("User {UserId} downgraded to Free", user.Id);
                    }
                }
                break;
            }
            case EventTypes.InvoicePaymentFailed:
            {
                var invoice = stripeEvent.Data.Object as Invoice;
                logger.LogWarning("Payment failed for Stripe customer {CustomerId}", invoice?.CustomerId);
                break;
            }
        }

        return Results.Ok();
    }
    catch (StripeException ex)
    {
        logger.LogWarning(ex, "Stripe webhook signature verification failed");
        return Results.BadRequest();
    }
}).DisableAntiforgery();

// Stripe: Customer portal for billing management
app.MapPost("/account/billing-portal", async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user?.StripeCustomerId is null) return Results.Redirect("/");

    var portalService = new Stripe.BillingPortal.SessionService();
    var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
    {
        Customer = user.StripeCustomerId,
        ReturnUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/account/manage"
    });

    return Results.Redirect(session.Url);
}).RequireAuthorization();

// Insights: enqueue brief generation command
app.MapPost("/insights/generate", async (DailyBriefService briefService) =>
{
    await briefService.RequestGenerationAsync();
    return Results.Redirect("/insights?requested=true");
}).RequireAuthorization();

// Economy: enqueue economic data sync command
app.MapPost("/economy/sync", async (EconomicDashboardService econ) =>
{
    await econ.RequestSyncAsync();
    return Results.Redirect("/economy?requested=true");
}).RequireAuthorization();

// Dashboard: enqueue scan command
app.MapPost("/dashboard/scan", async (ScanDashboardService dashboard) =>
{
    await dashboard.RequestScanAsync();
    return Results.Redirect("/?requested=true");
}).RequireAuthorization();

// Dashboard: CSV export of latest scan candidates
app.MapGet("/dashboard/export.csv", async (ScanDashboardService dashboard) =>
{
    var result = await dashboard.GetLatestResultAsync();
    if (result is null || result.Candidates.Count == 0)
        return Results.NotFound();

    var csv = Signavex.Domain.Helpers.CsvExportHelper.GenerateCsv(result.Candidates);
    var fileName = $"signavex-scan-{DateTime.UtcNow:yyyy-MM-dd}.csv";
    return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
}).RequireAuthorization();

// Backtest: kick off a backtest run (fire and forget)
app.MapPost("/backtest/run", (BacktestRunnerService backtest, HttpContext ctx) =>
{
    var asOfStr = ctx.Request.Form["asOfDate"].ToString();
    if (!DateOnly.TryParse(asOfStr, out var asOf))
        asOf = DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));

    // Fire and forget — backtest takes minutes, user must refresh to see result
    _ = Task.Run(() => backtest.RunBacktestAsync(asOf));

    return Results.Redirect("/backtest?requested=true");
}).RequireAuthorization().DisableAntiforgery();

// Backtest: CSV export of latest backtest result
app.MapGet("/backtest/export.csv", (BacktestRunnerService backtest) =>
{
    var result = backtest.LatestResult;
    if (result is null || result.Candidates.Count == 0)
        return Results.NotFound();

    var csv = Signavex.Domain.Helpers.CsvExportHelper.GenerateCsv(result.Candidates);
    var fileName = $"signavex-backtest-{result.AsOfDate:yyyy-MM-dd}.csv";
    return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
}).RequireAuthorization();

app.MapRazorComponents<App>();

app.Run();

static async Task<ApplicationUser?> FindUserByStripeCustomerId(
    UserManager<ApplicationUser> userManager, string stripeCustomerId)
{
    return await userManager.Users
        .FirstOrDefaultAsync(u => u.StripeCustomerId == stripeCustomerId);
}
