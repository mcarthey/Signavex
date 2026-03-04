namespace Signavex.Domain.Configuration;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@signavex.com";
    public string FromName { get; set; } = "Signavex";
}
