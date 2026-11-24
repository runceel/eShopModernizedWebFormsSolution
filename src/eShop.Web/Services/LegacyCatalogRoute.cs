using System.Globalization;

namespace eShop.Web.Services;

public static class LegacyCatalogRoute
{
    public static bool TryParsePagedList(
        string path,
        out int pageIndex,
        out int pageSize)
    {
        pageIndex = 0;
        pageSize = 0;

        var segments = path.Split('?', 2)[0].Trim('/').Split('/');
        if (segments.Length != 5
            || !string.Equals(segments[0], "Default", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[1], "index", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[3], "size", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(segments[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out pageIndex)
            || !int.TryParse(segments[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out pageSize))
        {
            pageIndex = 0;
            pageSize = 0;
            return false;
        }

        return true;
    }
}
