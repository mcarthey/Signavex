namespace Signavex.Domain.Configuration;

/// <summary>
/// Configuration for all external data provider API connections.
/// Bound from the "DataProviders" section of appsettings.
/// </summary>
public class DataProviderOptions
{
    public const string SectionName = "DataProviders";

    public ApiProviderOptions Polygon { get; set; } = new();
    public ApiProviderOptions AlphaVantage { get; set; } = new();
    public ApiProviderOptions Fred { get; set; } = new();
}

public class ApiProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public int MaxRequestsPerMinute { get; set; }
}
