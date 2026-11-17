using System.Globalization;

namespace eShop.Web.Services;

public sealed class UiSessionInfo
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    public string MachineName { get; } = Environment.MachineName;

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;

    public string StartedAtDisplay => StartedAt.ToString("M/d/yyyy h:mm:ss tt", EnUs);
}
