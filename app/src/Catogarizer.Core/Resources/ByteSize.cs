using System.Globalization;

namespace Catogarizer.Core.Resources;

/// <summary>Memory as people read it: "380 MB", "1.2 GB".</summary>
public static class ByteSize
{
    private const long Mb = 1024 * 1024;
    private const long Gb = 1024 * Mb;

    public static string Format(long bytes)
    {
        if (bytes >= Gb) return (bytes / (double)Gb).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        return Math.Max(0, bytes / Mb).ToString(CultureInfo.InvariantCulture) + " MB";
    }
}
