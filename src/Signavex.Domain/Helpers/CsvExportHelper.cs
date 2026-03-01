using System.Text;
using Signavex.Domain.Models;

namespace Signavex.Domain.Helpers;

/// <summary>
/// Generates CSV content from StockCandidate lists. Pure domain logic, no I/O dependencies.
/// </summary>
public static class CsvExportHelper
{
    public static string GenerateCsv(IEnumerable<StockCandidate> candidates)
    {
        var sb = new StringBuilder();

        // Header row
        sb.AppendLine("Ticker,Company,Tier,RawScore,FinalScore,SignalCount,EvaluatedAt,SignalScores");

        foreach (var c in candidates)
        {
            var signalScores = string.Join("; ",
                c.SignalResults
                    .Where(s => s.IsAvailable)
                    .Select(s => $"{s.SignalName}={s.Score:F2}"));

            sb.Append(Escape(c.Ticker));
            sb.Append(',');
            sb.Append(Escape(c.CompanyName));
            sb.Append(',');
            sb.Append(Escape(c.Tier.ToString()));
            sb.Append(',');
            sb.Append(c.RawScore.ToString("F4"));
            sb.Append(',');
            sb.Append(c.FinalScore.ToString("F4"));
            sb.Append(',');
            sb.Append(c.SignalResults.Count(s => s.IsAvailable));
            sb.Append(',');
            sb.Append(Escape(c.EvaluatedAt.ToString("o")));
            sb.Append(',');
            sb.Append(Escape(signalScores));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
