namespace Signavex.Domain.Configuration;

/// <summary>
/// Root configuration block for Signavex, bound from the "Signavex" section of appsettings.
/// </summary>
public class SignavexOptions
{
    public const string SectionName = "Signavex";

    /// <summary>
    /// Minimum FinalScore for a stock to be surfaced in the dashboard (default 0.65).
    /// Small-cap tier uses 0.75.
    /// </summary>
    public double SurfacingThreshold { get; set; } = 0.65;

    /// <summary>
    /// Which index universes to scan. Valid values: "SP500", "SP400", "SP600".
    /// SP600 is opt-in and uses a higher surfacing threshold (0.75).
    /// </summary>
    public List<string> Universe { get; set; } = ["SP500", "SP400"];

    /// <summary>
    /// Absolute path to the shared data directory containing signavex.db.
    /// Both Worker and Web must point to the same directory.
    /// </summary>
    public string DataDirectory { get; set; } = "";

    public string DatabaseProvider { get; set; } = "Sqlite";

    public string ConnectionString { get; set; } = "";

    public SignalWeightsOptions SignalWeights { get; set; } = new();

    public MarketSignalWeightsOptions MarketSignalWeights { get; set; } = new();
}
