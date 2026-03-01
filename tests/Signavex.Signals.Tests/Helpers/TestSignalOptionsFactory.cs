using Microsoft.Extensions.Options;
using Signavex.Domain.Configuration;

namespace Signavex.Signals.Tests.Helpers;

internal static class TestSignalOptionsFactory
{
    public static IOptions<SignavexOptions> CreateDefault()
    {
        return Options.Create(new SignavexOptions());
    }
}
