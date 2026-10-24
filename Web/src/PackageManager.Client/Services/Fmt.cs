using System.Globalization;

namespace PackageManager.Client.Services;

public static class Fmt
{
    private static readonly CultureInfo Num = CultureInfo.InvariantCulture;

    /// <summary>302 MB, 1.2 GB, 640 KB — the precision the design uses.</summary>
    public static string Size(long? bytes)
    {
        if (bytes is not { } b) return "—";
        const double kb = 1024, mb = kb * 1024, gb = mb * 1024;
        if (b >= gb) return (b / gb).ToString("0.#", Num) + " GB";
        if (b >= mb) return Math.Round(b / mb).ToString("0", Num) + " MB";
        if (b >= kb) return Math.Round(b / kb).ToString("0", Num) + " KB";
        return b + " B";
    }

    public static string Date(DateTime? utc) =>
        utc is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "—";

    public static string Time(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Initial(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : char.ToUpperInvariant(name.Trim()[0]).ToString();

    public static string Duration(double seconds) => seconds switch
    {
        < 60 => $"{Math.Max(1, Math.Round(seconds))} giây",
        < 3600 => $"{Math.Round(seconds / 60)} phút",
        _ => $"{seconds / 3600:0.#} giờ",
    };

    /// <summary>"com.cysharp.unitask" → "com.cysharp"; "thanhdv.utilities" → "thanhdv".</summary>
    public static string UpmScope(string name)
    {
        var parts = name.Split('.');
        return parts.Length >= 3 ? $"{parts[0]}.{parts[1]}" : parts[0];
    }
}
